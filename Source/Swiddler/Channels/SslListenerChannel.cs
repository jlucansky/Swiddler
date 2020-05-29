using Swiddler.Common;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Swiddler.Channels
{
    public class SslListenerChannel : TcpListenerChannel
    {
        public SslProtocols SslProtocols { get; set; }
        public bool RequireClientCertificate { get; set; }
        public bool IgnoreInvalidClientCertificate { get; set; }
        public X509Certificate2 ServerCertificate { get; set; }
        public X509Certificate2 CertificateAuthority { get; set; }
        public bool AutoGenerateServerCertificate { get; set; }
        public string GeneratedServerCertificatePrefix { get; set; }


        public SslListenerChannel(Session session, TcpListener listener) : base(session, listener) { }

        protected override Channel CreateChildChannel(Session childSession, TcpClient acceptedClient)
        {
            var sslChannel = (SslChannel)childSession.CreateTcpChannel(acceptedClient, ssl: true);

            sslChannel.AuthenticateAsServer = true;
            sslChannel.SslProtocols = SslProtocols;
            sslChannel.RequireClientCertificate = RequireClientCertificate;
            sslChannel.IgnoreInvalidClientCertificate = IgnoreInvalidClientCertificate;
            sslChannel.ServerCertificate = ServerCertificate;
            sslChannel.CertificateAuthority = CertificateAuthority;
            sslChannel.AutoGenerateServerCertificate = AutoGenerateServerCertificate;
            sslChannel.GeneratedServerCertificatePrefix = GeneratedServerCertificatePrefix;

            return sslChannel;
        }
    }
}
