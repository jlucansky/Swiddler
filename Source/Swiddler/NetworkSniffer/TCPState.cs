using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Swiddler.NetworkSniffer
{
    class TCPState
    {
        public int PurgeTimeout { get; set; } = 1000;
        public int Backoff { get; set; } = 1024;

        public uint AckedSequence = 0;

        public bool IsActiveOpener = false; // first SYN (client)
        public bool IsClosing = false; // FIN
        public bool IsBroken = false; // RST

        DateTime LastAckUtc = DateTime.UtcNow;
        readonly List<RawPacket> UnackedSegments = new List<RawPacket>();
        
        public long Dropped = 0;

        public int UnackedCount => UnackedSegments.Count;
        public TimeSpan IdleTime => DateTime.UtcNow - LastAckUtc;

        public bool CanPurge()
        {
            return LastAckUtc < DateTime.UtcNow.AddSeconds(-PurgeTimeout);
        }

        class PacketComparer : IComparer<RawPacket>
        {
            public uint Sequence;
            public int Compare(RawPacket x, RawPacket y) => x.Sequence.CompareTo(Sequence);
        }
        static readonly PacketComparer PacketComparerInstance = new PacketComparer();

        public void Put(RawPacket packet)
        {
            /*
            if (prevSegment != null)
            {
                if (PacketEquals(prevSegment, packet)) return; // ignore duplicity

                var prevSeq = prevSegment.Sequence;

                if (packet.Sequence < prevSeq) return; // old segment appeared
                if (packet.Sequence - prevSeq > int.MaxValue) return; // old segment appeared from previous epoch
            }
            */
            var index = SearchUnackedPacket(packet.Sequence);
            if (index < 0)
            {
                index = ~index;
            }
            else
            {
                //if (PacketEquals(UnackedSegments[index], packet)) return; // ignore duplicity
                
                while (index < UnackedSegments.Count && UnackedSegments[index].Sequence == packet.Sequence)
                    index++;
            }

            UnackedSegments.Insert(index, packet);
        }

        int SearchUnackedPacket(uint sequence)
        {
            PacketComparerInstance.Sequence = sequence;
            return UnackedSegments.BinarySearch(null, PacketComparerInstance);
        }

        bool PacketEquals(RawPacket x, RawPacket y)
        {
            return x.Checksum == y.Checksum && x.Flags == y.Flags && x.Sequence == y.Sequence && x.Acknowledgment == y.Acknowledgment;
        }

        public RawPacket TryGet()
        {
            if (UnackedSegments.Count == 0) return null;

            //var found = SearchUnackedPacket(AckedSequence);

            /*
            if (found < 0)
            {
                if (UnackedSegments.Count > Backoff)
                {
                    // drop some segments

                    found = ~found;
                    if (found < 0 || found >= UnackedSegments.Count)
                        return null;

                    AckedSequence = UnackedSegments[found].Sequence;
                }
            }
            */

            var packet = UnackedSegments[0];
            bool found = false;

            if (UnackedSegments.Count > Backoff)
            {
                RemoveAcked(); // purge duplicits
                if (UnackedSegments.Count == 0) return null;

                packet = UnackedSegments[0];

                packet.Dropped = packet.Sequence - AckedSequence;
                if (packet.Dropped > int.MaxValue) packet.Dropped = 0; // overlapping segment, not dropped

                Dropped += packet.Dropped;

                found = true;
            }
            else
            {
                if (packet.Contains(AckedSequence))
                {
                    found = true;
                }
                else
                {
                    if (UnackedSegments.Count > 1)
                    {
                        packet = UnackedSegments.Last();
                        if (packet.Contains(AckedSequence))
                        {
                            found = true;
                        }
                    }
                }
            }

            if (found)
            {
                var shift = AckedSequence - packet.Sequence;
                if (shift > int.MaxValue) shift = 0; // dropped

                packet.HeaderLength += (int)shift;

                LastAckUtc = DateTime.UtcNow;

                AckedSequence = packet.Sequence + (uint)packet.DataLength;
                
                if (packet.Flags.HasFlag(TCPFlags.FIN))
                {
                    AckedSequence++;
                    IsClosing = true;
                }

                if (packet.Flags.HasFlag(TCPFlags.SYN))
                    AckedSequence++;

                if (packet.Flags.HasFlag(TCPFlags.RST))
                    IsBroken = true;

                if (IsClosing || IsBroken)
                    PurgeTimeout = 100; // TIME_WAIT

                RemoveAcked();

                if (UnackedSegments.FirstOrDefault() == packet)
                    UnackedSegments.RemoveAt(0);

                return packet;
            }

            return null;
        }

        void RemoveAcked()
        {
            int i;
            for (i = 0; i < UnackedSegments.Count; i++)
            {
                var seg = UnackedSegments[i];
                if (seg.Sequence >= AckedSequence || seg.Contains(AckedSequence))
                {
                    break;
                }
            }
            if (i > 0) UnackedSegments.RemoveRange(0, i);

            if (AckedSequence < int.MaxValue)
            {
                long ackedEpoch = AckedSequence + uint.MaxValue + 1;
                for (i = UnackedSegments.Count - 1; i >= 0; i++)
                {
                    var seg = UnackedSegments[i];
                    if (seg.Sequence < int.MaxValue)
                        break;
                    if (seg.Contains(ackedEpoch))
                        break;
                    UnackedSegments.RemoveAt(i--);
                }
            }
        }

        public override string ToString()
        {
            return $"NextSeq={AckedSequence}, Unacked={UnackedSegments.Count}, Idle={DateTime.UtcNow - LastAckUtc}";
        }
    }

    class TCPStateKey
    {
        public IPEndPoint Source, Destination;

        public override bool Equals(object obj)
        {
            var other = obj as TCPStateKey;
            if (other == null) return false;
            return Source.Equals(other.Source) && Destination.Equals(other.Destination);
        }

        public override int GetHashCode()
        {
            return Source.GetHashCode() ^ Destination.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Source} -> {Destination}";
        }

        public TCPStateKey Invert() => new TCPStateKey() { Destination = Source, Source = Destination };
    }
}
