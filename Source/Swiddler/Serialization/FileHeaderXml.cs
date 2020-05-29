using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Swiddler.Serialization
{
    [XmlRoot(RootElementName)]
    public class FileHeaderXml
    {
        public const string LatestFormatVersion = "1.0";

        [XmlAttribute(VersionAttributeName)] public string Version { get; set; } = LatestFormatVersion;

        [XmlElement(SessionElementName)] public List<SessionXml> Sessions { get; set; } = new List<SessionXml>();


        #region Serialization

        private const string VersionAttributeName = "version";
        private const string SessionElementName = "Session";
        private const string RootElementName = "SeekablePacketCapture";

        private static readonly XmlWriterSettings writerSettings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, Encoding = new UTF8Encoding(false) };
        private static readonly XmlSerializerNamespaces emptyNamespaces = new XmlSerializerNamespaces(new[] { new XmlQualifiedName("", "") });
        private static readonly XmlSerializer serializer = new XmlSerializer(typeof(FileHeaderXml));

        public byte[] Serialize()
        {
            using (var stream = new MemoryStream())
            using (var writer = XmlWriter.Create(stream, writerSettings))
            {
                serializer.Serialize(writer, this, emptyNamespaces);
                return stream.ToArray();
            }
        }

        public static FileHeaderXml Deserialize(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            using (var reader = XmlReader.Create(stream))
            {
                reader.Read();
                if (reader.IsStartElement(RootElementName))
                {
                    var version = reader.GetAttribute(VersionAttributeName);
                    stream.Position = 0;

                    if (LatestFormatVersion.Equals(version))
                    {
                        return (FileHeaderXml)serializer.Deserialize(stream);
                    }
                    else
                    {
                        // handle legacy versions
                        //if (FileHeaderXml_v1.TryDeserialize(version, stream, out var result_v1)) return result_v1;

                        throw new Exception("Unsupported file version " + version);
                    }
                }
            }

            throw new Exception("Invalid file format.");
        }

        /*
        public class FileHeaderXml_v1
        {
            public static bool TryDeserialize(string version, Stream stream, out FileHeaderXml result)
        }
        */

        #endregion
    }
}
