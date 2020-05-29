using Swiddler.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows;

namespace Swiddler.Security
{
    /// <summary>
    /// Cross-process synced certificate management.
    /// </summary>
    public class CertProvider : IDisposable
    {
        public bool MachineContext { get; set; }

        public bool IsRoot { get; set; } // this provider only issues CAs

        public string IssuerThumbprint { get; set; }

        public Encoding LogEncoding { get; set; } = new UTF8Encoding(false);
        public SyncedFileStream LogFile { get; private set; }

        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromDays(60);

        public string SubjectPrefix { get; set; } = "MITM_";
        public string SubjectPostfix { get; set; } = "_MITM";

        public bool AutoCleanup { get; set; }

        public bool AppendOnly { get; set; }

        public int RemovedCertificates { get; private set; }

        X509Certificate2 _IssuerCertificate = null;
        public X509Certificate2 IssuerCertificate
        {
            get
            {
                if (_IssuerCertificate == null && !string.IsNullOrEmpty(IssuerThumbprint))
                    _IssuerCertificate = FindCert(IssuerThumbprint) ?? throw new Exception("Issuer with private not found: " + IssuerThumbprint);
                return _IssuerCertificate;
            }
            set
            {
                _IssuerCertificate = value;
                IssuerThumbprint = value?.Thumbprint;
            }
        }


        readonly Dictionary<string, LogEntry> logs // metadata about created certificate; key is domain name
            = new Dictionary<string, LogEntry>(StringComparer.OrdinalIgnoreCase);

        readonly HashSet<string> thumbprints // all issued thumbprints
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Lazy<X509Store> LazyX509Store;
        X509Store X509store => LazyX509Store.Value;

        public CertProvider()
        {
            LazyX509Store = new Lazy<X509Store>(OpenStore);
        }

        void Init()
        {
            if (initDone) throw new Exception("Already initialized.");
            initDone = true;

            var filename = IsRoot ? "root" : IssuerThumbprint;
            if (string.IsNullOrEmpty(filename)) throw new ArgumentException("Invalid issuer thumbprint.", nameof(IssuerThumbprint));

            LogFile = new SyncedFileStream(Path.Combine(App.Current.CertLogPath, filename));

            if (AppendOnly) 
                SeekToEnd();
            else
                ReadLogFile(); // read log of already issued certs

            if (AutoCleanup)
            {
                LogFile.LastClosed += () => RemoveIssued();
            }
        }

        private readonly object initSync = new object();
        private bool initDone = false;
        void EnsureInit()
        {
            if (initDone == false) lock(initSync) if (initDone == false) Init();
        }

