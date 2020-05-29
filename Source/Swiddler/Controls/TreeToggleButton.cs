using Swiddler.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace Swiddler.Controls
{
    public class TreeToggleButton : ToggleButton
    {
        static TreeToggleButton()
        {
            // this is needed when style is defined in themes/generic.xaml
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeToggleButton), new FrameworkPropertyMetadata(typeof(TreeToggleButton)));
        }

        public static readonly RoutedEvent TreeToggleEvent = 
            EventManager.RegisterRoutedEvent(nameof(TreeToggle), RoutingStrategy.Bubble, typeof(EventHandler<TreeToggleRoutedEventArgs>), typeof(TreeToggleButton));

        public event RoutedEventHandler TreeToggle
        {
            add { AddHandler(TreeToggleEvent, value); }
            remove { RemoveHandler(TreeToggleEvent, value); }
        }

        protected virtual void RaiseTreeToggleEvent()
        {
            RoutedEventArgs args = new RoutedEventArgs(TreeToggleEvent);
            RaiseEvent(args);
        }

        public TreeToggleButton()
        {
            Click += TreeToggleButton_Click;
        }

        private void TreeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new TreeToggleRoutedEventArgs(TreeToggleEvent, (SessionListItem)DataContext));
        }
    }

    public class TreeToggleRoutedEventArgs : RoutedEventArgs
    {
        public SessionListItem Item { get; set; }

        public TreeToggleRoutedEventArgs(RoutedEvent routedEvent, SessionListItem item)
            :base(routedEvent)
        {
            Item = item;
        }
    }
}
