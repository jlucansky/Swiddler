using Swiddler.DataChunks;
using Swiddler.Rendering;
using Swiddler.Serialization;
using System.Windows;
using System.Windows.Media;

namespace Swiddler.ChunkViews
{
    public class Message : ChunkViewItem<MessageData>
    {
        public object SerializedObject { get; private set; }

        public string IconName
        {
            get
            {
                if (Chunk.Type == MessageType.Information)
                    return "Info";
                else if (Chunk.Type == MessageType.Error)
                    return "Error";
                else if (Chunk.Type == MessageType.SocketError)
                    return "Disconnected";
                else if (Chunk.Type == MessageType.Connecting || Chunk.Type == MessageType.ConnectionBanner)
                    return "Connecting";

                return null;
            }
        }

        public Brush Color
        {
            get
            {
                if (Chunk.Type == MessageType.Information)
                    return App.Current.Res.MessageInfoBrush;
                else if (Chunk.Type == MessageType.Connecting || Chunk.Type == MessageType.ConnectionBanner)
                    return App.Current.Res.MessageInfoBrush;
                else if (Chunk.Type == MessageType.Error)
                    return App.Current.Res.MessageErrorBrush;
                else if (Chunk.Type == MessageType.SocketError)
                    return App.Current.Res.MessageErrorBrush;

                return null;
            }
        }

        public string Text => Chunk?.Text;

        public override void Build()
        {
            if (Chunk.Type > MessageType.SerializedObject)
                SerializedObject = ViewContent.Session.GetCachedObject(Chunk.SequenceNumber, Chunk.GetSerializedObject);
            
            Fragment fragment;
            double height;
            if (Chunk.Type == MessageType.ConnectionBanner && ViewContent.Session.ConnectionBanner != null)
            {
                // show connection info instead "Connecting to ..."
                fragment = new ConnectionFragment() { Model = ViewContent.Session.ConnectionBanner };
                height = 76;
            }
            else if (Chunk.Type == MessageType.SslHandshake)
            {
                fragment = new SslHandshakeFragment() { Model = (SslHandshake)SerializedObject };
                height = 45;
            }
            else
            {
                fragment = new MessageFragment();
                height = 56;
            }

            ViewContent.MoveInsertionPointToLineBeginning();

            fragment.Source = this;
            fragment.Bounds = new Rect(0, ViewContent.InsertionPoint.Y, ViewContent.Metrics.Viewport.Width, ViewContent.SnapToPixelsY(height));
            fragment.ApproxFileOffset = Chunk.ActualOffset;
            fragment.ApproxFileLength = Chunk.ActualLength;

            ViewContent.TextLayer.Add(fragment);
            ViewContent.MoveInsertionPointToLineBeginning();
        }
    }
}
