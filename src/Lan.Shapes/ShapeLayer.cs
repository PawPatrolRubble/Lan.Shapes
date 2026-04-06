#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Lan.Shapes.Enums;
using Lan.Shapes.Styler;

namespace Lan.Shapes
{
    /// <summary>
    /// responsible for grouping shapes drawn and setting display style in the group
    /// all shapes must be managed by layer, 
    /// layer information is read from app setting.json
    /// a shape layer will instruct renderContext how to show the geometry contained
    /// all shapes in a shapeLayer shape common ui styles,
    /// like line weight, stroke color, when they are selected, hovered over, etc.
    /// </summary>
    public class ShapeLayer
    {
        #region fields


        /// <summary>
        /// all shape stylers contained in one layer
        /// </summary>
        private Dictionary<ShapeVisualState, IShapeStyler> _stylers = new Dictionary<ShapeVisualState, IShapeStyler>();


        #endregion

        
        #region properties
        
        public Dictionary<ShapeVisualState,IShapeStyler> Stylers
        {
            get => _stylers;
        }



        /// <summary>
        /// pixel per unit
        /// </summary>
        public double PixelPerUnit { get; set; } = 1;

        /// <summary>
        /// unit conversion ratio to mm， defining how many unit is equal to 1 mm
        /// </summary>
        public int  UnitsPerMillimeter { get; set; } = 1;
        public int LayerId { get; }
        public string Name { get; }
        public string Description { get; }
        public int MaximumThickenedShapeWidth { get; set; }
        public int TagFontSize { get; set; }
        public string UnitName { get; set; }


        public Brush TextForeground { get; } = Brushes.Black;
        public Brush BorderBackground { get; } = Brushes.LightBlue;

        
        #endregion


        #region constructor

        private ShapeLayer(int layerId, string name, string description)
        {
            LayerId = layerId;
            Name = name;
            Description = description;
        }

        public ShapeLayer(ShapeLayerParameter shapeLayerParameter)
        {
            LayerId = shapeLayerParameter.LayerId;
            Name = shapeLayerParameter.Name;
            Description = shapeLayerParameter.Description;
            MaximumThickenedShapeWidth = shapeLayerParameter.MaximumThickenedShapeWidth;
            TagFontSize = shapeLayerParameter.TagFontSize;
            UnitName = shapeLayerParameter.UnitName;

            _stylers = new Dictionary<ShapeVisualState, IShapeStyler>(shapeLayerParameter.StyleSchema.Select(x =>
                new KeyValuePair<ShapeVisualState, IShapeStyler>(x.Key, new ShapeStyler(x.Value))));

            BorderBackground = shapeLayerParameter.BorderBackground;
            TextForeground = shapeLayerParameter.TextForeground;
            UnitsPerMillimeter = shapeLayerParameter.UnitsPerMillimeter;
            PixelPerUnit = shapeLayerParameter.PixelPerUnit;
        }

        
        #endregion


        #region public interfaces
        

        #endregion
        
        
        private Brush BrushFromHexString(string hextString)
        {
            var converter = new BrushConverter();
            return (Brush)converter.ConvertFromString(hextString);
        }
        
        

        /// <summary>
        /// get styler based on the state of shape
        /// </summary>
        /// <param name="shapeState"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"> if shapeState is not found</exception>
        public IShapeStyler GetStyler(ShapeVisualState shapeState)
        {
            if (_stylers.TryGetValue(shapeState, out var styler))
            {
                return styler;
            }

            if (_stylers.TryGetValue(ShapeVisualState.Normal, out styler))
            {
                return styler;
            }

            throw new InvalidOperationException($"No styler configured for state '{shapeState}' and no fallback '{ShapeVisualState.Normal}' styler is available.");
        }

      

        public ShapeLayerParameter ToShapeLayerParameter()
        {
            return new ShapeLayerParameter()
            {
                LayerId = LayerId,
                BorderBackground = BorderBackground,
                Description = Description,
                Name = Name,
                StyleSchema = new Dictionary<ShapeVisualState, ShapeStylerParameter>(_stylers.Select(x => new KeyValuePair<ShapeVisualState, ShapeStylerParameter>(x.Key,x.Value.ToStylerParameter())))
            };
            //throw new NotImplementedException();
        }
    }
}