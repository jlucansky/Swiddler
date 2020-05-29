using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Swiddler.Converters
{
    public class ScrollBarVisibility : MarkupExtension, IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is Visibility visibility)
            {
                if (visibility == Visibility.Visible)
                    return visibility;
                else
                    return values[1];
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


        public static readonly DependencyProperty NonVisibleValueProperty =
            DependencyProperty.RegisterAttached("NonVisibleValue", typeof(Visibility), typeof(ScrollBarVisibility),
            new FrameworkPropertyMetadata(Visibility.Collapsed, FrameworkPropertyMetadataOptions.Inherits));
        
        public static Visibility GetNonVisibleValue(DependencyObject depo) => (Visibility)depo.GetValue(NonVisibleValueProperty);

        public static void SetNonVisibleValue(DependencyObject depo, Visibility value) => depo.SetValue(NonVisibleValueProperty, value);


    }
}
