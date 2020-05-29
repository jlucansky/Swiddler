using System.Text;
using System.Windows;
using System.Windows.Media;

namespace Swiddler.Rendering
{
    public class FragmentViewMetrics
    {
        public double CurrentLineIndex { get; set; } // VerticalOffset
        public double LinesCountApprox { get; set; } // update after append
        public long StartFileOffset { get; set; }
        public long CurrentFileOffset { get; set; } // set with SetVerticalOffset/SetFileOffset
        public long MaxFileLength { get; set; } // update after Append
        public double LinesPerPage { get; set; } // set on MeasureOverride
        public double OverExtentLines { get; set; }
        public Size Viewport { get; set; }
        public Encoding Encoding { get; set; }
        public Typeface Typeface { get; set; }
        public double FontSize { get; set; }

        public long UsableFileLength => MaxFileLength - StartFileOffset;

        public double GetLineFromOffset (long fileOffset) => (fileOffset - StartFileOffset) / (double)UsableFileLength * LinesCountApprox;
        public long GetOffsetFromLine(double lineIndex) => (long)(lineIndex / LinesCountApprox * UsableFileLength) + StartFileOffset;

        public bool InViewport(Point point) => point.X >= 0 && point.Y >= 0 && point.X < Viewport.Width && point.Y < Viewport.Height;
    }
}
