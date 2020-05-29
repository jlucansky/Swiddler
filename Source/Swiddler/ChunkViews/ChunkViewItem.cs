using Swiddler.IO;
using Swiddler.Rendering;
using System;
using System.Windows;

namespace Swiddler.ChunkViews
{
    public abstract class ChunkViewItem
    {
        public int FirstFragmentIndex { get; set; } = -1;
        public int LastFragmentIndex { get; set; } = -1;

        public Point Location { get; set; }

        public long BlockIndex { get; set; }

        public FragmentViewContent ViewContent { get; set; }

        public abstract IDataChunk BaseChunk { get; }

        /// <summary>
        /// Append fragments to <see cref="ViewContent"/>
        /// </summary>
        public abstract void Build();

        /// <summary>
        /// Method should resolve missing properties <see cref="SelectionAnchorCaret.Fragment"/> or <see cref="SelectionAnchorCaret.Anchor"/> and update <see cref="SelectionAnchorCaret.Bounds"/>.
        /// </summary>
        public virtual void PrepareSelectionAnchor(SelectionAnchorCaret caret) => throw new NotImplementedException();
    }

    public abstract class ChunkViewItem<TDataChunk> : ChunkViewItem 
        where TDataChunk : IDataChunk
    {
        public sealed override IDataChunk BaseChunk => Chunk;

        public TDataChunk Chunk { get; set; }
    }
}
