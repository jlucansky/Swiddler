using System;
using System.Net;
using System.Net.Sockets;

namespace Swiddler.NetworkSniffer
{
    abstract class PacketParserBase
    {
        public abstract bool ParseHeader(RawPacket packet);
    }

    class IPParser_v4 : PacketParserBase
    {
        const int SOURCE_IP_START_INDEX = 12;
        const int DESTINATION_IP_START_INDEX = 16;

        public override bool ParseHeader(RawPacket packet)
        {
            var raw = packet.Buffer;

            if (raw.Length < 20) return false;

            var ihl = raw[0] & 0xf; // Internet Header Length
            if (ihl < 5 || ihl > 15)
                return false;

            packet.Source = new IPEndPoint(BitConverter.ToUInt32(raw, SOURCE_IP_START_INDEX), 0);
            packet.Destination = new IPEndPoint(BitConverter.ToUInt32(raw, DESTINATION_IP_START_INDEX), 0);
            packet.Protocol = (ProtocolType)raw[9];
            packet.HeaderLength = ihl * 4;
            packet.DataLength = ((raw[2] << 8) | raw[3]) - packet.HeaderLength;

            if (packet.DataLength > raw.Length + packet.HeaderLength)
                return false;

            return true;
        }

        public bool ParseIdentification(RawPacket packet, out int identification, out int fragmentOffset, out bool moreFragments)
        {
            var raw = packet.Buffer;

            identification = (raw[4] << 8) | raw[5];
            moreFragments = ((raw[6] >> 5) & 1) == 1; // take 3rd bit
            fragmentOffset = (((byte)(raw[6] << 3) << 5) + raw[7]) * 8; // take remaining 5bits + 8bits
            
            return true;
        }
    }

    class IPParser_v6 : PacketParserBase
    {
        const int SOURCE_IP_START_INDEX = 8;
        const int DESTINATION_IP_START_INDEX = 24;

        public override bool ParseHeader(RawPacket packet)
        {
            var raw = packet.Buffer;

            if (raw.Length < 40) return false;

            var ip = new byte[16];

            Buffer.BlockCopy(raw, SOURCE_IP_START_INDEX, ip, 0, ip.Length);
            packet.Source = new IPEndPoint(new IPAddress(ip), 0);

            Buffer.BlockCopy(raw, DESTINATION_IP_START_INDEX, ip, 0, ip.Length);
            packet.Destination = new IPEndPoint(new IPAddress(ip), 0);

            packet.Protocol = (ProtocolType)raw[6];
            packet.HeaderLength = 40;
            packet.DataLength = (raw[4] << 8) | raw[5];

            if (packet.DataLength > raw.Length + packet.HeaderLength)
                return false;

            return true;
        }

        public bool ParseIdentification(RawPacket packet, out int identification, out int fragmentOffset, out bool moreFragments)
        {
            var raw = packet.Buffer;

            if (packet.HeaderLength + 8 > raw.Length) // check minimum extension len
            {
                identification = 0;
                fragmentOffset = 0;
                moreFragments = false;
                return false;
            }

            var h = packet.HeaderLength;

            identification = BitConverter.ToInt32(raw, h + 4);
            moreFragments = (raw[h + 3] & 1) == 1;
            fragmentOffset = ((raw[h + 2] << 8) | (byte)((raw[h + 3] & 0xF8) >> 3)) * 8; // take 13 bits

            packet.Protocol = (ProtocolType)raw[h]; // update next protocol
            packet.HeaderLength += 8;

            return true;
        }

        public bool ParseExtensionHeader(RawPacket packet)
        {
            var raw = packet.Buffer;

            if (packet.HeaderLength + 8 > raw.Length) // check minimum extension len
                return false;

            packet.Protocol = (ProtocolType)raw[packet.HeaderLength++]; // update next protocol
            var len = raw[packet.HeaderLength++] * 8;
            packet.HeaderLength += len + 6; // increment len header - in 8-octet units, not including the first 8 octets

            return true;
        }
    }

}
