using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Swiddler.ViewModels
{
    public class IPAddressItem
    {
        public IPAddress IPAddress { get; set; }

        public bool IsLoopback { get; set; }
        
        public bool IsAny { get; set; }

        public bool IsUp { get; set; }

        public string InterfaceId { get; set; }
        
        public string InterfaceName { get; set; }
        
        public string InterfaceDescription { get; set; }

        public override string ToString() => IPAddress.ToString();

        public static List<IPAddressItem> GetAll(bool mapToIPv6 = false)
        {
            return new[] {
                new IPAddressItem() { IPAddress = IPAddress.Any, IsAny = true, InterfaceName="Any" },
                new IPAddressItem() { IPAddress = IPAddress.IPv6Any, IsAny = true, InterfaceName="Any" },
            }
            .Concat(NetworkInterface.GetAllNetworkInterfaces()
                .Select(iface => iface.GetIPProperties().UnicastAddresses
                .Select(adr => new IPAddressItem()
                {
                    InterfaceDescription = iface.Description,
                    InterfaceName = iface.Name,
                    InterfaceId = iface.Id,
                    IPAddress = mapToIPv6 && adr.Address.AddressFamily == AddressFamily.InterNetwork ? adr.Address.MapToIPv6() : adr.Address,
                    IsLoopback = iface.NetworkInterfaceType == NetworkInterfaceType.Loopback,
                    IsUp = iface.OperationalStatus == OperationalStatus.Up
                }))
                .SelectMany(x => x))
                .ToList();
        }

        public static List<IPAddressItem> GetAll(AddressFamily family)
        {
            return GetAll(family == AddressFamily.InterNetworkV6).Where(x => x.IPAddress.AddressFamily == family).ToList();
        }

        public override bool Equals(object obj)
        {
            if (obj is IPAddressItem other)
            {
                return 
                    IPAddress?.Equals(other.IPAddress) == true &&
                    InterfaceId?.Equals(other.InterfaceId) == true;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return (IPAddress?.GetHashCode() ?? 0) ^ (InterfaceId?.GetHashCode() ?? 0);
        }
    }
}
