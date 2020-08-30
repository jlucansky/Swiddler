using Swiddler.Common;
using Swiddler.IO;
using Swiddler.ChunkViews;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Swiddler.DataChunks;
using System.Diagnostics;

namespace Swiddler.Rendering
{
    public class FragmentViewContent
    {
        public Session Session;
        public FragmentView View;
        public FragmentViewMetrics Metrics;
        public FragmentLayer TextLayer, BackgroundLayer;
        public SelectionLayer SelectionLayer;
        public SelectionAnchor SelectionStart, SelectionEnd;

        public Vector DpiScale = new Vector(1, 1);

        public int LineLength => (int)(Metrics.Viewport.Width / CharWidth);
        public double LineHeight = 0, CharWidth = 0; // Pixel snapped values (based on DPI)
        public Point InsertionPoint; // Location where a new fragment should be inserted.
        public long MaxAppendedSequence = -1;

        private long CurrentBlockIndex = -1; // begining
        private long LastAppendedBlockIndex = -1;

        private readonly Dictionary<long, ChunkViewItem> Chunks = new Dictionary<long, ChunkViewItem>(); // Active chunks materialized to fragments by sequence number.

        public FragmentViewContent() { }

        public bool HasSelection => SelectionStart != null && SelectionEnd != null;

        public void FontChanged()
        {
            _GlyphTypeface = null;
            LineHeight = SnapToPixelsY(Metrics.FontSize, ceiling: true);
            ComputeCharWidth();
        }

        internal void ComputeDpi()
        {
            if (View == null) return;

            var ps = PresentationSource.FromVisual(View);

            if (ps == null) return;

            var m = ps.CompositionTarget.TransformToDevice;
            DpiScale = new Vector(m.M11, m.M22);
            FontChanged();
        }

        static readonly byte[] _sweep = Enumerable.Range(32, 256-32).Select(x => (byte)x).ToArray();
        static readonly char[] _controlChars = new ushort[] { // Code Page 437 to Unicode mapping
            0x0000, 0x263A, 0x263B, 0x2665, 0x2666, 0x2663, 0x2660, 0x2022,
            0x25D8, 0x0020, 0x25D9, 0x2642, 0x2640, 0x266A, 0x266B, 0x263C, 
            0x25BA, 0x25C4, 0x2195, 0x203C, 0x00B6, 0x00A7, 0x25AC, 0x21A8,
            0x2191, 0x2193, 0x2192, 0x2190, 0x221F, 0x2194, 0x25B2, 0x25BC }
        .Select(x => (char)x).ToArray();

        private GlyphTypeface _GlyphTypeface;
        public GlyphTypeface GlyphTypeface
        {
            get
            {
                if (_GlyphTypeface == null)
                    if (!Metrics.Typeface.TryGetGlyphTypeface(out _GlyphTypeface))
                        throw new NotSupportedException();

                var map = _GlyphTypeface.CharacterToGlyphMap;
                _glyphIndices = _controlChars.Concat(Metrics.Encoding.GetChars(_sweep)).Select(x => { map.TryGetValue(x, out var val); return val; }).ToArray();

                return _GlyphTypeface;
            }
        }

        ushort[] _glyphIndices;

        /// <summary>
        /// Convert bytes to glyph indices considering current GlyphTypeface and Encoding
        /// </summary>
        public ushort[] GetGlyphIndices(byte[] data, int start, int count)
        {
            var result = new ushort[count];
            for (int i = 0; i < count; i++)
                result[i] = _glyphIndices[data[start + i]];
            return result;
        }

        void ComputeCharWidth()
        {
            if (LineHeight > 0)
            {
                FormattedText ft = new FormattedText("X", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Metrics.Typeface, Metrics.FontSize, Brushes.Black);
                CharWidth = SnapToPixelsX(ft.WidthIncludingTrailingWhitespace);
            }
            else CharWidth = 0;
        }

        public double SnapToPixelsX(double n, bool ceiling = false)
        {
            var fac = DpiScale.X;
            if (ceiling)
                return Math.Ceiling(n * fac) / fac;
            else
                return Math.Floor(n * fac) / fac;
        }

        public double SnapToPixelsY(double n, bool ceiling = false)
        {
            var fac = DpiScale.Y;
            if (ceiling)
                return Math.Ceiling(n * fac) / fac;
            else
                return Math.Floor(n * fac) / fac;
        }

        public Point MoveInsertionPointAfter(Fragment fragment)
        {
            var x = fragment.Bounds.Right;
            var y = fragment.Bounds.Y;

            if (x > Metrics.Viewport.Width - CharWidth)
            {
                x = 0;
                y = fragment.Bounds.Bottom;
            }

            return InsertionPoint = new Point(x, y); ;
        }

        public Point MoveInsertionPointToLineBeginning()
        {
            return InsertionPoint = new Point(0, TextLayer.Height);
        }

        public void Reset()
        {
            TextLayer.Clear();
            BackgroundLayer.Clear();
            SelectionLayer.Clear();
            InsertionPoint = new Point();
            Chunks.Clear();
            CurrentBlockIndex = -1;
            LastAppendedBlockIndex = -1;
            MaxAppendedSequence = -1;
            OverExtentForLayerHeight = 0;
            OverExtentForViewport = Size.Empty;
            FirstBlockIndex = LastBlockIndex = -1;
        }

