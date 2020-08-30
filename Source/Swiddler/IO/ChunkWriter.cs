using Swiddler.Common;
using Swiddler.DataChunks;
using Swiddler.Serialization;
using Swiddler.Serialization.Pcap;
using Swiddler.Serialization.Rtf;
using Swiddler.Utils;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Swiddler.IO
{
    public interface IChunkWriter
    {
        void Write(IDataChunk chunk, byte[] data);
    }

    public class StreamChunkWriter : IChunkWriter
    {
        readonly Stream _stream;
        public StreamChunkWriter(Stream stream) => _stream = stream;
        public void Write(IDataChunk chunk, byte[] data) => _stream.Write(data, 0, data.Length);
    }

    public class PcapChunkWriter : IChunkWriter, IDisposable
    {
        readonly IPBuilder _encoder;
        readonly PcapWriter _pcap;
        ulong lastMicroseconds;
        bool disposed = false;

        public PcapChunkWriter(Stream stream, ProtocolType protocolType)
        {
            _encoder = new IPBuilder(protocolType);

            var sh = SectionHeader.CreateEmptyHeader();
            sh.LinkType = LinkTypes.Raw;
            sh.MaximumCaptureLength = 0x40000;
            _pcap = new PcapWriter(stream, sh);
        }

        public void Write(IDataChunk chunk, byte[] data)
        {
            const int microseconds = 1_000_000;

            if (chunk is Packet packet)
            {
                foreach (var raw in _encoder.BuildPacket(packet))
                {
                    if (disposed) break;
                    var unixUs = packet.Timestamp.UtcDateTime.GetUnixMicroSeconds();
                    if (unixUs <= lastMicroseconds) unixUs = lastMicroseconds + 1; // every packet should have unique timestamp and increment sequentially
                    _pcap.WritePacket(new PcapPacket(unixUs / microseconds, unixUs % microseconds, raw));
                    lastMicroseconds = unixUs;
                }
            }
        }

        public void Dispose()
        {
            _pcap.Dispose();
        }
    }

    public class RtfChunkWriter : IChunkWriter
    {
        RtfDocument _doc;
        ColorDescriptor _inFg, _inBg, _outFg, _outBg;
        Encoding _encoding { get; set; }

        public RtfChunkWriter(RtfDocument doc, Encoding encoding)
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
    }

    public class CompositeChunkWriter : IChunkWriter, IDisposable
    {
        IChunkWriter[] _targets;
        public CompositeChunkWriter(params IChunkWriter[] targets) => _targets = targets;

        public void Write(IDataChunk chunk, byte[] data) { foreach (var t in _targets) t.Write(chunk, data); }
        public void Dispose() { foreach (var t in _targets) (t as IDisposable)?.Dispose(); }
    }

}
