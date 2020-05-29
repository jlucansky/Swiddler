using System;

namespace Swiddler.Security
{
    public class SslAlertException : Exception
    {
        public int MajorVersion { get; set; }
        public int MinorVersion { get; set; }
        public int Level { get; set; } // warning(1), fatal(2)
        public SslAlertDescription Description { get; set; }

        public override string Message => GetMessage();

        string GetMessage()
        {
            string sLevel = null;

            if (Level == 1)
                sLevel = "warning";
            else if (Level == 2)
                sLevel = "fatal";

            if (sLevel != null)
                return $"SSL/TLS Alert: 0x{((int)Description):X2}, {Description} ({sLevel})";
            else
                return "Unknown SSL/TLS Alert";
        }
    }

    //https://tools.ietf.org/html/rfc5246#section-7.2
    public enum SslAlertDescription : byte
    {
        CLOSE_NOTIFY = 0,
        UNEXPECTED_MESSAGE = 10,
        BAD_RECORD_MAC = 20,
        DECRYPTION_FAILED_RESERVED = 21,
        RECORD_OVERFLOW = 22,
        DECOMPRESSION_FAILURE = 30,
        HANDSHAKE_FAILURE = 40,
        NO_CERTIFICATE_RESERVED = 41,
        BAD_CERTIFICATE = 42,
        UNSUPPORTED_CERTIFICATE = 43,
        CERTIFICATE_REVOKED = 44,
        CERTIFICATE_EXPIRED = 45,
        CERTIFICATE_UNKNOWN = 46,
        ILLEGAL_PARAMETER = 47,
        UNKNOWN_CA = 48,
        ACCESS_DENIED = 49,
        DECODE_ERROR = 50,
        DECRYPT_ERROR = 51,
        EXPORT_RESTRICTION_RESERVED = 60,
        PROTOCOL_VERSION = 70,
        INSUFFICIENT_SECURITY = 71,
        INTERNAL_ERROR = 80,
        INAPPROPRIATE_FALLBACK = 86,
        USER_CANCELED = 90,
        MISSING_EXTENSION = 109,
        NO_RENEGOTIATION = 100,
        UNSUPPORTED_EXTENSION = 110,
        UNRECOGNIZED_NAME = 112,
        BAD_CERTIFICATE_STATUS_RESPONSE = 113,
        UNKNOWN_PSK_IDENTITY = 115,
        CERTIFICATE_REQUIRED = 116,
        NO_APPLICATION_PROTOCOL = 120,
    }
}
