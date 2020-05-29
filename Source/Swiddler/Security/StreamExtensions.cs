using System;
using System.Collections.Generic;
using System.IO;

namespace Swiddler.Security
{
    public static class StreamExtensions
    {
        public static SslClientHello ReadSslClientHello(this Stream stream)
        {
            int recordType = stream.ReadByte();
            if (recordType == -1)
            {
                return null;
            }

            if ((recordType & 0x80) == 0x80) //SSL 2.0
            {
                int recordLength = ((recordType & 0x7f) << 8) + stream.ReadByte();
                if (recordLength < 9)
                {
                    // Message body too short.
                    return null;
                }

                if (stream.ReadByte() != 0x01)
                {
                    // should be ClientHello
                    return null;
                }

                int majorVersion = stream.ReadByte();
                int minorVersion = stream.ReadByte();

                int ciphersCount = stream.ReadInt16BE() / 3;
                int sessionIdLength = stream.ReadInt16BE();
                int randomLength = stream.ReadInt16BE();

                int[] ciphers = new int[ciphersCount];
                for (int i = 0; i < ciphers.Length; i++)
                {
                    ciphers[i] = (stream.ReadByte() << 16) + (stream.ReadByte() << 8) + stream.ReadByte();
                }

                byte[] sessionId = stream.ReadBytes(sessionIdLength);
                byte[] random = stream.ReadBytes(randomLength);

                var clientHelloInfo = new SslClientHello
                {
                    HandshakeVersion = 2,
                    MajorVersion = majorVersion,
                    MinorVersion = minorVersion,
                    Random = random,
                    SessionId = sessionId,
                    Ciphers = ciphers,
                };

                return clientHelloInfo;
            }
            else if (recordType == 0x16) //SSL 3.0 or TLS 1.0, 1.1 and 1.2
            {
                int majorVersion = stream.ReadByte();
                int minorVersion = stream.ReadByte();

                int recordLength = stream.ReadInt16BE();

                if (stream.ReadByte() != 0x01)
                {
                    // should be ClientHello
                    return null;
                }

                var length = stream.ReadInt24BE();

                majorVersion = stream.ReadByte();
                minorVersion = stream.ReadByte();

                byte[] random = stream.ReadBytes(32);

                if (-1 == (length = stream.ReadByte())) return null;

                byte[] sessionId = stream.ReadBytes(length);

                length = stream.ReadInt16BE();

                byte[] ciphersData = stream.ReadBytes(length);
                int[] ciphers = new int[ciphersData.Length / 2];
                for (int i = 0; i < ciphers.Length; i++)
                    ciphers[i] = (ciphersData[2 * i] << 8) + ciphersData[2 * i + 1];

                length = stream.ReadByte();
                if (length < 1)
                    return null;

                byte[] compressionData = stream.ReadBytes(length);

                Dictionary<string, SslExtension> extensions;
                extensions = ReadSslExtensions(stream, majorVersion, minorVersion);

                var clientHelloInfo = new SslClientHello
                {
                    HandshakeVersion = 3,
                    MajorVersion = majorVersion,
                    MinorVersion = minorVersion,
                    Random = random,
                    SessionId = sessionId,
                    Ciphers = ciphers,
                    CompressionData = compressionData,
                    Extensions = extensions,
                };

                return clientHelloInfo;
            }

            return null;
        }

