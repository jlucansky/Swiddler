using Swiddler.Commands;
using Swiddler.Utils;
using Swiddler.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace Swiddler.Views.SocketSettings
{
    /// <summary>
    /// Interaction logic for Client.xaml
    /// </summary>
    public partial class Client : UserControl
    {
        public Client()
        {
            InitializeComponent();

            btnClientCertMenu.CommandBindings.HandleCertificateCommands(certificateType: CertificateType.ClientCertificate);
            CommandBindings.HandleCertificateChangedNotification(CertificateChangedNotification);
        }

        void CertificateChangedNotification(ExecutedRoutedEventArgs e)
        {
            cmbClientCert.ReloadCertificateItems();
            if (e.Parameter is Certificate crt && crt.SignByCertificateAuthority && crt.CertificateType == CertificateType.ClientCertificate)
                cmbClientCert.SelectedValue = crt.Value;
        }
    }
}
