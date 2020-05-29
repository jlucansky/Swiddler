using Swiddler.Common;
using Swiddler.DataChunks;
using System;
using System.IO;
using System.Net;

namespace Swiddler.IO
{
    public class BlockReader : IDisposable
    {
        public Stream BaseStream { get; }
        public IDataChunk CurrentChunk { get; private set; }
        public bool EndOfStream { get; private set; }
        public long HighestAllowedOffset { get; set; } = long.MaxValue; // furthest position we can read (aligned to block size)
        protected int CurrentBlockPosition => (int)(BaseStream.Position % Constants.BlockSize);
        public bool LeaveStreamOpen { get; set; } = true;

        private readonly BinaryReader reader;

        public long Position
        {
            get => BaseStream.Position;
            set
            {
                Seek(value);
                Read();
            }
        }

        public BlockReader(Stream stream)
        {
            BaseStream = stream;
            reader = new BinaryReader(stream, Constants.UTF8Encoding, leaveOpen: true);
        }

        public bool Read()
        {
            /*
            BLOCK // Exactly on every 32kB must start a new block - this ensures fast random access anywhere in the large capture files
            {
                start block count       : uint16 (2)    // number of blocks backward to the start of the current packet; if 0, the packet header must follow, otherwise 2 bytes (uint16) must follow which determine offset to the next packet header
                (length of prev data)   : uint16 (2)    // present only if "start block offset" > 0
                (previous packet data)  : byte (n)      // present only if "start block offset" > 0; n = "length of prev data" field
                PACKET
                {
                    type                : byte (1)      // type of the packet; 1 = InboundIPv4; 2 = OutboundIPv4; 3 = InboundIPv6; 4 = OutboundIPv6;
                    block count         : uint16 (2)    // number of blocks this packet excesses (0 if this packet fits to the current block)
                    length              : uint32 (4)    // data length - number of bytes
                    ...
                }
            }
            */

            // reset all states
            CurrentChunk = null;
            EndOfStream = false;

            int headByte = ReadHeadByte();
            if (headByte == -1)
            {
                EndOfStream = true;
                return false;
            }

            switch (headByte)
            {
                case Constants.InboundIPv4Packet:
                case Constants.OutboundIPv4Packet:
                case Constants.InboundIPv6Packet:
                case Constants.OutboundIPv6Packet:
                    CurrentChunk = ReadPacket((byte)headByte);
                    break;
                case Constants.MessageData:
                    CurrentChunk = ReadMessage();
                    break;
                default:
                    ThrowUnexpectedData(1, "Unknown record type"); break;
            }

            return true;
        }

        private int GetHeaderSize(byte headByte)
        {
            switch (headByte)
            {
                case Constants.InboundIPv4Packet:
                case Constants.OutboundIPv4Packet:
                    return Constants.IPv4PacketHeaderSize;
                case Constants.InboundIPv6Packet:
                case Constants.OutboundIPv6Packet:
                    return Constants.IPv6PacketHeaderSize;
                case Constants.MessageData:
                    return Constants.MessageHeaderSize;
                default:
                    ThrowUnexpectedData(1, "Unknown record type"); return 0;
            }
        }

        private Packet ReadPacket(byte type)
        {
            bool isIPv6 = type == Constants.InboundIPv6Packet || type == Constants.OutboundIPv6Packet;
            int addressLen = isIPv6 ? 16 : 4;

            /*
            type                : byte (1)      // type of the packet; 1 = InboundIPv4; 2 = OutboundIPv4; 3 = InboundIPv6; 4 = OutboundIPv6;
            block count         : uint16 (2)    // number of blocks this packet excesses (0 if this packet fits to the current block)
            length              : uint32 (4)    // data length - number of bytes
            sequence number     : uint64 (8)    // index of the packet
            time ticks utc      : int64 (8)     // time of the packet
            src ip              : byte (4/16)   // source IP (IPv4 / IPv6)
            src port            : uint16 (2)    // source port number
            dst ip              : byte (4/16)   // destination IP (IPv4 / IPv6)
            dst port            : uint16 (2)    // destination port number
            data                : byte (n)      // packet payload; n = MIN("length" field; remaining bytes of the current block)
            */

            var actualOffset = BaseStream.Position - 1; // minus type byte

            var totalBlockCount = reader.ReadUInt16();
            var packet = new Packet()
            {
                ActualOffset = actualOffset,
                Flow = (type == Constants.InboundIPv4Packet || type == Constants.InboundIPv6Packet) ? TrafficFlow.Inbound : TrafficFlow.Outbound,
                Payload = new byte[reader.ReadUInt32()],
                SequenceNumber = (long)reader.ReadUInt64(),
                Timestamp = new DateTime(reader.ReadInt64(), DateTimeKind.Utc),
                LocalEndPoint = new IPEndPoint(new IPAddress(reader.ReadBytes(addressLen)), reader.ReadUInt16()),
                RemoteEndPoint = new IPEndPoint(new IPAddress(reader.ReadBytes(addressLen)), reader.ReadUInt16()),
            };

            FillBuffer(packet.Payload, totalBlockCount);

            packet.ActualLength = (int)(BaseStream.Position - actualOffset);

            return packet;
        }

