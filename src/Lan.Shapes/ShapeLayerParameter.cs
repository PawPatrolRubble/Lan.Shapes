using System;
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

        /// <summary>Validates a definition and returns an independent, normalized draft.</summary>
        public ShapeLayerParameter CreateValidatedCopy()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("A layer name is required.", nameof(Name));
            if (MaximumThickenedShapeWidth < 0 || TagFontSize < 0)
                throw new ArgumentOutOfRangeException(nameof(MaximumThickenedShapeWidth),
                    "Layer widths and font sizes must not be negative.");
            if (!double.IsFinite(AnnotationFontToHandleRatio) || AnnotationFontToHandleRatio <= 0)
                throw new ArgumentOutOfRangeException(nameof(AnnotationFontToHandleRatio));

            ShapeLayer.EnsureRequiredStylerStates(StyleSchema, Name, LayerId);
            var schema = new Dictionary<ShapeVisualState, ShapeStylerParameter>();
            foreach (var entry in StyleSchema)
            {
                var style = entry.Value;
                if (style == null || !double.IsFinite(style.StrokeThickness) || style.StrokeThickness < 0
                    || !double.IsFinite(style.FillOpacity) || style.FillOpacity < 0 || style.FillOpacity > 1
                    || !double.IsFinite(style.DragHandleSize) || style.DragHandleSize < 0
                    || style.StrokeColor == null || style.FillColor == null)
                    throw new ArgumentException("Layer colors, widths, handle sizes and opacity must be valid.");
                if (entry.Key == ShapeVisualState.Normal && style.StrokeThickness <= 0)
                    throw new ArgumentException("Normal-state stroke thickness must be greater than zero.");
                schema.Add(entry.Key, new ShapeStylerParameter
                {
                    StrokeColor = style.StrokeColor.Clone(),
                    FillColor = style.FillColor.Clone(),
                    StrokeThickness = style.StrokeThickness,
                    DragHandleSize = style.DragHandleSize,
                    FillOpacity = style.FillOpacity,
                    DashStyle = ShapeStyler.NormalizeDashStyle(style.DashStyle)
                });
            }

            return new ShapeLayerParameter
            {
                LayerId = LayerId,
                Name = Name.Trim(),
                Description = Description ?? string.Empty,
                MaximumThickenedShapeWidth = MaximumThickenedShapeWidth,
                TagFontSize = TagFontSize,
                AnnotationFontToHandleRatio = AnnotationFontToHandleRatio,
                TextForeground = TextForeground?.Clone(),
                BorderBackground = BorderBackground?.Clone(),
                StyleSchema = schema
            };
        }
    }
}
