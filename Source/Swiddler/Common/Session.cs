using Swiddler.Channels;
using Swiddler.DataChunks;
using Swiddler.IO;
using Swiddler.Security;
using Swiddler.SocketSettings;
using Swiddler.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows;

namespace Swiddler.Common
{
    public delegate void DataChangedDelegate(long atOffset);

    public enum SessionState { New, Starting, Started, Error, Stopped }

    public class Session : BindableBase, IDisposable
    {
        public event DataChangedDelegate StorageDataChanged;
        public event EventHandler<Session> ChildSessionAdded;

        public List<Session> Children { get; } = new List<Session>();
        public Session Parent { get; private set; }
        public bool IsChildSession => Parent != null;

        string _Name = "New connection";
        public string Name { get => _Name; set => SetProperty(ref _Name, value); }

        int _PID;
        public int PID { get => _PID; private set => SetProperty(ref _PID, value); }

        string _CountersFormatted; // in / out
        public string CountersFormatted { get => _CountersFormatted; private set => SetProperty(ref _CountersFormatted, value); }

        SessionState _State;
        public SessionState State { get => _State; private set => SetProperty(ref _State, value); }


        public StorageHandle Storage { get; private set; }

        public string SettingsFileName { get; set; }

        public ServerSettingsBase ServerSettings { get; set; }
        public ClientSettingsBase ClientSettings { get; set; }
        public RewriteRule[] Rewrites { get; set; }
        public SnifferSettings Sniffer { get; set; }
        public Injector Injector { get; set; } // TODO: MonitorSettings

        public SessionChannel SessionChannel { get; } // input/output to FragmentView
        public Channel ServerChannel { get; set; }
        public Channel ClientChannel { get; set; }

        public ConnectionBanner ConnectionBanner { get; private set; } // info about estabilished connection

        public Dictionary<long, object> ObjectCache { get; } = new Dictionary<long, object>(); // key is chunk's SequenceNumber, value is associated deserialized object with chunk

        public object ViewContent { get; set; }


        private string nameWhenStopped; // name to set after session is stopped
        private bool propagateClientSettings = true;
        private bool shouldStopChildren = true;

        public Session()
        {
            InitStorage();
            SessionChannel = new SessionChannel(this);
        }

        public void InitStorage()
        {
            Storage = StorageHandle.CreateTemporary();
            //Storage = StorageHandler.OpenSession("test_s", new Serialization.SessionXml() {  });

            Storage.FileChangedCallback = RaiseDataChanged;
        }

        void RaiseDataChanged(long offset) => StorageDataChanged?.Invoke(offset);

        public void HandleChannelError(Channel sender, Exception exception)
        {
            try
            {
                exception = exception.GetBaseException();
                if (exception is ObjectDisposedException) return; // do not write useless errors

                var msg = new MessageData()
                {
                    Text = exception.Message,
                    Type = exception is SocketException ? MessageType.SocketError : MessageType.Error,
                };

                if (Storage.CanWrite)
                {
                    try
                    {
                        Storage.Write(msg);
                        State = SessionState.Error;
                    }
                    catch (Exception ex)
                    {
                        // TODO: messagebox
                        System.Diagnostics.Debug.Fail(ex.ToString());
                    }
                }
                else
                {
                    // TODO: messagebox
                    System.Diagnostics.Debug.Fail(msg.Text);
                }
            }
            finally
            {
                Stop();
            }
        }

        public Task StartAsync() => Task.Run(Start);

        bool _startCalled = false;
        public void Start()
        {
            if (_startCalled) throw new InvalidOperationException("Session already started.");
            _startCalled = true;

            State = SessionState.Starting;

            try
            {
                if (ServerSettings != null)
                {
                    StartServer();
                }
                else if (ClientSettings != null)
                {
                    StartClient();
                }
                else if (Injector != null)
                {
                    StartMonitor();
                }
                else if (Sniffer != null)
                {
                    StartSniffer();
                }

                StartChannels();

                if (State == SessionState.Starting) // there could be an handled error
                    State = SessionState.Started;
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(Name ?? nameWhenStopped)) nameWhenStopped = "Error";
                HandleChannelError(null, ex);
            }
        }

        private void StartSniffer()
        {
            var settings = Sniffer;

            var localIP = IPAddress.Parse(settings.InterfaceAddress);

            Name = $"Sniffing at {localIP}";

            WriteMessage("Starting network sniffer at " + localIP);

            var sniffer = new SnifferChannel(this)
            {
                LocalAddress = localIP,
                CaptureFilter = settings.CaptureFilter.Select(x => x.ToTuple()).ToArray()
            };

            ServerChannel = sniffer;

            shouldStopChildren = false;
        }

