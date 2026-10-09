using System;
using System.Windows;

namespace Lan.Shapes.Utilities
{
    internal static class GeometryEditValidation
    {
        public static void EnsureEditable(ShapeVisualBase shape)
        {
            shape.VerifyAccess();
            if (!shape.IsGeometryRendered || shape.IsLocked)
                throw new InvalidOperationException("Only completed, unlocked shapes can be edited.");
        }

        public static void EnsureFinite(Point point, string parameterName)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
                throw new ArgumentOutOfRangeException(parameterName, "Coordinates must be finite.");
        }

        public static void EnsureBounds(Rect bounds, string parameterName)
        {
            if (bounds.IsEmpty || !double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y)
                || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)
                || bounds.Width <= 0 || bounds.Height <= 0
                || !double.IsFinite(bounds.Right) || !double.IsFinite(bounds.Bottom)
                || !double.IsFinite(bounds.Right - bounds.Left) || !double.IsFinite(bounds.Bottom - bounds.Top)
                || bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top)
                throw new ArgumentOutOfRangeException(parameterName, "Bounds must have finite coordinates and positive, representable dimensions.");
        }

        public static double GetLength(Vector vector)
        {
            var maximum = Math.Max(Math.Abs(vector.X), Math.Abs(vector.Y));
            if (maximum == 0) return 0;
            var ratio = Math.Min(Math.Abs(vector.X), Math.Abs(vector.Y)) / maximum;
            return maximum * Math.Sqrt(1 + ratio * ratio);
        }
    }
}
