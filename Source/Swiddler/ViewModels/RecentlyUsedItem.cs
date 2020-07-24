using System;

namespace Swiddler.ViewModels
{
    public class RecentlyUsedItem : QuickActionItem
    {
        public ConnectionSettings ConnectionSettings { get; set; }
        public string DateTime { get; set; }

        public RecentlyUsedItem(ConnectionSettings connectionSettings) : base(QuickActionTemplate.Undefined)
        {
            var cs = ConnectionSettings = connectionSettings;

            DateTime = cs.CreatedAt.ToString("g");

            if (cs.ClientChecked && !cs.ServerChecked)
            {
                Icon = "Connect";
                if (cs.TCPChecked)
                    Caption = "TCP Client";
                else if (cs.UDPChecked)
                    Caption = "UDP Client";
                Description = cs.ClientSettings.TargetHost + ":" + cs.ClientSettings.TargetPort;
            }
            else if (cs.ServerChecked && !cs.ClientChecked)
            {
                Icon = "Port";
                if (cs.TCPChecked)
                    Caption = "TCP Server";
                else if (cs.UDPChecked)
                    Caption = "UDP Server";
                Description = cs.ServerSettings.IPAddress + ":" + cs.ServerSettings.Port;
            }
            else if (cs.ClientChecked && cs.ServerChecked)
            {
                Icon = "Tunnel";
                Caption = "Tunnel";
                Description = 
                    ":" + cs.ServerSettings.Port + " > " +
                    cs.ClientSettings.TargetHost + ":" + cs.ClientSettings.TargetPort;
            }
            else if (cs.SnifferChecked)
            {
                Icon = "Eye";
                Caption = "Network sniffer";
                Description = cs.Sniffer.InterfaceAddress;
            }
            else
            {
                throw new NotImplementedException();
            }

        }

        public override ConnectionSettings GetConnectionSettings(ConnectionSettings recent)
        {
            return recent.Copy(ConnectionSettings);
        }
    }
}