        private void StartServer()
        {
            var settings = ServerSettings;
            var localEP = new IPEndPoint(IPAddress.Parse(settings.IPAddress), settings.Port.Value);

            Name = $"{localEP}";

            if (settings.Protocol == ProtocolType.Tcp)
            {
                var tcpSettings = (TCPServerSettings)settings;

                shouldStopChildren = false; // children uses their own sockets
                var listener = new TcpListener(localEP);
                SetupSocket(listener.Server, ServerSettings);
                listener.Start();
                SocketEndpoint(listener.Server, out localEP);

                WriteMessage("Started listening at " + listener.LocalEndpoint);
                Name = $"{localEP} listen";

                ServerChannel = CreateTcpListenerChannel(listener, tcpSettings.EnableSSL);

                if (ServerChannel is SslListenerChannel sslChannel)
                {
                    sslChannel.SslProtocols = tcpSettings.GetSslProtocols();
                    sslChannel.ServerCertificate = FindCert(tcpSettings.ServerCertificate);
                    sslChannel.GeneratedServerCertificatePrefix = tcpSettings.GeneratedServerCertificatePrefix;
                    sslChannel.IgnoreInvalidClientCertificate = tcpSettings.IgnoreInvalidClientCertificate;
                    sslChannel.RequireClientCertificate = tcpSettings.RequireClientCertificate;
                    sslChannel.AutoGenerateServerCertificate = tcpSettings.AutoGenerateServerCertificate;
                    if (sslChannel.AutoGenerateServerCertificate)
                        sslChannel.CertificateAuthority = FindCert(tcpSettings.CertificateAuthority);
                }
            }
            else if (settings.Protocol == ProtocolType.Udp)
            {
                var listener = new UdpClient(localEP.AddressFamily);
                SetupSocket(listener.Client, ServerSettings);
                listener.Client.Bind(localEP);
                SocketEndpoint(listener.Client, out localEP);

                WriteMessage("Socket bound to " + listener.Client.LocalEndPoint);
                Name = $"{localEP} bind";

                ServerChannel = CreateUdpChannel(listener);
                ServerChannel.ObserveTwoWay(new RemoteChildChannel(this));
            }
        }

        private void StartClient()
        {
            var settings = ClientSettings;

            int elapsed = 0; // TODO: niekam vypisat alebo poznacit

            IPEndPoint localEP, targetEP = null;
            IPAddress.TryParse(settings.TargetHost, out var targetAddr);
            Uri targetUri = new UriBuilder(settings.Protocol.ToString(), settings.TargetHost, settings.TargetPort.Value).Uri;
            string targetUriFmt = targetUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);

            if (settings.LocalBinding)
                localEP = new IPEndPoint(IPAddress.Parse(settings.LocalAddress), settings.LocalPort.Value);
            else
                localEP = new IPEndPoint(targetAddr?.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);

            if (!IsChildSession) // name was set by parent session
            {
                Name = $"{targetUriFmt} connecting..."; ;
                nameWhenStopped = $"{targetUriFmt}";
            }

