using System;
using System.Linq;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Swiddler.Properties;
using System.Collections;

namespace Swiddler.Utils
{
    public static class Net
    {
        const int NO_ERROR = 0;
        const int AF_INET = (int)AddressFamily.InterNetwork;
        const int AF_INET6 = (int)AddressFamily.InterNetworkV6;

        public const int ERROR_GRACEFUL_DISCONNECT = 0x4CA;


        public static void Connect(this TcpClient client, Uri uri, out int elapsedMilliseconds)
        {
            var watch = Stopwatch.StartNew();

            if (IPAddress.TryParse(uri.Host, out var ip))
            {
                client.Connect(ip, uri.Port);
            }
            else
            {
                if (client.Client.LocalEndPoint.IsIPv6() && client.Client.DualMode)
                {
                    // in dual-mode let's decide DNS server, which IP address prefer first
                    IPAddress[] addresses = Dns.GetHostAddresses(uri.Host);
                    if (addresses.Length == 0) throw new SocketException((int)SocketError.NotConnected);
                    client.Connect(addresses[0], uri.Port);
                }
                else
                {
                    client.Connect(uri.Host, uri.Port);
                }
            }

            elapsedMilliseconds = (int)watch.ElapsedMilliseconds;
        }

        public static void Connect(this UdpClient client, Uri uri, out int elapsedMilliseconds)
        {
            var watch = Stopwatch.StartNew();

            if (IPAddress.TryParse(uri.Host, out var ip))
            {
                client.Connect(ip, uri.Port);
            }
            else
            {
                if (client.Client.LocalEndPoint.IsIPv6() && client.Client.DualMode)
                {
                    // in dual-mode let's decide DNS server, which IP address prefer first
                    IPAddress[] addresses = Dns.GetHostAddresses(uri.Host);
                    if (addresses.Length == 0) throw new SocketException((int)SocketError.NotConnected);
                    client.Connect(addresses[0], uri.Port);
                }
                else
                {
                    client.Connect(uri.Host, uri.Port);
                }
            }

            elapsedMilliseconds = (int)watch.ElapsedMilliseconds;
        }

        public static bool IsIPv6(this EndPoint ep)
        {
            return ep.AddressFamily == AddressFamily.InterNetworkV6;
        }

        public static bool IsIPv6(this IPAddress addr)
        {
            return addr.AddressFamily == AddressFamily.InterNetworkV6;
        }

        public static int GetProcessId(this IPEndPoint ep, bool local = true)
        {
            if (ep == null) return 0;

            int dwPort = (ushort)IPAddress.HostToNetworkOrder((short)ep.Port);
            if (ep.AddressFamily == AddressFamily.InterNetwork)
            {
                foreach (var conn in EnumTcpConnectionsV4())
                {
                    if (local)
                    {
                        if (new IPAddress(conn.dwLocalAddr).Equals(ep.Address) && conn.dwLocalPort == dwPort)
                            return conn.dwOwningPid;
                    }
                    else
                    {
                        if (new IPAddress(conn.dwRemoteAddr).Equals(ep.Address) && conn.dwRemotePort == dwPort)
                            return conn.dwOwningPid;
                    }
                }
            }
            if (ep.AddressFamily == AddressFamily.InterNetworkV6)
            {
                foreach (var conn in EnumTcpConnectionsV6())
                {
                    if (local)
                    {
                        if (new IPAddress(conn.ucLocalAddr).Equals(ep.Address) && conn.dwLocalPort == dwPort)
                            return conn.dwOwningPid;
                    }
                    else
                    {
                        if (new IPAddress(conn.ucRemoteAddr).Equals(ep.Address) && conn.dwRemotePort == dwPort)
                            return conn.dwOwningPid;
                    }
                }
            }

            if (ep.Address.IsIPv4MappedToIPv6) // try again with IPv4
                return GetProcessId(new IPEndPoint(ep.Address.MapToIPv4(), ep.Port), local);

            return 0; // nothing else to do
        }

