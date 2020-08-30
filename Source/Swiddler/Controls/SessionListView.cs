using Swiddler.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

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

        private void FocusSelectedItem()
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (SelectedIndex >= 0)
                {
                    var lbi = ItemContainerGenerator.ContainerFromIndex(SelectedIndex) as ListBoxItem;
                    lbi?.Focus();
                }
            }));
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            var item = (SessionListItem)SelectedItem;

            if (item != null)
            {
                if (item.HasToggleButton)
                {
                    if (e.Key == Key.Left)
                    {
                        if (item.IsExpanded == true)
                        {
                            Collapse(item);
                            e.Handled = true;
                        }
                    }
                    else if (e.Key == Key.Right || e.Key == Key.Enter)
                    {
                        if (item.IsExpanded == false)
                        {
                            Expand(item);
                            e.Handled = true;
                        }
                    }
                }
                else
                {
                    if (e.Key == Key.Left && item.Session?.IsChildSession == true)
                    {
                        var parentItem = Tree.GetItem(item.Session.Parent);
                        if (parentItem != null)
                        {
                            Collapse(parentItem);
                            e.Handled = true;
                        }
                    }
                }
            }

            base.OnPreviewKeyDown(e);
        }

        public void Collapse(SessionListItem item)
        {
            var selItem = SelectedItem as SessionListItem;
            if (selItem?.Session?.Parent == item.Session)
            {
                var parentItem = Tree.GetItem(item.Session);
                SelectedIndex = Tree.IndexOf(parentItem);
            }

            Tree.Collapse(Tree.IndexOf(item));
        }

        public void Expand(SessionListItem item)
        {
            Tree.Expand(Tree.IndexOf(item));
        }

        private void SelectionChanged_(object sender, SelectionChangedEventArgs e)
        {
            foreach (SessionListItem item in e.RemovedItems)
                item.IsSelected = false;
            foreach (SessionListItem item in e.AddedItems)
                item.IsSelected = true;

            FocusSelectedItem(); // fix lost focus when removing selected item
        }

        void TreeToggled(object sender, TreeToggleRoutedEventArgs e)
        {
            if (e.Item.IsExpanded)
                Collapse(e.Item);
            else
                Expand(e.Item);
        }

    }
}