            if (settings.Protocol == ProtocolType.Tcp)
            {
                var tcpSettings = (TCPClientSettings)settings;

                WriteMessage("Connecting to " + targetUriFmt, MessageType.ConnectionBanner);

                var tcpClient = new TcpClient(localEP.AddressFamily);
                SetupSocket(tcpClient.Client, ClientSettings);
                tcpClient.Client.Bind(localEP);
                tcpClient.Connect(targetUri, out elapsed);
                SocketEndpoints(tcpClient.Client, out localEP, out targetEP);

                SetConnectionBannerDispatched(new ConnectionBanner(tcpClient.Client) { ConnectionText = "Connected to " + targetUriFmt });

                ClientChannel = CreateTcpChannel(tcpClient, tcpSettings.EnableSSL);
                if (ClientChannel is SslChannel sslChannel)
                {
                    sslChannel.AuthenticateAsClient = true;
                    sslChannel.SslProtocols = tcpSettings.GetSslProtocols();
                    sslChannel.SNI = tcpSettings.SNI;
                    sslChannel.ClientCertificate = FindCert(tcpSettings.ClientCertificate);
                    sslChannel.IgnoreInvalidServerCertificate = !tcpSettings.ValidateCertificate;
                }
            }
            else if (settings.Protocol == ProtocolType.Udp)
            {
                var udpSettings = (UDPClientSettings)settings;

                WriteMessage("Connecting to " + targetUriFmt, MessageType.ConnectionBanner);

                var udpClient = new UdpClient(localEP.AddressFamily);
                SetupSocket(udpClient.Client, ClientSettings);

                if (udpSettings.IsBroadcast) udpClient.EnableBroadcast = true;
                if (udpSettings.IsMulticast) udpClient.JoinMulticastGroup(targetAddr, localEP.Address);

                udpClient.Client.Bind(localEP);

                if (!udpSettings.IsBroadcast && !udpSettings.IsMulticast)
                {
                    // CONNECT
                    udpClient.Connect(targetUri, out elapsed);
                    SocketEndpoints(udpClient.Client, out localEP, out targetEP);
                    SetConnectionBannerDispatched(new ConnectionBanner(udpClient.Client) { ConnectionText = "Connected to " + targetUriFmt });
                    ClientChannel = CreateUdpChannel(udpClient);
                }
                else
                {
                    // BROADCAST / MULTICAST
                    propagateClientSettings = false; // aviod child session to use this ClientSettings again
                    SocketEndpoint(udpClient.Client, out localEP);
                    string msg = "";
                    if (udpSettings.IsBroadcast) msg = "Initiated broadcast to ";
                    if (udpSettings.IsMulticast) msg = "Joined multicast group ";
                    targetEP = new IPEndPoint(targetAddr, settings.TargetPort.Value);
                    SetConnectionBannerDispatched(new ConnectionBanner(localEP, targetEP) { ConnectionText = msg + targetUriFmt });
                    ClientChannel = CreateUdpChannel(udpClient, targetEP);
                    if (!IsChildSession) // do not create nested channels this is already children
                    {
                        ClientChannel.ObserveTwoWay(new RemoteChildChannel(this));
                        SessionChannel.Observe(ClientChannel);
                    }
                }
            }

