#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes.Handle;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;

namespace Lan.Shapes.Shapes
{
    /// <summary>
    /// Image-spanning rulers whose intersection is a movable measurement origin.
    /// Coordinates increase to the right and down, following image coordinates.
    /// </summary>
    public class RulerCross : ShapeVisualBase, IDataExport<RulerCrossData>, IBoardContextAware
    {
        private const int MinorTicksPerMajor = 10;

        private readonly LineGeometry _horizontalLine = new LineGeometry();
        private readonly LineGeometry _verticalLine = new LineGeometry();
        private readonly List<(Point Location, double Value, bool Horizontal)> _labels =
            new List<(Point, double, bool)>();
        private readonly CircleDragHandle _originHandle;
        private Point _center;

        public RulerCross(ShapeLayer layer) : base(layer)
        {
            _originHandle = RegisterHandle(new CircleDragHandle(DragHandleSize, default, 0));
        }

        public Point Center
        {
            get => _center;
            set
            {
                ValidatePoint(value);
                var center = Width > 0 && Height > 0
                    ? ForcePointInRange(value, 0, Width, 0, Height)
                    : value;
                if (SetField(ref _center, center))
                {
                    UpdateGeometry();
                    UpdateVisual();
                }
            }
        }

        public double Width { get; private set; }
        public double Height { get; private set; }

        /// <summary>Current major tick spacing in the layer's calibrated units.</summary>
        public double MajorTickInterval { get; private set; }

        public override Rect BoundsRect => Width > 0 && Height > 0
            ? new Rect(0, 0, Width, Height)
            : Rect.Empty;

        public override bool CanSnapDuringResize => false;

        private double UnitsPerPixel =>
            ShapeLayer.Measurement.UnitsPerMillimeter / ShapeLayer.Measurement.PixelPerUnit;

        /// <summary>Returns calibrated coordinates relative to the ruler origin.</summary>
        public Point GetRulerCoordinates(Point imagePoint)
        {
            var offset = imagePoint - Center;
            return new Point(offset.X * UnitsPerPixel, offset.Y * UnitsPerPixel);
        }

        public void OnBoardContextAvailable(double boardWidth, double boardHeight)
        {
            if (IsGeometryRendered)
            {
                return;
            }

            SetDimensions(boardWidth, boardHeight);
            _center = new Point(Width / 2, Height / 2);
            OnPropertyChanged(nameof(Center));
            UpdateGeometry();
            UpdateVisual();
        }

        protected override void CreateHandles()
        {
            _originHandle.GeometryCenter = Center;
        }

        protected override void HandleResizing(Point point)
        {
            HandleTranslate(point);
        }

        protected override void HandleTranslate(Point newPoint)
        {
            if (OldPointForTranslate.HasValue)
            {
                Center += newPoint - OldPointForTranslate.Value;
            }
        }

        protected override void OnViewportScaleChanged(double viewportScale)
        {
            UpdateGeometry();
        }

        public override void OnMouseLeftButtonUp(Point newPoint)
        {
            base.OnMouseLeftButtonUp(newPoint);
            UpdateVisual();
        }

        private void SetDimensions(double width, double height)
        {
            ValidateDimension(width, nameof(width));
            ValidateDimension(height, nameof(height));
            Width = width;
            Height = height;
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(BoundsRect));
        }

        private void UpdateGeometry()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            _horizontalLine.StartPoint = new Point(0, Center.Y);
            _horizontalLine.EndPoint = new Point(Width, Center.Y);
            _verticalLine.StartPoint = new Point(Center.X, 0);
            _verticalLine.EndPoint = new Point(Center.X, Height);

            // Keep ticks legible in screen space and bound geometry size at high zoom.
            var targetPixels = Math.Max(90 / ViewportScale, Math.Max(Width, Height) / 100);
            MajorTickInterval = NiceInterval(targetPixels * UnitsPerPixel);
            var majorPixels = MajorTickInterval / UnitsPerPixel;
            var ticks = new StreamGeometry();
            _labels.Clear();
            using (var context = ticks.Open())
            {
                AddTicks(context, Width, Center.X, majorPixels, horizontal: true);
                AddTicks(context, Height, Center.Y, majorPixels, horizontal: false);
            }
            ticks.Freeze();

