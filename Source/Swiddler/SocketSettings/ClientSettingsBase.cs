using System;

namespace Swiddler.SocketSettings
{
    public abstract class ClientSettingsBase : SettingsBase
    {
        public event EventHandler<bool> DualModeChanged;

        protected string _TargetHost;
        public string TargetHost { get => _TargetHost; set => SetProperty(ref _TargetHost, value); }

        protected int? _TargetPort;
        public int? TargetPort { get => _TargetPort; set => SetProperty(ref _TargetPort, value); }

        protected string _LocalAddress = "0.0.0.0";
        public string LocalAddress { get => _LocalAddress; set => SetProperty(ref _LocalAddress, value); }

        protected int? _LocalPort = 0;
        public int? LocalPort { get => _LocalPort; set => SetProperty(ref _LocalPort, value); }

        protected bool _LocalBinding;
        public bool LocalBinding { get => _LocalBinding; set { SetProperty(ref _LocalBinding, value); } }

        protected bool _DualMode;
        public bool DualMode { get => _DualMode; set { if (SetProperty(ref _DualMode, value)) DualModeChanged?.Invoke(this, value); } }


        [System.Xml.Serialization.XmlIgnore] public System.Net.Sockets.ProtocolType Protocol { get; protected set; } = System.Net.Sockets.ProtocolType.Unknown;
        public override string ToString() => TargetHost + ":" + TargetPort;
    }
}
