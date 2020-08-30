using Swiddler.Commands;
using Swiddler.Common;
using Swiddler.Serialization;
using Swiddler.Utils;
using Swiddler.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

        public bool PcapSelectionExport { get => App.Current.PcapSelectionExport; set => App.Current.PcapSelectionExport = value; }
        public bool BinaryInput { get; private set; }

        public Encoding InputEncoding { get; } = Encoding.GetEncoding(437); // IBM 437 (OEM-US)

        public BindableProperties Properties { get; } = new BindableProperties();

        public MainWindow()
        {
            var userSettings = UserSettings.Load();

            App.Current.PcapSelectionExport = userSettings.PcapSelectionExport;

            DataContext = this;

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
                if (userSettings.MainWindowLeftColumn > 0) leftCol.Width = new GridLength(userSettings.MainWindowLeftColumn);
                if (userSettings.MainWindowBottomRow > 0) InputRowHeight = new GridLength(userSettings.MainWindowBottomRow);
            }

            InitMRU(userSettings.QuickMRU);

            sessionTree = sessionListView.Tree;
            sessionTree.ItemAdded += SessionTree_ItemAdded;
            sessionListView.SelectionChanged += SessionListView_SelectionChanged;

            chunkView.FragmentView.OleProgressChanged += OleProgressChanged;
            chunkView.FragmentView.SessionChanged += FragmentView_SessionChanged;

            CommandManager.AddPreviewExecutedHandler(inputText, (s, e) => { if (e.Command == ApplicationCommands.Paste) OnPasteCommand(s, e); });

            CommandBindings.Add(new CommandBinding(ApplicationCommands.New, NewConnection_Click));
            CommandBindings.Add(new CommandBinding(MiscCommands.Disconnect, Disconnect_Click));
            CommandBindings.Add(new CommandBinding(MiscCommands.QuickConnect, (s, e) => QuickConnectCombo.Focus()));
            CommandBindings.Add(new CommandBinding(MiscCommands.Send, (s, e) => Send()));
            CommandBindings.Add(new CommandBinding(MiscCommands.SelectAll, (s, e) => SelectAll()));
            CommandBindings.Add(new CommandBinding(MiscCommands.GoToStart, (s, e) => GoToStart()));
            CommandBindings.Add(new CommandBinding(MiscCommands.GoToEnd, (s, e) => GoToEnd()));
            CommandBindings.Add(new CommandBinding(MiscCommands.ToggleHex, (s, e) => ToggleBinaryInput(!BinaryInput)));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Properties.InputText_ScrollViewer = inputText.Template.FindName("PART_ContentHost", inputText) as ScrollViewer;
            Properties.InputVisibility = Visibility.Collapsed;
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
            settings.MainWindowBottomRow = InputRowHeight.Value;
            settings.MainWindowState = WindowState;
            settings.QuickMRU = quickMRU.Take(20).ToList();
            settings.PcapSelectionExport = App.Current.PcapSelectionExport;

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

        private void FragmentView_SessionChanged(object sender, Session newSession)
        {
            var oldSession = chunkView.FragmentView.CurrentSession;

            if (oldSession != null)
                oldSession.StateChanged -= Session_StateChanged;

            newSession.StateChanged += Session_StateChanged;

            UpdateCanStop(newSession.State);
            UpdateInputVisibility(newSession);
        }

        private void Session_StateChanged(object sender, SessionState state)
        {
            UpdateCanStop(state);
            UpdateInputVisibility(chunkView.CurrentSession);
        }

        void UpdateCanStop(SessionState state)
        {
            Properties.CanStop = state == SessionState.New || state == SessionState.Started || state == SessionState.Starting;
        }

        GridLength validInputRowHeight;

        void UpdateInputVisibility(Session session)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Properties.InputVisibility = session.SessionChannel?.IsActive == true ? Visibility.Visible : Visibility.Collapsed;

                if (Properties.InputVisibility == Visibility.Visible && inputRow.Height.GridUnitType == GridUnitType.Auto)
                {
                    inputRow.Height = validInputRowHeight;
                    inputRow.MinHeight = 40;
                }
                else if (Properties.InputVisibility != Visibility.Visible && inputRow.Height.GridUnitType != GridUnitType.Auto)
                {
                    validInputRowHeight = inputRow.Height;
                    inputRow.Height = GridLength.Auto;
                    inputRow.MinHeight = 0;
                }
            }));
        }

        GridLength InputRowHeight {
            get => inputRow.Height.GridUnitType == GridUnitType.Auto ? validInputRowHeight : inputRow.Height;
            set
            {
                validInputRowHeight = value;
                if (inputRow.Height.GridUnitType != GridUnitType.Auto) inputRow.Height = value;
            }
        }

        private void SessionTree_ItemAdded(object sender, int index)
        {
            var item = (SessionListItem)sessionListView.Items[index];

            if (!item.Session.IsChildSession || item.Session.Parent.Children.Count == 1)
            {
                sessionListView.SelectedIndex = index;
                sessionListView.ScrollIntoView(sessionListView.SelectedItem);
            }
        }

        private void SessionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sessionListView.SelectedIndex >= 0 && sessionListView.SelectedIndex < sessionTree.FlattenItems.Count)
            {
                var item = sessionTree.FlattenItems[sessionListView.SelectedIndex];
                chunkView.SetSession(item.Session);
            }
        }

        private void InputText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control) &&
                chunkView.CurrentSession?.State == SessionState.Started)
            {
                Send();
            }
        }

        void SelectAll() => chunkView.FragmentView.SelectAll();

        void GoToStart() => chunkView.FragmentView.ScrollOwner.ScrollToHome();

        void GoToEnd() => chunkView.FragmentView.ScrollOwner.ScrollToEnd();

        void Send()
        {
            if (chunkView.CurrentSession?.SessionChannel?.IsActive != true)
                return;

            try
            {
                chunkView.CurrentSession.SessionChannel.Submit(GetInputData(BinaryInput, inputText.Text));
                inputText.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void OnPasteCommand(object sender, ExecutedRoutedEventArgs e)
        {
            string binFormat = typeof(byte[]).ToString();

            if (Clipboard.ContainsData(binFormat) && Clipboard.GetData(binFormat) is byte[] data)
            {
                string insertingText;

                if (BinaryInput)
                    insertingText = data.FormatHex();
                else
                    insertingText = InputEncoding.GetString(data);

                var selectionStart = inputText.SelectionStart;
                var selectionLength = inputText.SelectionLength;
                var text = inputText.Text;

                text = $"{text.Substring(0, selectionStart)}{insertingText}{text.Substring(selectionStart + selectionLength)}";
                inputText.Text = text;
                inputText.SelectionStart = selectionStart + insertingText.Length;

                e.Handled = true;
            }
        }

        int GetHexLocation(int original)
        {
            return original * 3 + (original / 4) + (original / 16) + (original / 32);
        }

        void ToggleBinaryInput(bool enabled)
        {
            if (BinaryInput == enabled) return;

            var selectionStart = inputText.SelectionStart;
            var selectionLength = inputText.SelectionLength;
            var selectionEnd = selectionStart + selectionLength;

            if (enabled)
            {
                var data = InputEncoding.GetBytes(inputText.Text);
                inputText.Text = data.FormatHex();
                inputText.SelectionStart = GetHexLocation(selectionStart);
                inputText.SelectionLength = GetHexLocation(selectionStart + selectionLength) - inputText.SelectionStart;
            }
            else
            {
                try
                {
                    inputText.Text.TokenizeHex(out var data, out var locations);
                    inputText.Text = InputEncoding.GetString(data);

                    if (data.Any())
                    {
                        int start = 0, end = 0;
                        for (int i = 0; i < locations.Length; i++)
                        {
                            //var iplus = i + 1;
                            if (locations[i] <= selectionStart)
                                start = end = i;
                            else if (locations[i] <= selectionEnd)
                                end = i;
                            else
                            {
                                if (selectionLength > 0 && locations[i - 1] < selectionEnd)
                                    end++;
                                break;
                            }
                        }

                        if (locations.Last() < selectionStart)
                            start++;

                        if (selectionLength > 0 && locations.Last() < selectionEnd)
                            end++;

                        inputText.SelectionStart = start;
                        inputText.SelectionLength = Math.Max(0, end - start);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    enabled = BinaryInput;
                }
            }

            BinaryInput = enabled;
            binaryInputButton.IsChecked = enabled;
        }

        byte[] GetInputData(bool binary, string text)
        {
            if (binary)
            {
                text.TokenizeHex(out var data, out _);
                return data;
            }
            else
            {
                return InputEncoding.GetBytes(text);
            }
        }

        private void BinaryInput_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleBinaryInput(((ToggleButton)sender).IsChecked == true);
        }

        private void inputText_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                try
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                    if (files[0].Length > 0)
                    {
                        var fi = new FileInfo(files[0]);
                        if (fi.Length > 1024 * 1024)
                            throw new Exception("File is too large.");

                        byte[] data;

                        using (var stream = File.Open(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (var mem = new MemoryStream())
                        {
                            stream.CopyTo(mem);
                            data = mem.ToArray();
                        }

                        if (BinaryInput)
                            inputText.Text = data.FormatHex();
                        else
                            inputText.Text = InputEncoding.GetString(data);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }

        private void inputText_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && !e.Data.ContainsSwiddlerSelection())
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true; // accept files
            }
        }

        private void inputText_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Properties.ShowSubmitButton = e.NewSize.Height > 50;
        }

        public class BindableProperties : BindableBase
        {
            ScrollViewer _InputText_ScrollViewer;
            public ScrollViewer InputText_ScrollViewer { get => _InputText_ScrollViewer; set => SetProperty(ref _InputText_ScrollViewer, value); }

            bool _CanStop = true;
            public bool CanStop { get => _CanStop; set => SetProperty(ref _CanStop, value); }

            Visibility _InputVisibility = Visibility.Visible;
            public Visibility InputVisibility { get => _InputVisibility; set => SetProperty(ref _InputVisibility, value); }

            bool _ShowSubmitButton = true;
            public bool ShowSubmitButton { get => _ShowSubmitButton; set => SetProperty(ref _ShowSubmitButton, value); }
        }

    }
}
