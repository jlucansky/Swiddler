using Swiddler.Common;
using Swiddler.DataChunks;
using System;
using System.Collections.Generic;
using System.Net;

namespace Swiddler.Channels
{
    /// <summary>
    /// For every remote EndPoint creates a new child Session
    /// </summary>
    public class RemoteChildChannel : Channel
    {
        readonly Dictionary<IPEndPoint, Mediator> ChildrenSessions = new Dictionary<IPEndPoint, Mediator>();

        private class Mediator : Channel, IDisposable
        {
            readonly Action<Packet> ownerNotify;
            readonly IPEndPoint ep;
            Action<IPEndPoint> disposed;
            public Mediator(Session session, Action<Packet> ownerNotify, IPEndPoint ep, Action<IPEndPoint> disposed) : base(session)
            {
                this.ownerNotify = ownerNotify;
                this.disposed = disposed;
                this.ep = ep;
            }

            protected override void OnReceiveNotification(Packet packet) // send to UdpChannel from session editor
            {
                packet.Destination = ep;
                ownerNotify(packet);
            }

            public void Send(Packet packet)
            {
                if (DefaultFlow != TrafficFlow.Undefined)
                    packet.Flow = DefaultFlow;
                NotifyObservers(packet);
            }

            public void Dispose()
            {
                Observers.Clear();
                var disposedCopy = disposed;
                disposed = null;
                disposedCopy?.Invoke(ep);
            }
        }

        public RemoteChildChannel(Session session) : base(session) { }
        protected override void OnReceiveNotification(Packet packet) => GetChild(packet.Source).Send(packet);

        private Mediator GetChild(IPEndPoint ep)
        {
            lock (ChildrenSessions)
            {
                if (ChildrenSessions.TryGetValue(ep, out var item) == false)
                {
                    var child = Session.NewChildSession("Received from " + ep, 
                        s => item = new Mediator(s, NotifyObservers, ep, RemoveChild));
                    
                    child.Name = $"{ep}";
                    child.Start();
                    ChildrenSessions.Add(ep, item);
                }
                return item;
            }
        }

        private void RemoveChild(IPEndPoint ep)
        {
            lock (ChildrenSessions)
                ChildrenSessions.Remove(ep);
        }
    }
}
