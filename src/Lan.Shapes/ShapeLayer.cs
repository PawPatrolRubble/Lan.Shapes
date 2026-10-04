#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using Lan.Shapes.Enums;
using Lan.Shapes.Styler;

namespace Lan.Shapes
{
    /// <summary>
    /// Visual style profile for shapes drawn on a sketch board.
    /// <para>
    /// A layer does <b>not</b> own shape instances — ownership lives on
    /// <see cref="Interfaces.IShapeRepository"/>. Shapes hold a reference to a layer only
    /// to resolve <see cref="IShapeStyler"/> values for their current
    /// <see cref="ShapeVisualState"/>.
    /// </para>
    /// Layer definitions and their shared measurement calibration are typically loaded
    /// from <see cref="LanShapesConfiguration"/>.
    /// </summary>
    public class ShapeLayer : INotifyPropertyChanged
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
        private readonly IShapeStylerFactory _stylerFactory;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Stylers keyed by visual state. Mutated at runtime for zoom scale only.</summary>
        public Dictionary<ShapeVisualState, IShapeStyler> Stylers => _stylers;

        /// <summary>Global measurement calibration shared by all configured layers.</summary>
        public ShapeMeasurementSettings Measurement { get; }

        public int LayerId { get; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public int MaximumThickenedShapeWidth { get; set; }
        public int TagFontSize { get; set; }
        /// <summary>Annotation font size as a multiple of the normal drag-handle size.</summary>
        public double AnnotationFontToHandleRatio { get; private set; }

        public Brush TextForeground { get; private set; } = Brushes.Black;
        public Brush BorderBackground { get; private set; } = Brushes.LightBlue;

        /// <summary>
        /// Builds a layer from configuration using the default <see cref="ShapeStylerFactory"/>.
        /// Requires at least <see cref="ShapeVisualState.Normal"/> and
        /// <see cref="ShapeVisualState.Selected"/> stylers in
        /// <see cref="ShapeLayerParameter.StyleSchema"/>.
        /// </summary>
        public ShapeLayer(ShapeLayerParameter shapeLayerParameter)
            : this(
                shapeLayerParameter,
                new ShapeMeasurementSettings(),
                new ShapeStylerFactory())
        {
        }

        /// <summary>
        /// Builds a layer from configuration, creating stylers via
        /// <paramref name="stylerFactory"/> (substitutable for tests/themes).
        /// </summary>
        public ShapeLayer(ShapeLayerParameter shapeLayerParameter, IShapeStylerFactory stylerFactory)
            : this(shapeLayerParameter, new ShapeMeasurementSettings(), stylerFactory)
        {
        }

        public ShapeLayer(
            ShapeLayerParameter shapeLayerParameter,
            ShapeMeasurementSettings measurement,
            IShapeStylerFactory stylerFactory)
        {
            if (shapeLayerParameter == null)
            {
                throw new ArgumentNullException(nameof(shapeLayerParameter));
            }

            if (stylerFactory == null)
            {
                throw new ArgumentNullException(nameof(stylerFactory));
            }

            Measurement = measurement ?? throw new ArgumentNullException(nameof(measurement));
            Measurement.Validate();
            _stylerFactory = stylerFactory;

            LayerId = shapeLayerParameter.LayerId;
            Name = shapeLayerParameter.Name;
            Description = shapeLayerParameter.Description;
            MaximumThickenedShapeWidth = shapeLayerParameter.MaximumThickenedShapeWidth;
            TagFontSize = shapeLayerParameter.TagFontSize;
            if (!double.IsFinite(shapeLayerParameter.AnnotationFontToHandleRatio) ||
                shapeLayerParameter.AnnotationFontToHandleRatio <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(shapeLayerParameter),
                    "AnnotationFontToHandleRatio must be finite and greater than zero.");
            }
            AnnotationFontToHandleRatio = shapeLayerParameter.AnnotationFontToHandleRatio;
            BorderBackground = shapeLayerParameter.BorderBackground;
            TextForeground = shapeLayerParameter.TextForeground;

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
                AnnotationFontToHandleRatio = AnnotationFontToHandleRatio,
                TextForeground = TextForeground,
                StyleSchema = new Dictionary<ShapeVisualState, ShapeStylerParameter>(
                    _stylers.Select(x => new KeyValuePair<ShapeVisualState, ShapeStylerParameter>(
                        x.Key,
                        x.Value.ToStylerParameter())))
            };
        }

        /// <summary>Applies an edited definition while retaining this layer's identity and measurement.</summary>
        public void ApplyConfiguration(ShapeLayerParameter parameter)
        {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));
            if (parameter.LayerId != LayerId)
                throw new ArgumentException("A layer ID cannot be changed while editing a layer.", nameof(parameter));
            if (string.IsNullOrWhiteSpace(parameter.Name))
                throw new ArgumentException("A layer name is required.", nameof(parameter));
            if (!double.IsFinite(parameter.AnnotationFontToHandleRatio) ||
                parameter.AnnotationFontToHandleRatio <= 0)
                throw new ArgumentOutOfRangeException(nameof(parameter));

            var schema = parameter.StyleSchema
                ?? throw new ArgumentException("A layer style schema is required.", nameof(parameter));
            EnsureRequiredStylerStates(schema, parameter.Name, LayerId);
            var stylers = schema.ToDictionary(x => x.Key, x => _stylerFactory.CreateStyler(x.Value));

            Name = parameter.Name.Trim();
            Description = parameter.Description ?? string.Empty;
            MaximumThickenedShapeWidth = parameter.MaximumThickenedShapeWidth;
            TagFontSize = parameter.TagFontSize;
            AnnotationFontToHandleRatio = parameter.AnnotationFontToHandleRatio;
            TextForeground = parameter.TextForeground;
            BorderBackground = parameter.BorderBackground;
            foreach (var key in _stylers.Keys.ToArray()) _stylers.Remove(key);
            foreach (var entry in stylers) _stylers.Add(entry.Key, entry.Value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stylers)));
        }

        /// <summary>
        /// Returns a new layer with the same configuration but independent
        /// <see cref="IShapeStyler"/> instances. Use when a board/manager must
        /// mutate stylers for zoom without affecting other viewers that share
        /// the original config layer.
        /// </summary>
        public ShapeLayer CreateIndependentCopy()
        {
            return new ShapeLayer(
                ToShapeLayerParameter(),
                Measurement,
                _stylerFactory);
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
