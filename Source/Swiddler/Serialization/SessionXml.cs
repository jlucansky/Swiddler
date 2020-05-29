using Swiddler.Common;
using System;

namespace Swiddler.Serialization
{
    public class SessionXml
    {
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        public long LowestOffset { get; set; }
        public long HighestOffset { get; set; }

        public long ChunkCount { get; set; }

        public long LinesCountApprox { get; set; }

        public ConnectionBanner ConnectionBanner { get; set; }
    }
}
