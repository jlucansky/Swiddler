using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;

namespace Swiddler.SocketSettings
{
    public class SnifferSettings : SettingsBase
    {
        public SnifferSettings()
        {
            Caption = "Network sniffer";
            ImageName = "Eye";

            CaptureFilter.CollectionChanged += (s, e) => CaptureFilterChanges++;
        }

        string _InterfaceAddress;
        public string InterfaceAddress { get => _InterfaceAddress; set => SetProperty(ref _InterfaceAddress, value); }

        bool _PromiscuousMode;
        public bool PromiscuousMode { get => _PromiscuousMode; set => SetProperty(ref _PromiscuousMode, value); }


        public ObservableCollection<CaptureFilterItem> CaptureFilter { get; set; } = new ObservableCollection<CaptureFilterItem>();

        public static CaptureProtocol[] CaptureProtocols { get; } = new[] { CaptureProtocol.Both, CaptureProtocol.TCP, CaptureProtocol.UDP };

        public class CaptureFilterItem
        {
            public CaptureProtocol Protocol { get; set; }
            public string IPAddress { get; set; }
            public int? Port { get; set; }

            public Tuple<IPEndPoint, ProtocolType> ToTuple()
            {
                var ep = new IPEndPoint(System.Net.IPAddress.Any, 0);
                var proto = ProtocolType.Unspecified;

                if (!string.IsNullOrEmpty(IPAddress))
                    ep.Address = System.Net.IPAddress.Parse(IPAddress);
                if (Port != null)
                    ep.Port = Port.Value;

                if (Protocol == CaptureProtocol.TCP)
                    proto = ProtocolType.Tcp;
                if (Protocol == CaptureProtocol.UDP)
                    proto = ProtocolType.Udp;
                
                return Tuple.Create(ep, proto);
            }
        }

        public enum CaptureProtocol
        {
            Both = 0,
            TCP,
            UDP
        }

        int _CaptureFilterChanges;
        [System.Xml.Serialization.XmlIgnore] public int CaptureFilterChanges { get => _CaptureFilterChanges; set => SetProperty(ref _CaptureFilterChanges, value); }

        public static SnifferSettings DesignInstance => new SnifferSettings();

    }
}
