using Swiddler.Common;
using Swiddler.DataChunks;
using Swiddler.Utils;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Swiddler.IO
{
    public class BlockWriter : IDisposable
    {
        private delegate void HeaderDelegate(int totalBlockCount);

        public Stream BaseStream { get; }
        public long Position => BaseStream.Position;
        public bool LeaveStreamOpen { get; set; } = true;


        private readonly BinaryWriter writer;

        public BlockWriter(Stream stream)
        {
            BaseStream = stream;
            writer = new BinaryWriter(stream, Constants.UTF8Encoding, leaveOpen: true);
        }

        public void Write(IDataChunk chunk)
        {
            if (chunk is Packet packet)
                Write(packet);
            else if (chunk is MessageData message)
                Write(message);
            else
                throw new InvalidOperationException("Unsupported data type: " + chunk.GetType());

            BaseStream.Flush();
        }

        public void Write(Packet packet)
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
                    sequence number     : uint64 (8)    // index of the packet
                    time ticks utc      : int64 (8)     // time of the packet
                    src ip              : byte (4/16)   // source IP (IPv4 / IPv6)
                    src port            : uint16 (2)    // source port number
                    dst ip              : byte (4/16)   // destination IP (IPv4 / IPv6)
                    dst port            : uint16 (2)    // destination port number
                    data                : byte (n)      // packet payload; n = MIN("length" field; remaining bytes of the current block)
                }
                NOP
                {
                    type                : byte (1)      // value = 0; // ignore rest of the block
                }
                ...
            }
            */

            var src = packet.Source.Address;
            var dst = packet.Destination.Address;

            var srcBytes = src.GetAddressBytes();
            var dstBytes = dst.GetAddressBytes();

            var packetHeaderSize = Constants.PacketBaseHeaderSize + srcBytes.Length + dstBytes.Length;

            WriteCore(packetHeaderSize, packet.Payload, totalBlockCount =>
            {
                packet.ActualOffset = Position;
                writer.Write(GetPacketTypeCode(src, dst, packet.Flow)); // type
                writer.Write((UInt16)totalBlockCount);                  // block count
                writer.Write((UInt32)packet.Payload.Length);            // length
                writer.Write((UInt64)packet.SequenceNumber);            // sequence number
                writer.Write((Int64)packet.Timestamp.UtcDateTime.Ticks);// time
                writer.Write(srcBytes);                                 // src ip
                writer.Write((UInt16)packet.Source.Port);               // src port
                writer.Write(dstBytes);                                 // dst ip
                writer.Write((UInt16)packet.Destination.Port);          // dst port
            });

            packet.ActualLength = (int)(Position - packet.ActualOffset);
        }

        public void Write(MessageData message)
        {
            /*
            BLOCK // Exactly on every 32kB must start a new block - this ensures fast random access anywhere in the large capture files
            {
                start block count       : uint16 (2)    // number of blocks backward to the start of the current chunk; if 0, the chunk header must follow, otherwise 2 bytes (uint16) must follow which determine offset to the next chunk header
                (length of prev data)   : uint16 (2)    // present only if "start block offset" > 0
                (previous chunk data)  : byte (n)      // present only if "start block offset" > 0; n = "length of prev data" field
                MESSAGE
                {
                    type                : byte (1)      // type of the chunk (message)
                    block count         : uint16 (2)    // number of blocks this chunk excesses (0 if this chunk fits to the current block)
                    length              : uint32 (4)    // data length - number of bytes
                    sequence number     : uint64 (8)    // index of the chunk
                    time ticks utc      : int64 (8)     // time of the message
                    msg type            : byte (1)      // message type
                    data                : byte (n)      // message text
                }
            }
            */

            var data = Constants.UTF8Encoding.GetBytes(message.Text);

            WriteCore(Constants.MessageHeaderSize, data, totalBlockCount =>
            {
                message.ActualOffset = Position;
                writer.Write(Constants.MessageData);                    // type
                writer.Write((UInt16)totalBlockCount);                  // block count
                writer.Write((UInt32)data.Length);                      // length
                writer.Write((UInt64)message.SequenceNumber);           // sequence number
                writer.Write((Int64)message.Timestamp.UtcDateTime.Ticks);// time
                writer.Write((byte)message.Type);                       // msg type
            });

            message.ActualLength = (int)(Position - message.ActualOffset);
        }

        private void WriteCore(int headerSize, byte[] payload, HeaderDelegate headerDelegate)
        {
            var blockPosition = (int)(BaseStream.Position % Constants.BlockSize);
            var remainingBlockBytes = Constants.BlockSize - blockPosition;
            UInt16 blockIndex = 0;

            if (remainingBlockBytes <= headerSize) // packet header doesn't fit into remaining space in current block
            {
                BaseStream.Position += remainingBlockBytes; // fill remaining space with zeroes (unused data)
                blockPosition = 0;
            }

            if (blockPosition == 0) // beginning of the block
            {
                writer.Write((UInt16)blockIndex); // "start block count"
                blockPosition = sizeof(UInt16);
            }

            var totalBlockCount = GetBlockCount(payload.Length, blockPosition + headerSize);

            // packet header
            headerDelegate(totalBlockCount);

            // compute current position & remaining
            blockPosition = (int)(BaseStream.Position % Constants.BlockSize);
            remainingBlockBytes = Constants.BlockSize - blockPosition;

            // TODO: update ApproxLineCount

            int payloadOffset = 0;
            while (payloadOffset < payload.Length)
            {
                int len = Math.Min(remainingBlockBytes, payload.Length - payloadOffset);

                if (blockIndex > 0) // beginning of the block
                {
                    writer.Write((UInt16)blockIndex);    // "start block count"
                    writer.Write((UInt16)len);           // "length of prev data"
                }

                BaseStream.Write(payload, payloadOffset, len);

                payloadOffset += len;
                blockIndex++;

                // init new block
                remainingBlockBytes = Constants.BlockSize - sizeof(UInt16) - sizeof(UInt16); // "start block count" + "length of prev data"
            }
        }

        private static int GetBlockCount(int bytesToWrite, int blockPosition)
        {
            int blockCount = 0;
            while (bytesToWrite > 0)
            {
                int len = Math.Min(Constants.BlockSize - blockPosition, bytesToWrite);

                blockPosition += len;
                bytesToWrite -= len;

                if (bytesToWrite > 0)
                {
                    blockCount++;
                    blockPosition = sizeof(UInt16) + sizeof(UInt16); // "start block count" + "length of prev data"
                }
            }

            return blockCount;
        }

        private static byte GetPacketTypeCode(IPAddress src, IPAddress dst, TrafficFlow flow)
        {
            byte flag = 0;

            if (flow == TrafficFlow.Inbound)
                flag |= Constants.InboundPacket;
            else if (flow == TrafficFlow.Outbound)
                flag |= Constants.OutboundPacket;
            else
                throw new InvalidOperationException("Invalid flow " + flow);

            if (src.AddressFamily == AddressFamily.InterNetworkV6)
                flag |= Constants.IPv6_Src;
            if (dst.AddressFamily == AddressFamily.InterNetworkV6)
                flag |= Constants.IPv6_Dst;

            return flag;
        }

        public void Dispose()
        {
            if (!LeaveStreamOpen)
                BaseStream.Dispose();
        }
    }
}
