using Swiddler.Common;
using Swiddler.DataChunks;
using Swiddler.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Swiddler.Channels
{
    public class TcpChannel : Channel, IDisposable
    {
        public TcpClient Client { get; }
        public NetworkStream NetworkStream { get; private set; }

        public bool IsServerChannel { get; set; } // in tunnel mode, this is source channel

        private Stream stream;
        private byte[] buffer;
        private Socket socket;
        private IPEndPoint localEndPoint, remoteEndPoint;

        public TcpChannel(Session session, TcpClient client) : base(session)
        {
            Client = client;
            DefaultFlow = TrafficFlow.Inbound;
        }

        protected override void StartOverride()
        {
            socket = Client.Client;

            if (socket == null) return; // diposed by error on other side of tunnel

            localEndPoint = (IPEndPoint)socket.LocalEndPoint;
            remoteEndPoint = (IPEndPoint)socket.RemoteEndPoint;

            try
            {
                buffer = new byte[Client.ReceiveBufferSize];
                NetworkStream = Client.GetStream();
                stream = GetStream();
                BeginRead();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        protected virtual Stream GetStream()
        {
            return NetworkStream;
        }

        void BeginRead()
        {
            try
            {
                EnsureIsConnected();
                stream.BeginRead(buffer, 0, buffer.Length, ReadCallback, null);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        void EnsureIsConnected()
        {
            if (socket.Connected && socket.Poll(1000, SelectMode.SelectRead) && socket.Available == 0)
                throw new SocketException(Net.ERROR_GRACEFUL_DISCONNECT);
        }

        private void ReadCallback(IAsyncResult result)
        {
            try
            {
                int bytes = stream.EndRead(result);

                if (bytes > 0)
                {
                    var packet = new Packet() { Payload = new byte[bytes] };
                    Array.Copy(buffer, packet.Payload, packet.Payload.Length);

                    NotifyObservers(FillEndpoints(packet));
                }

                BeginRead();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        protected Packet FillEndpoints(Packet packet)
        {
            if (packet.LocalEndPoint == null) packet.LocalEndPoint = localEndPoint;
            if (packet.RemoteEndPoint == null) packet.RemoteEndPoint = remoteEndPoint;
            return packet;
        }

        protected override void OnReceiveNotification(Packet packet)
        {
            if (stream == null) return; // on tunnel mode when client handshake fails

            try
            {
                FillEndpoints(packet);
                stream.Write(packet.Payload, 0, packet.Payload.Length);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }
        
        public virtual void Dispose()
        {
            NetworkStream?.Dispose();
            Client.Close();
        }
    }
}
