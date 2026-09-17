using System.Windows;
using Lan.Shapes.Interfaces;

namespace Lan.Shapes.Models
{
    public class RulerCrossData : IGeometryMetaData
    {
        public Point Center { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double StrokeThickness { get; set; } = 1;
    }
}
