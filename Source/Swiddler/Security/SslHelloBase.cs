using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Swiddler.Security
{
    public abstract class SslHelloBase
    {
        [XmlAttribute] public int HandshakeVersion { get; set; }

        [XmlAttribute] public int MajorVersion { get; set; }
        [XmlAttribute] public int MinorVersion { get; set; }

        [XmlIgnore] public byte[] Random { get; set; }
        [XmlElement(nameof(Random))] public string RandomEncoded { get => Convert.ToBase64String(Random); set => Random = Convert.FromBase64String(value); }

        [XmlIgnore] public byte[] SessionId { get; set; }
        [XmlElement(nameof(SessionId))] public string SessionIdEncoded { get => Convert.ToBase64String(SessionId); set => SessionId = Convert.FromBase64String(value); }

        [XmlIgnore] public Dictionary<string, SslExtension> Extensions { get; set; }
        [XmlElement("Extension")]
        public SslExtension[] ExtensionsList { get => Extensions?.Values.ToArray(); set => Extensions = value?.ToDictionary(x => x.Name); }

        private string SslVersionToString()
        {
            int major = MajorVersion, minor = MinorVersion;

            string str = "Unknown";
            if (major == 3 && minor == 4)
                str = "TLS/1.3";
            else if (major == 3 && minor == 3)
                str = "TLS/1.2";
            else if (major == 3 && minor == 2)
                str = "TLS/1.1";
            else if (major == 3 && minor == 1)
                str = "TLS/1.0";
            else if (major == 3 && minor == 0)
                str = "SSL/3.0";
            else if (major == 2 && minor == 0)
                str = "SSL/2.0";

            return str;
        }

        public override string ToString() => SslVersionToString();

        public string ProtocolVersion => SslVersionToString();
    }
}
