using Swiddler.Security;
using System;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Data;
using System.Windows.Markup;

namespace Swiddler.Converters
{
    public class CertificateNameConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is X509Certificate2 cert)
            {
                return cert.GetCertDisplayName();
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
