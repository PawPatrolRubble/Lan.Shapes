#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
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
        private readonly ReadOnlyDictionary<ShapeVisualState, IShapeStyler> _stylerView;
        private readonly HashSet<Freezable> _observedVisuals = new();
        private int _maximumThickenedShapeWidth;
        private int _tagFontSize;

        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>Allows an owning catalogue to validate edits before they are committed.</summary>
        public event EventHandler<ShapeLayerParameter>? ConfigurationChanging;
        /// <summary>One notification for each supported definition or style edit.</summary>
        public event EventHandler? DefinitionChanged;
        internal static readonly EventArgs CatalogueCommitNotification = new EventArgs();

        /// <summary>Stylers keyed by visual state. Structure is read-only; supported value edits notify owners.</summary>
        public IReadOnlyDictionary<ShapeVisualState, IShapeStyler> Stylers => _stylerView;

        /// <summary>Global measurement calibration shared by all configured layers.</summary>
        public ShapeMeasurementSettings Measurement { get; private set; }

        public int LayerId { get; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public int MaximumThickenedShapeWidth
        {
            get => _maximumThickenedShapeWidth;
            set => SetDimension(ref _maximumThickenedShapeWidth, value, nameof(MaximumThickenedShapeWidth));
        }
        public int TagFontSize
        {
            get => _tagFontSize;
            set => SetDimension(ref _tagFontSize, value, nameof(TagFontSize));
        }
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

            shapeLayerParameter = shapeLayerParameter.CreateValidatedCopy();

            LayerId = shapeLayerParameter.LayerId;
            Name = shapeLayerParameter.Name;
            Description = shapeLayerParameter.Description;
            _maximumThickenedShapeWidth = shapeLayerParameter.MaximumThickenedShapeWidth;
            _tagFontSize = shapeLayerParameter.TagFontSize;
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
            _stylerView = new ReadOnlyDictionary<ShapeVisualState, IShapeStyler>(_stylers);
            ObserveStylers();
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
                BorderBackground = BorderBackground?.Clone(),
                Description = Description,
                Name = Name,
                MaximumThickenedShapeWidth = MaximumThickenedShapeWidth,
                TagFontSize = TagFontSize,
                AnnotationFontToHandleRatio = AnnotationFontToHandleRatio,
                TextForeground = TextForeground?.Clone(),
                StyleSchema = new Dictionary<ShapeVisualState, ShapeStylerParameter>(
                    _stylers.Select(x => new KeyValuePair<ShapeVisualState, ShapeStylerParameter>(
                        x.Key,
                        x.Value.ToStylerParameter())))
            };
        }

        /// <summary>Applies an edited definition while retaining this layer's identity and measurement.</summary>
        public void ApplyConfiguration(ShapeLayerParameter parameter)
            => ApplyConfiguration(parameter, Measurement);

        /// <summary>Rebinds a board copy to a complete definition, including global calibration.</summary>
        public void ApplyDefinition(ShapeLayer definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            ApplyConfiguration(definition.ToShapeLayerParameter(), definition.Measurement);
        }

        private void ApplyConfiguration(ShapeLayerParameter parameter, ShapeMeasurementSettings measurement)
        {
            var commit = PrepareConfiguration(parameter, measurement);
            commit();
            NotifyConfigurationChanged(committedToCatalogue: false);
        }

        // The catalogue prepares every fallible operation before persisting. The returned
        // commit retains layer identity and does not publish events until the owner is ready.
        internal Action PrepareConfiguration(ShapeLayerParameter parameter)
            => PrepareConfiguration(parameter, Measurement);

        private Action PrepareConfiguration(ShapeLayerParameter parameter, ShapeMeasurementSettings measurement)
        {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));
            if (parameter.LayerId != LayerId)
                throw new ArgumentException("A layer ID cannot be changed while editing a layer.", nameof(parameter));
            parameter = parameter.CreateValidatedCopy();
            measurement.Validate();
            var stylers = parameter.StyleSchema.ToDictionary(x => x.Key, x => _stylerFactory.CreateStyler(x.Value));
            ConfigurationChanging?.Invoke(this, parameter.CreateValidatedCopy());
            return () =>
            {
                UnobserveStylers();
                Name = parameter.Name;
                Description = parameter.Description;
                _maximumThickenedShapeWidth = parameter.MaximumThickenedShapeWidth;
                _tagFontSize = parameter.TagFontSize;
                Measurement = measurement;
                AnnotationFontToHandleRatio = parameter.AnnotationFontToHandleRatio;
                TextForeground = parameter.TextForeground;
                BorderBackground = parameter.BorderBackground;
                _stylers.Clear();
                foreach (var entry in stylers) _stylers.Add(entry.Key, entry.Value);
                ObserveStylers();
            };
        }

        internal void NotifyConfigurationChanged(bool committedToCatalogue)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stylers)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Measurement)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TagFontSize)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaximumThickenedShapeWidth)));
            DefinitionChanged?.Invoke(this, committedToCatalogue ? CatalogueCommitNotification : EventArgs.Empty);
        }

        private void SetDimension(ref int field, int value, string propertyName)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (field == value) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ObserveStylers()
        {
            foreach (var styler in _stylers.Values)
            {
                if (styler is INotifyPropertyChanged notifications)
                    notifications.PropertyChanged += Styler_PropertyChanged;
                ObserveVisual(styler.SketchPen);
                ObserveVisual(styler.FillColor);
                ObserveVisual(styler.TagColor);
            }
            ObserveVisual(TextForeground);
            ObserveVisual(BorderBackground);
        }

        private void ObserveVisual(Freezable? visual)
        {
            if (visual != null && !visual.IsFrozen && _observedVisuals.Add(visual)) visual.Changed += Visual_Changed;
        }

        private void UnobserveStylers()
        {
            foreach (var styler in _stylers.Values)
                if (styler is INotifyPropertyChanged notifications)
                    notifications.PropertyChanged -= Styler_PropertyChanged;
            foreach (var visual in _observedVisuals) visual.Changed -= Visual_Changed;
            _observedVisuals.Clear();
        }

        private void Styler_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(IShapeStyler.FillColor) or nameof(IShapeStyler.TagColor))
            {
                UnobserveStylers();
                ObserveStylers();
            }
            NotifyStyleChanged();
        }

        private void Visual_Changed(object? sender, EventArgs e) => NotifyStyleChanged();

        private void NotifyStyleChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stylers)));
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
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
