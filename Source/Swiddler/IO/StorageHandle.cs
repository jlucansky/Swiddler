using Swiddler.Serialization;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Swiddler.IO
{
    public class StorageHandle : IDisposable
    {
        public string FileName { get; private set; }
        public bool CanWrite { get; private set; }
        public bool DeleteOnClose { get; private set; }
        public long LowestAllowedOffset { get; private set; }
        public long HighestAllowedOffset { get; private set; }
        public long SequenceCounter { get; private set; } = 1;
        public long FileLength { get; private set; }
        public long ChunkCount { get; private set; }
        public long LinesCountApprox { get; private set; }

        /// <summary>
        /// Callback when data was written.
        /// </summary>
        public Action<long> FileChangedCallback { get; set; }

        public CachedBlockIterator BlockIterator { get; private set; }

        private FileStream readerStream, writerStream;
        private BlockWriter writer;
        private BlockReader reader;

        private StorageHandle() { }

        public static StorageHandle CreateTemporary()
        {
            var storage = new StorageHandle()
            {
                FileName = Path.Combine(GetTempDir(), Guid.NewGuid().ToString()),
                CanWrite = true,
                DeleteOnClose = true,
            };
            storage.Open();
            return storage;
        }

        public static StorageHandle OpenSession(string fileName, SessionXml session)
        {
            var storage = new StorageHandle()
            {
                FileName = fileName,
                LowestAllowedOffset = session.LowestOffset,
                HighestAllowedOffset = session.HighestOffset,
                ChunkCount = session.ChunkCount,
                LinesCountApprox = session.LinesCountApprox,
            };
            storage.Open();
            return storage;
        }

        private void Open()
        {
            var options = FileOptions.None;

            if (DeleteOnClose)
                options |= FileOptions.DeleteOnClose;

            if (CanWrite)
            {
                if (LowestAllowedOffset != 0 || HighestAllowedOffset != 0)
                    throw new InvalidOperationException($"{nameof(LowestAllowedOffset)} and {nameof(HighestAllowedOffset)} must be zero.");

                writerStream = new FileStream(FileName, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete, Constants.BlockSize, options);
                writer = new BlockWriter(writerStream);
            }

            readerStream = OpenRead();

            if (HighestAllowedOffset == 0)
                HighestAllowedOffset = readerStream.Length;
            
            reader = new BlockReader(readerStream) { HighestAllowedOffset = HighestAllowedOffset };

            BlockIterator = new CachedBlockIterator(reader);

            FileLength = HighestAllowedOffset;
        }

        public FileStream OpenRead()
        {
            return new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, Constants.BlockSize, FileOptions.RandomAccess) { Position = LowestAllowedOffset };
        }

        public BlockReader CreateReader()
        {
            var stream = OpenRead();
            return new BlockReader(stream) { HighestAllowedOffset = reader.HighestAllowedOffset, LeaveStreamOpen = false };
        }

        /// <summary>
        /// Thread-safe append chunk to file.
        /// </summary>
        public void Write(IDataChunk item)
        {
            lock (_syncObject)
            {
                if (!CanWrite) throw new InvalidOperationException("Not writable.");

                item.SequenceNumber = SequenceCounter++;

                writer.Write(item);

                ChunkCount++;
                Interlocked.Exchange(ref writerPosition, writerStream.Position);
                Interlocked.Exchange(ref writerLinesCountApprox, 0 /* TODO */);

                NotifyWritten(item);
            }
        }

        readonly object _syncObject = new object();

        public void MakeReadOnly()
        {
            lock (_syncObject)
            {
                CanWrite = false;
                writerStream?.Dispose();
                writerStream = null;
            }
        }

        public void Dispose()
        {
            writerStream?.Dispose();
            readerStream?.Dispose();

            writerStream = null;
            readerStream = null;
        }


        private long currentOffset, writerPosition, writerLinesCountApprox;
        private volatile bool notifyInProgress = false;
        private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;
        void NotifyWritten(IDataChunk item)
        {
            Interlocked.Exchange(ref currentOffset, item.ActualOffset);
            if (!notifyInProgress)
            {
                notifyInProgress = true;
                _dispatcher.BeginInvoke(new Action(NotifyWrittenDispatched), DispatcherPriority.Background);
            }
        }

        void NotifyWrittenDispatched()
        {
            notifyInProgress = false;

            FileLength = Interlocked.Read(ref writerPosition);
            LinesCountApprox = Interlocked.Read(ref writerLinesCountApprox);
            reader.HighestAllowedOffset = FileLength;

            BlockIterator.FileChanged();
            FileChangedCallback(currentOffset);
        }

        public override string ToString() => $"{FileName}@{readerStream.Position}:{FileLength}";

        static string GetTempDir()
        {
            string dir = Path.Combine(Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.Process), nameof(Swiddler), "Sessions");

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            return dir;
        }

        public FileStream CreateTempFile(string name)
        {
            var dir = Path.Combine(GetTempDir(), Guid.NewGuid().ToString("N"));
            var path = Path.Combine(dir, name);

            Directory.CreateDirectory(dir);

            var stream = new TempFileStream(path);

            stream.Disposing += () =>
            {
                try
                {
                    File.Delete(path);
                    Directory.Delete(dir);
                }
                catch { }
            };

            return stream;
        }

        class TempFileStream : FileStream
        {
            public event Action Disposing;
            public TempFileStream(string path) : base(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 0x10000) { }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing) Disposing?.Invoke();
            }
        }
    }
}
