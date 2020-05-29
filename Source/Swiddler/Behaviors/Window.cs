using Swiddler.Utils;
using System.Windows;

namespace Swiddler.Behaviors
{
    public static class Window
    {
        public static bool GetDisableControls(System.Windows.Window wnd) => (bool)wnd.GetValue(DisableControlsProperty);
        public static void SetDisableControls(System.Windows.Window wnd, bool value) => wnd.SetValue(DisableControlsProperty, value);

        public static readonly DependencyProperty DisableControlsProperty =
                DependencyProperty.RegisterAttached("DisableControls",
                typeof(bool), typeof(Window),
                new UIPropertyMetadata(false, OnDisableControlsChanged));

        private static void OnDisableControlsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Window wnd && e.NewValue is bool == true)
                wnd.SourceInitialized += (_, __) => wnd.DisableWindowControls();
        }
    }
}
