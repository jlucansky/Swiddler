using Swiddler.Common;
using Swiddler.DataChunks;
using System;
using System.Net.Sockets;

namespace Swiddler.Channels
{
    public class TcpListenerChannel : Channel, IDisposable
    {
        public TcpListener Listener { get; }

        public TcpListenerChannel(Session session, TcpListener listener) : base(session)
        {
            Listener = listener;
        }

        protected override void OnReceiveNotification(Packet packet)
        {
            throw new NotImplementedException(); // channel is not intended to send packets into
        }

        protected override void StartOverride()
        {
            BeginAcceptTcpClient();
        }

        void BeginAcceptTcpClient()
        {
            Listener.BeginAcceptTcpClient(AcceptTcpClientCallback, null);
        }

        private void AcceptTcpClientCallback(IAsyncResult result)
        {
            TcpClient client = null;
            try
            {
                client = Listener.EndAcceptTcpClient(result);
                var remoteEP = client.Client.RemoteEndPoint;
                var child = Session.NewChildSession("Accepted connection from " + remoteEP, newSession => CreateChildChannel(newSession, client));
                ((TcpChannel)child.ServerChannel).IsServerChannel = true;
                child.Name = $"{remoteEP}";
                child.ResolveProcessIdAsync(remoteEP);
                child.StartAsync();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
            finally
            {
                if (client != null) BeginAcceptTcpClient(); // accept another one
            }
        }

        protected virtual Channel CreateChildChannel(Session childSession, TcpClient acceptedClient)
        {
            return childSession.CreateTcpChannel(acceptedClient);
        }

        public void Dispose()
        {
            Listener.Stop();
        }
    }
}