        private MessageData ReadMessage()
        {
            /*
            type                : byte (1)      // type of the chunk (message)
            block count         : uint16 (2)    // number of blocks this chunk excesses (0 if this chunk fits to the current block)
            length              : uint32 (4)    // data length - number of bytes
            sequence number     : uint64 (8)    // index of the chunk
            time ticks utc      : int64 (8)     // time of the message
            msg type            : byte (1)      // message type
            data                : byte (n)      // message text
            */

            var actualOffset = BaseStream.Position - 1; // minus type byte

            var totalBlockCount = reader.ReadUInt16();
            var data = new byte[reader.ReadUInt32()];
            var message = new MessageData()
            {
                ActualOffset = actualOffset,
                SequenceNumber = (long)reader.ReadUInt64(),
                Timestamp = new DateTime(reader.ReadInt64(), DateTimeKind.Utc),
                Type = (MessageType)reader.ReadByte(),
            };

            FillBuffer(data, totalBlockCount);

            message.Text = Constants.UTF8Encoding.GetString(data);
            message.ActualLength = (int)(BaseStream.Position - actualOffset);

            return message;
        }

        private void FillBuffer(byte[] buffer, int maxBlockCount)
        {
            int payloadOffset = 0;
            int blockIndex = 0;
            int blockCutBytes = 0;
            while (payloadOffset < buffer.Length)
            {
                int len = Math.Min(Constants.BlockSize - CurrentBlockPosition - blockCutBytes, buffer.Length - payloadOffset);

                if (CurrentBlockPosition == 0) // beginning of the block
                {
                    // validate fields

                    if (blockIndex != reader.ReadUInt16() && blockIndex <= maxBlockCount) // "start block count"
                        ThrowUnexpectedData(2, "Invalid block index");
                    if (len != reader.ReadUInt16()) // "length of prev data"
                        ThrowUnexpectedData(2, "Invalid length");
                }

                BaseStream.Read(buffer, payloadOffset, len);

                payloadOffset += len;
                blockIndex++;
                blockCutBytes = sizeof(UInt16) + sizeof(UInt16);
            }
        }

        // find first usable header or EOF
        private int ReadHeadByte()
        {
            int headByte;
            do
            {
                if (BaseStream.Position >= HighestAllowedOffset)
                    return -1;

                if (CurrentBlockPosition == 0) // beginning of the block
                {
                    // check "start block count" (should be zero)
                    if (reader.ReadUInt16() != 0)
                        ThrowUnexpectedData(2);
                }

                headByte = BaseStream.ReadByte();

                if (headByte == Constants.UnusedData && CurrentBlockPosition != 0) // jump to the end of the block (except last ReadByte introduces new block already)
                    BaseStream.Position += Constants.BlockSize - CurrentBlockPosition;

            } while (headByte == Constants.UnusedData);

            return headByte;
        }

        public void Seek(long position)
        {
            position = Math.Min(HighestAllowedOffset, position);

            BaseStream.Position = position / Constants.BlockSize * Constants.BlockSize;

            if (position == 0) return;

            var startBlock = reader.ReadUInt16(); // "start block count"

            if (startBlock != 0) 
            {
                // jump to the first block where current record begins
                BaseStream.Position = (position / Constants.BlockSize - startBlock) * Constants.BlockSize;
            }

            long currentHeadOffset = BaseStream.Position;

            while (true)
            {
                if (CurrentBlockPosition == 0) // beginning of the block
                {
                    if (reader.ReadUInt16() != 0) // "start block count"
                    {
                        int len = reader.ReadUInt16(); // "length of prev data"
                        BaseStream.Position += len;
                        continue;
                    }
                }

                int headByte = ReadHeadByte();

                if (headByte == -1 || BaseStream.Position - 1 > position)
                    break;

                currentHeadOffset = BaseStream.Position - 1; // minus head byte

                var totalBlockCount = reader.ReadUInt16();
                if (totalBlockCount > 0) // packet is splitted across multiple blocks
                {
                    // jump to latest block of the current packet
                    BaseStream.Position = (BaseStream.Position / Constants.BlockSize + totalBlockCount) * Constants.BlockSize;
                }
                else
                {
                    var length = reader.ReadUInt32();
                    // jump to the end of the packet
                    BaseStream.Position = currentHeadOffset + GetHeaderSize((byte)headByte) + length;
                }
            }

            BaseStream.Position = currentHeadOffset; // align to the packet header
        }

        private void ThrowUnexpectedData(int size, string message = "Unexpected data")
        {
            throw new IOException($"{message} at position 0x{BaseStream.Position - size:X}");
        }

        public void Dispose()
        {
            if (!LeaveStreamOpen)
                BaseStream.Dispose();
        }
    }
}
