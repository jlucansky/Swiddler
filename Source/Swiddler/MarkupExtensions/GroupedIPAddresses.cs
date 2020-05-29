using Swiddler.ViewModels;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Net.Sockets;
using System.Windows.Data;
using System.Windows.Markup;

namespace Swiddler.MarkupExtensions
{
    public class GroupedIPAddresses : MarkupExtension
    {
        public AddressFamily? AddressFamily { get; set; }

        class IPAddressGroupDescription : GroupDescription
        {
            public override object GroupNameFromItem(object item, int level, CultureInfo culture)
            {
                var addr = (IPAddressItem)item;
                if (addr.IsAny || addr.IsLoopback) return IPAdapterEmptyHeader.Default;
                return new IPAdapterHeader(addr);
            }
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var addrViewSource = new CollectionViewSource();
            addrViewSource.GroupDescriptions.Add(new IPAddressGroupDescription());
            addrViewSource.Source = AddressFamily.HasValue ? IPAddressItem.GetAll(AddressFamily.Value) : IPAddressItem.GetAll();
            return addrViewSource.View;
        }

        public static object Value => new GroupedIPAddresses().ProvideValue(null);
        public static object Value_v4 => new GroupedIPAddresses() { AddressFamily = System.Net.Sockets.AddressFamily.InterNetwork }.ProvideValue(null);
        public static object Value_v6 => new GroupedIPAddresses() { AddressFamily = System.Net.Sockets.AddressFamily.InterNetworkV6 }.ProvideValue(null);

        public static object LazyValue => new Lazy<object>(() => Value);
        public static object LazyValue_v4 => new Lazy<object>(() => Value_v4);
        public static object LazyValue_v6 => new Lazy<object>(() => Value_v6);
    }
}
