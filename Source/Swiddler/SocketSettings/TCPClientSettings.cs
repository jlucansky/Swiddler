using System;
using System.Security.Authentication;

namespace Swiddler.SocketSettings
{
    public class TCPClientSettings : ClientSettingsBase
    {
        public event EventHandler<bool> EnableSSLChanged;

        public TCPClientSettings()
        {
            Protocol = System.Net.Sockets.ProtocolType.Tcp;
            Caption = "Client";
            ImageName = "Connect";
            _TargetHost = "example.org";
            _TargetPort = 80;
        }


        bool _EnableSSL;
        public bool EnableSSL { get => _EnableSSL; set { if (SetProperty(ref _EnableSSL, value)) EnableSSLChanged?.Invoke(this, value); } }

        protected bool _ValidateCertificate = true;
        public bool ValidateCertificate { get => _ValidateCertificate; set { SetProperty(ref _ValidateCertificate, value); } }

        protected string _SNI;
        public string SNI { get => _SNI; set => SetProperty(ref _SNI, value); }

        protected string _ClientCertificate;
        public string ClientCertificate { get => _ClientCertificate; set => SetProperty(ref _ClientCertificate, value); }


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

        public static TCPClientSettings DesignInstance => new TCPClientSettings() { LocalBinding = true, EnableSSL = true };

        public override string ToString() => $"{GetScheme()}://{base.ToString()}";

        string GetScheme()
        {
            if (TargetPort == 80) return "http";
            if (TargetPort == 443) return "https";
            return "tcp";
        }
    }
}
