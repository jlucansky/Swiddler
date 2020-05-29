using Swiddler.ChunkViews;
using Swiddler.Common;
using Swiddler.DataChunks;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Swiddler.Channels
{
    public class MonitorChannel : Channel, IDisposable
    {
        public TcpClient Client { get; }
        public Monitor Monitor { get; }

        readonly Dictionary<ulong, ChildSessionItem> ChildrenSessions = new Dictionary<ulong, ChildSessionItem>();

        readonly Socket socket;

        private class ChildSessionItem
        {
            public Session ChildSession;
            public Mediator Mediator;
        }

        private class Mediator : Channel
        {
            public Mediator(Session session) : base(session) { }
            protected override void OnReceiveNotification(Packet packet) => throw new NotImplementedException();
            public void Send(Packet packet) => NotifyObservers(packet); // write to session UI
        }


        public MonitorChannel(Session session, TcpClient monitorClient) : base(session)
        {
            Client = monitorClient;
            socket = monitorClient.Client;

            try
            {
                Monitor = new Monitor(Client.GetStream());
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        protected override void StartOverride()
        {
            Task.Factory.StartNew(ReadLoop, TaskCreationOptions.LongRunning);
        }

        void ReadLoop()
        {
            try
            {
                while (true) NewCapturedPacked(Monitor.Read());
            }
            catch (Exception ex)
            {
                HandleError(ex);

                foreach (var item in ChildrenSessions.Values)
                    item.ChildSession.Stop();
            }
        }

        void NewCapturedPacked(CapturedPacket capPacket)
        {
            var isNew = !ChildrenSessions.ContainsKey(capPacket.Handle);

            if (isNew && capPacket.Event == MonitorEvent.Close)
                return; // ignore unknown closed sockets

            var sessionItem = GetChild(capPacket.Handle);

            if (capPacket.Error != SocketError.Success)
            {
                Log(sessionItem.ChildSession, new SocketException((int)capPacket.Error).Message, MessageType.Error);
            }

            if (isNew || (capPacket.Event == MonitorEvent.Connected && capPacket.Error == SocketError.Success))
                sessionItem.ChildSession.Name = GetSessionName(capPacket);

            switch (capPacket.Event)
            {
                case MonitorEvent.Send:
                case MonitorEvent.SendTo:
                case MonitorEvent.Recv:
                case MonitorEvent.RecvFrom:
                    var packet = new Packet()
                    {
                        Payload = capPacket.Data,
                        LocalEndPoint = (IPEndPoint)socket.LocalEndPoint,
                        RemoteEndPoint = (IPEndPoint)socket.RemoteEndPoint,
                        Flow = GetTrafficFlow(capPacket.Event)
                    };

                    sessionItem.Mediator.Send(packet);
                    break;
                case MonitorEvent.Connecting:
                    if (!isNew) break; // can be observed multiple times on non-blocking socket without any Connected event
                    Log(sessionItem.ChildSession, $"Connecting to {capPacket.Protocol.ToString().ToLower()}://{capPacket.RemoteEndPoint}");
                    break;
                case MonitorEvent.Connected:
                    // dump socket info
                    break;
                case MonitorEvent.Accepted:
                    Log(sessionItem.ChildSession, $"Accepted from {capPacket.Protocol.ToString().ToLower()}://{capPacket.RemoteEndPoint}");
                    break;
                case MonitorEvent.Listen:
                    Log(sessionItem.ChildSession, $"Starting listener at {capPacket.LocalEndPoint}");
                    break;
                case MonitorEvent.Close:
                    Log(sessionItem.ChildSession, $"Socket closed", MessageType.Error);
                    break;
            }
            
        }

        static string GetSessionName(CapturedPacket capPacket)
        {
            if (capPacket.LocalEndPoint != null && capPacket.RemoteEndPoint != null)
                return $"{capPacket.Protocol.ToString().Substring(0, 1)} :{ capPacket.LocalEndPoint.Port } -> {capPacket.RemoteEndPoint}";
            else
                return $"0x{capPacket.Handle:X8}";
        }

        TrafficFlow GetTrafficFlow(MonitorEvent e)
        {
            if (e == MonitorEvent.Send || e == MonitorEvent.SendTo)
                return TrafficFlow.Outbound;
            if (e == MonitorEvent.Recv || e == MonitorEvent.RecvFrom)
                return TrafficFlow.Inbound;
            return TrafficFlow.Undefined;
        }

        void Log(Session session, string message, MessageType msgType = MessageType.Information)
        {
            session.Storage.Write(new MessageData() { Text = message, Type = msgType });

            if (msgType == MessageType.Error)
                session.Stop();
        }

        ChildSessionItem GetChild(ulong handle)
        {
            if (ChildrenSessions.TryGetValue(handle, out var item) == false)
            {
                var child = Session.NewChildSession();

                child.Parent.Storage.Write(new MessageData() { Text = $"New socket observed (0x{handle:X})" });

                item = new ChildSessionItem()
                {
                    ChildSession = child,
                    Mediator = new Mediator(Session),
                };

                //childSession.Name = $"{ep}";

                item.Mediator.Observe(child.SessionChannel); // received packet write to session Log

                child.Start(); // immediately set session state to stared

                ChildrenSessions.Add(handle, item);
            }

            return item;
        }

        protected override void OnReceiveNotification(Packet packet)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            Client.Close();
        }
    }
}
