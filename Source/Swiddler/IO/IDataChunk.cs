using System;

namespace Swiddler.IO
{
    public interface IDataChunk
    {
        /// <summary>
        /// Time when data was captured.
        /// </summary>
        DateTimeOffset Timestamp { get; set; }
        
        /// <summary>
        /// Actual position of the data chunk in file including headers
        /// </summary>
        long ActualOffset { get; set; }
        /// <summary>
        /// Real length including headers
        /// </summary>
        int ActualLength { get; set; }

        /// <summary>
        /// Unique index of the chunk.
        /// </summary>
        long SequenceNumber { get; set; }
    }
}