        long FirstBlockIndex = -1, LastBlockIndex = -1; // range of blocks in use
        public void Invalidate()
        {
            var iterator = Session.Storage.BlockIterator;
            var blocks = iterator.EnumerateBlocks(Metrics.CurrentFileOffset);
            bool firstBlock = true;
            bool isReset = false;
            long newFirstBlockIndex = -1, newLastBlockIndex = -1;

            foreach (var block in blocks)
            {
                block.IsValid = true;
                newLastBlockIndex = block.BlockIndex;
                if (firstBlock)
                {
                    firstBlock = false;
                    newFirstBlockIndex = block.BlockIndex;

                    if (CurrentBlockIndex != block.BlockIndex)
                    {
                        Reset(); // rebuild all fragments because old first block is too far
                        isReset = true;
                        CurrentBlockIndex = block.BlockIndex;
                        Append(block); // build fragments for first block
                    }
                    else
                    {
                        UpdateDirtyBlock(block);
                    }

                    var seq = block.FindSequenceNumber(Metrics.CurrentFileOffset);

                    View.ScrollTransform.X = -View.HorizontalOffset;
                    View.ScrollTransform.Y = -GetScrollPosition(Chunks[seq]);
                }
                else
                {
                    if (block.BlockIndex == LastAppendedBlockIndex + 1)
                        Append(block); // build fragments for current block if necessary
                    else if (block.BlockIndex > LastAppendedBlockIndex)
                        throw new ArgumentOutOfRangeException(nameof(LastAppendedBlockIndex)); // unexpected, should never happen

                    UpdateDirtyBlock(block);
                }

                var lastChunkView = Chunks[block.LastSequenceNumber]; // last chunk in block

                double blockEndPointY;
                if (lastChunkView.FirstFragmentIndex == -1) // chunk doesn't have any fragments
                    blockEndPointY = lastChunkView.Location.Y;
                else
                    blockEndPointY = TextLayer.Items[lastChunkView.FirstFragmentIndex].Bounds.Y;

                if (blockEndPointY > Metrics.Viewport.Height - View.ScrollTransform.Y) // last chunk in block is out of viewport
                    break;
            }

            iterator.Invalidate(); // remove unused (invalid) blocks

            if (!isReset && (newFirstBlockIndex != FirstBlockIndex || newLastBlockIndex != LastBlockIndex)) 
                RemoveChunksOutOfRange(newFirstBlockIndex, newLastBlockIndex); // cleanup if necessary

            FirstBlockIndex = newFirstBlockIndex; LastBlockIndex = newLastBlockIndex;

            AdjustOverExtent();

            SelectionLayer.AdjustSelectionAnchors();
        }

        public void Append(ChunkDictionary block, long? fromSequence = null)
        {
            LastAppendedBlockIndex = block.BlockIndex;
            for (long seq = fromSequence ?? block.FirstSequenceNumber; seq <= block.LastSequenceNumber; seq++)
            {
                if (seq > MaxAppendedSequence)
                    MaxAppendedSequence = seq;

                if (Chunks.ContainsKey(seq)) continue;

                var chunkView = CreateChunkView(block[seq]);

                chunkView.BlockIndex = block.BlockIndex;
                chunkView.Location = InsertionPoint;

                var firstFragmentIndex = TextLayer.Items.Count; // preserve old items count before appending some fragments

                chunkView.Build();

                if (firstFragmentIndex == TextLayer.Items.Count) // no fragment was added
                {
                    chunkView.FirstFragmentIndex = chunkView.LastFragmentIndex = -1;
                }
                else
                {
                    chunkView.FirstFragmentIndex = firstFragmentIndex;
                    chunkView.LastFragmentIndex = TextLayer.Items.Count - 1;
                    TextLayer.Items.Last().IsTrailing = true;
                }

                Chunks.Add(seq, chunkView);
            }
        }

        void UpdateDirtyBlock(ChunkDictionary block)
        {
            if (block.LastSequenceNumber > MaxAppendedSequence)
            {
                if (MaxAppendedSequence >= block.FirstSequenceNumber && MaxAppendedSequence <= block.LastSequenceNumber)
                    Append(block, fromSequence: MaxAppendedSequence); // when storage data changed
            }
        }

        ChunkViewItem CreateChunkView(IDataChunk chunk)
        {
            switch (chunk)
            {
                case Packet tChunk: return new RawData() { Chunk = tChunk, ViewContent = this };
                case MessageData tChunk: return new Message() { Chunk = tChunk, ViewContent = this };
                default: throw new ArgumentException($"Unsupported chunk type: {chunk.GetType()}", nameof(chunk));
            }
        }

