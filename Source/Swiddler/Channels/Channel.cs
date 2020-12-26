using Swiddler.Common;
using Swiddler.DataChunks;
using Swiddler.IO;
using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Swiddler.Channels
{
    public delegate void ChannelErrorHandler(Channel sender, Exception exception);

    public abstract class Channel
    {
        public Session Session { get; } 

        /// <summary>
        /// Recipients of the outbound traffic from the channel (Reads)
        /// </summary>
        public List<Channel> Observers { get; } = new List<Channel>();

        public void ObserveTwoWay(Channel other)
        {
            Observe(other);
            other.Observe(this);
        }

        public void ObserveSelf()
        {
            Observe(this);
        }

        public void Observe(Channel other)
        {
            Observers.Add(other);
        }

        public void ObserveAfter<T>(Channel other) where T: Channel
        {
            int index = Observers.FindIndex(x => x is T) + 1;
            if (index < 0) index = 0;
            Observers.Insert(index, other);
        }

        /// <summary>
        /// Marks all sending packets (Reads) from this channel with specified flow.
        /// </summary>
        public TrafficFlow DefaultFlow { get; set; }

        public Channel(Session session)
        {
            Session = session;
        }

        protected void NotifyObservers(Packet packet)
        {
            //if (packet.Sender == null)
                //packet.Sender = this;

            if (packet.Flow == TrafficFlow.Undefined && DefaultFlow != TrafficFlow.Undefined)
                packet.Flow = DefaultFlow;

            foreach (var o in Observers)
            {
                o.OnReceiveNotification(packet);
            }
        }

        /// <summary>
        /// Write data to the this channel from different channel
        /// </summary>
        protected abstract void OnReceiveNotification(Packet packet);


        protected void HandleError(Exception ex)
        {
            Session.HandleChannelError(this, ex);
        }
        
        protected void WriteChunk(IDataChunk chunk)
        {
            Session.WriteChunk(chunk);
        }

        protected void WriteMessage(string message, MessageType type = MessageType.Information)
        {
            Session.WriteMessage(message, type);
        }

        /// <summary>
        /// Start read
        /// </summary>
        protected virtual void StartOverride() { }


        public bool Started { get; private set; }

        public void Start()
        {
            if (Started == false)
            {
                StartOverride();
                Started = true;
            }
        }

    }
}
