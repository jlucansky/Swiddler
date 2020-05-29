using Swiddler.Common;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Swiddler.ViewModels
{
    public class Certificate : BindableBase
    {
        public static string[] CertType { get; } = new[] { "Client Certificate", "Server Certificate" };
        public static int[] KeyLengths { get; } = new[] { 1024, 2048, 3072, 4096, 6144, 7680, 8192, 15360, 16384 };
        public static string[] CountryCodes { get; } = new[] { "US", "CA", "AX", "AD", "AE", "AF", "AG", "AI", "AL", "AM", "AN", "AO", "AQ", "AR", "AS", "AT", "AU", "AW", "AZ", "BA", "BB", "BD", "BE", "BF", "BG", "BH", "BI", "BJ", "BM", "BN", "BO", "BR", "BS", "BT", "BV", "BW", "BZ", "CC", "CF", "CH", "CI", "CK", "CL", "CM", "CN", "CO", "CR", "CS", "CV", "CX", "CY", "CZ", "DE", "DJ", "DK", "DM", "DO", "DZ", "EC", "EE", "EG", "EH", "ER", "ES", "ET", "FI", "FJ", "FK", "FM", "FO", "FR", "FX", "GA", "GB", "GD", "GE", "GF", "GG", "GH", "GI", "GL", "GM", "GN", "GP", "GQ", "GR", "GS", "GT", "GU", "GW", "GY", "HK", "HM", "HN", "HR", "HT", "HU", "ID", "IE", "IL", "IM", "IN", "IO", "IS", "IT", "JE", "JM", "JO", "JP", "KE", "KG", "KH", "KI", "KM", "KN", "KR", "KW", "KY", "KZ", "LA", "LC", "LI", "LK", "LS", "LT", "LU", "LV", "LY", "MA", "MC", "MD", "ME", "MG", "MH", "MK", "ML", "MM", "MN", "MO", "MP", "MQ", "MR", "MS", "MT", "MU", "MV", "MW", "MX", "MY", "MZ", "NA", "NC", "NE", "NF", "NG", "NI", "NL", "NO", "NP", "NR", "NT", "NU", "NZ", "OM", "PA", "PE", "PF", "PG", "PH", "PK", "PL", "PM", "PN", "PR", "PS", "PT", "PW", "PY", "QA", "RE", "RO", "RS", "RU", "RW", "SA", "SB", "SC", "SE", "SG", "SH", "SI", "SJ", "SK", "SL", "SM", "SN", "SR", "ST", "SU", "SV", "SZ", "TC", "TD", "TF", "TG", "TH", "TJ", "TK", "TM", "TN", "TO", "TP", "TR", "TT", "TV", "TW", "TZ", "UA", "UG", "UM", "UY", "UZ", "VA", "VC", "VE", "VG", "VI", "VN", "VU", "WF", "WS", "YE", "YT", "ZA", "ZM" };
        public static string[] DigestAlgorithms { get; } = new[] { "SHA1", "SHA256", "SHA384", "SHA512" };
        public static SANType[] SANTypes { get; } = new[] { SANType.DNS, SANType.IP, SANType.URI, SANType.email };


        bool _SignByCertificateAuthority = true;
        public bool SignByCertificateAuthority { get => _SignByCertificateAuthority; set => SetProperty(ref _SignByCertificateAuthority, value); }

        string _CertificateAuthority, _CommonName, _DigestAlgorithm = "SHA256";
        public string CertificateAuthority { get => _CertificateAuthority; set => SetProperty(ref _CertificateAuthority, value); }
        public string CommonName { get => _CommonName; set => SetProperty(ref _CommonName, value); }
        public string DigestAlgorithm { get => _DigestAlgorithm; set => SetProperty(ref _DigestAlgorithm, value); }

        string _Organization, _OrganizationUnit, _Country, _State, _Locality;
        public string Organization { get => _Organization; set => SetProperty(ref _Organization, value); }
        public string OrganizationUnit { get => _OrganizationUnit; set => SetProperty(ref _OrganizationUnit, value); }
        public string Country { get => _Country; set => SetProperty(ref _Country, value); }
        public string State { get => _State; set => SetProperty(ref _State, value); }
        public string Locality { get => _Locality; set => SetProperty(ref _Locality, value); }


        int _KeyLength = 2048;
        int? _Lifetime = 3650;
        public int KeyLength { get => _KeyLength; set => SetProperty(ref _KeyLength, value); }
        public int? Lifetime { get => _Lifetime; set => SetProperty(ref _Lifetime, value); }


        CertificateType _CertificateType;
        public CertificateType CertificateType { get => _CertificateType; set => SetProperty(ref _CertificateType, value); }

        public List<SANItem> SANList { get; set; } = new List<SANItem>();


        public X509Certificate2 Value { get; set; }

        public bool MachineContext { get; set; }


        public void Validate()
        {
            ValidateMandatory(CommonName, "Common Name", nameof(CommonName));
            ValidateMandatory(Lifetime, "Lifetime", nameof(Lifetime));

            if (SignByCertificateAuthority) 
                ValidateMandatory(CertificateAuthority, "Certificate Authority", nameof(CertificateAuthority));

            if (Lifetime.Value < 1 || Lifetime.Value > 9999)
                throw new ValueException(nameof(Lifetime), $"Enter lifetime days between 1-9999");
        }

        public void ValidateMandatory(object obj, string name, string property)
        {
            if (obj == null || (obj is string str && string.IsNullOrWhiteSpace(str)))
                throw new ValueException(property, $"The field '{name}' is mandatory.");
        }

        public class SANItem
        {
            public SANType Type { get; set; }
            public string Value { get; set; }
        }

        public enum SANType
        {
            DNS,
            IP,
            URI,
            email
        }
    }

    public enum CertificateType
    {
        None = -1,
        ClientCertificate = 0,
        ServerCertificate = 1,
    }

}
