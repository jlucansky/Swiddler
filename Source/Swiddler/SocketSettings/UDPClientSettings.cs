using System;

namespace Swiddler.SocketSettings
{
    public class UDPClientSettings : ClientSettingsBase
    {
        public event EventHandler<bool> IsBroadcastChanged;
        public event EventHandler<bool> IsMulticastChanged;

        public UDPClientSettings()
        {
            Protocol = System.Net.Sockets.ProtocolType.Udp;
            Caption = "Connect";
            ImageName = "Connect";
            _TargetHost = "8.8.8.8";
            _TargetPort = 53;
        }

        bool _IsBroadcast, _IsMulticast;
        public bool IsBroadcast { get => _IsBroadcast; set { if (SetProperty(ref _IsBroadcast, value)) IsBroadcastChanged?.Invoke(this, value); } }
        public bool IsMulticast { get => _IsMulticast; set { if (SetProperty(ref _IsMulticast, value)) IsMulticastChanged?.Invoke(this, value); } }


        public static UDPClientSettings DesignInstance => new UDPClientSettings() { LocalBinding = true };

        public override string ToString() => "udp://" + base.ToString();
    }
}
