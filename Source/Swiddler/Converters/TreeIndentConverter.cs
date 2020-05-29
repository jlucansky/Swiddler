using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Swiddler.Converters
{
    public class TreeIndentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new Thickness()
            {
                Left = (int)value * (int)parameter
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
