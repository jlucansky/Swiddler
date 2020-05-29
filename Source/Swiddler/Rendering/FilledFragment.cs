using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Swiddler.Rendering
{
    public class FilledFragment : DrawingFragment
    {
        public Brush Brush { get; set; }
        public Pen Pen { get; set; }

        public PolygonFigure[] Polgyons { get; set; }

        public override void OnRender(DrawingContext drawingContext, Rect bounds)
        {
            if (Polgyons != null)
            {
                var offset = new Vector();

                if (Pen != null)
                {
                    // snap to pixels
                    var half = Pen.Thickness * .5;
                    offset.X -= half;
                    offset.Y -= half;
                }

                StreamGeometry streamGeometry = new StreamGeometry();
                using (StreamGeometryContext geometryContext = streamGeometry.Open())
                {
                    foreach (var absPoly in Polgyons)
                    {
                        var poly = absPoly - offset;
                        geometryContext.BeginFigure(poly.StartPoint, Brush != null, true);
                        geometryContext.PolyLineTo(poly.Points, Pen != null, true);
                    }
                }

                drawingContext.DrawGeometry(Brush, Pen, streamGeometry);
            }
        }

        public FilledFragment() { }

        public FilledFragment(Point start, Point end, double lineHeight, double width)
        {
            Polgyons = CreatePolygons(start, end, lineHeight, width, out var rect);
            Bounds = rect;
        }

        protected override bool HitTest(Point point)
        {
            return Polgyons?.Any(x => x.Contains(point)) == true;
        }

        public static PolygonFigure[] CreatePolygons(Point start, Point end, double lineHeight, double width, out Rect rect)
        {
            /*
             start = A2, end = B1
                       
                    A2          A3
                    +-----------+
            B4      |           |
            +-------+A1         |
            |                   |
            |         B1+-------+
            |           |       A4
            +-----------+ 
            B3          B2
            
            |<----- width ----->|

            */

            Point
                a1 = new Point(start.X, start.Y + lineHeight),
                a2 = start,
                a3 = new Point(width, a2.Y),
                a4 = new Point(a3.X, end.Y),

                b1 = end,
                b2 = new Point(b1.X, end.Y + lineHeight),
                b3 = new Point(0, b2.Y),
                b4 = new Point(b3.X, a1.Y);

            if (a1.Y == b1.Y && a1.X >= b1.X) // two separate rectangles
            {
                rect = new Rect(b3.X, a3.Y, a4.X - b4.X, b2.Y - a2.Y);
                return new[] {
                    new PolygonFigure(a1, a2, a3, a4) { HitRects = new[] {new Rect(a2, a4)} },
                    new PolygonFigure(b1, b2, b3, b4) { HitRects = new[] {new Rect(b4, b2)} },
                };
            }
            else if (a1.Y > b1.Y) // single rectangle
            {
                rect = new Rect(a2, b2);
                return new[] { new PolygonFigure(a1, a2, b1, b2) { HitRects = new[] { new Rect(a2, b2) } } };
            }
            else
            {
                bool joinStart = a1 == b4, joinEnd = b1 == a4;
                
                if (joinStart && joinEnd)
                {
                    rect = new Rect(a2, b2);
                    return new[] { new PolygonFigure(a2, a3, b2, b3) { HitRects = new[] {
                        new Rect(a2, b2)
                    } } };
                }
                else if (!joinStart && joinEnd)
                {
                    rect = new Rect(b3.X, a3.Y, a4.X - b4.X, b2.Y - a2.Y);
                    return new[] { new PolygonFigure(a1, a2, a3, b2, b3, b4) { HitRects = new[] {
                        new Rect(a2.X, a2.Y, a3.X - a2.X, a1.Y - a2.Y),
                        new Rect(b4, b2)
                    } } };
                }
                else if (joinStart && !joinEnd)
                {
                    rect = new Rect(a2.X, a2.Y, a3.X - a2.X, b2.Y - a2.Y);
                    return new[] { new PolygonFigure(a2, a3, a4, b1, b2, b3) { HitRects = new[] {
                        new Rect(a2, a4),
                        new Rect(b3.X, b1.Y, b2.X - b3.X, b2.Y - b1.Y),
                    } } };
                }
                else
                {
                    rect = new Rect(b3.X, a3.Y, a4.X - b4.X, b2.Y - a2.Y);
                    return new[] { new PolygonFigure(a1, a2, a3, a4, b1, b2, b3, b4) { HitRects = new[] {
                        new Rect(a2.X, a2.Y, a3.X - a2.X, a1.Y - a2.Y),
                        new Rect(b4, a4),
                        new Rect(b3.X, b1.Y, b2.X - b3.X, b2.Y - b1.Y)
                    } } };
                }
            }

        }

    }
}
