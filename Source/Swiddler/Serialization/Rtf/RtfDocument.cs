using System.Collections.Generic;
using System.Text;

namespace Swiddler.Serialization.Rtf
{
    /// <summary>
    /// Summary description for RtfDocument
    /// </summary>
    public class RtfDocument : RtfBlockList
    {
        private List<string> _fontTable;
        private List<RtfColor> _colorTable;
        
        public RtfDocument()
        {
            _fontTable = new List<string>();
            _fontTable.Add(DefaultValue.Font);
            _colorTable = new List<RtfColor>();
            _colorTable.Add(new RtfColor());
        }
        
        public FontDescriptor createFont(string fontName)
        {
            if (_fontTable.Contains(fontName)) {
                return new FontDescriptor(_fontTable.IndexOf(fontName));
            }
            _fontTable.Add(fontName);
            return new FontDescriptor(_fontTable.IndexOf(fontName));
        }

        public ColorDescriptor createColor(RtfColor color)
        {
            if (_colorTable.Contains(color)) {
                return new ColorDescriptor(_colorTable.IndexOf(color));
            }
            _colorTable.Add(color);
            return new ColorDescriptor(_colorTable.IndexOf(color));
        }
        
        public override string render()
        {
            StringBuilder rtf = new StringBuilder();
            
            // ---------------------------------------------------
            // Prologue
            // ---------------------------------------------------
            rtf.AppendLine(@"{\rtf1\ansi\deff0");
            rtf.AppendLine();

            // ---------------------------------------------------
            // Insert font table
            // ---------------------------------------------------
            rtf.AppendLine(@"{\fonttbl");
            for (int i = 0; i < _fontTable.Count; i++) {
                rtf.AppendLine(@"{\f" + i + " " + RtfUtility.unicodeEncode(_fontTable[i].ToString()) + ";}");
            }
            rtf.AppendLine("}");
            rtf.AppendLine();

            // ---------------------------------------------------
            // Insert color table
            // ---------------------------------------------------
            rtf.AppendLine(@"{\colortbl");
            rtf.AppendLine(";");
            for (int i = 1; i < _colorTable.Count; i++) {
                RtfColor c = _colorTable[i];
                rtf.AppendLine(@"\red" + c.Red + @"\green" + c.Green + @"\blue" + c.Blue + ";");
            }
            rtf.AppendLine("}");
            rtf.AppendLine();
            /*

            // ---------------------------------------------------
            // Preliminary
            // ---------------------------------------------------
            rtf.AppendLine(@"\deflang" + (int)_lcid + @"\plain\fs"
                           + RtfUtility.pt2HalfPt(DefaultValue.FontSize) + @"\widowctrl\hyphauto\ftnbj");
            */

            rtf.AppendLine();

            // ---------------------------------------------------
            // Document body
            // ---------------------------------------------------
            rtf.Append(base.render());
            
            // ---------------------------------------------------
            // Ending
            // ---------------------------------------------------
            rtf.AppendLine("}");
            
            return rtf.ToString();
        }
    }
}