        public static SslServerHello ReadSslServerHello(this Stream stream)
        {
            //detects the HTTPS ClientHello message as it is described in the following url:
            //https://stackoverflow.com/questions/3897883/how-to-detect-an-incoming-ssl-https-handshake-ssl-wire-format

            int recordType = stream.ReadByte();
            if (recordType == -1)
            {
                return null;
            }

            if ((recordType & 0x80) == 0x80) //SSL 2.0
            {
                // not tested. SSL2 is deprecated

                int recordLength = ((recordType & 0x7f) << 8) + stream.ReadByte();
                if (recordLength < 38)
                {
                    // Message body too short.
                    return null;
                }

                if (stream.ReadByte() != 0x04)
                {
                    // should be ServerHello
                    return null;
                }

                int majorVersion = stream.ReadByte();
                int minorVersion = stream.ReadByte();

                byte[] random = stream.ReadBytes(32);
                byte[] sessionId = stream.ReadBytes(1);
                int cipherSuite = stream.ReadInt16BE();

                var serverHelloInfo = new SslServerHello
                {
                    HandshakeVersion = 2,
                    MajorVersion = majorVersion,
                    MinorVersion = minorVersion,
                    Random = random,
                    SessionId = sessionId,
                    CipherSuite = cipherSuite,
                };

                return serverHelloInfo;
            }
            else if (recordType == 0x16) //SSL 3.0 or TLS 1.0, 1.1 and 1.2
            {
                int majorVersion = stream.ReadByte();
                int minorVersion = stream.ReadByte();

                int recordLength = stream.ReadInt16BE();

                if (stream.ReadByte() != 0x02)
                {
                    // should be ServerHello
                    return null;
                }

                var length = stream.ReadInt24BE();

                majorVersion = stream.ReadByte();
                minorVersion = stream.ReadByte();

                byte[] random = stream.ReadBytes(32);
                length = stream.ReadByte();

                byte[] sessionId = stream.ReadBytes(length);

                int cipherSuite = stream.ReadInt16BE();
                int compressionMethod = stream.ReadByte();

                if (compressionMethod == -1) return null;

                Dictionary<string, SslExtension> extensions = null;
                extensions = ReadSslExtensions(stream, majorVersion, minorVersion);

                if (extensions.TryGetValue("supported_versions", out var supported_versions) && supported_versions.RawData.Length == 2)
                {
                    // override for TLS 1.3
                    majorVersion = supported_versions.RawData[0];
                    minorVersion = supported_versions.RawData[1];
                }

                var serverHelloInfo = new SslServerHello
                {
                    HandshakeVersion = 3,
                    MajorVersion = majorVersion,
                    MinorVersion = minorVersion,
                    Random = random,
                    SessionId = sessionId,
                    CipherSuite = cipherSuite,
                    CompressionMethod = (byte)compressionMethod,
                    Extensions = extensions,
                };

                return serverHelloInfo;
            }
            else if (recordType == 0x15) // alert
            {
                int majorVersion = stream.ReadByte();
                int minorVersion = stream.ReadByte();

                int recordLength = stream.ReadInt16BE();

                if (recordLength == 2)
                {
                    int level = stream.ReadByte(); // warning(1), fatal(2)
                    
                    int description = stream.ReadByte();

                    if (description != -1)
                    {
                        throw new SslAlertException()
                        {
                            MajorVersion = majorVersion,
                            MinorVersion = minorVersion,
                            Level = level,
                            Description = (SslAlertDescription)description,
                        };
                    }
                }
            }

            return null;
        }

        private static Dictionary<string, SslExtension> ReadSslExtensions(this Stream stream, int majorVersion, int minorVersion)
        {
            Dictionary<string, SslExtension> extensions = null;
            if (majorVersion > 3 || majorVersion == 3 && minorVersion >= 1)
            {
                int extensionsLength = stream.ReadInt16BE();

                extensions = new Dictionary<string, SslExtension>();
                int idx = 0;
                while (extensionsLength > 3)
                {
                    int id = stream.ReadInt16BE();
                    int length = stream.ReadInt16BE();
                    byte[] data = stream.ReadBytes(length);
                    var extension = new SslExtension(id, data, idx++);
                    extensions[extension.Name] = extension;
                    extensionsLength -= 4 + length;
                }

            }

            return extensions;
        }

        internal static int ReadInt24BE(this Stream stream)
        {
            int i1 = stream.ReadByte();
            int i2 = stream.ReadByte();
            int i3 = stream.ReadByte();
            return (i1 << 16) + (i2 << 8) + i3;
        }

        internal static int ReadInt16BE(this Stream stream)
        {
            int i1 = stream.ReadByte();
            int i2 = stream.ReadByte();
            return (i1 << 8) + i2;
        }

        internal static byte[] ReadBytes(this Stream stream, int length)
        {
            var buffer = new byte[length];
            if (length == 0)
                return buffer;

            stream.Read(buffer, 0, buffer.Length);
            return buffer;
        }
    }


}
