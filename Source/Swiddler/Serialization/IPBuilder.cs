using Swiddler.DataChunks;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Swiddler.Serialization
{
    public class IPBuilder
    {
        readonly int maxLength_v4; // max payload len
        readonly int ipHeaderLength_v4 = 20;
        readonly int totalHeaderLength_v4 = 0; // including L4 header

        readonly int maxLength_v6; // max payload len
        readonly int ipHeaderLength_v6 = 40;
        readonly int totalHeaderLength_v6 = 0; // including L4 header
        
        readonly int tcpHeaderLength = 20;
        readonly int udpHeaderLength = 8;

        readonly int layer4HeaderLength;
        
        readonly ProtocolType protocolType;
        uint seqInbound, seqOutbound;

        public byte TTL { get; set; } = 64;
            
        public IPBuilder(ProtocolType protocolType)
        {
            if (protocolType == ProtocolType.Unspecified)
                throw new Exception("Invalid protocol");

            this.protocolType = protocolType;

            layer4HeaderLength = 0;
            if (protocolType == ProtocolType.Tcp) layer4HeaderLength = tcpHeaderLength;
            if (protocolType == ProtocolType.Udp) layer4HeaderLength = udpHeaderLength;

            totalHeaderLength_v4 = ipHeaderLength_v4 + layer4HeaderLength;
            totalHeaderLength_v6 = ipHeaderLength_v6 + layer4HeaderLength;

            maxLength_v4 = 0x10000 - totalHeaderLength_v4;
            maxLength_v6 = 0x10000 - totalHeaderLength_v6;
        }

        public IEnumerable<byte[]> BuildPacket(Packet packet)
        {
            int remaining = packet.Payload.Length;
            int start = 0;

            int version = 4;
            int maxLength = maxLength_v4;
            
            if (packet.Source.AddressFamily == AddressFamily.InterNetworkV6 || packet.Destination.AddressFamily == AddressFamily.InterNetworkV6)
            {
                version = 6;
                maxLength = maxLength_v6;
            }

            while (remaining > 0) // split larger payloads into multiple IP packets
            {
                var len = Math.Min(remaining, maxLength);
                yield return BuildPacket(packet, version, start, len);
                remaining -= len;
                start += len;
            }
        }
        
        byte[] BuildPacket(Packet packet, int version, int start, int len)
        {
            var totalHeaderLength = version == 4 ? totalHeaderLength_v4 : totalHeaderLength_v6;
            var h = version == 4 ? ipHeaderLength_v4 : ipHeaderLength_v6;

            var totalLen = totalHeaderLength + len;

            var raw = new byte[totalLen];

            Buffer.BlockCopy(packet.Payload, start, raw, totalHeaderLength, len);

            IPEndPoint srcEP, dstEP; uint seq, ack;

            srcEP = packet.Source;
            dstEP = packet.Destination;

            if (packet.Flow == Common.TrafficFlow.Outbound)
            {
                seq = seqOutbound;
                ack = seqInbound;
                seqOutbound = (uint)(seqOutbound + len);
            }
            else
            {
                seq = seqInbound;
                ack = seqOutbound;
                seqInbound = (uint)(seqInbound + len);
            }

            if (version == 4)
            {
                if (srcEP.Address.AddressFamily != AddressFamily.InterNetwork || dstEP.Address.AddressFamily != AddressFamily.InterNetwork)
                    throw new InvalidOperationException("Invalid address family");

                raw[0] = 0x45; // version=4, ihl=5
                raw[2] = (byte)(totalLen >> 8);
                raw[3] = (byte)totalLen;
                raw[8] = TTL;
                raw[9] = (byte)protocolType;

                Buffer.BlockCopy(srcEP.Address.GetAddressBytes(), 0, raw, 12, 4);
                Buffer.BlockCopy(dstEP.Address.GetAddressBytes(), 0, raw, 16, 4);

                var checksum = ComputeChecksum(0, raw, 0, h);
                raw[10] = (byte)(checksum >> 8);
                raw[11] = (byte)checksum;
            }
            if (version == 6)
            {
                // convert possible IPv4 to IPv6 address
                if (srcEP.Address.AddressFamily == AddressFamily.InterNetwork) srcEP.Address = srcEP.Address.MapToIPv6();
                if (dstEP.Address.AddressFamily == AddressFamily.InterNetwork) dstEP.Address = dstEP.Address.MapToIPv6();

                var payloadLen = layer4HeaderLength + len; // size of the payload in bytes, including any extension headers

                raw[0] = 0x60; // version=6

                raw[4] = (byte)(payloadLen >> 8);
                raw[5] = (byte)payloadLen;
                raw[6] = (byte)protocolType;
                raw[7] = TTL;

                Buffer.BlockCopy(srcEP.Address.GetAddressBytes(), 0, raw, 8, 16);
                Buffer.BlockCopy(dstEP.Address.GetAddressBytes(), 0, raw, 24, 16);
            }

            int pseudoSum = (int)protocolType;
            if (version == 4) pseudoSum += Sum16(raw, 12, 8); // src + dst IP
            if (version == 6) pseudoSum += Sum16(raw, 8, 32);

            if (protocolType == ProtocolType.Tcp)
            {
                var tcpLen = len + tcpHeaderLength;

                raw[h + 0] = (byte)(srcEP.Port >> 8);
                raw[h + 1] = (byte)srcEP.Port;
                
                raw[h + 2] = (byte)(dstEP.Port >> 8);
                raw[h + 3] = (byte)dstEP.Port;

                raw[h + 4] = (byte)(seq >> 24);
                raw[h + 5] = (byte)(seq >> 16);
                raw[h + 6] = (byte)(seq >> 8);
                raw[h + 7] = (byte)seq;

                raw[h + 8] = (byte)(ack >> 24);
                raw[h + 9] = (byte)(ack >> 16);
                raw[h + 10] = (byte)(ack >> 8);
                raw[h + 11] = (byte)ack;
                
                raw[h + 12] = 0x50; // minimum hdr size = 20 (5*4)
                raw[h + 13] = 0x10; // ACK flag

                raw[h + 15] = 1; // constant windows size

                var checksum = ComputeChecksum(pseudoSum + tcpLen, raw, h, raw.Length - h);

                raw[h + 16] = (byte)(checksum >> 8);
                raw[h + 17] = (byte)checksum;

            }

            if (protocolType == ProtocolType.Udp)
            {
                var udpLen = len + udpHeaderLength; // length field is the length of the UDP header and data

                raw[h + 0] = (byte)(srcEP.Port >> 8);
                raw[h + 1] = (byte)srcEP.Port;
                
                raw[h + 2] = (byte)(dstEP.Port >> 8);
                raw[h + 3] = (byte)dstEP.Port;

                raw[h + 4] = (byte)(udpLen >> 8);
                raw[h + 5] = (byte)udpLen;

                var checksum = ComputeChecksum(pseudoSum + udpLen, raw, h, raw.Length - h);

                raw[h + 6] = (byte)(checksum >> 8);
                raw[h + 7] = (byte)checksum;
            }

            return raw;
        }

        static int Sum16(byte[] buffer, int offset, int length)
        {
            int sum = 0;

            while (length > 1)
            {
                sum += (buffer[offset++] << 8) | buffer[offset++];
                length -= 2;
            }

            return sum;
        }

        // https://github.com/xXxTheDarkprogramerxXx/PSPSHARP/blob/master/PSP_EMU/network/protocols/InternetChecksum.cs
        static int ComputeChecksum(int pseudoHdrSum, byte[] buffer, int offset, int length)
        {
            int sum = pseudoHdrSum;

            while (length > 1)
            {
                sum += (buffer[offset++] << 8) | buffer[offset++];
                length -= 2;
            }

            // Add left-over byte, if any
            if (length > 0)
                sum += buffer[offset++] << 8;

            // Add the carry
            while (((int)((uint)sum >> 16)) != 0)
                sum = (sum & 0xFFFF) + ((int)((uint)sum >> 16));

            // Flip all the bits to obtain the checksum
            int checksum = sum ^ 0xFFFF;

            return checksum;
        }

    }
}
