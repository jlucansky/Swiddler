using System.Collections.Generic;
using System.Linq;

namespace Swiddler.IO
{
    public class CachedBlockIterator
    {
        readonly int BlockSize = Constants.BlockSize;

        readonly Dictionary<long, ChunkDictionary> blocks = new Dictionary<long, ChunkDictionary>(); // key is block index (offset/blocksize)

        public BlockReader Reader { get; }

        public long EndingBlockIndex { get; private set; } = -1; // highest valid block in the file

        public CachedBlockIterator(BlockReader reader)
        {
            Reader = reader;
        }

        public IEnumerable<ChunkDictionary> EnumerateBlocks(long fromOffset)
        {
            var blockIndex = fromOffset / BlockSize;

            if (!blocks.TryGetValue(blockIndex, out var block))
                block = ReadBlock(blockIndex);

            while (block != null)
            {
                if (block.IsDirty)
                    RefreshBlock(block);

                yield return block;

                blockIndex++;

                if (!blocks.TryGetValue(blockIndex, out block))
                    block = ReadBlock(blockIndex);
            }
        }

        ChunkDictionary ReadBlock(long blockIndex)
        {
            if (blocks.TryGetValue(blockIndex - 1, out var prevBlock))
            {
                var prevChunkEndOffset = prevBlock.LastChunk.ActualOffset + prevBlock.LastChunk.ActualLength;

                if (prevChunkEndOffset / BlockSize >= blockIndex) // previous chunk overlaps with current block
                    Reader.BaseStream.Position = prevBlock.LastChunk.ActualOffset;
                else
                    Reader.BaseStream.Position = prevChunkEndOffset; // move to the next chunk because previous is ended in previous block

                Reader.Read();
            }
            else
            {
                Reader.Position = blockIndex * BlockSize; // Seek & Read first chunk in block
            }

            ChunkDictionary block = null;

            if (Reader.CurrentChunk != null)
            {
                block = new ChunkDictionary() { FirstSequenceNumber = Reader.CurrentChunk.SequenceNumber, BlockIndex = blockIndex };
                blocks.Add(blockIndex, FillBlock(block));
            }

            if (block != null)
            {
                if (Reader.EndOfStream)
                    EndingBlockIndex = block.BlockIndex;
                else if (blockIndex > EndingBlockIndex)
                    EndingBlockIndex = -1;
            }

            return block;
        }

        private void RefreshBlock(ChunkDictionary block)
        {
            block.IsDirty = false;

            // try read next chunk recently added
            Reader.BaseStream.Position = block.LastChunk.ActualOffset + block.LastChunk.ActualLength;
            Reader.Read();

            FillBlock(block);
        }

        private ChunkDictionary FillBlock(ChunkDictionary block)
        {
            while (Reader.CurrentChunk != null)
            {
                var chunk = Reader.CurrentChunk;

                if (chunk.ActualOffset / BlockSize > block.BlockIndex) break;

                block.Add(chunk.SequenceNumber, chunk);
                block.LastSequenceNumber = chunk.SequenceNumber;

                Reader.Read();
            }

            return block;
        }

        /// <summary>
        /// Clean all blocks not in use.
        /// </summary>
        public void Invalidate()
        {
            foreach (var key in blocks.Keys.ToArray())
            {
                var block = blocks[key];
                if (block.IsValid)
                    block.IsValid = false;
                else
                    blocks.Remove(key);
            }
        }

        internal void FileChanged() // called from Storage after data was written
        {
            if (blocks.TryGetValue(EndingBlockIndex, out var endingBlock)) endingBlock.IsDirty = true;
        }

        public void ClearCache()
        {
            blocks.Clear();
        }
    }
}
