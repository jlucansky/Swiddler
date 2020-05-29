using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Markup;

namespace Swiddler.Converters
{
    public class SslVersionStringConverter : MarkupExtension, IMultiValueConverter
    {
        static readonly string[] versionNames = new[] { "SSL 3.0", "TLS 1.0", "TLS 1.1", "TLS 1.2", "TLS 1.3" };

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == versionNames.Length)
            {
                var states = values.Select(x => x as bool?).ToArray();
                var list = new List<string>();

                for (int i = 0; i < states.Length; i++)
                    if (states[i] == true) list.Add(versionNames[i]);

                if (list.Count == 0)
                    return "No protocol version selected";
                else
                    return string.Join(", ", list);
            }

            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
