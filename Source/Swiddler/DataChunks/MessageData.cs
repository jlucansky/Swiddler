using Swiddler.IO;
using Swiddler.Serialization;
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Swiddler.DataChunks
{
    public enum MessageType : byte
    {
        Information,
        Error,
        SocketError,
        Connecting,
        ConnectionBanner,

        SerializedObject = 200,
        SslHandshake,
    }

    public class MessageData : IDataChunk
    {
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
        public long ActualOffset { get; set; }
        public int ActualLength { get; set; }
        public long SequenceNumber { get; set; }

        public string Text { get; set; }
        public MessageType Type { get; set; }

        public MessageData() { }
        public MessageData(SslHandshake obj) { SetSerializedObject(obj); }

        private static readonly XmlWriterSettings writerSettings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = false, Encoding = new UTF8Encoding(false) };
        private static readonly XmlSerializerNamespaces emptyNamespaces = new XmlSerializerNamespaces(new[] { new XmlQualifiedName("", "") });

        /// <summary>
        /// When called, <see cref="Text"/> will contains XML serialized object.
        /// </summary>
        public MessageData SetSerializedObject<T>(T obj)
        {
            if (obj is SslHandshake)
                Type = MessageType.SslHandshake;
            else
                throw new InvalidOperationException("Invalid type: " + obj.GetType());

            var serializer = new XmlSerializer(typeof(T));
            using (var strWriter = new StringWriter())
            {
                using (var writer = XmlWriter.Create(strWriter, writerSettings))
                    serializer.Serialize(writer, obj, emptyNamespaces);

                Text = strWriter.ToString();
            }

            return this;
        }

        public object GetSerializedObject()
        {
            switch (Type)
            {
                case MessageType.SslHandshake:
                    return GetSerializedObject<SslHandshake>();
                default:
                    throw new InvalidOperationException("Invalid MessageType: " + Type);
            }
        }

        private T GetSerializedObject<T>()
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var reader = new StringReader(Text))
            {
                return (T)serializer.Deserialize(reader);
            }
        }

    }
}
