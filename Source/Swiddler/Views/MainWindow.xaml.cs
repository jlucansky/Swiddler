using Swiddler.Commands;
using Swiddler.Common;
using Swiddler.DataChunks;
using Swiddler.Serialization;
using Swiddler.Utils;
using Swiddler.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;

namespace Swiddler.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly SessionTree sessionTree;

        private TaskbarProgress taskbarProgress;

        private ObservableCollection<string> quickMRU;

        public MainWindow()
        {
            var userSettings = UserSettings.Load();
            
            InitializeComponent();

            if (userSettings.MainWindowBounds.HasSize())
            {
                var rect = userSettings.MainWindowBounds;
                Left = rect.Left;
                Top = rect.Top;
                Width = rect.Width;
                Height = rect.Height;
            }

            if (userSettings.MainWindowBounds.HasSize())
            {
                if (userSettings.MainWindowLeftColumn > 0) leftCol.Width = new GridLength(userSettings.MainWindowLeftColumn, GridUnitType.Pixel);
            }

            InitMRU(userSettings.QuickMRU);

            sessionTree = sessionListView.Tree;
            sessionTree.ItemAdded += SessionTree_ItemAdded;
            sessionListView.SelectionChanged += SessionListView_SelectionChanged;

            chunkView.FragmentView.OleProgressChanged += OleProgressChanged;

            CommandBindings.Add(new CommandBinding(ApplicationCommands.New, NewConnection_Click));
            CommandBindings.Add(new CommandBinding(MiscCommands.Disconnect, Disconnect_Click));
            CommandBindings.Add(new CommandBinding(MiscCommands.QuickConnect, (s, e) => QuickConnectCombo.Focus()));
            CommandBindings.Add(new CommandBinding(MiscCommands.Send, (s, e) => Send()));
        }


        void InitMRU(IEnumerable<string> mru)
        {
            quickMRU = new ObservableCollection<string>(mru) ?? new ObservableCollection<string>();

            var viewSource = new CollectionViewSource() { Source = quickMRU };
            ((ListCollectionView)viewSource.View).CustomSort = StringComparer.InvariantCultureIgnoreCase;
            QuickConnectCombo.ItemsSource = viewSource.View;
            QuickConnectCombo.Text = "http://example.org";
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            var settings = UserSettings.Load();

            settings.MainWindowBounds = new Rect(Left, Top, Width, Height);
            settings.MainWindowLeftColumn = leftCol.Width.Value;
            settings.MainWindowState = WindowState;
            settings.QuickMRU = quickMRU.Take(20).ToList();

            settings.Save();
        }

        private void OleProgressChanged(object sender, double val)
        {
            taskbarProgress.Visible = (0 < val);
            taskbarProgress.ProgressValue = val;
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            var hwndSource = (HwndSource)HwndSource.FromVisual(this);
            taskbarProgress = new TaskbarProgress(hwndSource.Handle);
        }

        private void SessionTree_ItemAdded(object sender, int index)
        {
            sessionListView.SelectedIndex = index;
            sessionListView.ScrollIntoView(sessionListView.SelectedItem);
        }

        private void SessionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sessionListView.SelectedIndex >= 0 && sessionListView.SelectedIndex < sessionTree.FlattenItems.Count)
            {
                var item = sessionTree.FlattenItems[sessionListView.SelectedIndex];
                chunkView.SetSession(item.Session);
            }
        }

        // TODO: prec
#if DEBUG
        private void Log(string message)
        {
            //chunkView.CurrentSession.Logs.Add(new RawData() { Data = Encoding.Default.GetBytes(message), Flow = TrafficFlow.Inbound });

            /*
            chunkView.CurrentSession.Logs.Add(new Message() { Text = "1 longa sdlkjsdflkj ls kd lkjasl fkkldfs jas f" + DateTime.Now.Ticks + "" });
            chunkView.CurrentSession.Logs.Add(new Message() { Text = "2 longa sdlkjsdflkj ls kd lkjasl fkkldfs jas f" + DateTime.Now.Ticks + "" });
            chunkView.CurrentSession.Logs.Add(new Message() { Text = "3 longa sdlkjsdflkj ls kd lkjasl fkkldfs jas f" + DateTime.Now.Ticks + "" });
            chunkView.CurrentSession.Logs.Add(new Message() { Text = "4 longa sdlkjsdflkj ls kd lkjasl fkkldfs jas f" + DateTime.Now.Ticks + "" });
            chunkView.CurrentSession.Logs.Add(new Message() { Text = DateTime.Now.Ticks + "" });
            chunkView.CurrentSession.Logs.Add(new Message() { Text = "5 longa sdlkjsdfldkj ls kd lkjasl fkkldfs jas f" + DateTime.Now.Ticks + "" });
            */

            chunkView.CurrentSession.Storage.Write(new Packet()
            {
                Flow = TrafficFlow.Outbound,
                Payload = Encoding.Default.GetBytes("\nSTART\nrfeoiewoi=éiľršonľf\néiľršonľf\néiľršonľf\n\n\n\néiľršonľf\néiľršonľf\néiľršonľf\n")
            });

            chunkView.CurrentSession.Storage.Write(new Packet()
            {
                Flow = TrafficFlow.Outbound,
                Payload = Encoding.Default.GetBytes("\n\n")
            });

            chunkView.CurrentSession.Storage.Write(new Packet()
            {
                Flow = TrafficFlow.Inbound,
                Payload = Encoding.Default.GetBytes("\nWWWWWWWWWaaaaaaaabbbbbbbbb77778\n\n\n\n\n613531aaaaxWWWWWWWWWaaaaaaaabbbbbbbbb77778613531aaaax")
            });

            chunkView.CurrentSession.Storage.Write(new Packet()
            {
                Flow = TrafficFlow.Outbound,
                Payload = Encoding.Default.GetBytes("fwqaewqerfwqaewqerfwqaewqerfwqaewqerfwqaewqerfwqaewqerfwqaewqerfwqaewqerfwqaewqerfwqaewqerfwqaewqerfwqaewqerfwqaewqerfwqaewqer")
            });

        }
#endif

        private void InputText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control) &&
                chunkView.CurrentSession?.State == SessionState.Started)
            {
                Send();
            }
        }

        void Send()
        {
            string text = inputText.Text;
            chunkView.CurrentSession?.SessionChannel.Submit(Encoding.Default.GetBytes(text));
            inputText.Clear();
        }

        private void NewConnection_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new NewConnection() { Owner = this };
            dlg.ShowDialog();
            AddSessionAndStart(dlg.Result);
        }

        public bool AddSessionAndStart(Session session)
        {
            if (session == null) return false;

            if (session.ClientSettings != null)
            {
                var mrui = session.ClientSettings.ToString().ToLower();
                if (!quickMRU.Contains(mrui)) quickMRU.Add(mrui);
            }

            sessionListView.Tree.Add(session);
            session.StartAsync();

            inputText.Focus();

            return true;
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            chunkView.CurrentSession?.Stop();
        }

        private void QuickConnect_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(QuickConnectCombo.Text))
            {
                try
                {
                    AddSessionAndStart(ConnectionSettings.TryCreateFromString(QuickConnectCombo.Text)?.CreateSession());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            Send();
        }

        private void QuickConnectCombo_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                e.Handled = true;
                QuickConnect_Click(sender, e);
            }
        }

        private void inputText_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Keyboard.ClearFocus();
        }
    }
}
