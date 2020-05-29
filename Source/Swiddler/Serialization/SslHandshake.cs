using Swiddler.Security;
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Serialization;

namespace Swiddler.Serialization
{
    public class SslHandshake : IDisposable
    {
        [XmlAttribute] public string ProtocolVersion { get; set; }
        [XmlAttribute] public int ProtocolVersionCode { get; set; }
        public string Message { get; set; }

        [XmlAttribute] public bool IsServer { get; set; }
        [XmlAttribute] public bool IsClient { get; set; }

        public SslClientHello ClientHello { get; set; }
        public SslServerHello ServerHello { get; set; }

        [XmlIgnore] public X509Certificate2[] ServerCertificate { get; set; } // chain as captured in RemoteCertificateValidationCallback; first cert if leaf
        [XmlElement(nameof(ServerCertificate))]
        public string[] ServerCertificateEncoded { get => EncodeCert(ServerCertificate); set => ServerCertificate = DecodeCert(value); }

        [XmlIgnore] public X509Certificate2[] ClientCertificate { get; set; }
        [XmlElement(nameof(ClientCertificate))]
        public string[] ClientCertificateEncoded { get => EncodeCert(ClientCertificate); set => ClientCertificate = DecodeCert(value); }



        [XmlAttribute] public int CipherAlgorithm { get; set; }
        [XmlAttribute] public int CipherStrength { get; set; }
        [XmlAttribute] public int HashStrength { get; set; }
        [XmlAttribute] public int KeyExchangeAlgorithm { get; set; }
        [XmlAttribute] public int KeyExchangeStrength { get; set; }
        [XmlAttribute] public int HashAlgorithm { get; set; }

        [XmlAttribute] public bool IsAuthenticated { get; set; } // true on successfully completed handshake

        readonly Lazy<bool> isValidLazy;
        public bool IsValid() => isValidLazy.Value;



        public SslHandshake()
        {
            isValidLazy = new Lazy<bool>(() => GetPrimaryCertificateChain().ValidateChain());
        }

        public X509Certificate2[] GetPrimaryCertificateChain()
        {
            if (IsClient) return ServerCertificate;
            if (IsServer) return ClientCertificate?.Any() == true ? ClientCertificate : ServerCertificate;
            return null;
        }

        public SslHandshake(SslStreamExt stream, X509Certificate2[] chain) : this()
        {
            IsAuthenticated = stream.IsAuthenticated;
            IsServer = stream.IsServer;
            IsClient = !stream.IsServer;
            ClientHello = stream.ClientHello;
            ServerHello = stream.ServerHello;

            if (IsClient)
            {
                Message = $"client connection";
                ClientCertificate = GetLocalCert(stream);
                ServerCertificate = chain;
            }
            if (IsServer)
            {
                Message = $"server connection";
                ClientCertificate = chain;
                ServerCertificate = GetLocalCert(stream);
            }

            if (IsAuthenticated)
            {
                ProtocolVersionCode     = (int)stream.SslProtocol;
                ProtocolVersion         = GetVersionString(ProtocolVersionCode);
                CipherAlgorithm         = (int)stream.CipherAlgorithm;
                CipherStrength          = stream.CipherStrength;
                HashAlgorithm           = (int)stream.HashAlgorithm;
                HashStrength            = stream.HashStrength;
                KeyExchangeAlgorithm    = (int)stream.KeyExchangeAlgorithm;
                KeyExchangeStrength     = stream.KeyExchangeStrength;
                
                if (ServerHello != null) ProtocolVersion += " / " + ServerHello.CipherSuiteName;
            }
            else
            {
                ProtocolVersion = "SSL/TLS";
                Message += " error";
            }
        }

        static X509Certificate2[] GetLocalCert(SslStreamExt stream)
        {
            if (stream.IsAuthenticated && stream.LocalCertificate != null)
            {
                var crt2 = new X509Certificate2(stream.LocalCertificate);
                try
                {
                    return new[] { crt2 }.CompleteChain(); // find missing chain certs
                }
                finally
                {
                    crt2.Reset();
                }
            }

            return new X509Certificate2[0];
        }

        public string SslProtocol => GetVersionString(ProtocolVersionCode);

        string GetVersionString(int /* SslProtocols */ proto)
        {
            switch (proto)
            {
                case 12:    return "SSLv2.0";
                case 48:    return "SSLv3.0";
                case 192:   return "TLSv1.0";
                case 768:   return "TLSv1.1";
                case 3072:  return "TLSv1.2";
                case 12288: return "TLSv1.3";
            }

            return "#" + proto;
        }

        string[] EncodeCert(X509Certificate2[] crt)
        {
            if ((crt?.Length ?? 0) == 0) return null;
            return crt.Select(EncodeCert).ToArray();
        }

        X509Certificate2[] DecodeCert(string[] base64)
        {
            if ((base64?.Length ?? 0) == 0) return null;
            return base64.Select(DecodeCert).ToArray();
        }

        string EncodeCert(X509Certificate2 crt)
        {
            if (crt == null) return null;
            return Convert.ToBase64String(crt.GetRawCertData());
        }

        X509Certificate2 DecodeCert(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            var crt = new X509Certificate2(Convert.FromBase64String(base64));
            return crt;
        }

        public void Dispose()
        {
            foreach (var crt in ServerCertificate) crt?.Reset();
            foreach (var crt in ClientCertificate) crt?.Reset();

            ServerCertificate = null;
            ClientCertificate = null;
        }

    }
}
