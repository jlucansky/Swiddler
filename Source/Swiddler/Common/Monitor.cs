using Swiddler.Utils;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Swiddler.Common
{
    public enum MonitorEvent
    {
        Send = 1,
        Recv = 2,
        SendTo = 3,
        RecvFrom = 4,
        Connecting = 5,
        Connected = 6,
        Accepted = 7,
        Listen = 8,
        Close = 9,
    }

    public class CapturedPacket
    {
        public ulong Handle;
        public MonitorEvent Event;
        public SocketError Error;
        public ProtocolType Protocol;
        public IPEndPoint LocalEndPoint;
        public IPEndPoint RemoteEndPoint;
        public byte[] Data;

        public override string ToString()
        {
            return $"0x{Handle.ToString("X8")} {Protocol} {Event} [{LocalEndPoint}, {RemoteEndPoint}] {Error} {Data.Length}";
        }
    }

    public class Monitor
    {
        readonly BinaryReader _reader;
        static readonly byte[] _emptyData = new byte[0];

        public Monitor(Stream stream)
        {
            _reader = new BinaryReader(stream);
        }

        public CapturedPacket Read()
        {
            /*
				NOTIFY(&notif->op, sizeof(BYTE));
				NOTIFY(&sock_handle, sizeof(ULONG64));
				NOTIFY(&notif->wsError, sizeof(INT32));
				NOTIFY(&proto_info.iProtocol, sizeof(INT32)); // IPPROTO_TCP=6, IPPROTO_UDP=17
				NOTIFY(&local_address, local_addr_len);
				NOTIFY(&remote_address, remote_addr_len);

                NOTIFY(&notif->len, sizeof(INT32));
				if (notif->len > 0) 
					NOTIFY(notif->buffer, notif->len);
            */

            var packet = new CapturedPacket
            {
                Event = (MonitorEvent)_reader.ReadByte(),
                Handle = _reader.ReadUInt64(),
                Error = (SocketError)_reader.ReadInt32(),
                Protocol = (ProtocolType)_reader.ReadInt32(),

                LocalEndPoint = ReadEndPoint(),
                RemoteEndPoint = ReadEndPoint()
            };

            var len = _reader.ReadInt32();
            if (len > 0)
                packet.Data = _reader.ReadBytes(len);
            else
                packet.Data = _emptyData;

            if (packet.Event == MonitorEvent.Recv && packet.Error == SocketError.Success && packet.Protocol == ProtocolType.Tcp && packet.Data.Length == 0)
                packet.Error = (SocketError)Net.ERROR_GRACEFUL_DISCONNECT; // received empty data without error for TCP protocol signalize graceful disconnect

            return packet;
        }

        IPEndPoint ReadEndPoint()
        {
            /*
                16 IPv4
                2    USHORT sin_family; // = 2
                2    USHORT sin_port;
                4    ULONG sin_addr;
                8    CHAR sin_zero[8];

                28 IPv6
                2    USHORT sin6_family; // = 23
                2    USHORT sin6_port;
                4    ULONG  sin6_flowinfo;
                16   BYTE[16] sin6_addr;
                4	 ULONG sin6_scope_id;
            */

            var sin_family = (AddressFamily)_reader.ReadUInt16();

            if (sin_family == AddressFamily.InterNetwork)
            {
                var sin_port = (ushort)IPAddress.NetworkToHostOrder(_reader.ReadInt16());
                var sin_addr = _reader.ReadUInt32();
                var sin_zero = _reader.ReadBytes(8);
                return new IPEndPoint(sin_addr, sin_port);
            }
            else if (sin_family == AddressFamily.InterNetworkV6)
            {
                var sin_port = (ushort)IPAddress.NetworkToHostOrder(_reader.ReadInt16());
                var sin6_flowinfo = _reader.ReadUInt32();
                var sin6_addr = _reader.ReadBytes(16);
                var sin6_scope_id = _reader.ReadUInt32();

                return new IPEndPoint(new IPAddress(sin6_addr, sin6_scope_id), sin_port);
            }
            else if (sin_family == AddressFamily.Unspecified)
            {
                return null;
            }

            throw new InvalidOperationException("Invalid address family: " + sin_family);
        }
    }
}
