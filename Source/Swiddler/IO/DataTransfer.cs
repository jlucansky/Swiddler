using Swiddler.ChunkViews;
using Swiddler.DataChunks;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Swiddler.IO
{
    public class DataTransfer
    {
        public IProgress<double> Progress { get; set; } // dispatched notifications
        public event EventHandler<double> ProgressChanged; // synchronous notifications

        public CancellationToken CancellationToken { get; set; }
        public long ProgressDelay { get; set; } = 15; // milliseconds 


        readonly StorageHandle storage;
        readonly IDataTransferTarget target;

        public DataTransfer(StorageHandle storage, Stream target) : this (storage, new StreamTransferTarget(target)) { }

        public DataTransfer(StorageHandle storage, IDataTransferTarget target)
        {
            this.storage = storage;
            this.target = target;
        }

        public void Copy(Func<IDataChunk, byte[]> payload)
        {
            using (var reader = storage.CreateReader())
            {
                Copy(reader, payload);
            }
        }

        void Copy(BlockReader reader, Func<IDataChunk, byte[]> payload)
        {
            byte[] data;
            while (reader.CurrentChunk != null && null != (data = payload(reader.CurrentChunk)))
            {
                if (data.Length > 0)
                    target.Write(reader.CurrentChunk, data);

                reader.Read();
                CancellationToken.ThrowIfCancellationRequested();
            }
            target.Flush();
        }

        public void CopyRange(IDataChunk start, IDataChunk end, Func<IDataChunk, byte[]> payload)
        {
            var progressScale = 1.0 / (end.ActualOffset - start.ActualOffset);

            using (var reader = storage.CreateReader())
            {
                ReportProgress(0);

                reader.Position = start.ActualOffset;

                Copy(reader, chunk =>
                {
                    ReportProgress((chunk.ActualOffset - start.ActualOffset) * progressScale);
                    return chunk.ActualOffset <= end.ActualOffset ? payload(chunk) : null;
                });

                ReportProgress(1);
            }

        }

        public Task CopySelectionAsync(SelectionAnchor start, SelectionAnchor end)
        {
            return Task.Run(() => CopySelection(start, end));
        }

        public void CopySelection(SelectionAnchor start, SelectionAnchor end)
        {
            if (start.CompareTo(end) > 0) // swap when start > end
            {
                var _ = start;
                start = end;
                end = _;
            }

            var emptyBytes = new byte[0];
            var startSeq = start.Chunk.SequenceNumber;
            var endSeq = end.Chunk.SequenceNumber;

            byte[] GetBytes(IDataChunk chunk)
            {
                if (chunk is Packet packet)
                {
                    if (packet.SequenceNumber == startSeq)
                    {
                        if (packet.SequenceNumber == endSeq)
                            return packet.Payload.Skip(start.Offset).Take(end.Offset - start.Offset).ToArray();
                        else
                            return packet.Payload.Skip(start.Offset).ToArray();
                    }
                    else
                    {
                        if (packet.SequenceNumber == endSeq)
                            return packet.Payload.Take(end.Offset).ToArray();
                    }

                    return packet.Payload;
                }
                return emptyBytes;
            }

            CopyRange(start.Chunk, end.Chunk, GetBytes);
        }

        readonly Stopwatch reportWatch = Stopwatch.StartNew();
        protected void ReportProgress(double value)
        {
            if (value == 0 || value == 1 || reportWatch.ElapsedMilliseconds > ProgressDelay)
            {
                ProgressChanged?.Invoke(this, value);
                Progress?.Report(value);
                reportWatch.Restart();
            }
        }
    }
}
