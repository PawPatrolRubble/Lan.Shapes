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
    /// <summary>An angle defined by an endpoint, its vertex, and a second endpoint.</summary>
    public sealed class Angle : ShapeVisualBase, IDataExport<PointsData>, IStagedSketch
    {
        private readonly LineGeometry _firstRay = new LineGeometry();
        private readonly LineGeometry _secondRay = new LineGeometry();
        private readonly PathGeometry _arc = new PathGeometry();
        private readonly DragHandle _firstHandle;
        private readonly DragHandle _vertexHandle;
        private readonly DragHandle _secondHandle;

        private Point _firstPoint;
        private Point _vertex;
        private Point _secondPoint;
        private Point? _previewPoint;
        private int _clicks;

        public Angle(ShapeLayer layer) : base(layer)
        {
            _firstHandle = RegisterHandle(new CircleDragHandle(DragHandleSize, default, 0));
            _vertexHandle = RegisterHandle(new CircleDragHandle(DragHandleSize, default, 1));
            _secondHandle = RegisterHandle(new CircleDragHandle(DragHandleSize, default, 2));
            RenderGeometryGroup.Children.Add(_firstRay);
            RenderGeometryGroup.Children.Add(_secondRay);
            RenderGeometryGroup.Children.Add(_arc);
        }

        public Point FirstPoint
        {
            get => _firstPoint;
            set { SetField(ref _firstPoint, value); UpdateGeometry(); }
        }

        public Point Vertex
        {
            get => _vertex;
            set { SetField(ref _vertex, value); UpdateGeometry(); }
        }

        public Point SecondPoint
        {
            get => _secondPoint;
            set { SetField(ref _secondPoint, value); UpdateGeometry(); }
        }

        /// <summary>The smaller angle between the two rays, from 0 to 180 degrees.</summary>
        public double AngleDegrees => CalculateDegrees(_firstPoint, _vertex, _secondPoint);

        public override Rect BoundsRect => RenderGeometryGroup.Bounds;

        public override IEnumerable<Point> GetSnapPoints() => new[] { FirstPoint, Vertex, SecondPoint };

        public override bool CanSnapDuringResize => base.CanSnapDuringResize && SelectedDragHandle != null;

        private static double CalculateDegrees(Point first, Point vertex, Point second)
        {
            var a = first - vertex;
            var b = second - vertex;
            if (a.LengthSquared == 0 || b.LengthSquared == 0) return 0;
            return Math.Abs(Math.Atan2(Vector.CrossProduct(a, b), Vector.Multiply(a, b))) * 180 / Math.PI;
        }

        private void UpdateGeometry()
        {
            _firstRay.StartPoint = _vertex;
            _firstRay.EndPoint = _firstPoint;
            _secondRay.StartPoint = _vertex;
            _secondRay.EndPoint = _secondPoint;
            _firstHandle.GeometryCenter = _firstPoint;
            _vertexHandle.GeometryCenter = _vertex;
            _secondHandle.GeometryCenter = _secondPoint;

            _arc.Figures.Clear();
            if (_clicks >= 3 || IsGeometryRendered || (_clicks == 2 && _previewPoint.HasValue))
            {
                var a = _firstPoint - _vertex;
                var b = _secondPoint - _vertex;
                if (a.LengthSquared > 0 && b.LengthSquared > 0)
                {
                    var radius = Math.Min(28 / ViewportScale, Math.Min(a.Length, b.Length) * 0.4);
                    var start = _vertex + a / a.Length * radius;
                    var end = _vertex + b / b.Length * radius;
                    var sweep = Math.Atan2(Vector.CrossProduct(a, b), Vector.Multiply(a, b));
                    var figure = new PathFigure { StartPoint = start, IsClosed = false };
                    figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0, false,
                        sweep >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true));
                    _arc.Figures.Add(figure);
                }
            }

            PanSensitiveArea.Geometry1 = RenderGeometryGroup.GetWidenedPathGeometry(
                new Pen(Brushes.Black, 12 / ViewportScale));
            OnPropertyChanged(nameof(AngleDegrees));
            OnPropertyChanged(nameof(BoundsRect));
            UpdateVisual();
        }

        protected override void CreateHandles() { }

        protected override void HandleResizing(Point point)
        {
            if (ReferenceEquals(SelectedDragHandle, _firstHandle)) FirstPoint = point;
            else if (ReferenceEquals(SelectedDragHandle, _vertexHandle)) Vertex = point;
            else if (ReferenceEquals(SelectedDragHandle, _secondHandle)) SecondPoint = point;
        }

        protected override void HandleTranslate(Point point)
        {
            if (OldPointForTranslate is not Point previous) return;
            var delta = point - previous;
            _firstPoint += delta;
            _vertex += delta;
            _secondPoint += delta;
            OldPointForTranslate = point;
            OnPropertyChanged(nameof(FirstPoint));
            OnPropertyChanged(nameof(Vertex));
            OnPropertyChanged(nameof(SecondPoint));
            UpdateGeometry();
        }

        public override void OnMouseLeftButtonDown(Point point)
        {
            if (IsGeometryRendered)
            {
                base.OnMouseLeftButtonDown(point);
                return;
            }

            _previewPoint = null;
            if (_clicks == 0) _firstPoint = point;
            else if (_clicks == 1) _vertex = point;
            else if (_clicks == 2) _secondPoint = point;
            _clicks++;
            OldPointForTranslate = point;
            UpdateGeometry();
        }

        public override void OnMouseLeftButtonDoubleClick(Point point)
        {
            if (!IsGeometryRendered) OnMouseLeftButtonDown(point);
        }

        public void PreviewPointer(Point point)
        {
            if (IsGeometryRendered || _clicks == 0 || _clicks > 2) return;
            _previewPoint = point;
            if (_clicks == 1) _vertex = point;
            else _secondPoint = point;
            UpdateGeometry();
        }

        public override void OnMouseMove(Point point, MouseButtonState buttonState)
        {
            if (!IsGeometryRendered || buttonState != MouseButtonState.Pressed || IsLocked) return;
            IsBeingDraggedOrPanMoving = true;
            if (SelectedDragHandle != null) HandleResizing(point);
            else HandleTranslate(point);
            OldPointForTranslate = point;
        }

        public override void OnMouseLeftButtonUp(Point point)
        {
            if (!IsGeometryRendered && _clicks == 3)
            {
                IsGeometryRendered = true;
                _previewPoint = null;
                UpdateGeometry();
            }
            else if (IsGeometryRendered && OldPointForTranslate is Point previous && previous != point)
            {
                OnMouseMove(point, MouseButtonState.Pressed);
            }

            // The base implementation completes every sketch on its first mouse-up.
            if (IsGeometryRendered) base.OnMouseLeftButtonUp(point);
            SelectedDragHandle = null;
            IsBeingDraggedOrPanMoving = false;
            OldPointForTranslate = null;
        }

        public override void OnMouseRightButtonUp(Point point)
        {
            if (!IsGeometryRendered) OnShapeCreationCancelled();
            else base.OnMouseRightButtonUp(point);
        }

        protected override void OnViewportScaleChanged(double viewportScale) => UpdateGeometry();

        public override void UpdateVisual()
        {
            var styler = ShapeStyler;
            if (styler == null) return;
            using var context = RenderOpen();
            if (_clicks >= 1 || IsGeometryRendered)
            {
                if (_clicks >= 2 || IsGeometryRendered || _previewPoint.HasValue)
                {
                    context.DrawGeometry(null, new Pen(Brushes.Transparent, 12 / ViewportScale), _firstRay);
                    context.DrawGeometry(null, styler.SketchPen, _firstRay);
                }
                if (_clicks >= 3 || IsGeometryRendered || (_clicks == 2 && _previewPoint.HasValue))
                {
                    context.DrawGeometry(null, new Pen(Brushes.Transparent, 12 / ViewportScale), _secondRay);
                    context.DrawGeometry(null, styler.SketchPen, _secondRay);
                    context.DrawGeometry(null, styler.SketchPen, _arc);
                    DrawAngleLabel(context);
                }
            }
            if (AreDragHandlesActive)
            {
                var visibleCount = IsGeometryRendered ? 3 : _clicks;
                for (var i = 0; i < visibleCount; i++)
                {
                    var handle = Handles[i];
                    context.DrawGeometry(styler.FillColor, styler.SketchPen, handle.HandleGeometry);
                }
            }
            DrawText(context);
        }

        private void DrawAngleLabel(DrawingContext context)
        {
            var a = _firstPoint - _vertex;
            var b = _secondPoint - _vertex;
            if (a.LengthSquared == 0 || b.LengthSquared == 0) return;
            var sweep = Math.Atan2(Vector.CrossProduct(a, b), Vector.Multiply(a, b));
            var middle = Math.Atan2(a.Y, a.X) + sweep / 2;
            var radius = Math.Min(28 / ViewportScale, Math.Min(a.Length, b.Length) * 0.4);
            var distance = radius + 8 / ViewportScale;
            var position = _vertex + new Vector(Math.Cos(middle), Math.Sin(middle)) * distance;
            var text = CreateFormattedText(CalculateDegrees(_firstPoint, _vertex, _secondPoint)
                .ToString("0.##", CultureInfo.InvariantCulture) + "°",
                ShapeStyler?.TagColor ?? Brushes.Red);
            text.SetFontSize(AnnotationFontSize);
            context.DrawText(text, new Point(position.X - text.Width / 2, position.Y - text.Height / 2));
        }

        public void FromData(PointsData data)
        {
            if (data?.DataPoints == null || data.DataPoints.Count != 3)
                throw new ArgumentException("Angle data must contain exactly three points.", nameof(data));
            _firstPoint = data.DataPoints[0];
            _vertex = data.DataPoints[1];
            _secondPoint = data.DataPoints[2];
            _clicks = 3;
            IsGeometryRendered = true;
            Tag = data.Tag;
            UpdateGeometry();
        }

        public PointsData GetMetaData() => new PointsData(Tag ?? string.Empty,
            ShapeStyler?.SketchPen.Thickness ?? 1,
            new List<Point> { FirstPoint, Vertex, SecondPoint });
    }
}
