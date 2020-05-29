using Swiddler.Rendering;
using Swiddler.Security;
using Swiddler.Serialization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Swiddler.Views.Fragments
{
    /// <summary>
    /// Interaction logic for SslHandshakeView.xaml
    /// </summary>
    public partial class SslHandshakeView : UserControl
    {
        public SslHandshake Model => ((SslHandshakeFragment)DataContext).Model;

        public SslHandshakeView()
        {
            InitializeComponent();
        }

        private void Details_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SslHandshakeDetails(Model) { Owner = Window.GetWindow(this) };
            dlg.ShowDialog();
        }

        private void Cert_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var chain = Model.GetPrimaryCertificateChain();
            if (chain?.Any() == true)
                chain.ShowCertificateChain(this, new Point(4, btnCert.Height + 4));
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (e.ClickCount == 2 && e.LeftButton == MouseButtonState.Pressed) Details_Click(sender, e);
        }
    }
}
