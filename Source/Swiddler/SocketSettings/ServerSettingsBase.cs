using System;

namespace Swiddler.SocketSettings
{
    public abstract class ServerSettingsBase : SettingsBase
    {
        public event EventHandler<bool> DualModeChanged;

        string _IPAddress = "0.0.0.0";
        public string IPAddress { get => _IPAddress; set => SetProperty(ref _IPAddress, value); }

        int? _Port = 1337;
        public int? Port { get => _Port; set => SetProperty(ref _Port, value); }

        bool _ReuseAddress;
        public bool ReuseAddress { get => _ReuseAddress; set => SetProperty(ref _ReuseAddress, value); }

        bool _DualMode;
        public bool DualMode { get => _DualMode; set { if (SetProperty(ref _DualMode, value)) DualModeChanged?.Invoke(this, value); } }


        [System.Xml.Serialization.XmlIgnore] public System.Net.Sockets.ProtocolType Protocol { get; protected set; } = System.Net.Sockets.ProtocolType.Unknown;
        public override string ToString() => IPAddress + ":" + Port;
    }
}
