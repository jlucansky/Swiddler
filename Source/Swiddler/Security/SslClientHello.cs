using System;
using System.Linq;
using System.Xml.Serialization;

namespace Swiddler.Security
{
    /// <summary>
    /// Wraps up the client SSL hello information.
    /// </summary>
    public class SslClientHello : SslHelloBase
    {
        public int[] Ciphers { get; set; }

        [XmlIgnore] public byte[] CompressionData { get; set; }
        [XmlElement(nameof(CompressionData))] public string CompressionDataEncoded { get => CompressionData == null ? null : Convert.ToBase64String(CompressionData); set => CompressionData = value == null ? null : Convert.FromBase64String(value); }


        public string[] GetCipherSuites()
        {
            return Ciphers?.Select(x => SslCiphers.GetName(x)).ToArray();
        }

        public string GetServerNameIndication()
        {
            if (Extensions != null && 
                Extensions.TryGetValue("server_name", out var sslExtension) &&
                sslExtension?.GetExtensionData() is string[] data)
            {
                return data.FirstOrDefault();
            }

            return null;
        }
    }
}
