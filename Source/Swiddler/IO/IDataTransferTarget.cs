using Swiddler.Common;
using Swiddler.DataChunks;
using Swiddler.Utils.RtfWriter;
using System.IO;
using System.Text;

namespace Swiddler.IO
{
    public interface IDataTransferTarget
    {
        void Write(IDataChunk chunk, byte[] data);
        void Flush();
    }

    public class StreamTransferTarget : IDataTransferTarget
    {
        readonly Stream _stream;
        public StreamTransferTarget(Stream stream) => _stream = stream;
        public void Write(IDataChunk chunk, byte[] data) => _stream.Write(data, 0, data.Length);
        public void Flush() => _stream.Flush();
    }

    public class RtfTransferTarget : IDataTransferTarget
    {
        RtfDocument _doc;
        ColorDescriptor _inFg, _inBg, _outFg, _outBg;
        Encoding _encoding { get; set; }

        public RtfTransferTarget(RtfDocument doc, Encoding encoding)
        {
            _doc = doc;
            _encoding = encoding;

            doc.DefaultCharFormat.FontSize = 10;

            _inFg = doc.createColor(new RtfColor(App.Current.Res.InboundFlowTextBrush.Color));
            _inBg = doc.createColor(new RtfColor(App.Current.Res.InboundFlowBrush.Color));
            _outFg = doc.createColor(new RtfColor(App.Current.Res.OutboundFlowTextBrush.Color));
            _outBg = doc.createColor(new RtfColor(App.Current.Res.OutboundFlowBrush.Color));
        }

        public void Write(IDataChunk chunk, byte[] data)
        {
            var par = _doc.addParagraph();

            par.setText(_encoding.GetString(data));

            if (chunk is Packet packet)
            {
                if (packet.Flow == TrafficFlow.Inbound)
                {
                    par.DefaultCharFormat.FgColor = _inFg;
                    par.DefaultCharFormat.BgColor = _inBg;
                }
                if (packet.Flow == TrafficFlow.Outbound)
                {
                    par.DefaultCharFormat.FgColor = _outFg;
                    par.DefaultCharFormat.BgColor = _outBg;
                }
            }
        }
        
        public void Flush()
        {
        }
    }

    public class CompositeTransferTarget : IDataTransferTarget
    {
        IDataTransferTarget[] _targets;
        public CompositeTransferTarget(params IDataTransferTarget[] targets) => _targets = targets;
        public void Write(IDataChunk chunk, byte[] data)
        {
            foreach (var t in _targets) t.Write(chunk, data);
        }
        public void Flush()
        {
            foreach (var t in _targets) t.Flush();
        }
    }

}
