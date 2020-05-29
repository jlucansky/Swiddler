namespace Swiddler.SocketSettings
{
    public class UDPServerSettings : ServerSettingsBase
    {
        public UDPServerSettings()
        {
            Protocol = System.Net.Sockets.ProtocolType.Udp;
            Caption = "Bind";
            ImageName = "Port";
        }

        
        public static UDPServerSettings DesignInstance => new UDPServerSettings();
        
        public override string ToString() => "udp://" + base.ToString();
    }
}
