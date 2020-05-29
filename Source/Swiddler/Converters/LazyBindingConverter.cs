using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Swiddler.Converters
{
    public class LazyBindingConverter : MarkupExtension, IMultiValueConverter
    {
        object cachedValue;
        bool success;

        // params (bool, Lazy<object>)
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (success) return cachedValue;

            if (values.Length == 2 && values[0] as bool? == true)
            {
                success = true;
                return cachedValue = (values[1] as Lazy<object>)?.Value;
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
