using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Swiddler.Converters
{
    public class AssignableTypeConverter : MarkupExtension, IValueConverter
    {
        public Type Type { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || Type == null) return false;
            return Type.IsAssignableFrom(value.GetType());
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
