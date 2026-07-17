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
    /// Style and units profile for shapes drawn on a sketch board.
    /// <para>
    /// A layer does <b>not</b> own shape instances — ownership lives on
    /// <see cref="Interfaces.IShapeRepository"/>. Shapes hold a reference to a layer only
    /// to resolve <see cref="IShapeStyler"/> values for their current
    /// <see cref="ShapeVisualState"/>.
    /// </para>
    /// Layer definitions are typically loaded from app configuration JSON.
    /// </summary>
    public class ShapeLayer
    {
        /// <summary>States that must be present in a layer configuration.</summary>
        public static readonly ShapeVisualState[] RequiredStylerStates =
        {
            ShapeVisualState.Normal,
            ShapeVisualState.Selected
        };

        /// <summary>States recommended for full interaction styling (hover / lock).</summary>
        public static readonly ShapeVisualState[] RecommendedStylerStates =
        {
            ShapeVisualState.MouseOver,
            ShapeVisualState.Locked
        };

        private readonly Dictionary<ShapeVisualState, IShapeStyler> _stylers;

        /// <summary>Stylers keyed by visual state. Mutated at runtime for zoom scale only.</summary>
        public Dictionary<ShapeVisualState, IShapeStyler> Stylers => _stylers;

        /// <summary>Pixel count per logical unit used by measurement helpers.</summary>
        public double PixelPerUnit { get; set; } = 1;

        /// <summary>How many logical units equal 1 mm.</summary>
        public int UnitsPerMillimeter { get; set; } = 1;

        public int LayerId { get; }
        public string Name { get; }
        public string Description { get; }
        public int MaximumThickenedShapeWidth { get; set; }
        public int TagFontSize { get; set; }
        public string UnitName { get; set; }

        public Brush TextForeground { get; } = Brushes.Black;
        public Brush BorderBackground { get; } = Brushes.LightBlue;

        /// <summary>
        /// Builds a layer from configuration using the default <see cref="ShapeStylerFactory"/>.
        /// Requires at least <see cref="ShapeVisualState.Normal"/> and
        /// <see cref="ShapeVisualState.Selected"/> stylers in
        /// <see cref="ShapeLayerParameter.StyleSchema"/>.
        /// </summary>
        public ShapeLayer(ShapeLayerParameter shapeLayerParameter)
            : this(shapeLayerParameter, new ShapeStylerFactory())
        {
        }

        /// <summary>
        /// Builds a layer from configuration, creating stylers via
        /// <paramref name="stylerFactory"/> (substitutable for tests/themes).
        /// </summary>
        public ShapeLayer(ShapeLayerParameter shapeLayerParameter, IShapeStylerFactory stylerFactory)
        {
            if (shapeLayerParameter == null)
            {
                throw new ArgumentNullException(nameof(shapeLayerParameter));
            }

            if (stylerFactory == null)
            {
                throw new ArgumentNullException(nameof(stylerFactory));
            }

            LayerId = shapeLayerParameter.LayerId;
            Name = shapeLayerParameter.Name;
            Description = shapeLayerParameter.Description;
            MaximumThickenedShapeWidth = shapeLayerParameter.MaximumThickenedShapeWidth;
            TagFontSize = shapeLayerParameter.TagFontSize;
            UnitName = shapeLayerParameter.UnitName ?? string.Empty;
            BorderBackground = shapeLayerParameter.BorderBackground;
            TextForeground = shapeLayerParameter.TextForeground;
            UnitsPerMillimeter = shapeLayerParameter.UnitsPerMillimeter;
            PixelPerUnit = shapeLayerParameter.PixelPerUnit;

            var schema = shapeLayerParameter.StyleSchema
                ?? throw new InvalidOperationException(
                    $"Layer '{Name}' (id {LayerId}) has no StyleSchema.");

            EnsureRequiredStylerStates(schema, Name, LayerId);

            _stylers = new Dictionary<ShapeVisualState, IShapeStyler>(
                schema.Select(x => new KeyValuePair<ShapeVisualState, IShapeStyler>(
                    x.Key,
                    stylerFactory.CreateStyler(x.Value))));
        }

        /// <summary>
        /// Returns the styler for <paramref name="shapeState"/>, falling back to
        /// <see cref="ShapeVisualState.Normal"/> when the exact state is missing.
        /// </summary>
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

            throw new InvalidOperationException(
                $"No styler configured for state '{shapeState}' and no fallback '{ShapeVisualState.Normal}' styler is available.");
        }

        public ShapeLayerParameter ToShapeLayerParameter()
        {
            return new ShapeLayerParameter
            {
                LayerId = LayerId,
                BorderBackground = BorderBackground,
                Description = Description,
                Name = Name,
                MaximumThickenedShapeWidth = MaximumThickenedShapeWidth,
                TagFontSize = TagFontSize,
                UnitsPerMillimeter = UnitsPerMillimeter,
                PixelPerUnit = (int)PixelPerUnit,
                UnitName = UnitName,
                TextForeground = TextForeground,
                StyleSchema = new Dictionary<ShapeVisualState, ShapeStylerParameter>(
                    _stylers.Select(x => new KeyValuePair<ShapeVisualState, ShapeStylerParameter>(
                        x.Key,
                        x.Value.ToStylerParameter())))
            };
        }

        /// <summary>
        /// Returns a new layer with the same configuration but independent
        /// <see cref="IShapeStyler"/> instances. Use when a board/manager must
        /// mutate stylers for zoom without affecting other viewers that share
        /// the original config layer.
        /// </summary>
        public ShapeLayer CreateIndependentCopy()
        {
            return new ShapeLayer(ToShapeLayerParameter());
        }

        /// <summary>
        /// Fail-fast validation used by construction and by layer loaders.
        /// Requires <see cref="RequiredStylerStates"/>; missing recommended states are allowed
        /// (runtime falls back to Normal via <see cref="GetStyler"/>).
        /// </summary>
        public static void EnsureRequiredStylerStates(
            IReadOnlyDictionary<ShapeVisualState, ShapeStylerParameter> styleSchema,
            string? layerName = null,
            int? layerId = null)
        {
            if (styleSchema == null)
            {
                throw new ArgumentNullException(nameof(styleSchema));
            }

            var missing = RequiredStylerStates.Where(s => !styleSchema.ContainsKey(s)).ToArray();
            if (missing.Length == 0)
            {
                return;
            }

            var identity = layerId.HasValue
                ? $"Layer '{layerName}' (id {layerId})"
                : "Shape layer";
            throw new InvalidOperationException(
                $"{identity} StyleSchema is missing required state(s): {string.Join(", ", missing)}. " +
                $"Required: {string.Join(", ", RequiredStylerStates)}.");
        }
    }
}
