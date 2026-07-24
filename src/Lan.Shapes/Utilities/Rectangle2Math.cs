#nullable enable

using System;
using System.Windows;

namespace Lan.Shapes.Utilities
{
    /// <summary>
    /// Pure coordinate calculations shared by the Rectangle2 shape and tests.
    /// WPF's X/Y coordinates map to HALCON's Column/Row coordinates.
    /// </summary>
    public static class Rectangle2Math
    {
        public const double MinimumHalfLength = 0.000001;

        public static Vector Axis1(double phi)
        {
            return new Vector(Math.Cos(phi), Math.Sin(phi));
        }

        public static Vector Axis2(double phi)
        {
            return new Vector(-Math.Sin(phi), Math.Cos(phi));
        }

        public static Point[] GetCorners(Point center, double phi, double length1, double length2)
        {
            var axis1 = Axis1(phi);
            var axis2 = Axis2(phi);

            return new[]
            {
                center - axis1 * length1 - axis2 * length2,
                center + axis1 * length1 - axis2 * length2,
                center + axis1 * length1 + axis2 * length2,
                center - axis1 * length1 + axis2 * length2
            };
        }

        public static Point FromLocal(Point center, double phi, double localX, double localY)
        {
            var axis1 = Axis1(phi);
            var axis2 = Axis2(phi);
            return center + axis1 * localX + axis2 * localY;
        }

        public static Vector ToLocal(Point point, Point center, double phi)
        {
            var relative = point - center;
            var axis1 = Axis1(phi);
            var axis2 = Axis2(phi);
            return new Vector(
                Vector.Multiply(relative, axis1),
                Vector.Multiply(relative, axis2));
        }

        /// <summary>
        /// Normalizes the rectangle orientation to HALCON's canonical range
        /// (-pi/2, pi/2]. A rectangle is unchanged by adding pi to Phi.
        /// </summary>
        public static double NormalizePhi(double phi)
        {
            var normalized = phi % Math.PI;
            if (normalized <= -Math.PI / 2)
            {
                normalized += Math.PI;
            }
            else if (normalized > Math.PI / 2)
            {
                normalized -= Math.PI;
            }

            return normalized;
        }
    }
}
