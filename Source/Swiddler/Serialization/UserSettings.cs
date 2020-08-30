using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

namespace Swiddler.Serialization
{
    public class UserSettings
    {
        public Rect MainWindowBounds { get; set; }
        public WindowState MainWindowState { get; set; }
        public double MainWindowLeftColumn { get; set; }
        public double MainWindowBottomRow { get; set; }

        public Rect NewConnectionWindowBounds { get; set; }
        public double NewConnectionLeftColumn { get; set; }
        public double NewConnectionRightColumn { get; set; }

        public bool PcapSelectionExport { get; set; } = true;

        public List<string> QuickMRU { get; set; }

        #region IO

        public static string FileName => Path.Combine(App.Current.AppDataPath, nameof(UserSettings) + ".xml");

        public static UserSettings Load()
        {
            if (File.Exists(FileName))
                using (var file = File.OpenRead(FileName)) return Deserialize(file);
            else
                return new UserSettings(); 
        }

        public void Save()
        {
            using (var file = File.Open(FileName, FileMode.OpenOrCreate)) Serialize(file);
        }

        private static readonly XmlWriterSettings writerSettings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, Encoding = new UTF8Encoding(false) };
        private static readonly XmlSerializerNamespaces emptyNamespaces = new XmlSerializerNamespaces(new[] { new XmlQualifiedName("", "") });
        private static readonly XmlSerializer serializer = new XmlSerializer(typeof(UserSettings));

        void Serialize(Stream stream)
        {
            stream.SetLength(0);
            using (var writer = XmlWriter.Create(stream, writerSettings))
            {
                serializer.Serialize(writer, this, emptyNamespaces);
            }
        }

        static UserSettings Deserialize(Stream stream)
        {
            using (var reader = XmlReader.Create(stream))
            {
                return (UserSettings)serializer.Deserialize(reader);
            }
        }
        #endregion
    }
}
