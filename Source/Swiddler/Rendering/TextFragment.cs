using System.Text;
using System.Windows;
using System.Windows.Media;

namespace Swiddler.Rendering
{
    public class TextFragment : DrawingFragment
    {
        public byte[] Data { get; set; }

        public int Offset { get; set; }
        public int Length { get; set; }

        public Brush Brush { get; set; } = Brushes.Black;

        public Encoding Encoding { get; set; }

        public override void OnRender(DrawingContext drawingContext, Rect bounds)
        {
            if (Length == 0)
                return;

            GlyphTypeface glyphTypeface = View.Content.GlyphTypeface;

            var advanceWidths = new double[Length];
            var glyphIndices = View.Content.GetGlyphIndices(Data, Offset, Length);

            double charWidth = View.Content.CharWidth;
            double fontSize = View.Content.LineHeight - 2; // keep padding

            for (int n = 0; n < Length; n++) advanceWidths[n] = charWidth;

            var baseline = new Point(0, View.Content.SnapToPixelsY(glyphTypeface.Baseline * fontSize + 1.5, ceiling: true));

            GlyphRun run = new GlyphRun(glyphTypeface,
                            bidiLevel: 0,
                            isSideways: false,
                            renderingEmSize: fontSize,
                            glyphIndices: glyphIndices,
                            baselineOrigin: baseline,
                            advanceWidths: advanceWidths,
                            glyphOffsets: null,
                            characters: null,
                            deviceFontName: null,
                            clusterMap: null,
                            caretStops: null,
                            language: null);

            drawingContext.PushTransform(new TranslateTransform(bounds.X, bounds.Y));
            drawingContext.DrawGlyphRun(Brush, run);
            drawingContext.Pop();
        }

        public override string ToString()
        {
            return Encoding.GetString(Data, Offset, Length);
        }

    }
}
