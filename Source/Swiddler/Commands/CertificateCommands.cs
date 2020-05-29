using Swiddler.Security;
using Swiddler.ViewModels;
using Swiddler.Views;
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Input;

namespace Swiddler.Commands
{
    public static class CertificateCommands
    {
        public static RoutedCommand NotifyCertificateChanged { get; } = new RoutedCommand(nameof(NotifyCertificateChanged), typeof(CertificateCommands));

        public static RoutedCommand ViewCertificateDetails { get; } = new RoutedUICommand("View certificate", nameof(ViewCertificateDetails), typeof(CertificateCommands));
        public static RoutedCommand CreateCertificate { get; } = new RoutedUICommand("Create certificate", nameof(CreateCertificate), typeof(CertificateCommands));
        public static RoutedCommand CreateSelfSignedCA { get; } = new RoutedUICommand("Create self-signed CA", nameof(CreateSelfSignedCA), typeof(CertificateCommands));
        public static RoutedCommand ExportPFX { get; } = new RoutedUICommand("Export PKCS#12 (.pfx)", nameof(ExportPFX), typeof(CertificateCommands));
        public static RoutedCommand ExportDER { get; } = new RoutedUICommand("Export DER (.cer)", nameof(ExportDER), typeof(CertificateCommands));
        public static RoutedCommand ExportPEM { get; } = new RoutedUICommand("Export PEM (.crt)", nameof(ExportPEM), typeof(CertificateCommands));
        public static RoutedCommand ExportRSA { get; } = new RoutedUICommand("Export RSA key (.key)", nameof(ExportRSA), typeof(CertificateCommands));
        public static RoutedCommand Delete { get; } = new RoutedUICommand("Delete", nameof(Delete), typeof(CertificateCommands));
        public static RoutedCommand DeleteAll { get; } = new RoutedUICommand("Delete all created", nameof(DeleteAll), typeof(CertificateCommands));
        public static RoutedCommand InstallCA { get; } = new RoutedUICommand("Install root to trusted store", nameof(InstallCA), typeof(CertificateCommands));
        public static RoutedCommand ExportAllDER { get; } = new RoutedUICommand("Export chain DERs (.cer)", nameof(ExportAllDER), typeof(CertificateCommands));
        public static RoutedCommand ExportAllPEM { get; } = new RoutedUICommand("Export chain PEMs (.crt)", nameof(ExportAllPEM), typeof(CertificateCommands));


        public static void HandleCertificateCommands(this CommandBindingCollection commandBindings, bool onlyCA = false, CertificateType certificateType = CertificateType.None)
        {
            commandBindings.HandleViewCertificateDetails();
            commandBindings.HandleDelete();
            commandBindings.HandleDeleteAll();
            
            if (onlyCA == false) commandBindings.HandleCreateCertificate(certificateType);
            commandBindings.HandleCreateSelfSignedCA();

            commandBindings.HandleExportPFX();
            commandBindings.HandleExportDER();
            commandBindings.HandleExportPEM();
            commandBindings.HandleExportRSA();
        }

        public static void HandleCertificateChangedNotification(this CommandBindingCollection commandBindings, Action<ExecutedRoutedEventArgs> callback)
        {
            commandBindings.Add(new CommandBinding(NotifyCertificateChanged, (s, e) => callback(e)));
        }



        static void HandleViewCertificateDetails(this CommandBindingCollection commandBindings)
        {
            commandBindings.Add(new CommandBinding(ViewCertificateDetails,
                (s, e) =>
                {
                    if (TryGetStringDataContext(e, out var thumbprint)) FindCert(thumbprint)?.ShowCertificate(e?.Source as DependencyObject);
                }, CanExecuteOnlyStringDataContext));
        }

        static void HandleDelete(this CommandBindingCollection commandBindings)
        {
            commandBindings.Add(new CommandBinding(Delete,
                (s, e) =>
                {
                    if (TryGetStringDataContext(e, out var thumbprint)) {
                        var crt = FindCert(thumbprint);
                        if (crt != null)
                        {
                            crt.DeleteCertificate();
                            NotifyCertificateChanged.Execute(null, e?.Source as IInputElement);
                        }
                    }
                }, CanExecuteOnlyStringDataContext));
        }

        static void HandleDeleteAll(this CommandBindingCollection commandBindings)
        {
            commandBindings.Add(new CommandBinding(DeleteAll, (s, e) =>
            {
                if (CertProvider.RemoveAll())
                    NotifyCertificateChanged.Execute(null, e?.Source as IInputElement);
            }));
        }

        static void CanExecuteOnlyStringDataContext(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = TryGetStringDataContext(e, out _);
        }

        static void HandleExportPFX(this CommandBindingCollection commandBindings) { HandleExport(commandBindings, ExportPFX, X509ContentType.Pfx); }
        static void HandleExportDER(this CommandBindingCollection commandBindings) { HandleExport(commandBindings, ExportDER, X509ContentType.Cert); }
        static void HandleExportRSA(this CommandBindingCollection commandBindings) { HandleExport(commandBindings, ExportRSA, "RSA"); }
        static void HandleExportPEM(this CommandBindingCollection commandBindings) { HandleExport(commandBindings, ExportPEM, "PEM"); }

        static void HandleExport(this CommandBindingCollection commandBindings, ICommand command, object contentType)
        {
            commandBindings.Add(new CommandBinding(command,
                (s, e) =>
                {
                    if (TryGetStringDataContext(e, out var thumbprint)) Export(thumbprint, contentType);
                }, CanExecuteOnlyStringDataContext));
        }

        static X509Certificate2 FindCert(string thumbprint)
        {
            return X509.MyCurrentUserX509Store.Certificates
                .Find(X509FindType.FindByThumbprint, thumbprint, false)
                .OfType<X509Certificate2>().FirstOrDefault();
        }

        static bool TryGetStringDataContext(RoutedEventArgs e, out string str)
        {
            if (((FrameworkElement)e?.Source)?.DataContext is string tmp && !string.IsNullOrEmpty(tmp))
            {
                str = tmp;
                return true;
            }

            str = null;
            return false;
        }

        static void HandleCreateCertificate(this CommandBindingCollection commandBindings, CertificateType certificateType)
        {
            commandBindings.Add(new CommandBinding(CreateCertificate, (s, e) => ExecutedCreateCertCore(s, e, () => new NewCertificate(certificateType))));
        }

        static void HandleCreateSelfSignedCA(this CommandBindingCollection commandBindings)
        {
            commandBindings.Add(new CommandBinding(CreateSelfSignedCA, (s, e) => ExecutedCreateCertCore(s, e, () => new NewCertificate(isCA: true))));
        }

        static void ExecutedCreateCertCore(object sender, ExecutedRoutedEventArgs e, Func<NewCertificate> dlgFactory)
        {
            var dlg = dlgFactory();
            dlg.Owner = Window.GetWindow((DependencyObject)e.Source);
            dlg.ShowDialog();

            NotifyCertificateChanged.Execute(dlg.Result, e?.Source as IInputElement);
        }

        static void Export(string thumbprint, object contentType)
        {
            var crt = FindCert(thumbprint);
            crt?.SaveAs(contentType);
        }
    }
}