        X509Store OpenStore()
        {
            var store = new X509Store(StoreName.My, MachineContext ? StoreLocation.LocalMachine : StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            return store;
        }

        public X509Certificate2 GetCertificate(string hostname)
        {
            if (AppendOnly) throw new InvalidOperationException();

            EnsureInit();

            string domain = GetDomainBase(hostname);
            X509Certificate2 cert = FindCertForDomain(domain);

            if (cert == null)
            {
                LogFile.Lock();
                try
                {
                    // same logfile can be shared between multiple processes
                    ReadLogFile(shouldLock: false);

                    if (null == (cert = FindCertForDomain(domain)))
                    {
                        cert = CreateCertificate(domain, shouldLock: false);
                    }
                    else
                    {
                        if (cert.NotBefore > DateTime.Now || cert.NotAfter < DateTime.Now) // expired cert 
                            cert = CreateCertificate(domain, shouldLock: false); // refresh
                    }
                }
                finally
                {
                    LogFile.Unlock();
                }
            }

            return cert;
        }

        void SeekToEnd()
        {
            LogFile.Lock();
            try
            {
                LogFile.Seek(0, SeekOrigin.End);
            }
            finally
            {
                LogFile.Unlock();
            }
        }

        X509Certificate2 FindCertForDomain(string domain)
        {
            EnsureInit();

            if (logs.TryGetValue(domain, out var log))
            {
                if (log.CachedCertificate == null)
                    log.CachedCertificate = FindCert(log.Thumbprint);

                return log.CachedCertificate;
            }
            return null;
        }

        string GetDomainBase(string hostname)
        {
            var domParts = hostname.Split('.');
            if (domParts.Length > 2)
                hostname = string.Join(".", domParts.Skip(1));
            return hostname.ToLower();
        }

        X509Certificate2 CreateCertificate(string domain, bool shouldLock = true)
        {
            if (IsRoot) throw new InvalidOperationException();

            var cert = X509.CreateCertificate($"{SubjectPrefix ?? ""}{domain}{SubjectPostfix ?? ""}", domain, DateTime.UtcNow + DefaultExpiration, IssuerCertificate, MachineContext);
            var log = new LogEntry(cert) { Domain = domain };
            AppendLog(log, shouldLock);

            return cert;
        }

        /*
        public X509Certificate2 CreateSelfSignedCA(string subjectName)
        {
            var cert = X509.CreateSelfSignedCA(subjectName, DateTime.UtcNow + DefaultExpiration, MachineContext);
            var log = new LogEntry(cert) { Domain = subjectName, ExpirationUtc = cert.NotAfter, Thumbprint = cert.Thumbprint, CachedCertificate = cert };
            AppendLog(log);

            return cert;
        }
        */

        void ReadLogFile(bool shouldLock = true)
        {
            EnsureInit();

            if (AppendOnly)
            {
                SeekToEnd();
                return;
            }

            if (shouldLock) LogFile.Lock();
            try
            {
                using (var reader = new BinaryReader(LogFile, LogEncoding, leaveOpen: true))
                {
                    while (LogFile.DataAvailable)
                    {
                        var log = new LogEntry()
                        {
                            Domain = reader.ReadString(),
                            Thumbprint = reader.ReadString(),
                            ExpirationUtc = new DateTime(reader.ReadInt64(), DateTimeKind.Utc),
                        };

                        logs[log.Domain] = log;
                        thumbprints.Add(log.Thumbprint);
                    }
                }
            }
            finally
            {
                if (shouldLock) LogFile.Unlock();
            }
        }

        void AppendLog(LogEntry log, bool shouldLock = true)
        {
            EnsureInit();

            if (shouldLock)
            {
                LogFile.Lock();
                ReadLogFile(shouldLock: false); // roll to the tail
            }
            try
            {
                using (var writer = new BinaryWriter(LogFile, LogEncoding, leaveOpen: true))
                {
                    writer.Write(log.Domain);
                    writer.Write(log.Thumbprint);
                    writer.Write(log.ExpirationUtc.Ticks);

                    if (!string.IsNullOrEmpty(log.Domain))
                        logs[log.Domain] = log;

                    thumbprints.Add(log.Thumbprint);
                }
            }
            finally
            {
                if (shouldLock) LogFile.Unlock();
            }
        }

        public void Append(X509Certificate2 cert)
        {
            AppendLog(new LogEntry(cert));
        }

        X509Certificate2 FindCert(string thumbprint)
        {
            EnsureInit();

            return X509store.Certificates
                .Find(X509FindType.FindByThumbprint, thumbprint, false)
                .OfType<X509Certificate2>()
                .Where(c => c.HasPrivateKey).SingleOrDefault();
        }

        /// <summary>
        /// Delete all certificates from cert store created by this provider
        /// </summary>
        public void RemoveIssued(bool cleanTrustedRootStore = false)
        {
            EnsureInit();

            RemoveAll(StoreName.My);

            if (cleanTrustedRootStore)
            {
                RemoveAll(StoreName.Root);
                RemoveAll(StoreName.CertificateAuthority); // intermediate
            }
        }

        /// <summary>
        /// Delete all certificates created with application
        /// </summary>
        public static bool RemoveAll()
        {
            const string caption = "Delete All Certificates";
            var files = new DirectoryInfo(App.Current.CertLogPath).GetFiles();

            if (files.Length == 0)
            {
                MessageBox.Show("There is no certificate created by this application.", caption, MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
            }

            if (MessageBox.Show("Do you want to delete all certificates issued with this application?", caption, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                return false;

            int totalRemoved = 0;
            bool multiStore = false;
            foreach (var log in files)
            {
                var isRoot = log.Name.Equals("root", StringComparison.OrdinalIgnoreCase);

                using (var prov = new CertProvider())
                {
                    prov.IsRoot = isRoot;
                    if (!isRoot) prov.IssuerThumbprint = log.Name;
                    prov.RemoveIssued(isRoot);
                    totalRemoved += prov.RemovedCertificates;
                    multiStore |= isRoot;
                }

                try
                {
                    log.Delete();
                }
                catch { }
            }

            string msg = null;
            var ss = totalRemoved == 1 ? "" : "s";
            if (totalRemoved <= 1 || !multiStore)
                msg = $"{totalRemoved} certificate{ss} removed.";
            else
                msg = totalRemoved + " certificates removed from multiple stores.";

            MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Information);

            return totalRemoved > 0;
        }

        void RemoveAll(StoreName storeName)
        {
            X509Store store = null;
            try
            {
                store = new X509Store(storeName, MachineContext ? StoreLocation.LocalMachine : StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                var crtToDelete = store.Certificates.OfType<X509Certificate2>().Where(crt => thumbprints.Contains(crt.Thumbprint)).ToArray();
                store.RemoveRange(new X509Certificate2Collection(crtToDelete));
                RemovedCertificates += crtToDelete.Length;
                store.Close();
            }
            finally
            {
                store?.Close();
            }
        }

        public void Dispose()
        {
            if (LazyX509Store?.IsValueCreated == true)
                LazyX509Store.Value.Close();

            LazyX509Store = null;

            LogFile?.Dispose();
            LogFile = null;
        }

        private class LogEntry
        {
            public string Domain { get; set; }
            public string Thumbprint { get; set; }
            public DateTime ExpirationUtc { get; set; }

            public X509Certificate2 CachedCertificate { get; set; }

            public LogEntry() { }
            public LogEntry(X509Certificate2 cert)
            {
                Domain = "";
                ExpirationUtc = cert.NotAfter.ToUniversalTime();
                Thumbprint = cert.Thumbprint;
                CachedCertificate = cert;
            }
        }
    }
}
