using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Swiddler.Utils
{
    public static class DependencyObjectExtensions
    {
        public static DependencyObject FindBinding(this DependencyObject parent, string bindingPath)
        {
            if (parent == null) return null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is UIElement elm && elm.Visibility != Visibility.Visible)
                    continue; // skip hidden elements

                if (child is TextBox)
                {
                    if (FindBoundProperty(child, bindingPath, TextBox.TextProperty) != null) return child;
                }
                else if (child is ComboBox)
                {
                    if (FindBoundProperty(child, bindingPath, ComboBox.TextProperty, ComboBox.SelectedValueProperty, ComboBox.SelectedItemProperty) != null) return child;
                }

                var foundChild = FindBinding(child, bindingPath);
                if (foundChild != null)
                    return foundChild;
            }

            return null;
        }

        public static Binding FindBoundProperty(this DependencyObject obj, string bindingPath, params DependencyProperty[] properties)
        {
            foreach (var prop in properties)
            {
                var binding = BindingOperations.GetBinding(obj, prop);

                if (binding?.Path?.Path == bindingPath)
                {
                    return binding;
                }
            }

            return null;
        }

        public static void Focus(this DependencyObject parent, string bindingPath)
        {
            if (string.IsNullOrEmpty(bindingPath)) return;

            var elm = FindBinding(parent, bindingPath) as UIElement;
            elm?.Focus();
        }

    }
}
