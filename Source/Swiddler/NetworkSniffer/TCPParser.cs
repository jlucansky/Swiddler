namespace Swiddler.NetworkSniffer
{
    class TCPParser : PacketParserBase
    {
        public override bool ParseHeader(RawPacket packet)
        {
            var raw = packet.Buffer;
            var p = packet.HeaderLength; // IP header

            if (packet.DataLength < 20)
                return false; // too small

            packet.Source.Port = (raw[p++] << 8) | raw[p++];
            packet.Destination.Port = (raw[p++] << 8) | raw[p++];

            packet.Sequence = GetUInt32(raw, p); p += 4;
            packet.Acknowledgment = GetUInt32(raw, p); p += 4;

            var dataOffset = (raw[p] & 0xF0) >> 4;

            if (dataOffset < 5 || dataOffset > 15)
                return false;

            if ((raw[p++] & 0x01) != 0) packet.Flags |= TCPFlags.NS;
            
            if ((raw[p] & 0x80) != 0) packet.Flags |= TCPFlags.CWR;
            if ((raw[p] & 0x40) != 0) packet.Flags |= TCPFlags.ECE;
            if ((raw[p] & 0x20) != 0) packet.Flags |= TCPFlags.URG;
            if ((raw[p] & 0x10) != 0) packet.Flags |= TCPFlags.ACK;
            if ((raw[p] & 0x08) != 0) packet.Flags |= TCPFlags.PSH;
            if ((raw[p] & 0x04) != 0) packet.Flags |= TCPFlags.RST;
            if ((raw[p] & 0x02) != 0) packet.Flags |= TCPFlags.SYN;
            if ((raw[p] & 0x01) != 0) packet.Flags |= TCPFlags.FIN;

            p += 2; // ignore window size
            packet.Checksum = (raw[p++] << 8) + raw[p++];

            var dataByteOffset = dataOffset * 4;
            packet.HeaderLength += dataByteOffset;
            packet.DataLength -= dataByteOffset;

            return true;
        }

        uint GetUInt32(byte[] buffer, int index)
        {
            return (uint)(
                (buffer[index++] << 24) |
                (buffer[index++] << 16) |
                (buffer[index++] << 8)  |
                buffer[index]);
        }
    }
}
