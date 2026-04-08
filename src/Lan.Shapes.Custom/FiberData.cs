using Lan.Shapes.Interfaces;
using System.Windows;

namespace Lan.Shapes.Custom
{
    /// <summary>
    /// Metadata for Fiber shape serialization and deserialization.
    /// Contains the minimal state required to reconstruct a fiber geometry.
    /// </summary>
    public class FiberData : IGeometryMetaData
    {
        /// <summary>
        /// The stroke thickness for rendering.
        /// </summary>
        public double StrokeThickness { get; } = 1.0;

        /// <summary>
        /// The center point of the fillet circle at the triangle apex.
        /// </summary>
        public Point FilletCenter { get; set; }

        /// <summary>
        /// The rotation angle of the fiber in degrees.
        /// </summary>
        public double FiberAngleInDeg { get; set; }

        /// <summary>
        /// The radius of the fillet circle.
        /// </summary>
        public double FilletRadius { get; set; }

        /// <summary>
        /// The width of the fiber rectangle.
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// The height of the fiber rectangle.
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// Whether the fiber shape can be translated.
        /// </summary>
        public bool EnableTranslation { get; set; } = true;
    }
}