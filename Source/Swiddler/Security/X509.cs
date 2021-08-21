using CERTENROLLLib;
using Microsoft.Win32;
using Swiddler.Utils;
using Swiddler.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Swiddler.Security
{
    public static class X509
    {
        // https://github.com/asafga-gsr-it/CertIntegration/blob/master/CertificateAdmin/CertificateAdmin/obj/Release/Package/PackageTmp/Certificate.cs
        // https://www.sysadmins.lv/blog-en/introducing-to-certificate-enrollment-apis-part-2-creating-offline-requests.aspx
        // https://stackoverflow.com/questions/33001983/issues-compiling-in-windows-10

        /*
        public static X509Certificate2 CreateSelfSignedCA(string subjectName, DateTime notAfterUtc, bool machineContext)
        {
            // create DN for subject and issuer
            var dn = new CX500DistinguishedName();
            dn.Encode("CN=" + EscapeDNComponent(subjectName), X500NameFlags.XCN_CERT_NAME_STR_NONE);

            // create a new private key for the certificate
            CX509PrivateKey privateKey = new CX509PrivateKey();
            privateKey.ProviderName = "Microsoft Base Cryptographic Provider v1.0";
            privateKey.MachineContext = machineContext;
            privateKey.Length = 2048;
            privateKey.KeySpec = X509KeySpec.XCN_AT_SIGNATURE; // use is not limited
            privateKey.ExportPolicy = X509PrivateKeyExportFlags.XCN_NCRYPT_ALLOW_PLAINTEXT_EXPORT_FLAG;
            privateKey.Create();

            var hashobj = new CObjectId();
            hashobj.InitializeFromAlgorithmName(ObjectIdGroupId.XCN_CRYPT_HASH_ALG_OID_GROUP_ID,
                ObjectIdPublicKeyFlags.XCN_CRYPT_OID_INFO_PUBKEY_ANY,
                AlgorithmFlags.AlgorithmFlagsNone, "SHA256"); // https://docs.microsoft.com/en-us/windows/win32/seccng/cng-algorithm-identifiers

            CX509ExtensionKeyUsage keyUsage = new CX509ExtensionKeyUsage();
            keyUsage.InitializeEncode(
                CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_DIGITAL_SIGNATURE_KEY_USAGE |
                CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_KEY_CERT_SIGN_KEY_USAGE |
                CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_CRL_SIGN_KEY_USAGE |
                CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_OFFLINE_CRL_SIGN_KEY_USAGE);

            CX509ExtensionBasicConstraints bc = new CX509ExtensionBasicConstraints();
            bc.InitializeEncode(true, -1); // None
            bc.Critical = true;

            // add extended key usage if you want - look at MSDN for a list of possible OIDs
            var oid = new CObjectId();
            oid.InitializeFromValue("1.3.6.1.5.5.7.3.1"); // Server Authentication
            var oidlist = new CObjectIds();
            oidlist.Add(oid);
            var eku = new CX509ExtensionEnhancedKeyUsage();
            eku.InitializeEncode(oidlist);

            // Create the self signing request
            var cert = new CX509CertificateRequestCertificate();

            cert.InitializeFromPrivateKey(machineContext ? X509CertificateEnrollmentContext.ContextMachine: X509CertificateEnrollmentContext.ContextUser, privateKey, "");
            cert.Subject = cert.Issuer = dn; // the issuer and the subject are the same
            cert.NotBefore = DateTime.UtcNow.AddDays(-1);
            cert.NotAfter = notAfterUtc;
            cert.X509Extensions.Add((CX509Extension)keyUsage);
            cert.X509Extensions.Add((CX509Extension)eku); // add the EKU
            cert.X509Extensions.Add((CX509Extension)bc);
            cert.HashAlgorithm = hashobj; // Specify the hashing algorithm
            cert.Encode(); // encode the certificate

            // Do the final enrollment process
            var enroll = new CX509Enrollment();
            enroll.InitializeFromRequest(cert); // load the certificate
            enroll.CertificateFriendlyName = subjectName; // Optional: add a friendly name
            string csr = enroll.CreateRequest(); // Output the request in base64

            // install to MY store
            enroll.InstallResponse(InstallResponseRestrictionFlags.AllowUntrustedCertificate, csr, EncodingType.XCN_CRYPT_STRING_BASE64, ""); // no password

            // output a base64 encoded PKCS#12 so we can import it back to the .Net security classes
            var base64encoded = enroll.CreatePFX("", // no password, this is for internal consumption
                PFXExportOptions.PFXExportChainWithRoot);

            // instantiate the target class with the PKCS#12 data (and the empty password)
            var x509Certificate2 = new X509Certificate2(
                System.Convert.FromBase64String(base64encoded), "",
                // mark the private key as exportable (this is usually what you want to do)
                X509KeyStorageFlags.Exportable
            );

            X509Store rootStore = null;
            try
            {
                rootStore = new X509Store(StoreName.Root, machineContext ? StoreLocation.LocalMachine : StoreLocation.CurrentUser);
                rootStore.Open(OpenFlags.ReadWrite);
                // install to CA store
                var crtPub = new X509Certificate2(x509Certificate2) { PrivateKey = null };
                rootStore.Add(crtPub);
                crtPub.Reset();
            }
            catch
            {
                // ignore when adding to trust root failed
            }
            finally
            {
                rootStore?.Close();
            }

            return x509Certificate2;
        }
        */

        public static X509Certificate2 CreateCertificate(string subjectName, string hostname, DateTime notAfterUtc, X509Certificate issuer, bool machineContext)
        {
            CSignerCertificate signerCertificate = new CSignerCertificate();
            signerCertificate.Initialize(false, X509PrivateKeyVerify.VerifyNone, EncodingType.XCN_CRYPT_STRING_HEX, issuer.GetRawCertDataString());

            // create DN for subject and issuer
            var dn = new CX500DistinguishedName();
            dn.Encode("CN=" + EscapeDNComponent(subjectName), X500NameFlags.XCN_CERT_NAME_STR_NONE);

            // create a new private key for the certificate
            IX509PrivateKey privateKey = (IX509PrivateKey)Activator.CreateInstance(Type.GetTypeFromProgID("X509Enrollment.CX509PrivateKey", throwOnError: true));
            
            privateKey.ProviderName = "Microsoft Base Cryptographic Provider v1.0";
            privateKey.MachineContext = machineContext;
            privateKey.Length = 2048;
            privateKey.KeySpec = X509KeySpec.XCN_AT_SIGNATURE; // use is not limited
            privateKey.ExportPolicy = X509PrivateKeyExportFlags.XCN_NCRYPT_ALLOW_PLAINTEXT_EXPORT_FLAG;
            privateKey.Create();

            var hashobj = new CObjectId();
            hashobj.InitializeFromAlgorithmName(ObjectIdGroupId.XCN_CRYPT_HASH_ALG_OID_GROUP_ID,
                ObjectIdPublicKeyFlags.XCN_CRYPT_OID_INFO_PUBKEY_ANY,
                AlgorithmFlags.AlgorithmFlagsNone, "SHA256");

            CX509ExtensionKeyUsage keyUsage = new CX509ExtensionKeyUsage();
            keyUsage.InitializeEncode(
                CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_DATA_ENCIPHERMENT_KEY_USAGE |
                CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_KEY_ENCIPHERMENT_KEY_USAGE |
                CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_DIGITAL_SIGNATURE_KEY_USAGE);

            CX509ExtensionBasicConstraints bc = new CX509ExtensionBasicConstraints();
            bc.InitializeEncode(false, 0);
            bc.Critical = false;

            // SAN
            CX509ExtensionAlternativeNames san = null;
            if (!string.IsNullOrEmpty(hostname))
            {
                CAlternativeNames ians;
                if (IPAddress.TryParse(hostname, out var ip))
                {
                    var ian = new CAlternativeName();
                    ian.InitializeFromRawData(AlternativeNameType.XCN_CERT_ALT_NAME_IP_ADDRESS, EncodingType.XCN_CRYPT_STRING_BASE64, Convert.ToBase64String(ip.GetAddressBytes()));
                    ians = new CAlternativeNames { ian };
                }
                else
                {
                    var ian = new CAlternativeName();
                    ian.InitializeFromString(AlternativeNameType.XCN_CERT_ALT_NAME_DNS_NAME, hostname);
                    var ianStar = new CAlternativeName();
                    ianStar.InitializeFromString(AlternativeNameType.XCN_CERT_ALT_NAME_DNS_NAME, "*." + hostname); // wildcard
                    ians = new CAlternativeNames { ian, ianStar };
                }
                san = new CX509ExtensionAlternativeNames();
                san.InitializeEncode(ians);
            }

            // add extended key usage if you want - look at MSDN for a list of possible OIDs
            var oid = new CObjectId();
            oid.InitializeFromValue("1.3.6.1.5.5.7.3.1"); // SSL server
            var oidlist = new CObjectIds();
            oidlist.Add(oid);
            var eku = new CX509ExtensionEnhancedKeyUsage();
            eku.InitializeEncode(oidlist);

            var dnIssuer = new CX500DistinguishedName();
            dnIssuer.Encode(issuer.Subject, X500NameFlags.XCN_CERT_NAME_STR_NONE);

            // Create the self signing request
            var cert = (IX509CertificateRequestCertificate)Activator.CreateInstance(Type.GetTypeFromProgID("X509Enrollment.CX509CertificateRequestCertificate", throwOnError: true));

            cert.InitializeFromPrivateKey(machineContext ? X509CertificateEnrollmentContext.ContextMachine : X509CertificateEnrollmentContext.ContextUser, privateKey, "");
            cert.Subject = dn;
            cert.Issuer = dnIssuer;
            cert.SignerCertificate = signerCertificate;
            cert.NotBefore = DateTime.UtcNow.AddDays(-1);

            cert.NotAfter = notAfterUtc;
            cert.X509Extensions.Add((CX509Extension)keyUsage);
            cert.X509Extensions.Add((CX509Extension)eku);  // EnhancedKeyUsage
            cert.X509Extensions.Add((CX509Extension)bc);   // ExtensionBasicConstraints
            if (san != null) cert.X509Extensions.Add((CX509Extension)san);  // SAN

            cert.HashAlgorithm = hashobj; // Specify the hashing algorithm
            cert.Encode(); // encode the certificate

            // Do the final enrollment process
            var enroll = new CX509Enrollment();
            enroll.InitializeFromRequest(cert); // load the certificate
            //enroll.CertificateFriendlyName = subjectName; // Optional: add a friendly name
            string csr = enroll.CreateRequest(); // Output the request in base64
            // and install it back as the response

            enroll.InstallResponse(InstallResponseRestrictionFlags.AllowUntrustedCertificate, csr, EncodingType.XCN_CRYPT_STRING_BASE64, ""); // no password

            // output a base64 encoded PKCS#12 so we can import it back to the .Net security classes
            var base64encoded = enroll.CreatePFX("", // no password, this is for internal consumption
                PFXExportOptions.PFXExportChainWithRoot);

            // instantiate the target class with the PKCS#12 data (and the empty password)
            var x509Certificate2 = new X509Certificate2(
                Convert.FromBase64String(base64encoded), "",
                X509KeyStorageFlags.Exportable); // mark the private key as exportable (this is usually what you want to do)

            return x509Certificate2;
        }

        public static X509Certificate2 CreateCertificate(Certificate crt)
        {
            bool isCA = !crt.SignByCertificateAuthority;

            // create DN for subject and issuer
            var dn = new CX500DistinguishedName();
            dn.Encode(GetEncodedDistinguishedName(crt), X500NameFlags.XCN_CERT_NAME_STR_NONE);

            // create a new private key for the certificate
            IX509PrivateKey privateKey = (IX509PrivateKey)Activator.CreateInstance(Type.GetTypeFromProgID("X509Enrollment.CX509PrivateKey", throwOnError: true));

            privateKey.ProviderName = "Microsoft Base Cryptographic Provider v1.0";
            privateKey.MachineContext = crt.MachineContext;
            privateKey.Length = crt.KeyLength;
            privateKey.KeySpec = X509KeySpec.XCN_AT_SIGNATURE; // use is not limited
            privateKey.ExportPolicy = X509PrivateKeyExportFlags.XCN_NCRYPT_ALLOW_PLAINTEXT_EXPORT_FLAG;

            privateKey.Create();

            var hashobj = new CObjectId();
            hashobj.InitializeFromAlgorithmName(ObjectIdGroupId.XCN_CRYPT_HASH_ALG_OID_GROUP_ID,
                ObjectIdPublicKeyFlags.XCN_CRYPT_OID_INFO_PUBKEY_ANY,
                AlgorithmFlags.AlgorithmFlagsNone, crt.DigestAlgorithm);

            CERTENROLLLib.X509KeyUsageFlags x509KeyUsageFlags;
            CX509ExtensionBasicConstraints bc = new CX509ExtensionBasicConstraints();
            if (isCA)
            {
                x509KeyUsageFlags =
                    CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_DIGITAL_SIGNATURE_KEY_USAGE |
                    CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_KEY_CERT_SIGN_KEY_USAGE |
                    CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_CRL_SIGN_KEY_USAGE |
                    CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_OFFLINE_CRL_SIGN_KEY_USAGE;

                bc.InitializeEncode(true, -1);
                bc.Critical = true;
            }
            else
            {
                x509KeyUsageFlags =
                    CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_KEY_ENCIPHERMENT_KEY_USAGE |
                    CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_DIGITAL_SIGNATURE_KEY_USAGE;

                if (crt.CertificateType == CertificateType.ClientCertificate)
                    x509KeyUsageFlags |= CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_NON_REPUDIATION_KEY_USAGE;
                if (crt.CertificateType == CertificateType.ServerCertificate)
                    x509KeyUsageFlags |= CERTENROLLLib.X509KeyUsageFlags.XCN_CERT_NON_REPUDIATION_KEY_USAGE;

                bc.InitializeEncode(false, -1);
                bc.Critical = false;
            }

            CX509ExtensionKeyUsage keyUsage = new CX509ExtensionKeyUsage();
            keyUsage.InitializeEncode(x509KeyUsageFlags);
            keyUsage.Critical = false;

            // SAN
            var canList = new List<CAlternativeName>();
            foreach (var sanItem in crt.SANList)
            {
                if (!string.IsNullOrWhiteSpace(sanItem.Value))
                {
                    var can = new CAlternativeName();
                    switch (sanItem.Type)
                    {
                        case Certificate.SANType.DNS:
                            can.InitializeFromString(AlternativeNameType.XCN_CERT_ALT_NAME_DNS_NAME, sanItem.Value);
                            break;
                        case Certificate.SANType.IP:
                            can.InitializeFromRawData(AlternativeNameType.XCN_CERT_ALT_NAME_IP_ADDRESS, EncodingType.XCN_CRYPT_STRING_BASE64, 
                                Convert.ToBase64String(IPAddress.Parse(sanItem.Value).GetAddressBytes()));
                            break;
                        case Certificate.SANType.URI:
                            can.InitializeFromString(AlternativeNameType.XCN_CERT_ALT_NAME_URL, sanItem.Value);
                            break;
                        case Certificate.SANType.email:
                            can.InitializeFromString(AlternativeNameType.XCN_CERT_ALT_NAME_RFC822_NAME, sanItem.Value);
                            break;
                    }
                    canList.Add(can);
                }
            }

            CX509ExtensionAlternativeNames san = null;
            if (canList.Any())
            {
                san = new CX509ExtensionAlternativeNames();
                var cans = new CAlternativeNames();
                foreach (var item in canList) cans.Add(item);
                san.InitializeEncode(cans);
            }

            CX509ExtensionEnhancedKeyUsage eku = null;
            if (crt.CertificateType != CertificateType.None)
            {
                const string XCN_OID_PKIX_KP_SERVER_AUTH = "1.3.6.1.5.5.7.3.1";
                const string XCN_OID_PKIX_KP_CLIENT_AUTH = "1.3.6.1.5.5.7.3.2";

                var oid = new CObjectId();
                if (crt.CertificateType == CertificateType.ServerCertificate)
                    oid.InitializeFromValue(XCN_OID_PKIX_KP_SERVER_AUTH);
                if (crt.CertificateType == CertificateType.ClientCertificate)
                    oid.InitializeFromValue(XCN_OID_PKIX_KP_CLIENT_AUTH);
                
                var oidlist = new CObjectIds();
                oidlist.Add(oid);
                eku = new CX509ExtensionEnhancedKeyUsage();
                eku.InitializeEncode(oidlist);
            }

            // Create the self signing request
            var cereq = (IX509CertificateRequestCertificate)Activator.CreateInstance(Type.GetTypeFromProgID("X509Enrollment.CX509CertificateRequestCertificate", throwOnError: true));

            cereq.InitializeFromPrivateKey(crt.MachineContext ? X509CertificateEnrollmentContext.ContextMachine : X509CertificateEnrollmentContext.ContextUser, privateKey, "");

            cereq.Subject = dn;
            cereq.Issuer = dn;
            cereq.NotBefore = DateTime.UtcNow.AddDays(-1);
            cereq.NotAfter = DateTime.UtcNow.AddDays(crt.Lifetime.Value);

            if (crt.SignByCertificateAuthority)
            {
                var issuer = MyCurrentUserX509Store.Certificates
                    .Find(X509FindType.FindByThumbprint, crt.CertificateAuthority, false)
                    .OfType<X509Certificate2>()
                    .Where(c => c.HasPrivateKey).FirstOrDefault() ?? throw new Exception("Issuer not found: " + crt.CertificateAuthority);

                cereq.SignerCertificate = new CSignerCertificate();
                cereq.SignerCertificate.Initialize(false, X509PrivateKeyVerify.VerifyNone, EncodingType.XCN_CRYPT_STRING_HEX, issuer.GetRawCertDataString());

                cereq.Issuer = new CX500DistinguishedName();
                cereq.Issuer.Encode(issuer.Subject, X500NameFlags.XCN_CERT_NAME_STR_NONE);
            }

            cereq.X509Extensions.Add((CX509Extension)keyUsage);
            if (eku != null) cereq.X509Extensions.Add((CX509Extension)eku);  // EnhancedKeyUsage
            if (bc != null) cereq.X509Extensions.Add((CX509Extension)bc);    // ExtensionBasicConstraints
            if (san != null) cereq.X509Extensions.Add((CX509Extension)san);  // SAN

            cereq.HashAlgorithm = hashobj; // Specify the hashing algorithm
            cereq.Encode(); // encode the certificate

            // Do the final enrollment process
            var enroll = new CX509Enrollment();
            enroll.InitializeFromRequest(cereq); // load the certificate
            //enroll.CertificateFriendlyName = subjectName; // Optional: add a friendly name
            string csr = enroll.CreateRequest(); // Output the request in base64
            // and install it back as the response

            enroll.InstallResponse(InstallResponseRestrictionFlags.AllowUntrustedCertificate, csr, EncodingType.XCN_CRYPT_STRING_BASE64, ""); // no password

            // output a base64 encoded PKCS#12 so we can import it back to the .Net security classes
            var base64encoded = enroll.CreatePFX("", // no password, this is for internal consumption
                PFXExportOptions.PFXExportChainWithRoot);

            // instantiate the target class with the PKCS#12 data (and the empty password)
            var x509Certificate2 = new X509Certificate2(
                Convert.FromBase64String(base64encoded), "",
                X509KeyStorageFlags.Exportable); // mark the private key as exportable (this is usually what you want to do)

            if (isCA)
            {
                X509Store rootStore = null;
                try
                {
                    rootStore = new X509Store(StoreName.Root, crt.MachineContext ? StoreLocation.LocalMachine : StoreLocation.CurrentUser);
                    rootStore.Open(OpenFlags.ReadWrite);
                    // install to CA store
                    var crtPub = new X509Certificate2(x509Certificate2) { PrivateKey = null };
                    rootStore.Add(crtPub);
                    crtPub.Reset();
                }
                catch
                {
                    // ignore when adding to trust root failed
                }
                finally
                {
                    rootStore?.Close();
                }
            }

            crt.Value = x509Certificate2;

            return x509Certificate2;
        }

        static string GetEncodedDistinguishedName(Certificate crt)
        {
            var dict = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(crt.CommonName))
                dict["CN"] = crt.CommonName;
            if (!string.IsNullOrWhiteSpace(crt.Organization))
                dict["O"] = crt.Organization;
            if (!string.IsNullOrWhiteSpace(crt.OrganizationUnit))
                dict["OU"] = crt.OrganizationUnit;
            if (!string.IsNullOrWhiteSpace(crt.Locality))
                dict["L"] = crt.Locality;
            if (!string.IsNullOrWhiteSpace(crt.State))
                dict["S"] = crt.State;
            if (!string.IsNullOrWhiteSpace(crt.Country))
                dict["C"] = crt.Country;

            return string.Join(", ", dict.Select(x => $"{x.Key}={EscapeDNComponent(x.Value)}"));
        }

        static readonly char[] dangerousDDChars = " ,\\#+<>;\"=\n".ToArray();

        static string EscapeDNComponent(string value)
        {
            value = value.Trim();
            if (value.IndexOfAny(dangerousDDChars) != -1)
                value = "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        /*
        // get readable name
        public static string GetCertDisplayName(this X509Certificate2 cert)
        {
            if (!string.IsNullOrWhiteSpace(cert.FriendlyName))
                return cert.FriendlyName;

            var dict = GetDNComponents(cert.Subject);

            if (dict.Contains("CN"))
            {
                var cn = dict["CN"].First()?.Trim();
                if (!string.IsNullOrEmpty(cn))
                    return cn;
            }

            return cert.Thumbprint;
        }*/

        public static string GetCertDisplayName(this X509Certificate2 cert)
        {
            var name = cert.GetNameInfo(X509NameType.SimpleName, false);
            if (string.IsNullOrEmpty(name)) return cert.Thumbprint;
            return name;
        }

        /*
        static ILookup<string, string> GetDNComponents(string distinguishedName)
        {
            const string InvalidDNFormat = "Invalid DN Format";

            Debug.Assert(distinguishedName != null, "distinguishedName is null");

            // First split by ','
            string[] components = Split(distinguishedName, ',');
            var dnComponents = new KeyValuePair<string, string>[components.GetLength(0)];

            for (int i = 0; i < components.GetLength(0); i++)
            {
                // split each component by '='
                string[] subComponents = Split(components[i], '=');
                if (subComponents.GetLength(0) != 2)
                {
                    throw new ArgumentException(InvalidDNFormat, nameof(distinguishedName));
                }

                var key = subComponents[0].Trim();
                if (key.Length == 0)
                {
                    throw new ArgumentException(InvalidDNFormat, nameof(distinguishedName));
                }

                var value = subComponents[1].Trim();
                if (value.Length == 0)
                {
                    throw new ArgumentException(InvalidDNFormat, nameof(distinguishedName));
                }

                if (value.StartsWith("\"") && value.EndsWith("\""))
                    value = value.Substring(1, value.Length - 2).Replace("\"\"", "\"");

                dnComponents[i] = new KeyValuePair<string, string>(key, value);
            }

            return dnComponents.ToLookup(x => x.Key, x => x.Value);
        }

        static string[] Split(string distinguishedName, char delim)
        {
            const string InvalidDNFormat = "Invalid DN Format";

            bool inQuotedString = false;
            char curr;
            char quote = '\"';
            char escape = '\\';
            int nextTokenStart = 0;
            ArrayList resultList = new ArrayList();
            string[] results;

            // get the actual tokens
            for (int i = 0; i < distinguishedName.Length; i++)
            {
                curr = distinguishedName[i];

                if (curr == quote)
                {
                    inQuotedString = !inQuotedString;
                }
                else if (curr == escape)
                {
                    // skip the next character (if one exists)
                    if (i < (distinguishedName.Length - 1))
                    {
                        i++;
                    }
                }
                else if ((!inQuotedString) && (curr == delim))
                {
                    // we found an unqoted character that matches the delimiter
                    // split it at the delimiter (add the tokrn that ends at this delimiter)
                    resultList.Add(distinguishedName.Substring(nextTokenStart, i - nextTokenStart));
                    nextTokenStart = i + 1;
                }

                if (i == (distinguishedName.Length - 1))
                {
                    // we've reached the end 

                    // if we are still in quoted string, the format is invalid
                    if (inQuotedString)
                    {
                        throw new ArgumentException(InvalidDNFormat, nameof(distinguishedName));
                    }

                    // we need to end the last token
                    resultList.Add(distinguishedName.Substring(nextTokenStart, i - nextTokenStart + 1));
                }
            }

            results = new string[resultList.Count];
            for (int i = 0; i < resultList.Count; i++)
            {
                results[i] = (string)resultList[i];
            }

            return results;
        }
        */

        public static string ExportRSA(RSACryptoServiceProvider rsa)
        {
            if (rsa.PublicOnly) throw new ArgumentException("CSP does not contain a private key", nameof(rsa));

            var parameters = rsa.ExportParameters(true);

            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                writer.Write((byte)0x30); // SEQUENCE
                using (var innerStream = new MemoryStream())
                {
                    var innerWriter = new BinaryWriter(innerStream);
                    EncodeIntegerBE(innerWriter, new byte[] { 0x00 }); // Version
                    EncodeIntegerBE(innerWriter, parameters.Modulus);
                    EncodeIntegerBE(innerWriter, parameters.Exponent);
                    EncodeIntegerBE(innerWriter, parameters.D);
                    EncodeIntegerBE(innerWriter, parameters.P);
                    EncodeIntegerBE(innerWriter, parameters.Q);
                    EncodeIntegerBE(innerWriter, parameters.DP);
                    EncodeIntegerBE(innerWriter, parameters.DQ);
                    EncodeIntegerBE(innerWriter, parameters.InverseQ);
                    
                    var length = (int)innerStream.Length;
                    EncodeLength(writer, length);
                    writer.Write(innerStream.GetBuffer(), 0, length);
                }

                return FormatPEM(stream.GetBuffer(), "RSA PRIVATE KEY");
            }
        }

        public static string FormatPEM(byte[] data, string name)
        {
            using (var outputStream = new StringWriter())
            {
                var base64 = Convert.ToBase64String(data, 0, data.Length).ToCharArray();
                outputStream.WriteLine($"-----BEGIN {name}-----");

                for (var i = 0; i < base64.Length; i += 64)
                    outputStream.WriteLine(base64, i, Math.Min(64, base64.Length - i));

                outputStream.WriteLine($"-----END {name}-----");
                return outputStream.ToString();
            }
        }

        static void EncodeLength(BinaryWriter stream, int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException("length", "Length must be non-negative");
            if (length < 0x80)
            {
                // Short form
                stream.Write((byte)length);
            }
            else
            {
                // Long form
                var temp = length;
                var bytesRequired = 0;
                while (temp > 0)
                {
                    temp >>= 8;
                    bytesRequired++;
                }
                stream.Write((byte)(bytesRequired | 0x80));
                for (var i = bytesRequired - 1; i >= 0; i--)
                {
                    stream.Write((byte)(length >> (8 * i) & 0xff));
                }
            }
        }

        static void EncodeIntegerBE(BinaryWriter stream, byte[] value, bool forceUnsigned = true)
        {
            stream.Write((byte)0x02); // INTEGER
            var prefixZeros = 0;
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != 0) break;
                prefixZeros++;
            }
            if (value.Length - prefixZeros == 0)
            {
                EncodeLength(stream, 1);
                stream.Write((byte)0);
            }
            else
            {
                if (forceUnsigned && value[prefixZeros] > 0x7f)
                {
                    // Add a prefix zero to force unsigned if the MSB is 1
                    EncodeLength(stream, value.Length - prefixZeros + 1);
                    stream.Write((byte)0);
                }
                else
                {
                    EncodeLength(stream, value.Length - prefixZeros);
                }
                for (var i = prefixZeros; i < value.Length; i++)
                {
                    stream.Write(value[i]);
                }
            }
        }

        public static bool ValidateChain(this X509Certificate2[] certs)
        {
            if ((certs?.Length ?? 0) == 0) return false;

            // TODO: spravit cache pre validacie; vyprazdnit ak bola menej ako cca 1 minuta 

            X509Chain chain = null;
            try
            {
                chain = GetChainFromCerts(certs);
                return chain.Build(certs[0]);
            }
            finally
            {
                chain?.Reset();
            }
        }

        static X509Chain GetChainFromCerts(X509Certificate2[] certs)
        {
            var chain = new X509Chain
            {
                ChainPolicy = new X509ChainPolicy()
                {
                    RevocationFlag = X509RevocationFlag.ExcludeRoot,
                    RevocationMode = X509RevocationMode.NoCheck,
                    UrlRetrievalTimeout = TimeSpan.FromSeconds(5),
                }
            };
            if (certs?.Any() == true) chain.ChainPolicy.ExtraStore.AddRange(certs);
            return chain;
        }

        public static X509Certificate2[] CompleteChain(this X509Certificate2[] certs)
        {
            X509Chain chain = null;
            try
            {
                chain = GetChainFromCerts(certs);
                chain.Build(certs[0]);
                return chain.ChainElements.Cast<X509ChainElement>().Select(x => x.Certificate).ToArray();
            }
            finally
            {
                chain?.Reset();
            }
        }

        public static bool ValidateChain(this X509Certificate2 cert)
        {
            if (cert == null) return false;
            return ValidateChain(new[] { cert });
        }

        public static void ShowCertificate(this X509Certificate2 certificate, DependencyObject owner)
        {
            var hwndSource = (HwndSource)PresentationSource.FromDependencyObject(Window.GetWindow(owner));
            X509Certificate2UI.DisplayCertificate(certificate, hwndSource.Handle);
        }

        public static void DeleteCertificate(this X509Certificate2 certificate)
        {
            if (MessageBox.Show("Do you want to delete this certificate?", "Certificate", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                DeleteCertificate(certificate, StoreName.My);
                DeleteCertificate(certificate, StoreName.Root);
                DeleteCertificate(certificate, StoreName.CertificateAuthority);
            }
        }

        static void DeleteCertificate(X509Certificate2 certificate, StoreName storeName)
        {
            X509Store store = null;
            try
            {
                store = new X509Store(storeName, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                store.Remove(certificate);
            }
            catch { }
            store?.Close();
        }

        public static void ShowCertificateChain(this IEnumerable<X509Certificate2> chain, Control owner, Point location)
        {
            owner.CreateNativeWindow(hwnd => ShowCertificateChain(chain, hwnd), location);
        }

        public static bool SaveAs(this X509Certificate2 crt, object contentType)
        {
            if (crt == null) return false;

            try
            {
                var dlg = new SaveFileDialog() { FileName = SanitizeFileName(GetCertDisplayName(crt) ?? "") };

                if (contentType is X509ContentType x509ContentType)
                {
                    if (x509ContentType == X509ContentType.Pkcs12 || x509ContentType == X509ContentType.Pfx)
                    {
                        dlg.Filter = "Personal Information Exchange (*.pfx)|*.pfx";
                        dlg.FileName += ".pfx";
                    }
                    if (x509ContentType == X509ContentType.Cert)
                    {
                        dlg.Filter = "DER Encoded Binary X.509 (*.cer)|*.cer";
                        dlg.FileName += ".cer";
                    }
                    if (dlg.ShowDialog() == true)
                        File.WriteAllBytes(dlg.FileName, crt.Export(x509ContentType));
                    else
                        return false;
                }
                else if (contentType is string strContentType)
                {
                    if (strContentType == "RSA")
                    {
                        dlg.Filter = "Private RSA key in X.509 PEM format (*.key)|*.key";
                        dlg.FileName += ".key";
                        if (dlg.ShowDialog() == true)
                            File.WriteAllText(dlg.FileName, ExportRSA((RSACryptoServiceProvider)crt.PrivateKey), Encoding.ASCII);
                        else
                            return false;
                    }
                    if (strContentType == "PEM")
                    {
                        dlg.Filter = "Certificate in X.509 PEM format (*.crt)|*.crt";
                        dlg.FileName += ".crt";
                        if (dlg.ShowDialog() == true)
                            File.WriteAllText(dlg.FileName, FormatPEM(crt.Export(X509ContentType.Cert), "CERTIFICATE"), Encoding.ASCII);
                        else
                            return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }

        private static readonly Regex InvalidFileRegex = new Regex(string.Format("[{0}]", Regex.Escape(new string(Path.GetInvalidFileNameChars()))));

        static string SanitizeFileName(string fileName)
        {
            return InvalidFileRegex.Replace(fileName, "_");
        }

        static void ShowCertificateChain(this IEnumerable<X509Certificate2> chain, HwndSource hwndSource)
        {
            const int CERT_STORE_PROV_MEMORY = 2;
            const int CERT_CLOSE_STORE_CHECK_FLAG = 2;
            const uint CERT_STORE_ADD_ALWAYS = 4;
            const uint X509_ASN_ENCODING = 1;

            var storeHandle = CertOpenStore(CERT_STORE_PROV_MEMORY, 0, 0, 0, null);
            if (storeHandle == IntPtr.Zero) throw new Win32Exception();

            try
            {
                foreach (var cert in chain)
                {
                    var certificate = cert;
                    var certificateBytes = certificate.Export(X509ContentType.Cert);
                    var certContextHandle = CertCreateCertificateContext(X509_ASN_ENCODING, certificateBytes, (uint)certificateBytes.Length);

                    if (certContextHandle == IntPtr.Zero) throw new Win32Exception();

                    CertAddCertificateContextToStore(storeHandle, certContextHandle, CERT_STORE_ADD_ALWAYS, IntPtr.Zero);
                }

                var extraStoreArray = new[] { storeHandle };
                var extraStoreArrayHandle = GCHandle.Alloc(extraStoreArray, GCHandleType.Pinned);
                try
                {
                    var extraStorePointer = extraStoreArrayHandle.AddrOfPinnedObject();

                    var viewInfo = new CRYPTUI_VIEWCERTIFICATE_STRUCT
                    {
                        hwndParent = hwndSource?.Handle ?? IntPtr.Zero,
                        pCertContext = chain.First().Handle,
                        nStartPage = 0,
                        cStores = 1,
                        rghStores = extraStorePointer
                    };
                    viewInfo.dwSize = Marshal.SizeOf(viewInfo);

                    var fPropertiesChanged = false;
                    CryptUIDlgViewCertificate(ref viewInfo, ref fPropertiesChanged);
                }
                finally
                {
                    if (extraStoreArrayHandle.IsAllocated) extraStoreArrayHandle.Free();
                }
            }
            finally
            {
                CertCloseStore(storeHandle, CERT_CLOSE_STORE_CHECK_FLAG);
            }
        }

        [DllImport("CRYPT32", EntryPoint = "CertOpenStore", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CertOpenStore(int storeProvider, int encodingType, int hcryptProv, int flags, string pvPara);

        [DllImport("crypt32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr CertCreateCertificateContext([In] uint dwCertEncodingType, [In] byte[] pbCertEncoded, [In] uint cbCertEncoded);

        [DllImport("crypt32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CertAddCertificateContextToStore([In] IntPtr hCertStore, [In] IntPtr pCertContext, [In] uint dwAddDisposition, [In, Out] IntPtr ppStoreContext);

        [DllImport("crypt32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CertFreeCertificateContext([In] IntPtr pCertContext);

        [DllImport("CRYPT32", EntryPoint = "CertCloseStore", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CertCloseStore(IntPtr storeProvider, int flags);

        [DllImport("CryptUI.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CryptUIDlgViewCertificate(ref CRYPTUI_VIEWCERTIFICATE_STRUCT pCertViewInfo, ref bool pfPropertiesChanged);


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct CRYPTUI_VIEWCERTIFICATE_STRUCT
        {
            public int dwSize;
            public IntPtr hwndParent;
            public int dwFlags;
            [MarshalAs(UnmanagedType.LPWStr)]
            public String szTitle;
            public IntPtr pCertContext;
            public IntPtr rgszPurposes;
            public int cPurposes;
            public IntPtr pCryptProviderData;
            public Boolean fpCryptProviderDataTrustedUsage;
            public int idxSigner;
            public int idxCert;
            public Boolean fCounterSigner;
            public int idxCounterSigner;
            public int cStores;
            public IntPtr rghStores;
            public int cPropSheetPages;
            public IntPtr rgPropSheetPages;
            public int nStartPage;
        }




        static readonly Lazy<X509Store> myStore = new Lazy<X509Store>(() =>
        {
            var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            return store;
        });



        public static X509Store MyCurrentUserX509Store => myStore.Value;
    }
}
