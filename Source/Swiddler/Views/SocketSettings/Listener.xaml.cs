using Swiddler.Commands;
using Swiddler.Utils;
using Swiddler.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace Swiddler.Views.SocketSettings
{
    /// <summary>
    /// Interaction logic for Listener.xaml
    /// </summary>
    public partial class Listener : UserControl
    {
        public Listener()
        {
            InitializeComponent();

            btnServerCertMenu.CommandBindings.HandleCertificateCommands(certificateType: CertificateType.ServerCertificate);
            btnCAMenu.CommandBindings.HandleCertificateCommands(certificateType: CertificateType.ServerCertificate);
            CommandBindings.HandleCertificateChangedNotification(CertificateChangedNotification);
        }

        void CertificateChangedNotification(ExecutedRoutedEventArgs e)
        {
            cmbServerCert.ReloadCertificateItems();
            cmbCA.ReloadCertificateItems();

            if (e.Parameter is Certificate crt)
            {
                if (e.OriginalSource == btnServerCertMenu && crt.SignByCertificateAuthority && crt.CertificateType == CertificateType.ServerCertificate)
                    cmbServerCert.SelectedValue = crt.Value.Thumbprint;

                if (e.OriginalSource == btnCAMenu && !crt.SignByCertificateAuthority)
                    cmbCA.SelectedValue = crt.Value.Thumbprint;
            }
        }
    }
}
