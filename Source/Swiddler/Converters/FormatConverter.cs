using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Swiddler.Converters
{
    public class FormatConverter : MarkupExtension, IValueConverter
    {
        public string Format { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Format(CultureInfo.CurrentCulture, Format, value);
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