        double OverExtentForLayerHeight = 0;
        Size OverExtentForViewport = Size.Empty;
        void AdjustOverExtent()
        {
            double layerHeight = TextLayer.Height + DpiScale.Y; // leave space for highlight border
            if (LastBlockIndex != -1 && (OverExtentForLayerHeight != layerHeight || OverExtentForViewport != Metrics.Viewport))
            {
                OverExtentForLayerHeight = layerHeight;
                OverExtentForViewport = Metrics.Viewport;

                if (LastBlockIndex == Session.Storage.BlockIterator.EndingBlockIndex)
                {
                    // we built last block in file
                    if (layerHeight > Metrics.Viewport.Height)
                    {
                        var frag = TextLayer.FindFragmentAtOffset(layerHeight - Metrics.Viewport.Height + LineHeight);
                        if (frag != -1)
                        {
                            var lastPageFrag = TextLayer.Items[frag];
                            double lastLine = Metrics.GetLineFromOffset(lastPageFrag.ApproxFileOffset);
                            double over = Metrics.LinesPerPage - (Metrics.LinesCountApprox - lastLine);
                            SetOverExtentLines(over);
                            //System.Diagnostics.Debug.WriteLine($"OverExtent: {over}; Frag: {frag}; ContentH: {layerHeight}; WinH: {Metrics.Viewport.Height}; Items: {TextLayer.Items.Count}" );
                        }
                    }
                    else
                    {
                        SetOverExtentLines(0);
                    }
                }
            }

            ShrinkTailBlankSpace(layerHeight);
        }

        void ShrinkTailBlankSpace(double layerHeight) // shrink empty space below layer
        {
            var maxScrollY = SnapToPixelsY(Metrics.Viewport.Height - layerHeight);
            var extentHeight = View.ExtentHeight;
            if (maxScrollY < 0)
            {
                if (View.ScrollTransform.Y < maxScrollY || View.VerticalOffset >= extentHeight - View.ViewportHeight)
                {
                    View.ScrollTransform.Y = maxScrollY;
                    Metrics.CurrentLineIndex = extentHeight - View.ViewportHeight;
                    Metrics.CurrentFileOffset = Metrics.MaxFileLength;
                }
            }
            else
            {
                View.ScrollTransform.Y = 0;
                Metrics.CurrentLineIndex = 0;
                Metrics.CurrentFileOffset = Metrics.StartFileOffset;
            }
        }

        void SetOverExtentLines(double overExtentLines)
        {
            Metrics.OverExtentLines = overExtentLines;
            View.UpdateScrollInfo = true;
        }

        private void RemoveChunksOutOfRange(long firstBlockIndex, long lastBlockIndex)
        {
            if (firstBlockIndex == -1) return;

            foreach (var key in Chunks.Keys.ToArray())
            {
                var idx = Chunks[key].BlockIndex;
                if (idx < firstBlockIndex || idx > lastBlockIndex)
                    Chunks.Remove(key);
            }

            LastAppendedBlockIndex = lastBlockIndex;
        }

        public bool ShouldArrange(long offset)
        {
            if (FirstBlockIndex == -1 || LastBlockIndex == -1)
                return true; // maybe is visible
            if (Metrics.Viewport.Height > TextLayer.Height)
                return true; // cannot scroll yet

            var blockIndex = offset / Constants.BlockSize;

            return FirstBlockIndex <= blockIndex && blockIndex <= LastBlockIndex;
        }

        public ChunkViewItem TryGetChunkView(long sequenceNumber)
        {
            Chunks.TryGetValue(sequenceNumber, out var chunkViewItem);
            return chunkViewItem;
        }

        private double GetScrollPosition(ChunkViewItem chunkView)
        {
            if (chunkView.FirstFragmentIndex == -1) // chunk doesn't have any fragments
            {
                return chunkView.Location.Y;
            }
            else
            {
                // search for first visible fragment
                var visibleFragmentIndex = ~TextLayer.Items.BinarySearch(
                    index: chunkView.FirstFragmentIndex,
                    count: chunkView.LastFragmentIndex - chunkView.FirstFragmentIndex + 1,
                    item: null,
                    comparer: FirstVisibleFragmentComparer);

                if (visibleFragmentIndex > chunkView.FirstFragmentIndex)
                    visibleFragmentIndex--;

                // scroll to first visible line based on CurrentFileOffset
                return TextLayer.Items[visibleFragmentIndex].Bounds.Y;
            }
        }

        public void SetSelection(Packet start, Packet end)
        {
            SelectionLayer.ResetSelection();
            SelectionStart = new SelectionAnchor() { Chunk = start };
            SelectionEnd = new SelectionAnchor() { Chunk = end, Offset = end.Payload.Length };
            SelectionLayer.AdjustSelectionAnchors();
        }

        private readonly FirstVisibleFragmentComparerImpl _firstVisibleFragmentComparer = new FirstVisibleFragmentComparerImpl();
        IComparer<Fragment> FirstVisibleFragmentComparer
        {
            get
            {
                _firstVisibleFragmentComparer.CurrentFileOffset = Metrics.CurrentFileOffset;
                return _firstVisibleFragmentComparer;
            }
        }

        class FirstVisibleFragmentComparerImpl : IComparer<Fragment>
        {
            public long CurrentFileOffset = 0;
            public int Compare(Fragment x, Fragment _) => x.ApproxFileOffset < CurrentFileOffset ? -1 : 1;
        }
    }
}
