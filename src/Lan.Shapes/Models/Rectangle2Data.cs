#nullable enable

using Lan.Shapes.Enums;
using Lan.Shapes.Interfaces;

namespace Lan.Shapes.Models
{
    /// <summary>
    /// HALCON rectangle2 parameters. Row/Column are the center coordinates,
    /// Phi is the orientation in radians, and Length1/Length2 are half-edge
    /// lengths along the two local axes.
    /// </summary>
    public sealed class Rectangle2Data : IGeometryMetaData
    {
        public double Row { get; set; }
        public double Column { get; set; }
        public double Phi { get; set; }
        public double Length1 { get; set; }
        public double Length2 { get; set; }
        public double StrokeThickness { get; set; }
        public string? Tag { get; set; }
        public TagPosition TagPosition { get; set; } = TagPosition.Center;
    }
}
