using Swiddler.Common;
using Swiddler.Utils;
using Swiddler.ViewModels;
using Swiddler.Views;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace Swiddler
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public AppResources Res { get; private set; }

        public static new App Current { get; private set; }

        public static bool InDesignMode { get; private set; } = true;

        public string AppDataPath { get; private set; }
        public string RecentPath => Path.Combine(AppDataPath, "MRU");
        public string CertLogPath => Path.Combine(AppDataPath, "CertLog");


        protected override void OnStartup(StartupEventArgs e)
        {
            InDesignMode = false;
            Current = (App)Application.Current;
            Res = new AppResources();

            EnsureUserFolders();

            Task.Run(() => Firewall.Instance.GrantAuthorizationToSelf());

            var firstArg = e.Args.FirstOrDefault();

            if (firstArg?.Equals("-nonew", StringComparison.OrdinalIgnoreCase) == true)
            {
                new MainWindow().Show();
            }
            else if (firstArg?.Equals("-settings", StringComparison.OrdinalIgnoreCase) == true && e.Args.Length == 2)
            {
                var cs = ConnectionSettings.Deserialize(File.ReadAllBytes(e.Args[1]));
                var session = cs.CreateSession();
                if (session != null)
                {
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    mainWindow.AddSessionAndStart(session);
                }
            }
            else
            {
                Rect rect = Rect.Empty;
                var session = SessionFromArgs(e.Args);

                if (session == null)
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    var dlg = new NewConnection() { ShowInTaskbar = true, Title = "Swiddler", WindowStartupLocation = WindowStartupLocation.CenterScreen };
                    dlg.ShowDialog();
                    session = dlg.Result;
                    rect = new Rect(dlg.Left, dlg.Top, dlg.Width, dlg.Height);
                }

                if (session != null)
                {
                    ShutdownMode = ShutdownMode.OnLastWindowClose;
                    var mainWindow = new MainWindow();
                    if (!rect.IsEmpty)
                    {
                        mainWindow.Left = (rect.Width - mainWindow.Width) / 2.0 + rect.Left; // center of closed NewConnection dialog
                        mainWindow.Top = (rect.Height - mainWindow.Height) / 2.0 + rect.Top;
                    }
                    mainWindow.Show();
                    mainWindow.AddSessionAndStart(session);
                }
            }
        }

        Session SessionFromArgs(string[] args)
        {
            if (args.Length == 0) return null;
            try
            {
                ConnectionSettings cs = null;
                string listener = null;
                bool udp = false;
                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    if (arg.Equals("-l", StringComparison.OrdinalIgnoreCase) && args.Length > i + 1)
                    {
                        arg = args[++i];
                        if (int.TryParse(arg, out var port))
                            listener = "0.0.0.0:" + port;
                        else
                            listener = arg;
                        continue;
                    }
                    if (arg.Equals("-u", StringComparison.OrdinalIgnoreCase))
                    {
                        udp = true;
                        continue;
                    }

                    if (args[i].StartsWith("-")) throw new InvalidOperationException("Invalid switch: " + args[i]);

                    cs = ConnectionSettings.TryCreateFromString(args[i]);
                }

                udp |= cs?.UDPChecked ?? false; // enable UDP when target was url like udp://1.2.3.4:56

                if (Net.TryParseUri(listener, out var listenerUri) && cs == null)
                    cs = ConnectionSettings.New();

                if (cs != null)
                {
                    cs.TCPChecked = !udp;
                    cs.UDPChecked = udp;
                }
                if (listenerUri != null)
                {
                    cs.ServerChecked = true;
                    cs.ServerSettings.IPAddress = listenerUri.GetTrimmedHost(); // expected IP address
                    cs.ServerSettings.Port = listenerUri.Port;
                }

                return cs?.CreateSession();
            }
            catch (Exception ex)
            {
                string msg = "";
                if (ex is ValueException) msg = ex.Message + "\n\n";
                MessageBox.Show(msg + "Usage: swiddler.exe [remote_ip:port] [-l [listener_ip:]port] [-u]", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.Exit(1);
            }
            
            return null;
        }

        private void EnsureUserFolders()
        {
            AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(Swiddler));
            if (!Directory.Exists(AppDataPath)) Directory.CreateDirectory(AppDataPath);
            if (!Directory.Exists(RecentPath)) Directory.CreateDirectory(RecentPath);
            if (!Directory.Exists(CertLogPath)) Directory.CreateDirectory(CertLogPath);
        }
    }
}
