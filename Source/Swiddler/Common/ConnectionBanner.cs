using Swiddler.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Swiddler.Common
{
    public class ConnectionBanner : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string ConnectionText { get; set; } // "Connected to tcp://..."
        public string LocalEndpoint { get; set; }
        public string RemoteEndpoint { get; set; }

        public ConnectionBanner() { }
        public ConnectionBanner(Socket socket) : this((IPEndPoint)socket.LocalEndPoint, (IPEndPoint)socket.RemoteEndPoint) { }

        readonly IPEndPoint _remoteEP;

        static readonly Dictionary<IPAddress, string> dnsCache = new Dictionary<IPAddress, string>();
        static DateTime lastDnsResolve;

        public ConnectionBanner(IPEndPoint local, IPEndPoint remote)
        {
            _remoteEP = remote;

            LocalEndpoint = FormatAddress(local);


            if (ResolveHostFromCache(remote.Address) is string host)
            {
                UpdateRemoteEndpoint(host);
            }
            else
            {
                UpdateRemoteEndpoint();
                _ = ResolveHostAsync();
            }
        }

        async Task ResolveHostAsync()
        {
            var ip = _remoteEP.Address;
            try
            {
                var entry = await Dns.GetHostEntryAsync(ip);
                UpdateDnsCache(ip, entry.HostName);
                UpdateRemoteEndpoint(entry.HostName);
            }
            catch 
            {
                UpdateDnsCache(ip, null);
            }
        }

        static string ResolveHostFromCache(IPAddress ip)
        {
            lock (dnsCache)
            {
                if (lastDnsResolve.AddSeconds(20) < DateTime.UtcNow)
                    dnsCache.Clear();
                else if (dnsCache.TryGetValue(ip, out var host))
                    return host;
            }

            return null;
        }

        static void UpdateDnsCache(IPAddress ip, string host)
        {
            lock (dnsCache) dnsCache[ip] = host;
            lastDnsResolve = DateTime.UtcNow;
        }

        void UpdateRemoteEndpoint(string host = null)
        {
            var protocol = Net.GetPortName(_remoteEP.Port);

            if (protocol == null && host == null)
                RemoteEndpoint = FormatAddress(_remoteEP);
            else if (protocol == null && host != null)
                RemoteEndpoint = $"{FormatAddress(_remoteEP)} ({host})";
            else if (protocol != null && host == null)
                RemoteEndpoint = $"{FormatAddress(_remoteEP)} (:{protocol})";
            else
                RemoteEndpoint = $"{FormatAddress(_remoteEP)} ({host}:{protocol})";

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemoteEndpoint)));
        }

        string FormatAddress(IPEndPoint ep)
        {
            return ep.ToString();
        }
    }
}
