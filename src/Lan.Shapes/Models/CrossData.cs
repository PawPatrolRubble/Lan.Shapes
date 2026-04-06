using System.Windows;
using Lan.Shapes.Interfaces;

namespace Lan.Shapes.Models
{
    public class CrossData : IGeometryMetaData
    {
        public CrossData()
        {
        }

        public int Width { get; set; }
        public int Height { get; set; }
        public Point Center { get; set; }
        public double StrokeThickness { get; set; }
    }
}
