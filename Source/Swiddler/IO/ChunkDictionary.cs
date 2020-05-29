using System.Collections.Generic;

namespace Swiddler.IO
{
    /// <summary>
    /// Block of chunks. Sequence number is the key.
    /// </summary>
    public class ChunkDictionary : Dictionary<long, IDataChunk>
    {
        public long BlockIndex { get; set; }
        public bool IsValid { get; set; }

        public bool IsDirty { get; set; } // when data was written in currrent block

        public long FirstSequenceNumber { get; set; }
        public long LastSequenceNumber { get; set; }

        public IDataChunk FirstChunk => this[FirstSequenceNumber];
        public IDataChunk LastChunk => this[LastSequenceNumber];

        /// <summary>
        /// Find coresponding chunk where file offset belongs to.
        /// </summary>
        public long FindSequenceNumber(long offset)
        {
            long lower = FirstSequenceNumber, upper = LastSequenceNumber, middle;
            int comparisonResult;
            while (lower <= upper) // binary search
            {
                middle = lower + (upper - lower) / 2;
                comparisonResult = offset.CompareTo(this[middle].ActualOffset);
                if (comparisonResult < 0)
                    upper = middle - 1;
                else if (comparisonResult > 0)
                    lower = middle + 1;
                else
                    return middle;
            }

            if (lower == FirstSequenceNumber)
                return lower;

            return lower - 1; // align to lower offset
        }
    }
}
