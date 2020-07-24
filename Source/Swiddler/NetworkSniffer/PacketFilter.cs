using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Swiddler.NetworkSniffer
{
    class CaptureFilterItem
    {
        public IPEndPoint EP;
        public ProtocolType Protocol;
    }

    public class PacketFilter
    {
        readonly List<CaptureFilterItem> rules = new List<CaptureFilterItem>();

        public void Add(IPEndPoint ep, ProtocolType protocol = ProtocolType.Unspecified)
        {
            if (ep.AddressFamily != AddressFamily.InterNetwork)
                throw new Exception("Only IPv4 is supported.");

            rules.Add(new CaptureFilterItem() { EP = ep, Protocol = protocol });
        }

        public bool ShouldCapture(IPAddress source, IPAddress destination)
        {
            if (rules.Count == 0)
                return true;

            foreach (var filter in rules)
            {
                var addr = filter.EP.Address;
                if (addr.Equals(IPAddress.Any) ||
                    addr.Equals(source) ||
                    addr.Equals(destination))
                    return true;
            }

            return false;
        }

        public bool ShouldCapture(RawPacket packet)
        {
            if (rules.Count == 0)
                return true;

            foreach (var filter in rules)
            {
                if (filter.Protocol == ProtocolType.Unspecified ||
                    filter.Protocol == packet.Protocol)
                {
                    var addr = filter.EP.Address;
                    if (addr.Equals(IPAddress.Any) ||
                        addr.Equals(packet.Source.Address) ||
                        addr.Equals(packet.Destination.Address))
                    {
                        var port = filter.EP.Port;
                        if (port == 0 ||
                            port == packet.Source.Port ||
                            port == packet.Destination.Port)
                            return true;
                    }
                }
            }

            return false;
        }

    }

}
