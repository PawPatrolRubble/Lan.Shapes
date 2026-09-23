using System.Collections.Generic;
using System.Windows.Media;
using Lan.Shapes.Converters;
using Lan.Shapes.Enums;
using Lan.Shapes.Styler;
using Newtonsoft.Json;

namespace Lan.Shapes
{
    public class ShapeLayerParameter
    {
        public Dictionary<ShapeVisualState, ShapeStylerParameter> StyleSchema { get; set; }
        public int LayerId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int MaximumThickenedShapeWidth { get; set; }
        public int TagFontSize { get; set; }
        public double AnnotationFontToHandleRatio { get; set; } = 1.5;

        [JsonConverter(typeof(BrushToHexConverter))]
        public Brush TextForeground { get; set; }

        [JsonConverter(typeof(BrushToHexConverter))]
        public Brush BorderBackground { get; set; }
    }
}