            if (IsChildSession)
            {
                // TUNNEL MODE
                if (ServerChannel != null && ClientChannel != null)
                {
                    ServerChannel.DefaultFlow = TrafficFlow.Outbound;
                    ServerChannel.ObserveTwoWay(ClientChannel); // relaying between two sockets
                    SessionChannel.Observers.Insert(0, ClientChannel); // user input data send to client channel
                    ClientChannel.Observe(SessionChannel);
                }
            }
            else
            {
                Name = $":{localEP.Port} > {targetEP}";
                nameWhenStopped = null;
                ResolveProcessIdAsync(targetEP);
                if (!SessionChannel.Observers.Contains(ClientChannel)) // broadcast/multicast is oneway
                    SessionChannel.ObserveTwoWay(ClientChannel);
                SessionChannel.ObserveSelf();
            }
        }

        private void StartMonitor()
        {
            Name = "Process Monitor";
            PID = Injector.ProcessId;

            TcpClient monitorClient = Injector.InjectAndConnect();
            ServerChannel = new MonitorChannel(this, monitorClient);

            Name = $"Process Monitor :{ ((IPEndPoint)monitorClient.Client.LocalEndPoint).Port }";

            WriteMessage("Process monitor successfully initialized.");
        }

        void SetupSocket(Socket socket, SettingsBase settings)
        {
            if (settings is ClientSettingsBase client)
            {
                if (client.DualMode) socket.DualMode = true;
            }
            if (settings is ServerSettingsBase server)
            {
                if (server.DualMode) socket.DualMode = true;
                if (server.ReuseAddress) socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }
        }

        static X509Certificate2 FindCert(string thumbprint)
        {
            if (string.IsNullOrEmpty(thumbprint)) return null;
            return X509.MyCurrentUserX509Store.Certificates
                .Find(X509FindType.FindByThumbprint, thumbprint, false)
                .OfType<X509Certificate2>().FirstOrDefault() ?? throw new Exception("Cannot find certificate " + thumbprint);
        }

        void SocketEndpoints(Socket socket, out IPEndPoint local, out IPEndPoint remote)
        {
            local = (IPEndPoint)socket.LocalEndPoint;
            remote = (IPEndPoint)socket.RemoteEndPoint;
        }

        void SocketEndpoint(Socket socket, out IPEndPoint local)
        {
            local = (IPEndPoint)socket.LocalEndPoint;
        }

        void StartChannels()
        {
            var channels = new Channel[] { SessionChannel, ClientChannel, ServerChannel };
            foreach (var channel in channels.FindAllChannels()) channel.Start();
        }

        public Task ResolveProcessIdAsync(EndPoint ep, bool local = true)
        {
            IPEndPoint ipDP = (IPEndPoint)ep;
            return Task.Run(() => PID = ipDP.GetProcessId(local));
        }

        public Session NewChildSession()
        {
            var newSession = new Session() { Parent = this };
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                Children.Add(newSession);
                ChildSessionAdded?.Invoke(this, newSession);
            }));
            return newSession;
        }

        public Session NewChildSession(string message, Func<Session, Channel> channelFactory)
        {
            var child = NewChildSession();
            WriteMessage(message, MessageType.Connecting);

            if (propagateClientSettings)
                child.ClientSettings = ClientSettings;

            child.Rewrites = Rewrites;

            child.ServerChannel = channelFactory(child);
            child.ServerChannel.Observe(child.SessionChannel);

            if (child.ClientSettings == null)
                child.SessionChannel.Observe(child.ServerChannel);

            child.SessionChannel.ObserveSelf();

            child.WriteMessage(message, MessageType.Connecting);
            return child;
        }

        private void SetConnectionBannerDispatched(ConnectionBanner connectionBanner)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ConnectionBanner = connectionBanner;
                RaiseDataChanged(-1); // force update
            }));
        }

        public void Stop()
        {
            if (State == SessionState.Stopped) return;

            if (shouldStopChildren)
                StopChildren();

            var showInfo = State != SessionState.Error; // there is already error message in the log
            State = SessionState.Stopped;

            if (nameWhenStopped != null)
                Name = nameWhenStopped;

            Destroy(ClientChannel);
            Destroy(ServerChannel);

            SessionChannel.Observers.Clear();

            if (showInfo) WriteMessage("Session ended by user.", MessageType.Error);
        }

        void StopChildren()
        {
            foreach (var child in Children) child.Stop();
        }

        void Destroy(Channel channel)
        {
            try { (channel as IDisposable)?.Dispose(); } catch { } // ignore any errors
        }

        void Shutdown()
        {

        }

        public void Dispose()
        {
            var storageCopy = Storage;
            Storage = null;
            storageCopy?.Dispose();

            foreach (var item in ObjectCache.Values) (item as IDisposable)?.Dispose();
            ObjectCache.Clear();
        }

        public TcpListenerChannel CreateTcpListenerChannel(TcpListener listener, bool ssl = false)
        {
            return ssl ? new SslListenerChannel(this, listener) : new TcpListenerChannel(this, listener);
        }
        
        public TcpChannel CreateTcpChannel(TcpClient client, bool ssl = false)
        {
            var channel = ssl ? new SslChannel(this, client) : new TcpChannel(this, client);
            ObserveRewriteChannel(channel);
            return channel;
        }

        public UdpChannel CreateUdpChannel(UdpClient client, IPEndPoint remoteEP = null)
        {
            var channel = new UdpChannel(this, client) { RemoteEndpoint = remoteEP };
            ObserveRewriteChannel(channel);
            return channel;
        }

        void ObserveRewriteChannel(Channel channel)
        {
            if (Rewrites?.Any() == true)
            {
                var rewriteChannel = new RewriteChannel(this);
                channel.Observe(rewriteChannel);
                SessionChannel.Observe(rewriteChannel);
            }
        }

        public void WriteChunk(IDataChunk chunk)
        {
            Storage.Write(chunk);
        }

        public void WriteMessage(string message, MessageType type = MessageType.Information)
        {
            WriteChunk(new MessageData() { Text = message, Type = type });
        }


        public object GetCachedObject(long key, Func<object> valueFactory)
        {
            if (!ObjectCache.TryGetValue(key, out var obj))
                ObjectCache.Add(key, obj = valueFactory());
            return obj;
        }

        public bool StartAsAdmin()
        {
            if (!string.IsNullOrEmpty(SettingsFileName) && File.Exists(SettingsFileName))
            {
                using (Process proc = new Process())
                {
                    proc.StartInfo.FileName = System.Reflection.Assembly.GetEntryAssembly().GetLocalPath();
                    proc.StartInfo.Arguments = $"-settings \"{SettingsFileName}\"";
                    proc.StartInfo.UseShellExecute = true;
                    proc.StartInfo.Verb = "runas";
                    proc.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
                    proc.Start();
                    return true;
                }
            }

            return false;
        }
    }
}
