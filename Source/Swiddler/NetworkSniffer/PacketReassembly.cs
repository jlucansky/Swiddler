using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Swiddler.NetworkSniffer
{
    public class PacketReassembly
    {
        public PacketFilter Filter { get; set; } = new PacketFilter();
        public int FragmentCacheTimeout { get; set; } = 60;
        public int StateTimeout { get; set; } = 100;

        DateTime fragmentCachePurgeSchedule;
        private readonly Dictionary<IPFragmentKey, IPFragmentReassembly> fragmentCache = new Dictionary<IPFragmentKey, IPFragmentReassembly>();

        DateTime tcpStatePurgeSchedule;
        private readonly Dictionary<TCPStateKey, TCPState> tcpStates = new Dictionary<TCPStateKey, TCPState>();

        readonly IPParser_v4 ipParser_v4;
        readonly IPParser_v6 ipParser_v6;
        readonly TCPParser tcpParser;
        readonly UDPParser udpParser;

        readonly object syncObj = new object();

        public PacketReassembly()
        {
            ipParser_v4 = new IPParser_v4();
            ipParser_v6 = new IPParser_v6();
            tcpParser = new TCPParser();
            udpParser = new UDPParser();
        }

        public RawPacket[] GetPackets(byte[] raw)
        {
            lock (syncObj)
            {
                PurgeFragmentCache();
                PurgeStates();

                // *********************
                // *** Parse Layer 3 ***
                // *********************

                var version = (IPVersion)((raw[0] & 0xF0) >> 4);

                var packet = new RawPacket() { Buffer = raw, Version = version };

                PacketParserBase l3parser = null;

                switch (packet.Version)
                {
                    case IPVersion.IPv4:
                        l3parser = ipParser_v4;
                        break;
                    case IPVersion.IPv6:
                        l3parser = ipParser_v6;
                        break;
                    default: return null;
                }

                if (!l3parser.ParseHeader(packet)) 
                    return null;

                if (!Filter.ShouldCapture(packet.Source.Address, packet.Destination.Address))
                    return null;


                int identification = 0, fragmentOffset = 0;
                bool moreFragments = false;

                if (packet.Version == IPVersion.IPv4)
                {
                    if (!ipParser_v4.ParseIdentification(packet, out identification, out fragmentOffset, out moreFragments))
                        return null;
                }
                if (packet.Version == IPVersion.IPv6)
                {
                    while (true)
                    {
                        if (packet.Protocol == ProtocolType.IPv6HopByHopOptions ||
                            packet.Protocol == ProtocolType.IPv6RoutingHeader ||
                            packet.Protocol == ProtocolType.IPv6DestinationOptions)
                        {
                            if (!ipParser_v6.ParseExtensionHeader(packet)) return null;
                            continue;
                        }
                        if (packet.Protocol == ProtocolType.IPv6FragmentHeader)
                        {
                            if (!ipParser_v6.ParseIdentification(packet, out identification, out fragmentOffset, out moreFragments)) return null;
                            continue;
                        }
                        break;
                    }
                }

                if (moreFragments || fragmentOffset > 0)
                { 
                    // Fragmented packet

                    var fk = new IPFragmentKey() { Address = packet.Source.Address, Identification = identification };
                    
                    if (!fragmentCache.TryGetValue(fk, out var frag))
                    {
                        frag = new IPFragmentReassembly();
                        fragmentCache.Add(fk, frag);
                    }

                    frag.AddFragment(packet, fragmentOffset, moreFragments);

                    if (frag.ReassembledPacket != null) // successfully reassembled
                    {
                        packet = frag.ReassembledPacket;
                        fragmentCache.Remove(fk);
                    }
                }

                // *********************
                // *** Parse Layer 4 ***
                // *********************

                PacketParserBase l4parser = null;

                switch (packet.Protocol)
                {
                    case ProtocolType.Tcp:
                        l4parser = tcpParser;
                        break;
                    case ProtocolType.Udp:
                        l4parser = udpParser;
                        break;
                    default: return null;
                }

                if (!l4parser.ParseHeader(packet))
                    return null;

                if (!Filter.ShouldCapture(packet))
                    return null;

                if (packet.Protocol == ProtocolType.Tcp)
                {
                    // TCP reassembly

                    var stateKey = new TCPStateKey() { Source = packet.Source, Destination = packet.Destination };

                    if (!tcpStates.TryGetValue(stateKey, out var state))
                    {
                        state = new TCPState() { AckedSequence = packet.Sequence };
                        tcpStates.Add(stateKey, state);
                    }

                    state.Put(packet);

                    List<RawPacket> packets = null;
                    RawPacket segment = null;
                    while ((segment = state.TryGet()) != null)
                    {
                        if (segment.Flags.HasFlag(TCPFlags.SYN) && !tcpStates.ContainsKey(stateKey.Invert()))
                            state.IsActiveOpener = true; // first SYN is client side

                        if (packets == null) packets = new List<RawPacket>();
                        packets.Add(segment);
                    }

                    return packets?.ToArray();
                }
                else
                {
                    return new[] { packet };
                }
            }
        }

        void PurgeFragmentCache()
        {
            if (DateTime.UtcNow > fragmentCachePurgeSchedule)
            {
                var oldTimestamp = DateTime.UtcNow.AddSeconds(-FragmentCacheTimeout);

                foreach (var item in fragmentCache.Where(x => x.Value.TimestampUtc < oldTimestamp).ToArray())
                    fragmentCache.Remove(item.Key);

                fragmentCachePurgeSchedule = DateTime.UtcNow.AddSeconds(FragmentCacheTimeout);
            }
        }

        void PurgeStates()
        {
            if (DateTime.UtcNow > tcpStatePurgeSchedule)
            {
                foreach (var item in tcpStates.Where(x => x.Value.CanPurge()).ToArray())
                    tcpStates.Remove(item.Key);

                tcpStatePurgeSchedule = DateTime.UtcNow.AddSeconds(StateTimeout);
            }
        }

    }

}
