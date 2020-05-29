using Swiddler.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Swiddler.Controls
{
    public class SessionListView : ListView
    {
        public SessionTree Tree { get; } = new SessionTree();

        static SessionListView()
        {
            // this is needed when style is defined in themes/generic.xaml
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SessionListView), new FrameworkPropertyMetadata(typeof(SessionListView)));
        }

        public SessionListView()
        {
            AddHandler(TreeToggleButton.TreeToggleEvent, new EventHandler<TreeToggleRoutedEventArgs>(TreeToggled));

            SelectionChanged += SelectionChanged_;

            ItemsSource = Tree.FlattenItems;
        }

        private void SelectionChanged_(object sender, SelectionChangedEventArgs e)
        {
            foreach (SessionListItem item in e.RemovedItems)
                item.IsSelected = false;
            foreach (SessionListItem item in e.AddedItems)
                item.IsSelected = true;
        }

        void TreeToggled(object sender, TreeToggleRoutedEventArgs e)
        {
            if (e.Item.IsExpanded)
                Tree.Collapse(e.Item);
            else
                Tree.Expand(e.Item);
        }

    }
}
