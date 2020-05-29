using System;
using System.Security.Authentication;

namespace Swiddler.SocketSettings
{
    public class TCPServerSettings : ServerSettingsBase
    {
        public event EventHandler<bool> EnableSSLChanged;

        public TCPServerSettings()
        {
            Protocol = System.Net.Sockets.ProtocolType.Tcp;
            Caption = "Server";
            ImageName = "Port";
        }

        bool _EnableSSL;
        public bool EnableSSL { get => _EnableSSL; set { if (SetProperty(ref _EnableSSL, value)) EnableSSLChanged?.Invoke(this, value); } }


        protected bool _RequireClientCertificate;
        public bool RequireClientCertificate { get => _RequireClientCertificate; set { SetProperty(ref _RequireClientCertificate, value); } }

        protected bool _IgnoreInvalidClientCertificate = true;
        public bool IgnoreInvalidClientCertificate { get => _IgnoreInvalidClientCertificate; set { SetProperty(ref _IgnoreInvalidClientCertificate, value); } }


        protected string _ServerCertificate;
        public string ServerCertificate { get => _ServerCertificate; set => SetProperty(ref _ServerCertificate, value); }

        protected bool _AutoGenerateServerCertificate;
        public bool AutoGenerateServerCertificate { get => _AutoGenerateServerCertificate; set { SetProperty(ref _AutoGenerateServerCertificate, value); } }

        protected string _CertificateAuthority;
        public string CertificateAuthority { get => _CertificateAuthority; set => SetProperty(ref _CertificateAuthority, value); }

        protected string _GeneratedServerCertificatePrefix = "DO_NOT_TRUST_";
        public string GeneratedServerCertificatePrefix { get => _GeneratedServerCertificatePrefix; set => SetProperty(ref _GeneratedServerCertificatePrefix, value); }


        protected bool _Ssl30, _Tls10, _Tls11 = true, _Tls12 = true, _Tls13;
        public bool EnableProtocolSsl30 { get => _Ssl30; set { SetProperty(ref _Ssl30, value); } }
        public bool EnableProtocolTls10 { get => _Tls10; set { SetProperty(ref _Tls10, value); } }
        public bool EnableProtocolTls11 { get => _Tls11; set { SetProperty(ref _Tls11, value); } }
        public bool EnableProtocolTls12 { get => _Tls12; set { SetProperty(ref _Tls12, value); } }
        public bool EnableProtocolTls13 { get => _Tls13; set { SetProperty(ref _Tls13, value); } }


        public SslProtocols GetSslProtocols()
        {
            const int Ssl30 = 48;
            const int Tls10 = 192;
            const int Tls11 = 768;
            const int Tls12 = 3072;
            const int Tls13 = 12288;

            int protocols = 0;

            if (EnableProtocolSsl30) protocols |= Ssl30;
            if (EnableProtocolTls10) protocols |= Tls10;
            if (EnableProtocolTls11) protocols |= Tls11;
            if (EnableProtocolTls12) protocols |= Tls12;
            if (EnableProtocolTls13) protocols |= Tls13;

            return (SslProtocols)protocols;
        }

        public static TCPServerSettings DesignInstance => new TCPServerSettings() { EnableSSL = true, AutoGenerateServerCertificate = true };

        public override string ToString() => "tcp://" + base.ToString();
    }
}
