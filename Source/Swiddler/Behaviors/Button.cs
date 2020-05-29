using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Swiddler.Behaviors
{
    public static class Button
    {
        private static readonly DependencyProperty ClickOpensContextMenuProperty =
            DependencyProperty.RegisterAttached("ClickOpensContextMenu", typeof(bool), typeof(Button),
              new PropertyMetadata(false, new PropertyChangedCallback(HandlePropertyChanged)));

        public static bool GetClickOpensContextMenu(DependencyObject obj) => (bool)obj.GetValue(ClickOpensContextMenuProperty);

        public static void SetClickOpensContextMenu(DependencyObject obj, bool value) => obj.SetValue(ClickOpensContextMenuProperty, value);

        private static void HandlePropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            if (obj is ButtonBase btn)
            {
                btn.Click -= ExecuteClick;
                btn.Click += ExecuteClick;
            }
        }

        private static void ExecuteClick(object sender, RoutedEventArgs args)
        {
            if (sender is ButtonBase btn && GetClickOpensContextMenu(btn))
            {
                if (btn.ContextMenu != null)
                {
                    btn.ContextMenu.PlacementTarget = btn;
                    btn.ContextMenu.Placement = ContextMenuService.GetPlacement(btn);
                    btn.ContextMenu.IsOpen = true;
                }
            }
        }
    }
}
