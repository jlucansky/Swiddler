using System;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Swiddler.Security
{
    // Inspired by https://github.com/justcoding121/Stream-Extended

    public class SslStreamExt : SslStream
    {
        public SslServerHello ServerHello { get; private set; }
        public SslClientHello ClientHello { get; private set; }
        public Func<SslClientHello, X509Certificate> ServerCertificateCallback { get; set; } // return server certificate based on SNI
        public Action<byte[]> InvalidHandshake { get; set; } // dump invalid data from remote side

        private readonly InspectionStream _inspection; // useful to captures SSL handshake

        public SslStreamExt(Stream innerStream) : this(innerStream, false, null, null) { }
        public SslStreamExt(Stream innerStream, bool leaveInnerStreamOpen) : this(innerStream, leaveInnerStreamOpen, null, null, EncryptionPolicy.RequireEncryption) { }
        public SslStreamExt(Stream innerStream, bool leaveInnerStreamOpen, RemoteCertificateValidationCallback userCertificateValidationCallback) : this(innerStream, leaveInnerStreamOpen, userCertificateValidationCallback, null, EncryptionPolicy.RequireEncryption) { }
        public SslStreamExt(Stream innerStream, bool leaveInnerStreamOpen, RemoteCertificateValidationCallback userCertificateValidationCallback, LocalCertificateSelectionCallback userCertificateSelectionCallback) : this(innerStream, leaveInnerStreamOpen, userCertificateValidationCallback, userCertificateSelectionCallback, EncryptionPolicy.RequireEncryption) { }

        public SslStreamExt(Stream innerStream, bool leaveInnerStreamOpen, RemoteCertificateValidationCallback userCertificateValidationCallback, LocalCertificateSelectionCallback userCertificateSelectionCallback, EncryptionPolicy encryptionPolicy)
            : base(new InspectionStream(innerStream, leaveInnerStreamOpen), leaveInnerStreamOpen, userCertificateValidationCallback, userCertificateSelectionCallback, encryptionPolicy)
        {
            _inspection = (InspectionStream)InnerStream;
        }

        public override void AuthenticateAsClient(string targetHost, X509CertificateCollection clientCertificates, SslProtocols enabledSslProtocols, bool checkCertificateRevocation)
        {
            try
            {
                // to supress SNI, just set host to IP address
                base.AuthenticateAsClient(string.IsNullOrEmpty(targetHost) ? "0.0.0.0" : targetHost, clientCertificates, enabledSslProtocols, checkCertificateRevocation);
            }
            finally
            {
                // read captured client/server hello records
                _inspection.OutputCapture.Position = 0;
                _inspection.InputCapture.Position = 0;
                ClientHello = _inspection.OutputCapture.ReadSslClientHello();
                ServerHello = _inspection.InputCapture.ReadSslServerHello();

                if (ClientHello == null && _inspection.OutputCapture.Length == 0)
                    throw new Exception("Client doesn't write any data. Protocol may be not supported.");

                if (ServerHello == null && _inspection.InputCapture.Length > 0)
                    InvalidHandshake?.Invoke(_inspection.ReadCapturedData(0x1000));

                _inspection.StopCapture(); // cleanup captured streams
            }
        }

        public override void AuthenticateAsServer(X509Certificate serverCertificate, bool clientCertificateRequired, SslProtocols enabledSslProtocols, bool checkCertificateRevocation)
        {
            ClientHello = _inspection.ReadSslClientHello(); // first, read Hello record which may contains SNI

            if (ClientHello == null && _inspection.InputCapture.Length > 0)
                InvalidHandshake?.Invoke(_inspection.ReadCapturedData(0x1000));

            _inspection.StopCapture(resetInputPosition: true, keepOutputCapture: true); // IMPORTANT: rewind Client Hello record which was consumed with ReadSslClientHello

            try
            {
                base.AuthenticateAsServer(
                    ServerCertificateCallback?.Invoke(ClientHello) ?? serverCertificate ?? throw new Exception("Server certificate not selected or client not sending SNI."),
                    clientCertificateRequired, enabledSslProtocols, checkCertificateRevocation);
            }
            finally
            {
                _inspection.OutputCapture.Position = 0; // read Server Hello written by AuthenticateAsServer
                ServerHello = _inspection.OutputCapture.ReadSslServerHello();

                _inspection.StopCapture(); // cleanup captured streams
            }
        }

        public byte[] GetOutputData() => _inspection.GetOutputData();

        private class InspectionStream : Stream
        {
            protected Stream InnerStream { get; private set; }
            public bool LeaveStreamOpen { get; private set; }

            bool transitMode; // if true, stream is simple proxy between InnerStream without doing any capturing
            bool canCleanupReads = false;
            MemoryStream _reads, _writes;

            public InspectionStream(Stream innerStream, bool leaveInnerStreamOpen)
            {
                if (innerStream == null || innerStream == Null)
                    throw new ArgumentNullException(nameof(innerStream));

                if (!transitMode)
                {
                    _reads = new MemoryStream();
                    _writes = new MemoryStream();
                }

                InnerStream = innerStream;
                LeaveStreamOpen = leaveInnerStreamOpen;
            }

            public override bool CanRead => InnerStream.CanRead;
            public override bool CanSeek => InnerStream.CanSeek;
            public override bool CanWrite => InnerStream.CanWrite;
            public override long Length => InnerStream.Length;
            public override long Position { get => InnerStream.Position; set => InnerStream.Position = value; }
            public override void Flush() => InnerStream.Flush();
            public override long Seek(long offset, SeekOrigin origin) => InnerStream.Seek(offset, origin);
            public override void SetLength(long value) => InnerStream.SetLength(value);


            public override int Read(byte[] buffer, int offset, int count)
            {
                if (transitMode)
                {
                    if (_reads != null)
                    {
                        var n = _reads.Read(buffer, offset, count);
                        if (canCleanupReads && _reads.Position == _reads.Length)
                        {
                            _reads.Dispose();
                            _reads = null;
                        }
                        if (n > 0) return n;
                    }
                }

                count = InnerStream.Read(buffer, offset, count);
                if (count > 0) _reads?.Write(buffer, offset, count);
                return count;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _writes?.Write(buffer, offset, count);
                InnerStream.Write(buffer, offset, count);
            }

            /// <summary>
            /// Need to call after handshake, otherwise stream will continue to eat memory.
            /// </summary>
            /// <param name="resetInputPosition">In SSL server mode, need rewind Client Hello data</param>
            public void StopCapture(bool resetInputPosition = false, bool keepOutputCapture = false)
            {
                transitMode = true;
                canCleanupReads = true;

                if (!keepOutputCapture)
                {
                    _writes.Dispose();
                    _writes = null;
                }

                if (resetInputPosition)
                    _reads.Position = 0;

                if (_reads != null && (_reads.Length == 0 || !resetInputPosition))
                {
                    _reads.Dispose();
                    _reads = null;
                }
            }

            public Stream InputCapture => _reads;
            public Stream OutputCapture => _writes;

            public byte[] GetOutputData() => _writes?.ToArray();

            public byte[] ReadCapturedData(int count)
            {
                transitMode = true;
                _reads.Position = 0;

                var data = new byte[count];
                var ns = InnerStream as System.Net.Sockets.NetworkStream;
                int i = 0;
                try
                {
                    for (i = 0; i < count; i++)
                    {
                        var anyData = ns?.DataAvailable ?? false;
                        if (!anyData && _reads != null)
                            anyData = _reads.Position < _reads.Length;

                        if (!anyData) break;

                        var read = ReadByte();
                        if (read < 0) break;

                        data[i] = (byte)read;
                    }
                }
                catch { }

                Array.Resize(ref data, i);

                return data;
            }

            protected override void Dispose(bool disposing)
            {
                try
                {
                    if (disposing)
                    {
                        if (LeaveStreamOpen)
                            InnerStream.Flush();
                        else
                            InnerStream.Close();
                    }
                }
                finally
                {
                    _reads?.Dispose();
                    _writes?.Dispose();
                    base.Dispose(disposing);
                }
            }

            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                return InnerStream.BeginRead(buffer, offset, count, callback, state);
            }

            public override int EndRead(IAsyncResult asyncResult)
            {
                return InnerStream.EndRead(asyncResult);
            }

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                return InnerStream.BeginWrite(buffer, offset, count, callback, state);
            }

            public override void EndWrite(IAsyncResult asyncResult)
            {
                InnerStream.EndWrite(asyncResult);
            }

        }


    }
}
