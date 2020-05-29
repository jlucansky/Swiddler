
using System.Windows;
using System.Linq;

namespace Swiddler.Rendering
{
    public class PolygonFigure
    {
        public Point StartPoint;
        public Point[] Points;

        public Rect[] HitRects { get; set; }

        public PolygonFigure(Point startPoint, params Point[] points)
        {
            StartPoint = startPoint;
            Points = points;
        }

        public static PolygonFigure operator +(PolygonFigure polygon, Vector vector)
        {
            var oldPoints = polygon.Points;
            var len = oldPoints.Length;
            var newPoints = new Point[len];

            for (int i = 0; i < len; i++)
                newPoints[i] = oldPoints[i] + vector;

            return new PolygonFigure(polygon.StartPoint + vector, newPoints);
        }

        public static PolygonFigure operator -(PolygonFigure polygon, Vector vector)
        {
            var oldPoints = polygon.Points;
            var len = oldPoints.Length;
            var newPoints = new Point[len];

            for (int i = 0; i < len; i++)
                newPoints[i] = oldPoints[i] - vector;

            return new PolygonFigure(polygon.StartPoint - vector, newPoints);
        }

        public bool Contains(Point point)
        {
            return HitRects?.Any(x => x.Contains(point)) == true;
        }
    }
}
