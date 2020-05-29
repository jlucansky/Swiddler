using Swiddler.DataChunks;
using Swiddler.IO;
using Swiddler.Rendering;
using System;
using System.Windows;

namespace Swiddler.ChunkViews
{
    public class SelectionAnchorCaret
    {
        public Rect Bounds { get; set; }
        public Fragment Fragment { get; set; }
        public SelectionAnchor Anchor { get; set; }

        public bool IsResolved => Fragment != null && Anchor != null;

        public override bool Equals(object obj)
        {
            var other = obj as SelectionAnchorCaret;
            var fragEquals = (Fragment == null && other?.Fragment == null ) || (Fragment?.Equals(other?.Fragment) ?? false);

            return 
                Bounds.Equals(other?.Bounds) &&
                Anchor.Equals(other?.Anchor) &&
                fragEquals;
        }

        public override int GetHashCode()
        {
            return Bounds.GetHashCode() ^ Anchor.GetHashCode() ^ (Fragment?.GetHashCode() ?? 0);
        }
    }

    public class SelectionAnchor : IComparable<SelectionAnchor>
    {
        public IDataChunk Chunk { get; set; }
        /// <summary>
        /// Exact offset within chunk
        /// </summary>
        public int Offset { get; set; }

        public int CompareTo(SelectionAnchor other)
        {
            var seqCmp = Chunk.SequenceNumber.CompareTo(other.Chunk.SequenceNumber);
            return seqCmp == 0 ? Offset.CompareTo(other.Offset) : seqCmp;
        }

        public override bool Equals(object obj)
        {
            var other = obj as SelectionAnchor;
            return
                Chunk.SequenceNumber.Equals(other?.Chunk.SequenceNumber) &&
                Offset.Equals(other?.Offset);
        }

        public override int GetHashCode()
        {
            return Chunk.SequenceNumber.GetHashCode() ^ Offset.GetHashCode();
        }

        public override string ToString()
        {
            return $"Seq = {Chunk.SequenceNumber}; Offset = {Offset}";
        }
    }

}
