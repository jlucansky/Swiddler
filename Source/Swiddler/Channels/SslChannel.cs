using Swiddler.Common;
using Swiddler.DataChunks;
using Swiddler.Security;
using Swiddler.Serialization;
using System;
using System.Linq;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Swiddler.Channels
{
    public class SslChannel : TcpChannel
    {
        public SslProtocols SslProtocols { get; set; }

        public bool AuthenticateAsClient { get; set; }
        public string SNI { get; set; }
        public bool IgnoreInvalidServerCertificate { get; set; }
        public X509Certificate2 ClientCertificate { get; set; }


        public bool AuthenticateAsServer { get; set; }
        public bool RequireClientCertificate { get; set; }
        public bool IgnoreInvalidClientCertificate { get; set; }
        public X509Certificate2 ServerCertificate { get; set; } // default server cert when there is no SNI
        public X509Certificate2 CertificateAuthority { get; set; }
        public bool AutoGenerateServerCertificate { get; set; }
        public string GeneratedServerCertificatePrefix { get; set; }


        private SslStreamExt stream;
        private CertProvider certProvider;
        private X509Certificate2[] chain;

        public SslChannel(Session session, TcpClient client) : base(session, client) { }

        protected override Stream GetStream()
        {
            if (!AuthenticateAsClient && !AuthenticateAsServer)
                throw new Exception("Authenticate as client or server.");

            stream = new SslStreamExt(NetworkStream, true, RemoteCertificateValidationCallback)
            {
                ServerCertificateCallback = GetServerCertificate,
                InvalidHandshake = InvalidHandshake,
            };

            try
            {
                if (AuthenticateAsClient)
                {
                    stream.AuthenticateAsClient(
                        SNI, ClientCertificate == null ? null : new X509CertificateCollection() { ClientCertificate },
                        SslProtocols, checkCertificateRevocation: false);
                }

                if (AuthenticateAsServer)
                {
                    if (AutoGenerateServerCertificate)
                    {
                        certProvider = new CertProvider()
                        {
                            IssuerCertificate = CertificateAuthority,
                            SubjectPrefix = GeneratedServerCertificatePrefix,
                            SubjectPostfix = null,
                        };
                    }

                    stream.AuthenticateAsServer(ServerCertificate, RequireClientCertificate, SslProtocols, checkCertificateRevocation: false);
                }
            }
            finally
            {
                if (stream.ClientHello != null || stream.ServerHello != null)
                {
                    WriteChunk(new MessageData(new SslHandshake(stream, chain)));
                }
            }

            return stream;
        }

        void InvalidHandshake(byte[] data)
        {
            if (data?.Any() == true)
            {
                var output = stream.GetOutputData(); // CLIENT_HELLO in client mode
                if (output?.Any() == true)
                {
                    // dump CLIENT_HELLO
                    NotifyObservers(FillEndpoints(new Packet() { Payload = output, Flow = TrafficFlow.Outbound }));
                }

                // dump remote CLIENT_HELLO in server mode / remote SERVER_HELLO in client mode
                NotifyObservers(FillEndpoints(new Packet() { Payload = data }));
            }
        }

        X509Certificate2[] GetLocalCert()
        {
            return stream.LocalCertificate == null ? emptyX509Cert2arr : new[] { new X509Certificate2(stream.LocalCertificate) };
        }

        static readonly X509Certificate2[] emptyX509Cert2arr = new X509Certificate2[0];
        bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            this.chain = chain?.ChainElements.Cast<X509ChainElement>().Select(x => x.Certificate).ToArray() ?? emptyX509Cert2arr;

            if (sslPolicyErrors == SslPolicyErrors.None) return true; // VALID

            if (AuthenticateAsServer)
            {
                if (!RequireClientCertificate) return true;
                if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateNotAvailable) return false; // missing client cert
                if (IgnoreInvalidClientCertificate) return true; // skip client cert validation
            }
            if (AuthenticateAsClient)
            {
                if (IgnoreInvalidServerCertificate) return true; // ignore invalid server cert
            }

            return false; // INVALID
        }

        X509Certificate GetServerCertificate(SslClientHello hello)
        {
            if (hello == null || certProvider == null) return null;

            var reqSNI = hello.GetServerNameIndication();
            if (string.IsNullOrEmpty(reqSNI)) return null;

            return certProvider.GetCertificate(reqSNI); // get existing cert or create new one based on SNI
        }

        public override void Dispose()
        {
            certProvider?.Dispose();
            stream?.Dispose();
            base.Dispose();
        }
    }
}
