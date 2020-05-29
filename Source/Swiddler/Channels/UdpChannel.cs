using Swiddler.Common;
using Swiddler.DataChunks;
using System;
using System.Net;
using System.Net.Sockets;

namespace Swiddler.Channels
{
    public class UdpChannel : Channel, IDisposable
    {
        public UdpClient Client { get; }
        public IPEndPoint RemoteEndpoint { get; set; }
        public IPEndPoint LocalEndPoint { get; private set; }

        bool connected;

        public UdpChannel(Session session, UdpClient client) : base(session)
        {
            Client = client;
            DefaultFlow = TrafficFlow.Inbound;
        }

        protected override void StartOverride()
        {
            connected = Client.Client.Connected;
            if (connected) RemoteEndpoint = (IPEndPoint)Client.Client.RemoteEndPoint;
            LocalEndPoint = (IPEndPoint)Client.Client.LocalEndPoint;
            BeginRead();
        }

        void BeginRead()
        {
            try
            {
                Client.BeginReceive(ReceiveCallback, null);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                IPEndPoint remote = null;
                byte[] data = Client.EndReceive(result, ref remote);

                var packet = new Packet()
                {
                    Payload = data,
                    LocalEndPoint = LocalEndPoint,
                    RemoteEndPoint = remote,
                };

                NotifyObservers(packet);

                BeginRead();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        protected override void OnReceiveNotification(Packet packet)
        {
            try
            {
                if (packet.LocalEndPoint == null) packet.LocalEndPoint = LocalEndPoint;
                if (packet.RemoteEndPoint == null) packet.RemoteEndPoint = RemoteEndpoint;

                if (connected)
                    Client.Send(packet.Payload, packet.Payload.Length);
                else
                    Client.Send(packet.Payload, packet.Payload.Length, packet.RemoteEndPoint);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        public void Dispose()
        {
            Client.Close();
        }
    }
}
