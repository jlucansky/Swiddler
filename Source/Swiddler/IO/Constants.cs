using System;
using System.Text;

namespace Swiddler.IO
{
    public static class Constants
    {
        public static readonly UTF8Encoding UTF8Encoding = new UTF8Encoding(false, true); // UTF-8 without BOM header

        public const int BlockSize = 0x8000; // 32kB (best trade-off between fast random vs sequential access)


        /* Record Types */
        public const byte UnusedData = 0; // jump to the next block

        public const byte InboundIPv4Packet = 1; // reads
        public const byte OutboundIPv4Packet = 2; // writes
        public const byte InboundIPv6Packet = 3; // reads
        public const byte OutboundIPv6Packet = 4; // writes
        public const byte MessageData = 100;



        /* Record Headers */
        private const int _packetBaseHeaderSize =
            sizeof(byte)   +    // type                : byte (1)
            sizeof(UInt16) +    // block count         : uint16 (2)
            sizeof(UInt32) +    // length              : uint32 (4)
            sizeof(UInt64) +    // sequence number     : uint64 (8)
            sizeof(Int64)  +    // time ticks utc      : int64 (8)
                                // src ip              : byte (4/16)
            sizeof(UInt16) +    // src port            : uint16 (2)
                                // dst ip              : byte (4/16)
            sizeof(UInt16) ;    // dst port            : uint16 (2)

        public const int IPv4PacketHeaderSize = _packetBaseHeaderSize +
                4 +     // src ip       : byte (4)
                4 ;     // dst ip       : byte (4)

        public const int IPv6PacketHeaderSize = _packetBaseHeaderSize +
                16 +    // src ip       : byte (16)
                16 ;    // dst ip       : byte (16)

        public const int MessageHeaderSize =
            sizeof(byte)   +    // type                : byte (1)
            sizeof(UInt16) +    // block count         : uint16 (2)
            sizeof(UInt32) +    // length              : uint32 (4)
            sizeof(UInt64) +    // sequence number     : uint64 (8)
            sizeof(Int64) +     // time ticks utc      : int64 (8)
            sizeof(byte)   ;    // message type        : byte (1)

    }
}
