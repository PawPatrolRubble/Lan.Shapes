using System.Windows;
using Lan.Shapes.Interfaces;

namespace Lan.Shapes.Models
{
    public class TextGeometryData : IGeometryMetaData
    {
        public Point Location { get; set; }
        public double FontSize { get; set; }

        /// <summary>
        /// content
        /// </summary>
        public string Content { get; set; }

        /// <summary>Initializes a new instance of the <see cref="T:System.Object" /> class.</summary>
        public TextGeometryData(Point location, string textContent, double fontSize)
        {
            Location = location;
            Content = textContent;
            FontSize = fontSize;
        }

        public double StrokeThickness { get; set; } = 10;
    }
}