            RenderGeometryGroup.Children.Clear();
            RenderGeometryGroup.Children.Add(_horizontalLine);
            RenderGeometryGroup.Children.Add(_verticalLine);
            RenderGeometryGroup.Children.Add(ticks);
            PanSensitiveArea.Geometry1 = RenderGeometry.GetWidenedPathGeometry(new Pen(Brushes.Black, 12 / ViewportScale));
            CreateHandles();
        }

        private void AddTicks(StreamGeometryContext context, double extent, double origin,
            double majorPixels, bool horizontal)
        {
            var minorPixels = majorPixels / MinorTicksPerMajor;
            var first = (int)Math.Ceiling(-origin / minorPixels);
            var last = (int)Math.Floor((extent - origin) / minorPixels);
            for (var index = first; index <= last; index++)
            {
                if (index == 0)
                {
                    continue;
                }

                var major = index % MinorTicksPerMajor == 0;
                var midpoint = index % (MinorTicksPerMajor / 2) == 0;
                var length = (major ? 10 : midpoint ? 7.5 : 5) / ViewportScale;
                var position = origin + index * minorPixels;
                var point = horizontal ? new Point(position, Center.Y) : new Point(Center.X, position);
                var offset = horizontal ? new Vector(0, length) : new Vector(length, 0);
                context.BeginFigure(point - offset / 2, isFilled: false, isClosed: false);
                context.LineTo(point + offset / 2, isStroked: true, isSmoothJoin: false);
                if (major || midpoint)
                {
                    _labels.Add((point, (double)index / MinorTicksPerMajor * MajorTickInterval, horizontal));
                }
            }
        }

        private static double NiceInterval(double target)
        {
            var magnitude = Math.Pow(10, Math.Floor(Math.Log10(target)));
            var fraction = target / magnitude;
            return (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * magnitude;
        }

        public override void UpdateVisual()
        {
            using (var context = RenderOpen())
            {
                var styler = ShapeStyler;
                if (styler == null || Width <= 0 || Height <= 0)
                {
                    return;
                }

                context.PushClip(new RectangleGeometry(BoundsRect));
                // Transparent stroke gives the thin ruler lines a useful mouse target.
                context.DrawGeometry(null, new Pen(Brushes.Transparent, 12 / ViewportScale), RenderGeometry);
                context.DrawGeometry(null, styler.SketchPen, RenderGeometry);
                foreach (var label in _labels)
                {
                    DrawLabel(context, label.Value.ToString("G6", CultureInfo.InvariantCulture),
                        label.Location, label.Horizontal);
                }
                DrawLabel(context, "0 " + ShapeLayer.Measurement.UnitName, Center, horizontal: false, origin: true);
                DrawDragHandles(context);
                DrawText(context);
                context.Pop();
            }
        }

        private void DrawLabel(DrawingContext context, string value, Point point, bool horizontal, bool origin = false)
        {
            var text = CreateFormattedText(value, ShapeStyler?.SketchPen.Brush ?? Brushes.Red);
            text.SetFontSize(AnnotationFontSize);
            var gap = 8 / ViewportScale;
            var location = origin
                ? new Point(point.X + gap, point.Y + gap)
                : horizontal
                    ? new Point(point.X - text.Width / 2, point.Y + gap)
                    : new Point(point.X + gap, point.Y - text.Height / 2);
            // Keep labels visible when the origin or a tick lies near an image edge.
            location = ForcePointInRange(location, 0, Math.Max(0, Width - text.Width),
                0, Math.Max(0, Height - text.Height));
            context.DrawText(text, location);
        }

        public void FromData(RulerCrossData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            ValidatePoint(data.Center);
            SetDimensions(data.Width, data.Height);
            _center = ForcePointInRange(data.Center, 0, Width, 0, Height);
            OnPropertyChanged(nameof(Center));
            ShapeStyler?.SetStrokeThickness(data.StrokeThickness);
            IsGeometryRendered = true;
            UpdateGeometry();
            UpdateVisual();
        }

        public RulerCrossData GetMetaData()
        {
            return new RulerCrossData
            {
                Center = Center,
                Width = Width,
                Height = Height,
                StrokeThickness = ShapeStyler?.SketchPen.Thickness ?? 1
            };
        }

        private static void ValidateDimension(double value, string name)
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "Ruler dimensions must be finite and greater than zero.");
            }
        }

        private static void ValidatePoint(Point point)
        {
            if (double.IsNaN(point.X) || double.IsInfinity(point.X) ||
                double.IsNaN(point.Y) || double.IsInfinity(point.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(point));
            }
        }
    }
}