        public static bool IsInSubnet(this IPAddress address, IPAddress netmask, int cidr)
        {
            if (netmask.AddressFamily != address.AddressFamily)
                return false; // We got something like an IPV4-Address for an IPv6-Mask. This is not valid.

            if (netmask.AddressFamily == AddressFamily.InterNetwork)
            {
                // Convert the mask address to an unsigned integer.
                var maskAddressBits = BitConverter.ToUInt32(netmask.GetAddressBytes().Reverse().ToArray(), 0);

                // And convert the IpAddress to an unsigned integer.
                var ipAddressBits = BitConverter.ToUInt32(address.GetAddressBytes().Reverse().ToArray(), 0);

                // Get the mask/network address as unsigned integer.
                uint mask = uint.MaxValue << (32 - cidr);

                // https://stackoverflow.com/a/1499284/3085985
                // Bitwise AND mask and MaskAddress, this should be the same as mask and IpAddress
                // as the end of the mask is 0000 which leads to both addresses to end with 0000
                // and to start with the prefix.
                return (maskAddressBits & mask) == (ipAddressBits & mask);
            }

            if (netmask.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // Convert the mask address to a BitArray.
                var maskAddressBits = new BitArray(netmask.GetAddressBytes());

                // And convert the IpAddress to a BitArray.
                var ipAddressBits = new BitArray(address.GetAddressBytes());

                if (maskAddressBits.Length != ipAddressBits.Length)
                {
                    throw new ArgumentException("Length of IP Address and Subnet Mask do not match.");
                }

                // Compare the prefix bits.
                for (int i = 0; i < cidr; i++)
                {
                    if (ipAddressBits[i] != maskAddressBits[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            throw new NotSupportedException("Only InterNetworkV6 or InterNetwork address families are supported.");
        }

        static readonly IPAddress multicastAddress4 = IPAddress.Parse("224.0.0.0");
        static readonly IPAddress multicastAddress6 = IPAddress.Parse("ff00::");

        public static bool IsMulticast(this IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
                return address.IsInSubnet(multicastAddress4, 4);

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                return address.IsInSubnet(multicastAddress6, 8);

            throw new NotSupportedException("Only InterNetworkV6 or InterNetwork address families are supported.");
        }

        private static IEnumerable<MIB_TCPROW_OWNER_PID> EnumTcpConnectionsV4()
        {
            int dwSize = sizeof(int) + Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID)) * 100;
            IntPtr hTcpTable;
            {
                hTcpTable = Marshal.AllocHGlobal(dwSize);
                int ret = GetExtendedTcpTable(hTcpTable, ref dwSize, true, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_CONNECTIONS, 0);
                if (ret != NO_ERROR)
                {
                    // retry for new dwSize.
                    Marshal.FreeHGlobal(hTcpTable);
                    hTcpTable = Marshal.AllocHGlobal(dwSize);
                    ret = GetExtendedTcpTable(hTcpTable, ref dwSize, true, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_CONNECTIONS, 0);
                    if (ret != NO_ERROR)
                    {
                        Marshal.FreeHGlobal(hTcpTable);
                        throw new Exception("GetExtendedTcpTable return: " + ret);
                    }
                }
            }
            {
                MIB_TCPROW_OWNER_PID item = new MIB_TCPROW_OWNER_PID();
                int dwNumEntries = Marshal.ReadInt32(hTcpTable);
                IntPtr pItem = new IntPtr(hTcpTable.ToInt64() + sizeof(int));
                for (int i = 0; i < dwNumEntries; ++i)
                {
                    Marshal.PtrToStructure(pItem, item);
                    pItem = new IntPtr(pItem.ToInt64() + Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID)));
                    yield return item;
                }
                Marshal.FreeHGlobal(hTcpTable);
            }
        }

        private static IEnumerable<MIB_TCP6ROW_OWNER_PID> EnumTcpConnectionsV6()
        {
            int dwSize = sizeof(int) + Marshal.SizeOf(typeof(MIB_TCP6ROW_OWNER_PID)) * 100;
            IntPtr hTcpTable;
            {
                hTcpTable = Marshal.AllocHGlobal(dwSize);
                int ret = GetExtendedTcpTable(hTcpTable, ref dwSize, true, AF_INET6, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_CONNECTIONS, 0);
                if (ret != NO_ERROR)
                {
                    // retry for new dwSize.
                    Marshal.FreeHGlobal(hTcpTable);
                    hTcpTable = Marshal.AllocHGlobal(dwSize);
                    ret = GetExtendedTcpTable(hTcpTable, ref dwSize, true, AF_INET6, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_CONNECTIONS, 0);
                    if (ret != NO_ERROR)
                    {
                        Marshal.FreeHGlobal(hTcpTable);
                        throw new Exception("GetExtendedTcpTable return: " + ret);
                    }
                }
            }
            {
                MIB_TCP6ROW_OWNER_PID item = new MIB_TCP6ROW_OWNER_PID();
                int dwNumEntries = Marshal.ReadInt32(hTcpTable);
                IntPtr pItem = new IntPtr(hTcpTable.ToInt64() + sizeof(int));
                for (int i = 0; i < dwNumEntries; ++i)
                {
                    Marshal.PtrToStructure(pItem, item);
                    pItem = new IntPtr(pItem.ToInt64() + Marshal.SizeOf(typeof(MIB_TCP6ROW_OWNER_PID)));
                    yield return item;
                }
                Marshal.FreeHGlobal(hTcpTable);
            }
        }

