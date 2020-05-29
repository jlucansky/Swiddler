using Swiddler.Commands;
using Swiddler.Common;
using Swiddler.Security;
using Swiddler.Utils;
using Swiddler.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace Swiddler.Views
{
    /// <summary>
    /// Interaction logic for NewCertificate.xaml
    /// </summary>
    public partial class NewCertificate : Window
    {
        public Certificate Result { get; private set; }

        private readonly Certificate model;

        public NewCertificate(CertificateType certificateType) : this(false, certificateType) { }

        public NewCertificate(bool isCA, CertificateType certificateType = CertificateType.None)
        {
            model = new Certificate() { SignByCertificateAuthority = !isCA, CertificateType = certificateType };

            if (isCA) model.CommonName = "EVIL_CORP_CA";

            DataContext = model;
            InitializeComponent();

            if (isCA)
                Title = "Create self-signed certificate authority";

            btnCAMenu.CommandBindings.HandleCertificateCommands(onlyCA: true);
            CommandBindings.HandleCertificateChangedNotification(e =>
            {
                cmbCA.ReloadCertificateItems();
                if (e.Parameter is Certificate crt) model.CertificateAuthority = crt.Value.Thumbprint;
            });

            txtCommonName.Focus();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (Owner is NewCertificate owner)
            {
                Left = owner.Left + 16;
                Top = owner.Top + 16;
            }

            this.RemoveIcon();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                model.Validate();

                Mouse.OverrideCursor = Cursors.Wait;

                var crt = X509.CreateCertificate(model);

                using (var certProvider = new CertProvider() { AppendOnly = true })
                {
                    certProvider.IsRoot = !model.SignByCertificateAuthority;
                    if (model.SignByCertificateAuthority)
                        certProvider.IssuerThumbprint = model.CertificateAuthority;
                    certProvider.Append(crt);
                }

                model.Value = crt;
                Result = model;
                Close();
            }
            catch (Exception ex)
            {
                if (ex is ValueException vex)
                {
                    MessageBox.Show(vex.Message, "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
                    this.Focus(vex.PropertyName);
                }
                else
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
    }
}
