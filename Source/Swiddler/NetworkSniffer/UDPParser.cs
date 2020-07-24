namespace Swiddler.NetworkSniffer
{
    class UDPParser : PacketParserBase
    {
        public override bool ParseHeader(RawPacket packet)
        {
            if (packet.DataLength < 8)
                return false; // too small

            var raw = packet.Buffer;
            var p = packet.HeaderLength; // IP header

            packet.Source.Port = (raw[p++] << 8) | raw[p++];
            packet.Destination.Port = (raw[p++] << 8) | raw[p++];

            p += 2; // ignore Length field

            packet.Checksum = (raw[p++] << 8) + raw[p++];

            packet.HeaderLength += 8;
            packet.DataLength -= 8;

            return true;
        }
    }
}
