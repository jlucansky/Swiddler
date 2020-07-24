using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Swiddler.NetworkSniffer
{
    class IPFragmentReassembly
    {
        public RawPacket ReassembledPacket { get; private set; }
        public readonly DateTime TimestampUtc = DateTime.UtcNow;

        readonly List<IPFragment> fragments = new List<IPFragment>();
        int totalLength = 0;

        public void AddFragment(RawPacket packet, int fragmentOffset, bool moreFragments)
        {
            var f = new IPFragment() { IncompletePacket = packet, FragmentOffset = fragmentOffset, MoreFragments = moreFragments };

            var index = fragments.BinarySearch(f);
            if (index < 0)
            {
                index = ~index;

                if (index > 0)
                {
                    if (f.FragmentOffset < fragments[index - 1].FragmentOffset + fragments[index - 1].IncompletePacket.DataLength)
                        return; // overlapping data (DoS attack ???)
                }

                fragments.Insert(index, f);

                totalLength = Math.Max(totalLength, fragmentOffset + packet.DataLength);
                TryReassemble(packet);
            }
        }

        void TryReassemble(RawPacket packet)
        {
            if (fragments.Last().MoreFragments == false && fragments.Sum(x => x.IncompletePacket.DataLength) == totalLength)
            {
                int offset = 0;
                var dstPosition = packet.HeaderLength;
                var buffer = new byte[totalLength + dstPosition];

                foreach (var f in fragments)
                {
                    if (f.FragmentOffset != offset) return;

                    var len = f.IncompletePacket.DataLength;
                    Buffer.BlockCopy(f.IncompletePacket.Buffer, f.IncompletePacket.HeaderLength, buffer, dstPosition, len);
                    dstPosition += len;
                    offset += len;
                }

                packet.Buffer = buffer;
                packet.DataLength = totalLength;
                ReassembledPacket = packet;
            }
        }
    }

    class IPFragmentKey
    {
        public IPAddress Address;
        public int Identification;

        public override int GetHashCode()
        {
            return Address.GetHashCode() ^ Identification.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as IPFragmentKey;
            if (other == null) return false;
            return other.Identification.Equals(Identification) && other.Address.Equals(Address);
        }

        public override string ToString()
        {
            return $"id={Identification} address={Address}";
        }
    }

    class IPFragment : IComparable<IPFragment>
    {
        public int FragmentOffset;
        public bool MoreFragments; // true in all but the last fragment

        public RawPacket IncompletePacket;

        public int CompareTo(IPFragment other) => FragmentOffset.CompareTo(other.FragmentOffset);

        public override string ToString()
        {
            return "off=" + FragmentOffset;
        }
    }

}
