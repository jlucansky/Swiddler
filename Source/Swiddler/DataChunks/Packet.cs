using Swiddler.Common;
using Swiddler.IO;
using System;
using System.Net;

namespace Swiddler.DataChunks
{
    public class Packet : IDataChunk
    {
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
        public long ActualOffset { get; set; }
        public int ActualLength { get; set; }
        public long SequenceNumber { get; set; }

        public byte[] Payload { get; set; }

        public IPEndPoint LocalEndPoint { get; set; }
        public IPEndPoint RemoteEndPoint { get; set; }

        public TrafficFlow Flow { get; set; }
    }
}
