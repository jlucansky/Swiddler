using Swiddler.Commands;
using Swiddler.Common;
using Swiddler.Serialization;
using Swiddler.SocketSettings;
using Swiddler.Utils;
using Swiddler.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Swiddler.Views
{
    public partial class NewConnection : Window
    {
        public Session Result { get; private set; } // dialog result

        public List<QuickActionItem> QuickActions { get; private set; }
        public ICollectionView QuickActionsView { get; private set; }

        public string SearchTextBoxValue { set => QuickActionsView.Filter = obj => ((QuickActionItem)obj).MatchSearch(value); } // no need for DependencyProperty when doing OneWayToSource binding

        public static readonly DependencyProperty ConnectionSettingsProperty = DependencyProperty.Register(nameof(ConnectionSettings), typeof(ConnectionSettings), typeof(NewConnection), new PropertyMetadata(new PropertyChangedCallback((d, e) => ((NewConnection)d).OnConnectionSettingsChanged((ConnectionSettings)e.NewValue))));
        public ConnectionSettings ConnectionSettings { get => (ConnectionSettings)GetValue(ConnectionSettingsProperty); set => SetValue(ConnectionSettingsProperty, value); }

        public static readonly DependencyProperty SelectedQuickActionProperty = DependencyProperty.Register(nameof(SelectedQuickAction), typeof(QuickActionItem), typeof(NewConnection), new PropertyMetadata(new PropertyChangedCallback((d, e) => ((NewConnection)d).OnSelectedQuickActionChanged((QuickActionItem)e.NewValue))));
        public QuickActionItem SelectedQuickAction { get => (QuickActionItem)GetValue(SelectedQuickActionProperty); set => SetValue(SelectedQuickActionProperty, value); }

        
        ConnectionSettings clipboardConnectionSettings;

        public NewConnection()
        {
            var userSettings = UserSettings.Load();
            if (userSettings.NewConnectionWindowBounds.HasSize())
            {
                var rect = userSettings.NewConnectionWindowBounds;
                Width = rect.Width;
                Height = rect.Height;
            }
            else
            {
                Width = 700;
                Height = 500;
            }

            var history = ConnectionSettings.GetHistory();
            var firstConn = history.FirstOrDefault()?.Copy();

            clipboardConnectionSettings = TryCreateFromClipboard(firstConn);
            ConnectionSettings = firstConn ?? QuickActionItem.DefaultTemplates[0].GetConnectionSettings(null);

            QuickActions = history.Select(x => new RecentlyUsedItem(x)).ToList<QuickActionItem>();
            QuickActions.InsertRange(0, QuickActionItem.DefaultTemplates);
            QuickActionsView = CreateQuickActionView();

            DataContext = this;
            InitializeComponent();

            if (userSettings.NewConnectionWindowBounds.HasSize())
            {
                if (userSettings.NewConnectionRightColumn > 0) rightCol.Width = new GridLength(userSettings.NewConnectionRightColumn, GridUnitType.Star);
                if (userSettings.NewConnectionLeftColumn > 0) leftCol.Width = new GridLength(userSettings.NewConnectionLeftColumn, GridUnitType.Star);
            }

            if (clipboardConnectionSettings != null)
            {
                clipboardLink.Text = clipboardConnectionSettings.ClientSettings.ToString();
                hintPanel.Visibility = Visibility.Visible;
            }

            CommandBindings.Add(new CommandBinding(MiscCommands.Search, (s, e) => SearchTextBox.Focus()));

            this.AddShadow(dispatched: true);
        }

        ConnectionSettings TryCreateFromClipboard(ConnectionSettings fallback)
        {
            if (Clipboard.ContainsText(TextDataFormat.Text))
            {
                var cs = ConnectionSettings.TryCreateFromString(Clipboard.GetText(TextDataFormat.Text));
                if (cs?.Settings.FirstOrDefault() is ClientSettingsBase sock)
                {
                    if (fallback != null && fallback.TCPChecked == cs.TCPChecked && fallback.ClientChecked && (sock.TargetPort ?? -1) == -1)
                        sock.TargetPort = fallback.ClientSettings.TargetPort; // set port from fallback when there is the same protocol

                    var originalCaption = sock.Caption;
                    sock.Caption += " (recognized from clipboard)";
                    void ActionPropertyChanged(object sender, PropertyChangedEventArgs e)
                    {
                        sock.PropertyChanged -= ActionPropertyChanged;
                        sock.Caption = originalCaption; // show "from clipboard" banner until any values change
                    }
                    sock.PropertyChanged += ActionPropertyChanged;

                    return cs;
                }
            }

            return null;
        }

        ICollectionView CreateQuickActionView()
        {
            var source = new CollectionViewSource();
            source.GroupDescriptions.Add(QuickActionGroupDescription.Default);
            source.Source = QuickActions;
            return source.View;
        }

        private void CreateSession(object sender, RoutedEventArgs e)
        {
            try
            {
                Result = ConnectionSettings.CreateSession();
                ConnectionSettings.SaveRecent();
                if (Result != null)
                    Result.SettingsFileName = ConnectionSettings.FileName;
                Close();
            }
            catch (Exception ex)
            {
                if (ex is ValueException vex)
                {
                    MessageBox.Show(vex.Message, "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ConnectionSettingsControl.Focus(vex.PropertyName);
                }
                else
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        protected virtual void OnSelectedQuickActionChanged(QuickActionItem template)
        {
            if (template == null) return;
            QuickActionsListBox.ScrollIntoView(template); // needed when using up and down arrow keys
            ConnectionSettings = template.GetConnectionSettings(ConnectionSettings);
        }

        protected virtual void OnConnectionSettingsChanged(ConnectionSettings cs)
        {
            if (cs == null) return;
            cs.IsDirtyChanged += IsDirtyChanged;
        }

        void IsDirtyChanged()
        {
            SelectedQuickAction = null; // unset selected item on first IsDirty change
            ConnectionSettings.IsDirtyChanged -= IsDirtyChanged;
        }

        private void QuickActions_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && SelectedQuickAction is RecentlyUsedItem item)
            {
                var oldIndex = QuickActionsListBox.SelectedIndex;
                if (QuickActions.Remove(item))
                {
                    item.ConnectionSettings.Delete();
                    QuickActionsView.Refresh();
                    if (oldIndex < QuickActions.Count)
                        QuickActionsListBox.SelectedIndex = oldIndex;
                }
            }
        }

        private void DragMove(object sender, MouseButtonEventArgs e) { DragMove(); }

        private void Close(object sender, RoutedEventArgs e)
        {
            App.Current.ShutdownMode = ShutdownMode.OnLastWindowClose; // exit if current window is the only window
            Close(); 
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && QuickActionsListBox.SelectedIndex < QuickActionsListBox.Items.Count - 1)
                QuickActionsListBox.SelectedIndex++;
            if (e.Key == Key.Up && QuickActionsListBox.SelectedIndex > 0)
                QuickActionsListBox.SelectedIndex--;
        }

        private void QuickActions_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && SelectedQuickAction?.Template == QuickActionTemplate.Undefined) CreateSession(sender, e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            var settings = UserSettings.Load();
            settings.NewConnectionWindowBounds = new Rect(Left, Top, Width, Height);
            settings.NewConnectionLeftColumn = leftCol.Width.Value;
            settings.NewConnectionRightColumn = rightCol.Width.Value;
            settings.Save();
        }

        private void AddRewriter_Click(object sender, RoutedEventArgs e)
        {
            ConnectionSettings.AddRewrite(new RewriteSettings());
        }

        private void DeleteSettings_Click(object sender, RoutedEventArgs e)
        {
            var context = ((FrameworkElement)sender).DataContext;

            if (context is ServerSettingsBase) ConnectionSettings.ServerChecked = false;
            if (context is ClientSettingsBase) ConnectionSettings.ClientChecked = false;
            if (context is RewriteSettings r) ConnectionSettings.RemoveRewrite(r);
            if (context is SnifferSettings) ConnectionSettings.SnifferChecked = false;
        }

        private void ClipboardLink_Click(object sender, RoutedEventArgs e)
        {
            ConnectionSettings = clipboardConnectionSettings.Copy();
        }

        private void CloseHint_Click(object sender, RoutedEventArgs e)
        {
            hintPanel.Visibility = Visibility.Collapsed;
        }

        private void HintPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (e.ClickCount == 2 && e.LeftButton == MouseButtonState.Pressed) ClipboardLink_Click(sender, e);
        }
    }
}
