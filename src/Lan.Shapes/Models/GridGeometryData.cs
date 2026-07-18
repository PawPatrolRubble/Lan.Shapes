#nullable enable

using System.Windows;
using Lan.Shapes.Interfaces;

namespace Lan.Shapes.Models
{
    /// <summary>
    /// Serializable state for <c>GridGeometry</c>: bounding corners plus grid density.
    /// </summary>
    public class GridGeometryData : IGeometryMetaData
    {
        public Point TopLeft { get; set; }
        public Point BottomRight { get; set; }
        public int RowCount { get; set; } = 1;
        public int ColumnCount { get; set; } = 1;
        public double StrokeThickness { get; set; } = 1;
        public string? Tag { get; set; }
    }
}
