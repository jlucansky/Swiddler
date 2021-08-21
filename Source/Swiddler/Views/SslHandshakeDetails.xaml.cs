using Swiddler.Commands;
using Swiddler.Security;
using Swiddler.Serialization;
using Swiddler.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Swiddler.Views
{
    /// <summary>
    /// Interaction logic for SslHandshakeDetails.xaml
    /// </summary>
    public partial class SslHandshakeDetails : Window
    {
        public SslHandshake Model { get; }

        public List<PropertyValue> ChannelProperties { get; }
        public List<Certificate> Certificates { get; } = new List<Certificate>();
        public Certificate SelectedCertificate { get; set; }

        public string HandshakeText { get; }

        public bool HasCertificate => Model?.GetPrimaryCertificateChain()?.Any() == true;

        public class PropertyValue
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        public class Certificate
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string NotBefore { get; set; }
            public string NotAfter { get; set; }

            public X509Certificate2 Certificate2 { get; set; }
            public X509Certificate2[] Chain { get; set; }

            public  bool IsValid { get; set; }
            public string IconName { get; set; }
        }

        StringBuilder stringBuilder;

        public SslHandshakeDetails(SslHandshake model)
        {
            Model = model;
            DataContext = this;
            ChannelProperties = CreateCommonProperties();

            AddCerts();

            HandshakeText = CreateLog();

            InitializeComponent();

            BindCommand(CertificateCommands.ViewCertificateDetails, c => ShowCertificateChain(c.Chain));
            BindCommand(CertificateCommands.ExportPEM, c => ExportCerts("PEM", c.Certificate2));
            BindCommand(CertificateCommands.ExportDER, c => ExportCerts(X509ContentType.Cert, c.Certificate2));
            BindCommand(CertificateCommands.ExportAllPEM, c => ExportCerts("PEM", c.Chain));
            BindCommand(CertificateCommands.ExportAllDER, c => ExportCerts(X509ContentType.Cert, c.Chain));
            BindCommand(CertificateCommands.InstallCA, c => InstallCA(c.Chain.Last()));
        }

        void ShowCertificateChain(X509Certificate2[] chain)
        {
            chain.ShowCertificateChain(lvCertificates, new Point(16, 32));
        }

        void ExportCerts(object contentType, params X509Certificate2[] certs)
        {
            foreach (var crt in certs) if (!crt.SaveAs(contentType)) break;
        }

        void InstallCA(X509Certificate2 crt)
        {
            X509Store rootStore = null;
            try
            {
                rootStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                rootStore.Open(OpenFlags.ReadWrite);

                if  (rootStore.Certificates.Contains(crt))
                {
                    MessageBox.Show($"'{crt.GetCertDisplayName()}' is already installed.", "Certificate", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else
                {
                    var crtPub = new X509Certificate2(crt) { PrivateKey = null };
                    rootStore.Add(crtPub);
                    crtPub.Reset();
                }

                MessageBox.Show($"'{crt.GetCertDisplayName()}' successfully installed.", "Certificate", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                rootStore?.Close();
            }

            // force reload cert list
            Certificates.Clear();
            AddCerts();
            lvCertificates.ItemsSource = null;
            lvCertificates.ItemsSource = Certificates;
        }

        void BindCommand(RoutedCommand command, Action<Certificate> action)
        {
            lvCertificates.CommandBindings.Add(new CommandBinding(command, (s, e) => action((Certificate)((FrameworkElement)e.OriginalSource).DataContext)));
        }

        string CreateLog()
        {
            var sb = stringBuilder = new StringBuilder();

            if (ChannelProperties?.Any() == true)
            {
                sb.AppendLine();
                sb.AppendLine("CRYPTO PARAMETERS");
                sb.AppendLine("‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾");
                Append(ChannelProperties);
            }

            if (Model.ClientHello != null)
            {
                sb.AppendLine();
                sb.AppendLine("CLIENT HELLO");
                sb.AppendLine("‾‾‾‾‾‾‾‾‾‾‾‾");

                var hi = Model.ClientHello;

                var list = new List<PropertyValue>()
                {
                    Create("Client Version", $"0x{hi.MajorVersion:X2}{hi.MinorVersion:X2}"),
                    Create("Client Random", GetHex(hi.Random) ),
                    Create("Client SessionID", GetHex(hi.SessionId) ),
                    Create("Cipher Suites", string.Join(", ", hi.Ciphers.Select(x => $"0x{x:X4}") )),
                    Create("", string.Join(", ", hi.GetCipherSuites() )),
                    Create("Compression Methods", GetHex(hi.CompressionData) ),
                };

                if (hi.GetServerNameIndication() is string sni && !string.IsNullOrEmpty(sni))
                    list.Add(Create("Server Name Indication", hi.GetServerNameIndication()));

                if (hi.Extensions != null)
                    list.AddRange(FormatExtensions(hi.Extensions));

                Append(list, out var width);
            }

            if (Model.ServerHello != null)
            {
                sb.AppendLine();
                sb.AppendLine("SERVER HELLO");
                sb.AppendLine("‾‾‾‾‾‾‾‾‾‾‾‾");

                var hi = Model.ServerHello;
                Append(new[]
                {
                    Create("Server Version", $"0x{hi.MajorVersion:X2}{hi.MinorVersion:X2}"),
                    Create("Server Random", GetHex(hi.Random) ),
                    Create("Server SessionID", GetHex(hi.SessionId) ),
                    Create("Cipher Suite", $"0x{hi.CipherSuite:X4} ({hi.CipherSuiteName})" ),
                    Create("Compression Method", $"0x{hi.CompressionMethod:X2}") ,
                }.Concat(FormatExtensions(hi.Extensions)), out var width);
            }

            return sb.ToString();
        }

        void AddCerts()
        {
            AddCert(Model.ClientCertificate, "Client");
            AddCert(Model.ServerCertificate, "Server");
        }

        void AddCert(X509Certificate2[] chain, string type)
        {
            if (chain?.Any() == true)
            {
                var crt = new Certificate()
                {
                    Chain = chain,
                    Certificate2 = chain[0],
                    Type = type + " Certificate",
                    IsValid = chain.ValidateChain(),
                    Name = chain[0].GetCertDisplayName(),
                    NotBefore = chain[0].NotBefore.ToString("g"),
                    NotAfter = chain[0].NotAfter.ToString("g"),
                };
                crt.IconName = crt.IsValid ? "LockValid" : "LockInvalid";
                Certificates.Add(crt);
            }
        }

        List<PropertyValue> CreateCommonProperties()
        {
            if (!Model.IsAuthenticated) return new List<PropertyValue>();

            var list = new List<PropertyValue>()
            {
                Create("Secure Protocol",   $"{Model.SslProtocol}"),
                Create("Cipher Suite",      $"0x{Model.ServerHello.CipherSuite:X4} ({Model.ServerHello.CipherSuiteName})"),
                Create("Cipher Algorithm",  GetCipherAlgorithm(Model.CipherAlgorithm)   + FormatBits(Model.CipherStrength)),
                Create("Hash Algorithm",    GetHashAlgorithm(Model.HashAlgorithm)       + FormatBits(Model.HashStrength)),
                Create("Key Exchange",      GetKeyExchange(Model.KeyExchangeAlgorithm)  + FormatBits(Model.KeyExchangeStrength)),
            };
            return list;
        }
        
        string FormatBits(int number)
        {
            if (number == 0) return "";
            return $" {number}bits";
        }

        string GetCipherAlgorithm(int code)
        {
            if (Enum.IsDefined(typeof(CipherAlgorithmType), code))
            {
                var value = (CipherAlgorithmType)code;
                if (value == CipherAlgorithmType.None) return "None";
                if (value == CipherAlgorithmType.Null) return "Null";
                if (value == CipherAlgorithmType.TripleDes) return "3DES";
                return value.ToString().ToUpper();
            }

            return $"0x{code:X}";
        }

        string GetHashAlgorithm(int code)
        {
            switch (code)
            {
                case 0: return "None";
                case 32771: return "MD5";
                case 32772: return "SHA1";
                case 32780: return "SHA256";
                case 32781: return "SHA384";
                case 32782: return "SHA512";
            }

            return $"0x{code:X}";
        }

        string GetKeyExchange(int code)
        {
            if (Enum.IsDefined(typeof(ExchangeAlgorithmType), code))
            {
                var value = (ExchangeAlgorithmType)code;
                return value.ToString();
            }
            if (code == 44550) return "ECDHE";

            return $"0x{code:X}";
        }

        PropertyValue Create(string name, string value)
        {
            return new PropertyValue() { Name = name, Value = value };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.RemoveIcon();
            this.DisableMinMaxControls();
        }

        private void Certificates_LostFocus(object sender, RoutedEventArgs e)
        {
            lvCertificates.UnselectAll();
        }

        void Append(IEnumerable<PropertyValue> values) => Append(values, out var _);

        IEnumerable<PropertyValue> AlignNames(IEnumerable<PropertyValue> values)
        {
            int width = values.Max(x => x.Name.Length);

            foreach (var item in values)
            {
                string name = item.Name;
                string delim = string.IsNullOrEmpty(name) ? " " : ":";
                item.Name = $" {new string(' ', width - name.Length)}{name}{delim}";
            }

            return values;
        }

        void Append(IEnumerable<PropertyValue> values, out int width)
        {
            StringBuilder sb = stringBuilder;
            width = values.Max(x => x.Name.Length);

            int index = 0;
            foreach (var item in values)
            {
                string name = item.Name;

                //foreach (var line in WrapText(item.Value, 80))
                {
                    string delim = string.IsNullOrEmpty(name) ? " " : ":";
                    sb.AppendLine($" {new string(' ', width - name.Length)}{name}{delim} {item.Value}");
                    name = "";
                }

                index++;
            }
        }

        static IEnumerable<string> WrapText(string text, int width)
        {
            string[] originalLines = text.Split(new[] { ' ' }, StringSplitOptions.None);

            StringBuilder actualLine = new StringBuilder();
            int actualWidth = 0;

            foreach (var item in originalLines)
            {
                var cur = item + " ";
                actualLine.Append(cur);
                actualWidth += cur.Length;

                if (actualWidth > width)
                {
                    yield return actualLine.ToString();
                    actualLine.Clear();
                    actualWidth = 0;
                }
            }

            if (actualLine.Length > 0)
                yield return actualLine.ToString();
        }

        void Append(Dictionary<string, SslExtension> extensions, int margin)
        {
            margin = 1;
            StringBuilder sb = stringBuilder;

            int lastIndex = extensions.Count - 1;

            var names = extensions.Values.Select(x=> $"0x{x.Type:X4} ({x.Name})").ToArray();
            var len = names.Max(x => x.Length);

            string marginStr = new string(' ', margin);
            
            int index = 0;
            foreach (var item in extensions)
            {
                var value = item.Value;
                string beg = "┃";
                if (index == lastIndex)
                    beg = "┗";
                string name = names[index];
                sb.AppendLine($"{marginStr}{beg} {name}{new string(' ', len - name.Length)}: {GetHex(value.RawData)}");

                index++;
            }
        }

        IEnumerable<PropertyValue> FormatExtensions(Dictionary<string, SslExtension> extensions)
        {
            if (extensions?.Any() == false) yield break;

            var label = "Extensions";

            var values = extensions.Values.OrderBy(x => x.Position).ToArray();
            var names = values.Select(x => $"0x{x.Type:X4} ({x.Name})").ToArray();
            var len = names.Max(x => x.Length);

            for (int i = 0; i < values.Length; i++)
            {
                var name = names[i];
                yield return Create(label, $"{name}{new string(' ', len - name.Length)}: {GetHex(values[i].RawData)}");
                label = "";
            }
        }

        void Append(string label, ref int width)
        {
            width -= (label.Length - 1);
            stringBuilder.AppendLine(" ┏" + new string('-', width - 2) + label + ":");
        }

        string GetHex(byte[] data)
        {
            if (data == null) return "";
            return BitConverter.ToString(data).Replace("-", " ");
        }

        private void Certificate_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && SelectedCertificate != null)
            {
                ShowCertificateChain(SelectedCertificate.Chain);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }
    }
}