        /// <summary>
        /// Creates TcpListener on random port
        /// </summary>
        public static TcpListener CreateNewListener(IPAddress address, TimeSpan timeout)
        {
            var r = new Random();
            var timer = Stopwatch.StartNew();

            while (true)
            {
                try
                {
                    int port = r.Next(32768, 65535);
                    var listener = new TcpListener(new IPEndPoint(address, port));
                    listener.Start();
                    return listener; // success, nothing crashed
                }
                catch (SocketException) when (timer.Elapsed < timeout)
                {
                    continue; // ignore error until timeout reached
                }
            }
        }

        private static readonly Lazy<Dictionary<int, string>> _knownPorts = new Lazy<Dictionary<int, string>>(() =>
            Resources.port_numbers.Split('\n').Select(line => line.Split(',')).ToDictionary(arr => int.Parse(arr[0]), arr => arr[1]));
        private static readonly Lazy<Dictionary<string, int>> _knownPortsReverse = new Lazy<Dictionary<string, int>>(() =>
        {
            var dict = _knownPorts.Value.ToLookup(x => x.Value, x => x.Key).ToDictionary(x => x.Key, x => x.First());
            dict["rdp"] = 3389;
            dict["smb"] = 445;
            return dict;
        });

        public static string GetPortName(int port)
        {
            if (_knownPorts.Value.TryGetValue(port, out var name))
                return name;
            return null;
        }

        public static int GetPortNumber(string scheme)
        {
            if (_knownPortsReverse.Value.TryGetValue(scheme, out var port))
                return port;
            return -1;
        }

        public static bool TryParseUri(string text, out UriBuilder uri)
        {
            uri = null;

            text = text?.Trim();
            if (string.IsNullOrEmpty(text) || text.IndexOfAny(new[] { ' ', '\n', '\r', '\t', '\f' }) != -1) return false;

            var hasScheme = Uri.TryCreate(text, UriKind.Absolute, out var uri_) && text.StartsWith(uri_.Scheme + "://", StringComparison.OrdinalIgnoreCase);
            if (!hasScheme)
            {
                if (Uri.TryCreate("unknown://" + text, UriKind.Absolute, out uri_) && uri_.Port == -1)
                    return false; // without scheme, it must have a port at least
            }

            if (uri_ != null) uri = new UriBuilder(uri_);

            if (uri == null && IPAddress.TryParse(text.Trim('\\', '/'), out var ip)) // IPv4/6 without port and scheme
                uri = new UriBuilder("unknown", ip.ToString(), -1);

            if (uri == null)
                return false;

            if (uri.Port == -1)
                uri.Port = text.EndsWith(":0") ? 0 : GetPortNumber(uri.Scheme);

            return true;
        }

        public static string GetTrimmedHost(this UriBuilder uri)
        {
            if (IPAddress.TryParse(uri.Host, out var ip))
                return ip.ToString(); // removes brackets from IPv6 like "[::1]" => "::1"
            return uri.Host;
        }

        #region iphlpapi

        [DllImport("iphlpapi.dll", SetLastError = true)]
        static extern int GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ipVersion, TCP_TABLE_CLASS tblClass, int reserved);

        [StructLayout(LayoutKind.Sequential)]
        class MIB_TCPROW_OWNER_PID
        {
            public int dwState;
            public uint dwLocalAddr;
            public int dwLocalPort;
            public uint dwRemoteAddr;
            public int dwRemotePort;
            public int dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        class MIB_TCP6ROW_OWNER_PID
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucLocalAddr;
            public int dwLocalScopeId;
            public int dwLocalPort;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] ucRemoteAddr;
            public int dwRemoteScopeId;
            public int dwRemotePort;
            public int dwState;
            public int dwOwningPid;
        }

        enum TCP_TABLE_CLASS
        {
            TCP_TABLE_BASIC_LISTENER,
            TCP_TABLE_BASIC_CONNECTIONS,
            TCP_TABLE_BASIC_ALL,
            TCP_TABLE_OWNER_PID_LISTENER,
            TCP_TABLE_OWNER_PID_CONNECTIONS,
            TCP_TABLE_OWNER_PID_ALL,
            TCP_TABLE_OWNER_MODULE_LISTENER,
            TCP_TABLE_OWNER_MODULE_CONNECTIONS,
            TCP_TABLE_OWNER_MODULE_ALL,
        }

        enum TcpState
        {
            CLOSED = 1,
            LISTEN = 2,
            SYN_SENT = 3,
            SYN_RCVD = 4,
            ESTAB = 5,
            FIN_WAIT1 = 6,
            FIN_WAIT2 = 7,
            CLOSE_WAIT = 8,
            CLOSING = 9,
            LAST_ACK = 10,
            TIME_WAIT = 11,
            DELETE_TCB = 12,
        }

        #endregion
    }

}
