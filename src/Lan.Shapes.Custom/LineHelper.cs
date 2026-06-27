using System;
using System.Numerics;
using System.Windows;
using Vector = System.Windows.Vector;

namespace Lan.Shapes.Custom
{
    public static class LineHelper
    {
        #region properties        
        public static double Length(Point start, Point end)
        {
            return Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
        }

        public static double Angle(Point start, Point end)
        {
            return Math.Atan2(end.Y - start.Y, end.X - start.X);
        }

        #endregion

        #region other members

        public static bool TryGetIntersection(
            Vector2 p1, Vector2 p2,
            Vector2 p3, Vector2 p4,
            out Vector2 intersection)
        {
            intersection = default;

            float x1 = p1.X, y1 = p1.Y;
            float x2 = p2.X, y2 = p2.Y;
            float x3 = p3.X, y3 = p3.Y;
            float x4 = p4.X, y4 = p4.Y;

            var denominator = (x1 - x2) * (y3 - y4) -
                              (y1 - y2) * (x3 - x4);

            if (Math.Abs(denominator) < 1e-6)
            {
                // Lines are parallel or coincident
                return false;
            }

            var pre = x1 * y2 - y1 * x2;
            var post = x3 * y4 - y3 * x4;

            var x = (pre * (x3 - x4) - (x1 - x2) * post) / denominator;
            var y = (pre * (y3 - y4) - (y1 - y2) * post) / denominator;

            intersection = new Vector2(x, y);
            return true;
        }

        public static Point GetIntersectionWithLine(Point start, Point end, Point lineStart, Point lineEnd)
        {
            TryGetIntersection(new Vector2((float)start.X, (float)start.Y), new Vector2((float)end.X, (float)end.Y),
                new Vector2((float)lineStart.X, (float)lineStart.Y),
                new Vector2((float)lineEnd.X, (float)lineEnd.Y), out var intersection);
            return new Point(intersection.X, intersection.Y);
        }

        public static double GetAngleBetweenLines(Point start, Point end)
        {
            return Math.Atan2(end.Y - start.Y, end.X - start.X);
        }

        public static (Point, Point) GetPerpendicularLineThroughPoint(Point lineStart, Point lineEnd, Point point, double length = 200)
        {
            Vector direction = new Vector(lineEnd.X - lineStart.X, lineEnd.Y - lineStart.Y);
            if (direction.Length > 0)
            {
                direction.Normalize();
            }
            else
            {
                return (point, point);
            }
            Vector perpendicular = new Vector(direction.Y, -direction.X);
            perpendicular *= length / 2;
            Point start = new Point(point.X - perpendicular.X, point.Y - perpendicular.Y);
            Point end = new Point(point.X + perpendicular.X, point.Y + perpendicular.Y);
            return (start, end);
        }

        public static (Point, Point) GetParallelLineThroughPoint(Point lineStart, Point lineEnd, Point point)
        {
            Vector direction = new Vector(lineEnd.X - lineStart.X, lineEnd.Y - lineStart.Y);
            if (direction.Length < 1e-6)
            {
                return (point, point);
            }
            double originalLength = direction.Length;
            direction.Normalize();
            direction *= originalLength / 2;
            Point start = new Point(point.X - direction.X, point.Y - direction.Y);
            Point end = new Point(point.X + direction.X, point.Y + direction.Y);
            return (start, end);
        }
        #endregion
    }
}