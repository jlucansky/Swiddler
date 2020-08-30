using Swiddler.Common;
using Swiddler.DataChunks;
using Swiddler.NetworkSniffer;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Windows;

namespace Swiddler.Channels
{
    public class SnifferChannel : Channel, IDisposable
    {
        public IPAddress LocalAddress { get; set; }
        public bool PromiscuousMode { get; set; }
        public Tuple<IPEndPoint, ProtocolType>[] CaptureFilter { get; set; }


        private Socket socket;

        readonly byte[] RCVALL_ON = new byte[4] { 1, 0, 0, 0 }; // promiscuous mode
        readonly byte[] RCVALL_IPLEVEL = new byte[4] { 3, 0, 0, 0 };

        private readonly byte[] buffer = new byte[0x10000];

        private readonly PacketReassembly reassembly = new PacketReassembly();

        private readonly Dictionary<ConnectionKey, Mediator> connections = new Dictionary<ConnectionKey, Mediator>();

        class ConnectionKey
        {
            public IPEndPoint LocalEP, RemoteEP;

            public ConnectionKey(RawPacket raw, IPAddress localIP)
            {
                if (raw.Source.Address.Equals(localIP))
                {
                    LocalEP = raw.Source;
                    RemoteEP = raw.Destination;
                }
                else
                {
                    LocalEP = raw.Destination;
                    RemoteEP = raw.Source;
                }
            }

            public override bool Equals(object obj)
            {
                if (!(obj is ConnectionKey other)) return false;

                if (LocalEP.Equals(other.LocalEP) && RemoteEP.Equals(other.RemoteEP))
                    return true;
                if (RemoteEP.Equals(other.LocalEP) && LocalEP.Equals(other.RemoteEP))
                    return true;

                return false;
            }

            public override int GetHashCode()
            {
                var sh = LocalEP.GetHashCode();
                var dh = RemoteEP.GetHashCode();
                if (sh < dh) return sh ^ dh; else return dh ^ sh;
            }
        }

        private class Mediator : Channel
        {
            public ConnectionKey EP { get; set; }
            public bool IsServer { get; set; } // local EP is server
            public Mediator(Session session) : base(session) { }
            protected override void OnReceiveNotification(Packet packet) => throw new NotImplementedException();
            public void Send(Packet packet) => NotifyObservers(packet); // write to session UI

            public void Close(string reason)
            {
                HandleError(new Exception(reason));
            }
            public void ClosingDueFlag(string flag, IPEndPoint source)
            {
                Close($"Closing due to {flag} flag sent from {source}");
            }
        }

        public SnifferChannel(Session session) : base(session) { }

        protected override void StartOverride()
        {
            var ip = LocalAddress;

            foreach (var item in CaptureFilter)
            {
                reassembly.Filter.Add(item.Item1, item.Item2);
            }

            try
            {
                if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    // IPv6 support is experimental - currently doesn't work because IP headers are not captured
                    socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Raw, ProtocolType.Raw);
                    socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.HeaderIncluded, true);
                }
                else
                {
                    try
                    {
                        socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AccessDenied)
                    {
                        StartAsAdmin();
                        throw;
                    }
                }

                socket.Bind(new IPEndPoint(ip, 0));
                socket.IOControl(IOControlCode.ReceiveAll, PromiscuousMode ? RCVALL_ON : RCVALL_IPLEVEL, null);

                BeginReceive();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        void StartAsAdmin()
        {
            if (!string.IsNullOrEmpty(Session.SettingsFileName))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (MessageBox.Show(
                        "You don’t have permission to create raw sockets.\n\nDo you want to launch Swiddler as Administrator?",
                        "Access Denied", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
                    {
                        try { Session.StartAsAdmin(); } catch  { }
                    }
                }));
            }
        }

        void BeginReceive()
        {
            try
            {
                socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), null);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                int received = socket.EndReceive(result);

                if (received > 0)
                {
                    byte[] data = new byte[received];
                    Array.Copy(buffer, 0, data, 0, received);

                    var packets = reassembly.GetPackets(data);

                    if (packets?.Length > 0)
                    {
                        foreach (var raw in packets)
                        {
                            var packet = new Packet() { Payload = new byte[raw.DataLength] };
                            Array.Copy(raw.Buffer, raw.HeaderLength, packet.Payload, 0, raw.DataLength);

                            // TODO: raw.Dropped

                            var mediator = GetChild(raw);
                            
                            if (mediator != null)
                            {
                                if (mediator.EP.LocalEP.Equals(raw.Source))
                                {
                                    packet.Flow = TrafficFlow.Outbound;
                                    packet.Source = mediator.EP.LocalEP;
                                    packet.Destination = mediator.EP.RemoteEP;
                                }
                                else
                                {
                                    packet.Flow = TrafficFlow.Inbound;
                                    packet.Source = mediator.EP.RemoteEP;
                                    packet.Destination = mediator.EP.LocalEP;
                                }

                                mediator.Send(packet);

                                if (raw.Flags.HasFlag(TCPFlags.RST)) mediator.ClosingDueFlag("RST", raw.Source);
                                if (raw.Flags.HasFlag(TCPFlags.FIN)) mediator.ClosingDueFlag("FIN", raw.Source);
                            }
                        }
                    }
                }

                BeginReceive();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        Mediator GetChild(RawPacket raw)
        {
            var ep = new ConnectionKey(raw, LocalAddress);
            lock (connections)
            {
                if (connections.TryGetValue(ep, out var mediator) == false)
                {
                    if (raw.Flags.HasFlag(TCPFlags.RST) || raw.Flags.HasFlag(TCPFlags.FIN))
                        return null; // do not create child on closing session

                    var child = Session.NewChildSession();
                    child.ProtocolType = raw.Protocol;

                    Session.Storage.Write(new MessageData() { Text = $"New connection observed {raw.Source} -> {raw.Destination}", Type = MessageType.Connecting });

                    mediator = new Mediator(child) { EP = ep };

                    string protoStr = raw.Protocol.ToString().ToUpperInvariant();

                    var source = raw.Source;
                    var destination = raw.Destination;

                    // can happen that SYN|ACK sniffed as a reply from server before SYN
                    if (raw.Flags.HasFlag(TCPFlags.SYN) && raw.Flags.HasFlag(TCPFlags.ACK))
                    {
                        destination = raw.Source;
                        source = raw.Destination;
                    }

                    if (destination.Address.Equals(LocalAddress))
                    {
                        child.Name = $"{source} → :{destination.Port} ({protoStr})";
                        mediator.IsServer = true; // when first packet is directed toward local IP, then local EP is probably server
                    }
                    else
                    {
                        if (source.Address.Equals(LocalAddress))
                            child.Name = $":{source.Port} → {destination} ({protoStr})";
                        else
                            child.Name = $"{source} → {destination} ({protoStr})";
                    }

                    mediator.Observe(child.SessionChannel); // received packets write to session Log

                    child.Start(); // immediately set session state to stared

                    child.ResolveProcessIdAsync(ep.LocalEP);

                    connections.Add(ep, mediator);
                }

                return mediator;
            }
        }

        protected override void OnReceiveNotification(Packet packet)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            socket?.Dispose();
            socket = null;
        }
    }
}
