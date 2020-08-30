using System;
using System.Collections.Generic;
using System.Text;

namespace Swiddler.Serialization.Rtf
{
    /// <summary>
    /// A container for an array of content blocks. For example, a footnote
    /// is a RtfBlockList because it may contains a paragraph and an image.
    /// </summary>
    public class RtfBlockList : RtfRenderable
    {
        /// <summary>
        /// Storage for array of content blocks.
        /// </summary>
        protected List<RtfBlock> _blocks;
        /// <summary>
        /// Default character formats within this container.
        /// </summary>
        protected RtfCharFormat _defaultCharFormat;
        
        private bool _allowParagraph;
        private bool _allowFootnote;
        private bool _allowControlWord;
        
        /// <summary>
        /// Internal use only.
        /// Default constructor that allows containing all types of content blocks.
        /// </summary>
        internal RtfBlockList()
            : this(true, true, true)
        {
        }
        
        /// <summary>
        /// Internal use only.
        /// Constructor specifying allowed content blocks to be contained.
        /// </summary>
        /// <param name="allowParagraph">Whether an RtfParagraph is allowed.</param>
        internal RtfBlockList(bool allowParagraph)
            : this(allowParagraph, true, true)
        {
        }

        /// <summary>
        /// Internal use only.
        /// Constructor specifying allowed content blocks to be contained.
        /// </summary>
        /// <param name="allowParagraph">Whether an RtfParagraph is allowed.</param>
        /// <param name="allowFootnote">Whether an RtfFootnote is allowed in contained RtfParagraph.</param>
        /// <param name="allowControlWord">Whether an field control word is allowed in contained RtfParagraph.</param>
        internal RtfBlockList(bool allowParagraph, bool allowFootnote, bool allowControlWord)
        {
            _blocks = new List<RtfBlock>();
            _allowParagraph = allowParagraph;
            _allowFootnote = allowFootnote;
            _allowControlWord = allowControlWord;
            _defaultCharFormat = null;
        }
        
        /// <summary>
        /// Get default character formats within this container.
        /// </summary>
        public RtfCharFormat DefaultCharFormat
        {
            get
            {
                if (_defaultCharFormat == null) {
                    _defaultCharFormat = new RtfCharFormat(-1, -1, 1);
                }
                return _defaultCharFormat;
            }
        }

        private void addBlock(RtfBlock block)
        {
            if (block != null) {
                _blocks.Add(block);
            }
        }
        
        /// <summary>
        /// Add a paragraph to this container.
        /// </summary>
        /// <returns>Paragraph being added.</returns>
        public RtfParagraph addParagraph()
        {
            if (!_allowParagraph) {
                throw new Exception("Paragraph is not allowed.");
            }
            RtfParagraph block = new RtfParagraph(_allowFootnote, _allowControlWord);
            addBlock(block);
            return block;
        }

        /// <summary>
        /// Internal use only.
        /// Emit RTF code.
        /// </summary>
        /// <returns>Resulting RTF code for this object.</returns>
        public override string render()
        {
            StringBuilder result = new StringBuilder();
            
            result.AppendLine();
            for (int i = 0; i < _blocks.Count; i++) {
                if (_defaultCharFormat != null && _blocks[i].DefaultCharFormat != null) {
                    _blocks[i].DefaultCharFormat.copyFrom(_defaultCharFormat);
                }
                result.AppendLine(_blocks[i].render());
            }
            return result.ToString();
        }
    }
}
