using Swiddler.Common;
using Swiddler.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Markup;

namespace Swiddler.MarkupExtensions
{
    public class CertificateSource : MarkupExtension
    {
        public static readonly object EmptyItem = new object();

        static readonly object[] arrayWithEmptyItem = new[] { EmptyItem };

        public bool NoneAsFirstItem { get; set; } = true;
        public bool FilterKeyCertSign { get; set; } // only CAs
        public bool Lazy { get; set; } = true;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (Lazy)
                return new Lazy<object>(() => new ReloadableCollection<object>(() => GetItems()));
            else
                return new ReloadableCollection<object>(() => GetItems());
        }

        public List<object> GetItems()
        {
            var list = GetCollection().OfType<X509Certificate2>().Where(x => x.HasPrivateKey);

            if (NoneAsFirstItem)
                return arrayWithEmptyItem.Concat(list).ToList();

            return list.ToList<object>();
        }

        X509Certificate2Collection GetCollection()
        {
            var list = X509.MyCurrentUserX509Store.Certificates;
            
            if (FilterKeyCertSign)
                list = list.Find(X509FindType.FindByKeyUsage, "KeyCertSign", false);

            return list;
        }
    }
}
