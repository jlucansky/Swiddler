using System.Windows;

namespace Swiddler.Behaviors
{
    public static class FrameworkElement
    {
        public static bool GetSetDpi(System.Windows.FrameworkElement element) => (bool)element.GetValue(SetDpiProperty);
        public static void SetSetDpi(System.Windows.FrameworkElement element, bool value) => element.SetValue(SetDpiProperty, value);

        public static readonly DependencyProperty SetDpiProperty =
                DependencyProperty.RegisterAttached("SetDpi",
                typeof(bool), typeof(FrameworkElement),
                new UIPropertyMetadata(false, OnSetDpiChanged));

        private static void OnSetDpiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.FrameworkElement elm && e.NewValue is bool == true)
                App.Current?.Res.AttachDpiProperties(elm);
        }
    }
}
