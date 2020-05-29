using Swiddler.MarkupExtensions;
using Swiddler.Security;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Data;
using System.Windows.Markup;

namespace Swiddler.Converters
{
    public class CertificateThumbprintConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string thumbprint && !string.IsNullOrEmpty(thumbprint))
            {
                var cert = X509.MyCurrentUserX509Store.Certificates
                    .Find(X509FindType.FindByThumbprint, thumbprint, false)
                    .OfType<X509Certificate2>()
                    .Where(c => c.HasPrivateKey).FirstOrDefault();

                return (object)cert ?? thumbprint;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
                return str;
            if (value is X509Certificate2 cert)
                return cert.Thumbprint;

            return null;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
