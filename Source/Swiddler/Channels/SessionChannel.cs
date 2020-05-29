using Swiddler.Common;
using Swiddler.DataChunks;

namespace Swiddler.Channels
{
    public class SessionChannel : Channel
    {
        public bool IsActive => Observers.Count > 0;

        public SessionChannel(Session session) : base(session)
        {
            DefaultFlow = TrafficFlow.Outbound;
        }

        /// <summary>
        /// Append packet to session log
        /// </summary>
        protected override void OnReceiveNotification(Packet packet)
        {
            Session.WriteChunk(packet);
        }

        /// <summary>
        /// User can send packet interactively
        /// </summary>
        public void Submit(byte[] data)
        {
            if (IsActive)
            {
                var packet = new Packet() { Payload = data };
                NotifyObservers(packet);
            }
        }
    }
}
