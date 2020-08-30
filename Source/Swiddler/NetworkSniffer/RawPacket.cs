using System;
using System.Net;
using System.Net.Sockets;

namespace Swiddler.NetworkSniffer
{
    public class RawPacket
    {
        public IPVersion Version;

        public IPEndPoint Source, Destination;

        public ProtocolType Protocol = ProtocolType.Unspecified; 

        public int HeaderLength; // = dataOffset
        public int DataLength;

        /// <summary>
        /// TCP/UDP checksum
        /// </summary>
        public int Checksum;

        public TCPFlags Flags;
        public uint Sequence, Acknowledgment;

        public uint Dropped;

        public byte[] Buffer; // includes full copy of IP headers + data


        internal bool Contains(long seq)
        {
            if (DataLength == 0) return Sequence == (uint)seq;
            return Sequence <= seq && seq < Sequence + DataLength;
        }

        public override string ToString()
        {
            return $"{Flags} | {Source} -> {Destination}, Len={DataLength}, Seq={Sequence}, Ack={Acknowledgment}";
        }
    }

    public enum IPVersion
    {
        IPv4 = 4,
        IPv6 = 6,
    }

    [Flags]
    public enum TCPFlags
    {
        /// <summary>
        /// No more data from sender
        /// </summary>
        FIN = 0x1,

        /// <summary>
        /// Synchronize sequence numbers
        /// </summary>
        SYN = 0x2,

        /// <summary>
        /// Reset the connection
        /// </summary>
        RST = 0x4,

        /// <summary>
        /// Push data immediately to receiver
        /// </summary>
        PSH = 0x8,

        /// <summary>
        /// Acknowledgment field is significant
        /// </summary>
        ACK = 0x10,

        /// <summary>
        /// Urgent pointer is significant
        /// </summary>
        URG = 0x20,

        /// <summary>
        /// ECN-Echo [RFC3168]
        /// </summary>
        ECE = 0x40,

        /// <summary>
        /// Congestion Window Reduced [RFC3168]
        /// </summary>
        CWR = 0x80,

        /// <summary>
        /// Nonce sum [RFC3540, RFC3168]
        /// </summary>
        NS = 0x100
    }
}
