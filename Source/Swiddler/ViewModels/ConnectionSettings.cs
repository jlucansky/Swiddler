using Swiddler.Common;
using Swiddler.SocketSettings;
using Swiddler.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Swiddler.ViewModels
{
    public class ConnectionSettings : BindableBase, IComparable<ConnectionSettings>, ICloneable
    {
        public TCPServerSettings TCPServer { get; set; }
        public TCPClientSettings TCPClient { get; set; }
        public UDPClientSettings UDPClient { get; set; }
        public UDPServerSettings UDPServer { get; set; }

        // state properties must be after connection settings instances to preserve proper deserialization order

        bool _TCPChecked, _UDPChecked;
        public bool TCPChecked { get => _TCPChecked; set { if (SetProperty(ref _TCPChecked, value)) OnTCPToggled(value); } }
        public bool UDPChecked { get => _UDPChecked; set { if (SetProperty(ref _UDPChecked, value)) OnUDPToggled(value); } }

        bool _ClientChecked, _ServerChecked;
        public bool ClientChecked { get => _ClientChecked; set { if (SetProperty(ref _ClientChecked, value)) OnClientToggled(value); } }
        public bool ServerChecked { get => _ServerChecked; set { if (SetProperty(ref _ServerChecked, value)) OnServerToggled(value); } }

        string _ServerToggleText, _ClientToggleText;
        [XmlIgnore] public string ServerToggleText { get => _ServerToggleText; set => SetProperty(ref _ServerToggleText, value); }
        [XmlIgnore] public string ClientToggleText { get => _ClientToggleText; set => SetProperty(ref _ClientToggleText, value); }

        bool _TCPToggleLocked, _UDPToggleLocked, _ServerToggleLocked, _ClientToggleLocked;
        [XmlIgnore] public bool TCPToggleLocked { get => _TCPToggleLocked; set => SetProperty(ref _TCPToggleLocked, value); }
        [XmlIgnore] public bool UDPToggleLocked { get => _UDPToggleLocked; set => SetProperty(ref _UDPToggleLocked, value); }
        [XmlIgnore] public bool ServerToggleLocked { get => _ServerToggleLocked; set => SetProperty(ref _ServerToggleLocked, value); }
        [XmlIgnore] public bool ClientToggleLocked { get => _ClientToggleLocked; set => SetProperty(ref _ClientToggleLocked, value); }

        bool _AnyProtocolChecked;
        [XmlIgnore] public bool AnyProtocolChecked { get => _AnyProtocolChecked; set => SetProperty(ref _AnyProtocolChecked, value); }

        [XmlIgnore] public DateTime CreatedAt { get; private set; } = DateTime.Now;
        [XmlIgnore] public string FileName { get; private set; }

        public event Action IsDirtyChanged;
        [XmlIgnore] public bool IsDirty { get; private set; }

        [XmlIgnore] public ObservableCollection<SettingsBase> Settings { get; } = new ObservableCollection<SettingsBase>();

        public RewriteSettings[] Rewrites
        {
            get => Settings.OfType<RewriteSettings>().ToArray();
            set => AddRewrite(value);
        }

        private ConnectionSettings() { } // XML deserializer needs default ctor

        #region Initialization

        public static ConnectionSettings New() // create connection from template (QuickAction)
        {
            var obj = new ConnectionSettings();
            obj.Init();
            return obj;
        }

        void EnsureChildren()
        {
            if (TCPServer == null) TCPServer = new TCPServerSettings();
            if (TCPClient == null) TCPClient = new TCPClientSettings();
            if (UDPClient == null) UDPClient = new UDPClientSettings();
            if (UDPServer == null) UDPServer = new UDPServerSettings();
        }

        private void Init() // call after all properties are sets
        {
            EnsureChildren();

            PropertyChanged += MakeDirty;
            TCPClient.PropertyChanged += MakeDirty;
            TCPServer.PropertyChanged += MakeDirty;
            UDPClient.PropertyChanged += MakeDirty;
            UDPServer.PropertyChanged += MakeDirty;

            TCPClient.DualModeChanged += Client_DualModeChanged;
            TCPServer.DualModeChanged += Server_DualModeChanged;
            UDPClient.DualModeChanged += Client_DualModeChanged;
            UDPServer.DualModeChanged += Server_DualModeChanged;
            UDPClient.IsBroadcastChanged += UDPClient_IsBroadcastChanged;
            TCPClient.EnableSSLChanged += TCPClient_EnableSSLChanged;

            IsDirty = false;
        }

        public static ConnectionSettings TryCreateFromString(string text)
        {
            if (Net.TryParseUri(text, out var uri))
                return TryCreateFromUri(uri);
            return null;
        }

        static ConnectionSettings TryCreateFromUri(UriBuilder uri)
        {
            var cs = New();

            cs.ClientChecked = true;

            string host = uri.GetTrimmedHost();
            int? port = uri.Port;
            if (port == -1) port = null;

            cs.TCPChecked = !uri.Scheme.Equals("udp", StringComparison.OrdinalIgnoreCase);
            cs.UDPChecked = !cs.TCPChecked;

            cs.ClientSettings.TargetHost = host;
            cs.ClientSettings.TargetPort = port;

            if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                cs.TCPClient.EnableSSL = true;
            }

            return cs;
        }

        #endregion

        private void MakeDirty(object sender, PropertyChangedEventArgs e)
        {
            IsDirty = true;
            IsDirtyChanged?.Invoke();
        }

        protected void OnTCPToggled(bool value)
        {
            if (value)
            {
                UDPChecked = false;
                ClientToggleText = "_Client";
                ServerToggleText = "_Server";
                ProtocolChanged();
            }
            TCPToggleLocked = value;
            AnyProtocolChecked = _TCPChecked || _UDPChecked;
        }

        protected void OnUDPToggled(bool value)
        {
            if (value)
            {
                TCPChecked = false;
                ClientToggleText = "_Connect";
                ServerToggleText = "_Bind";
                ProtocolChanged();
            }
            UDPToggleLocked = value;
            AnyProtocolChecked = _TCPChecked || _UDPChecked;
        }

        void ProtocolChanged() // after TCP/UDP swapped
        {
            Settings.Remove(TCPClient);
            Settings.Remove(TCPServer);
            Settings.Remove(UDPClient);
            Settings.Remove(UDPServer);

            if (ClientChecked) OnClientToggled(true);
            if (ServerChecked) OnServerToggled(true);
        }

        protected void OnClientToggled(bool value)
        {
            var hasServerSettings = Settings.Any(x => x is ServerSettingsBase); // client setting should be after server settings
            if (TCPChecked)
            {
                if (value)
                    Settings.Insert(hasServerSettings ? 1 : 0, TCPClient);
                else
                    Settings.Remove(TCPClient);
            }
            if (UDPChecked)
            {
                if (value)
                    Settings.Insert(hasServerSettings ? 1 : 0, UDPClient);
                else
                    Settings.Remove(UDPClient);
            }
        }

        protected void OnServerToggled(bool value)
        {
            if (TCPChecked)
            {
                if (value)
                    Settings.Insert(0, TCPServer);
                else
                    Settings.Remove(TCPServer);
            }
            if (UDPChecked)
            {
                if (value)
                    Settings.Insert(0, UDPServer);
                else
                    Settings.Remove(UDPServer);
            }
        }

        public void AddRewrite(params RewriteSettings[] rewrite)
        {
            IsDirty = true;
            foreach (var item in rewrite)
            {
                item.BinaryChanged += RewriteBinary_Changed;
                item.PropertyChanged += MakeDirty;
                Settings.Add(item);
            }
        }

        public void RemoveRewrite(RewriteSettings rewrite)
        {
            IsDirty = true;
            rewrite.BinaryChanged -= RewriteBinary_Changed;
            rewrite.PropertyChanged -= MakeDirty;
            Settings.Remove(rewrite);
        }

        private void Client_DualModeChanged(object sender, bool e)
        {
            var client = (ClientSettingsBase)sender;
            if (e && !(IPAddress.TryParse(client.LocalAddress, out var addr) && addr.IsIPv6()))
                client.LocalAddress = IPAddress.IPv6Any.ToString(); // switch to IPv6 when dual-mode enabled
        }

        private void Server_DualModeChanged(object sender, bool e)
        {
            var server = (ServerSettingsBase)sender;
            if (e && !(IPAddress.TryParse(server.IPAddress, out var addr) && addr.IsIPv6()))
                server.IPAddress = IPAddress.IPv6Any.ToString(); // switch to IPv6 when dual-mode enabled
        }

        private void UDPClient_IsBroadcastChanged(object sender, bool value)
        {
            if (value) UDPClient.TargetHost = BroadcastHost[0];
        }


        private bool _TCPClientSNIhinted = false;
        private void TCPClient_EnableSSLChanged(object sender, bool value)
        {
            if (value && !_TCPClientSNIhinted)
            {
                if (string.IsNullOrEmpty(TCPClient.SNI))
                {
                    if (!IPAddress.TryParse(TCPClient.TargetHost, out _)) // only SNI as hostname is valid. no IP address.
                        TCPClient.SNI = TCPClient.TargetHost; // copy hostname to SNI when enabling first time SSL

                    _TCPClientSNIhinted = true;
                }
            }
        }

        private void RewriteBinary_Changed(object sender, bool enabled)
        {
            var obj = (RewriteSettings)sender;
            
            obj.MatchData = ToggleHexFormat(obj.MatchData, enabled);
            obj.ReplaceData = ToggleHexFormat(obj.ReplaceData, enabled);
        }

        string ToggleHexFormat(string source, bool hex)
        {
            if (string.IsNullOrEmpty(source)) return "";

            if (hex)
                return GetHex(Encoding.Default.GetBytes(source));

            RewriteSettings.TryParseHex(source, out var data);
            return Encoding.Default.GetString(data);
        }

        static string GetHex(byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", " ");
        }

        public Session CreateSession()
        {
            Validate();

            var session = new Session()
            {
                ClientSettings = ClientChecked ? ClientSettings : null,
                ServerSettings = ServerChecked ? ServerSettings : null,
                Rewrites = RewriteSettings.GetRewriteRules(Rewrites),
            };

            return session;
        }

        public bool ContainsHost(string host)
        {
            if (ClientChecked && ClientSettings is ClientSettingsBase client)
            {
                if (client.TargetHost?.Equals(host, StringComparison.OrdinalIgnoreCase) == true)
                    return true;
                if (client.LocalBinding && client.LocalAddress?.Equals(host, StringComparison.OrdinalIgnoreCase) == true)
                    return true;
            }
            if (ServerChecked && ServerSettings is ServerSettingsBase server)
            {
                if (server.IPAddress?.Equals(host, StringComparison.OrdinalIgnoreCase) == true)
                    return true;
            }

            return false;
        }

        [XmlIgnore] 
        public ClientSettingsBase ClientSettings
        {
            get
            {
                if (TCPChecked) return TCPClient;
                if (UDPChecked) return UDPClient;
                return null;
            }
        }
        
        [XmlIgnore] 
        public ProtocolType ProtocolType
        {
            get
            {
                if (TCPChecked) return ProtocolType.Tcp;
                if (UDPChecked) return ProtocolType.Udp;
                return ProtocolType.Unknown;
            }
        }

        [XmlIgnore]
        public ServerSettingsBase ServerSettings
        {
            get
            {
                if (TCPChecked) return TCPServer;
                if (UDPChecked) return UDPServer;
                return null;
            }
        }

        #region Validation

        void Validate()
        {
            if (!Settings.Any(x => x is ClientSettingsBase || x is ServerSettingsBase))
                throw new Exception("No socket to create!");

            if (ServerChecked)
            {
                ValidateHostname(ServerSettings.IPAddress, nameof(ServerSettings.IPAddress), true);
                ValidatePortRange(ServerSettings.Port, nameof(ServerSettings.Port), true);

                if (ServerSettings.DualMode)
                {
                    if (!IPAddress.Parse(ServerSettings.IPAddress).IsIPv6())
                        throw new ValueException(nameof(ServerSettings.IPAddress), "Only IPv6 is valid address in dual-mode.");
                }
                if (TCPChecked)
                {
                    if (TCPServer.EnableSSL)
                    {
                        if (TCPServer.GetSslProtocols() == System.Security.Authentication.SslProtocols.None)
                            throw new ValueException(null, "No server SSL/TLS protocol selected.");

                        if (!TCPServer.AutoGenerateServerCertificate && string.IsNullOrEmpty(TCPServer.ServerCertificate))
                            throw new ValueException(nameof(TCPServer.ServerCertificate), "No server certificate selected.");

                        if (TCPServer.AutoGenerateServerCertificate && string.IsNullOrEmpty(TCPServer.CertificateAuthority))
                            throw new ValueException(nameof(TCPServer.CertificateAuthority), "No certificate authority selected.");
                    }
                }
            }
            if (ClientChecked)
            {
                bool targetIpOnly = false;
                bool ipv4only = false;

                if (UDPChecked)
                {
                    targetIpOnly |= UDPClient.IsBroadcast || UDPClient.IsMulticast;
                    ipv4only |= UDPClient.IsBroadcast;
                }

                ValidateHostname(ClientSettings.TargetHost, nameof(ClientSettings.TargetHost), targetIpOnly, ipv4only);
                ValidatePortRange(ClientSettings.TargetPort, nameof(ClientSettings.TargetPort), false);

                if (ClientSettings.LocalBinding)
                {
                    ValidateHostname(ClientSettings.LocalAddress, nameof(ClientSettings.LocalAddress), true, ipv4only);
                    ValidatePortRange(ClientSettings.LocalPort, nameof(ClientSettings.LocalPort), true);
                }
                if (ClientSettings.DualMode)
                {
                    if (!IPAddress.Parse(ClientSettings.LocalAddress).IsIPv6())
                        throw new ValueException(nameof(ClientSettings.LocalAddress), "Only IPv6 is valid source address in dual-mode.");
                }
                if (TCPChecked)
                {
                    if (TCPClient.EnableSSL)
                    {
                        if (TCPClient.GetSslProtocols() ==  System.Security.Authentication.SslProtocols.None)
                            throw new ValueException(null, "No client SSL/TLS protocol selected.");
                    }
                }
            }

            foreach (var item in Rewrites) ValidateRewrite(item);
        }

        void ValidateRewrite(RewriteSettings rewrite)
        {
            if (!(rewrite.Inbound | rewrite.Outbound))
                throw new ValueException(null, "Select Inbound or Outbound flow in rewrite section.");
            
            if (string.IsNullOrWhiteSpace(rewrite.MatchData))
                throw new ValueException(null, "Enter Match data in rewrite section.");
            if (string.IsNullOrWhiteSpace(rewrite.ReplaceData))
                throw new ValueException(null, "Enter Replace data in rewrite section.");

            if (!rewrite.TryGetMatchBytes(out _))
                throw new ValueException(null, "Invalid Match data in rewrite section.");
            if (!rewrite.TryGetReplaceBytes(out _))
                throw new ValueException(null, "Invalid Replace data in rewrite section.");

            rewrite.Normalize();
        }

        void ValidatePortRange(int? port, string propertyName, bool allowAnyPort)
        {
            int min = allowAnyPort ? 0 : 1;
            int max = 65535;

            if (port == null || port < min || port > max)
            {
                throw new ValueException(propertyName, $"Enter port number between {min}-{max}");
            }
        }

        void ValidateHostname(string host, string propertyName, bool allowOnlyIpAddress, bool allowOnlyIPv4 = false)
        {
            if (IPAddress.TryParse(host, out var ip))
            {
                if (!allowOnlyIPv4 || ip.AddressFamily == AddressFamily.InterNetwork) return;
            }

            if (string.IsNullOrEmpty(host) || allowOnlyIpAddress || allowOnlyIPv4)
            {
                if (allowOnlyIPv4) throw new ValueException(propertyName, $"Enter IPv4 address in dotted-quad notation.");
                string hostnameOr = allowOnlyIpAddress ? "" : " host name or";
                throw new ValueException(propertyName, $"Enter{hostnameOr} IP address in dotted-quad notation for IPv4 and in colon-hexadecimal notation for IPv6.");
            }
        }

        #endregion

        #region Serialization and file handling

        private static readonly XmlWriterSettings writerSettings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, Encoding = new UTF8Encoding(false) };
        private static readonly XmlSerializerNamespaces emptyNamespaces = new XmlSerializerNamespaces(new[] { new XmlQualifiedName("", "") });
        private static readonly XmlSerializer serializer = new XmlSerializer(typeof(ConnectionSettings));

        public byte[] Serialize()
        {
            using (var stream = new MemoryStream())
            using (var writer = XmlWriter.Create(stream, writerSettings))
            {
                serializer.Serialize(writer, this, emptyNamespaces);
                return stream.ToArray();
            }
        }

        public static ConnectionSettings Deserialize(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            using (var reader = XmlReader.Create(stream))
            {
                var obj = (ConnectionSettings)serializer.Deserialize(reader);
                obj.Init();
                return obj;
            }
        }


        private static readonly string[] localKnownHosts = new[] { "localhost", "127.0.0.1", Environment.MachineName.ToLower() };

        public static IReadOnlyList<string> KnownHosts { get; private set; } // by-product of GetHistory
        public static string[] BroadcastHost { get; } = new[] { "255.255.255.255" };
        public static IReadOnlyList<string> KnownMulticastHosts => KnownHosts?.Where(host => IPAddress.TryParse(host, out var ip) && ip.IsMulticast()).ToList();
        public static IReadOnlyList<string> KnownHostnames => KnownHosts?.Where(host => !IPAddress.TryParse(host, out var ip)).ToList();

        public static ConnectionSettings[] GetHistory(int count = 20)
        {
            var list = new List<ConnectionSettings>();

            foreach (var file in new DirectoryInfo(App.Current.RecentPath).GetFiles("*.xml"))
            {
                try
                {
                    var cs = Deserialize(File.ReadAllBytes(file.FullName));
                    cs.CreatedAt = file.CreationTime;
                    cs.FileName = file.FullName;
                    list.Add(cs);
                }
                catch
                {
                    try { file.Delete(); } catch { } // delete invalid file
                }
            }

            list.Sort(); // sort by time

            foreach (var item in list.Skip(count)) // delete older files
            {
                item.Delete();
            }

            var result = list.Take(count).ToArray();

            KnownHosts = result
                .Select(x => x.ClientSettings?.TargetHost?.ToLower())
                .Concat(result.Select(x => x.ServerSettings?.IPAddress))
                .Where(x => !string.IsNullOrEmpty(x) && x != "0.0.0.0" && x != "::")
                .Concat(localKnownHosts)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            return result;
        }

        public void SaveRecent()
        {
            EnsureFileExists();
            if (IsDirty || FileName == null)
            {
                FileName = Path.Combine(App.Current.RecentPath, Guid.NewGuid().ToString("D") + ".xml");
                File.WriteAllBytes(FileName, Serialize());
            }
            else
            {
                // nothing changes, so only bump existing file time
                SetCurrentChangeTime();
            }
        }

        public void Delete()
        {
            try { File.Delete(FileName); } catch { }
            FileName = null;
        }

        void SetCurrentChangeTime()
        {
            try
            {
                CreatedAt = DateTime.Now;
                File.SetCreationTime(FileName, CreatedAt);
            }
            catch  {}
        }

        bool EnsureFileExists()
        {
            try
            {
                if (!string.IsNullOrEmpty(FileName) && File.Exists(FileName)) return true;
            }
            catch { }
            FileName = null;
            return false;
        }

        int IComparable<ConnectionSettings>.CompareTo(ConnectionSettings other) => -CreatedAt.CompareTo(other.CreatedAt);

        #endregion

        #region ICloneable

        object ICloneable.Clone() => Copy();
        
        public ConnectionSettings Copy(ConnectionSettings template = null)
        {
            if (template == null) template = this;

            var obj = new ConnectionSettings()
            {
                FileName = template.FileName,
                CreatedAt = template.CreatedAt,

                TCPClient = (TCPClientSettings)(template.TCPChecked && template.ClientChecked ? template : this).TCPClient.Clone(),
                TCPServer = (TCPServerSettings)(template.TCPChecked && template.ServerChecked ? template : this).TCPServer.Clone(),
                UDPClient = (UDPClientSettings)(template.UDPChecked && template.ClientChecked ? template : this).UDPClient.Clone(),
                UDPServer = (UDPServerSettings)(template.UDPChecked && template.ServerChecked ? template : this).UDPServer.Clone(),

                TCPChecked = template.TCPChecked,
                UDPChecked = template.UDPChecked,
                ClientChecked = template.ClientChecked,
                ServerChecked = template.ServerChecked,
                Rewrites = template.Rewrites.Select(x => (RewriteSettings)x.Clone()).ToArray(),
            };

            obj.Init();

            return obj;
        }

        #endregion

        #region DesignInstance

        private ConnectionSettings(bool isDesignInstance)
        {
            if (isDesignInstance)
            {
                TCPServer = TCPServerSettings.DesignInstance;
                TCPClient = TCPClientSettings.DesignInstance;
                UDPClient = UDPClientSettings.DesignInstance;
                UDPServer = UDPServerSettings.DesignInstance;

                TCPChecked = true;
                ClientChecked = true;

                Settings.Clear();
                // add every socket in design time to see all of them
                Settings.Add(TCPServer);
                Settings.Add(TCPClient);
                Settings.Add(UDPServer);
                Settings.Add(UDPClient);
            }
        }

        public static ConnectionSettings DesignInstance
        {
            get => new ConnectionSettings(isDesignInstance: true);
        }

        #endregion
    }
}
