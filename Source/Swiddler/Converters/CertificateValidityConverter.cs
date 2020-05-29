using Swiddler.Security;
using System;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;

namespace Swiddler.Converters
{
    public class CertificateValidityConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is X509Certificate2 cert)
            {
                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    return cert.ValidateChain() ? "Valid" : "Invalid"; ;
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
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
