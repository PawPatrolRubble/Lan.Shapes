#nullable enable

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes.Enums;
using Lan.Shapes.Handle;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;
using Lan.Shapes.Utilities;

namespace Lan.Shapes.Shapes
{
    /// <summary>
    /// A rotatable rectangle using HALCON rectangle2 parameters.
    /// </summary>
    public class Rectangle2 : ShapeVisualBase, IDataExport<Rectangle2Data>
    {
        private const int TopLeftHandleId = 1;
        private const int TopMiddleHandleId = 2;
        private const int TopRightHandleId = 3;
        private const int RightMiddleHandleId = 4;
        private const int BottomRightHandleId = 5;
        private const int BottomMiddleHandleId = 6;
        private const int BottomLeftHandleId = 7;
        private const int LeftMiddleHandleId = 8;
        private const int RotationHandleId = 9;

        private const double RotationGripScreenOffset = 24.0;

        private readonly PathGeometry _pathGeometry = new PathGeometry();
        private readonly PathFigure _pathFigure = new PathFigure { IsClosed = true };

        private readonly RectDragHandle _topLeftHandle;
        private readonly RectDragHandle _topMiddleHandle;
        private readonly RectDragHandle _topRightHandle;
        private readonly RectDragHandle _rightMiddleHandle;
        private readonly RectDragHandle _bottomRightHandle;
        private readonly RectDragHandle _bottomMiddleHandle;
        private readonly RectDragHandle _bottomLeftHandle;
        private readonly RectDragHandle _leftMiddleHandle;
        private readonly RectDragHandle _rotationHandle;

        private Point _center;
        private double _phi;
        private double _length1;
        private double _length2;
        private double? _strokeThicknessOverride;
        private TagPosition _tagPosition = TagPosition.Center;

        private Point? _creationStart;
        private Point _dragStartCenter;
        private double _dragStartPhi;
        private double _dragStartLength1;
        private double _dragStartLength2;
        private Point _dragStartPointer;

        public Rectangle2(ShapeLayer shapeLayer) : base(shapeLayer)
        {
            _pathGeometry.Figures.Add(_pathFigure);
            RenderGeometryGroup.Children.Add(_pathGeometry);

            var dragHandleSize = DragHandleSize;
            _topLeftHandle = RegisterHandle(new RectDragHandle(
                new Size(dragHandleSize, dragHandleSize), default, 10, TopLeftHandleId, DragLocation.TopLeft));
            _topMiddleHandle = RegisterHandle(new RectDragHandle(
                new Size(dragHandleSize, dragHandleSize), default, 10, TopMiddleHandleId, DragLocation.TopMiddle));
            _topRightHandle = RegisterHandle(new RectDragHandle(
                new Size(dragHandleSize, dragHandleSize), default, 10, TopRightHandleId, DragLocation.TopRight));
            _rightMiddleHandle = RegisterHandle(new RectDragHandle(
                new Size(dragHandleSize, dragHandleSize), default, 10, RightMiddleHandleId, DragLocation.RightMiddle));
            _bottomRightHandle = RegisterHandle(new RectDragHandle(
                new Size(dragHandleSize, dragHandleSize), default, 10, BottomRightHandleId, DragLocation.BottomRight));
            _bottomMiddleHandle = RegisterHandle(new RectDragHandle(
                new Size(dragHandleSize, dragHandleSize), default, 10, BottomMiddleHandleId, DragLocation.BottomMiddle));
            _bottomLeftHandle = RegisterHandle(new RectDragHandle(
                new Size(dragHandleSize, dragHandleSize), default, 10, BottomLeftHandleId, DragLocation.BottomLeft));
            _leftMiddleHandle = RegisterHandle(new RectDragHandle(
                new Size(dragHandleSize, dragHandleSize), default, 10, LeftMiddleHandleId, DragLocation.LeftMiddle));
            _rotationHandle = RegisterHandle(new RectDragHandle(
                new Size(dragHandleSize, dragHandleSize), default, 10, RotationHandleId, DragLocation.Rotate));

            RebuildGeometry();
        }

        public Point Center
        {
            get => _center;
            set => ApplyParameters(value, Phi, Length1, Length2, redraw: true);
        }

        /// <summary>HALCON row coordinate of the center (WPF Y).</summary>
        public double Row
        {
            get => Center.Y;
            set => Center = new Point(Column, value);
        }

        /// <summary>HALCON column coordinate of the center (WPF X).</summary>
        public double Column
        {
            get => Center.X;
            set => Center = new Point(value, Row);
        }

        /// <summary>Orientation of the first half axis, in radians.</summary>
        public double Phi
        {
            get => _phi;
            set => ApplyParameters(Center, value, Length1, Length2, redraw: true);
        }

        public double Length1
        {
            get => _length1;
            set => ApplyParameters(Center, Phi, value, Length2, redraw: true);
        }

        public double Length2
        {
            get => _length2;
            set => ApplyParameters(Center, Phi, Length1, value, redraw: true);
        }

        public override Rect BoundsRect => _pathGeometry.Bounds;

        protected override Pen? GetDragHandlePen()
        {
            return ShapeStyler == null ? null : GetRenderPen();
        }

        public void FromData(Rectangle2Data data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var center = new Point(data.Column, data.Row);
            ValidateParameters(center, data.Phi, data.Length1, data.Length2);

            _strokeThicknessOverride = data.StrokeThickness > 0
                ? data.StrokeThickness
                : null;

            ApplyParameters(center, data.Phi, data.Length1, data.Length2, redraw: false);
            _tagPosition = data.TagPosition;
            IsGeometryRendered = true;
            Tag = data.Tag;
            UpdateVisual();
        }

        public Rectangle2Data GetMetaData()
        {
            return new Rectangle2Data
            {
                Row = Row,
                Column = Column,
                Phi = Rectangle2Math.NormalizePhi(Phi),
                Length1 = Length1,
                Length2 = Length2,
                StrokeThickness = _strokeThicknessOverride ?? 0,
                Tag = Tag,
                TagPosition = _tagPosition
            };
        }

        protected override void CreateHandles()
        {
            // Handles are created once in the constructor and retained for the
            // lifetime of the shape so rendering and hit-testing share ownership.
        }

        protected override void OnViewportScaleChanged(double viewportScale)
        {
            UpdateHandleLocations();
        }

        public override void OnMouseLeftButtonDown(Point mousePoint)
        {
            MouseDownPoint = mousePoint;
            OldPointForTranslate = mousePoint;

            if (!IsGeometryRendered)
            {
                _creationStart = mousePoint;
                ApplyParameters(mousePoint, 0, Rectangle2Math.MinimumHalfLength,
                    Rectangle2Math.MinimumHalfLength, redraw: false);
                UpdateVisual();
                return;
            }

            FindSelectedHandle(mousePoint);
            _dragStartCenter = Center;
            _dragStartPhi = Phi;
            _dragStartLength1 = Length1;
            _dragStartLength2 = Length2;
            _dragStartPointer = mousePoint;
        }

        public override void OnMouseMove(Point point, MouseButtonState buttonState)
        {
            if (buttonState != MouseButtonState.Pressed)
            {
                return;
            }

            if (!IsGeometryRendered && _creationStart.HasValue)
            {
                IsBeingDraggedOrPanMoving = true;
                var start = _creationStart.Value;
                ApplyParameters(
                    new Point((start.X + point.X) / 2, (start.Y + point.Y) / 2),
                    0,
                    Math.Max(Rectangle2Math.MinimumHalfLength, Math.Abs(point.X - start.X) / 2),
                    Math.Max(Rectangle2Math.MinimumHalfLength, Math.Abs(point.Y - start.Y) / 2),
                    redraw: false);
            }
            else if (SelectedDragHandle != null)
            {
                IsBeingDraggedOrPanMoving = true;
                HandleResizing(point);
            }
            else if (MouseDownPoint.HasValue &&
                     (IsBeingDraggedOrPanMoving || _pathGeometry.FillContains(MouseDownPoint.Value)))
            {
                IsBeingDraggedOrPanMoving = true;
                HandleTranslate(point);
            }

            OldPointForTranslate = point;
            UpdateVisual();
        }

        public override void OnMouseLeftButtonUp(Point newPoint)
        {
            base.OnMouseLeftButtonUp(newPoint);
            _creationStart = null;
            OldPointForTranslate = null;
            MouseDownPoint = null;
        }

        protected override void HandleResizing(Point point)
        {
            if (SelectedDragHandle == null)
            {
                return;
            }

            if (ReferenceEquals(SelectedDragHandle, _rotationHandle))
            {
                var startAngle = Math.Atan2(
                    _dragStartPointer.Y - _dragStartCenter.Y,
                    _dragStartPointer.X - _dragStartCenter.X);
                var currentAngle = Math.Atan2(
                    point.Y - _dragStartCenter.Y,
                    point.X - _dragStartCenter.X);

                if (double.IsNaN(currentAngle) || double.IsNaN(startAngle))
                {
                    return;
                }

                var delta = currentAngle - startAngle;
                delta = Math.Atan2(Math.Sin(delta), Math.Cos(delta));
                ApplyParameters(
                    _dragStartCenter,
                    _dragStartPhi + delta,
                    _dragStartLength1,
                    _dragStartLength2,
                    redraw: false);
                return;
            }

            var grip = GetGrip(SelectedDragHandle);
            if (!grip.HasValue)
            {
                return;
            }

            var localPoint = Rectangle2Math.ToLocal(point, _dragStartCenter, _dragStartPhi);
            var localCenterX = 0.0;
            var localCenterY = 0.0;
            var newLength1 = _dragStartLength1;
            var newLength2 = _dragStartLength2;

            if (grip.Value.X != 0)
            {
                var fixedX = -grip.Value.X * _dragStartLength1;
                var minimumDistance = Rectangle2Math.MinimumHalfLength * 2;
                var newX = grip.Value.X > 0
                    ? Math.Max(localPoint.X, fixedX + minimumDistance)
                    : Math.Min(localPoint.X, fixedX - minimumDistance);
                newLength1 = Math.Max(
                    Rectangle2Math.MinimumHalfLength,
                    Math.Abs(newX - fixedX) / 2);
                localCenterX = (newX + fixedX) / 2;
            }

            if (grip.Value.Y != 0)
            {
                var fixedY = -grip.Value.Y * _dragStartLength2;
                var minimumDistance = Rectangle2Math.MinimumHalfLength * 2;
                var newY = grip.Value.Y > 0
                    ? Math.Max(localPoint.Y, fixedY + minimumDistance)
                    : Math.Min(localPoint.Y, fixedY - minimumDistance);
                newLength2 = Math.Max(
                    Rectangle2Math.MinimumHalfLength,
                    Math.Abs(newY - fixedY) / 2);
                localCenterY = (newY + fixedY) / 2;
            }

            ApplyParameters(
                Rectangle2Math.FromLocal(
                    _dragStartCenter, _dragStartPhi, localCenterX, localCenterY),
                _dragStartPhi,
                newLength1,
                newLength2,
                redraw: false);
        }

        protected override void HandleTranslate(Point newPoint)
        {
            if (!OldPointForTranslate.HasValue)
            {
                return;
            }

            ApplyParameters(
                Center + (newPoint - OldPointForTranslate.Value),
                Phi,
                Length1,
                Length2,
                redraw: false);
        }

        public override void UpdateVisual()
        {
            if (ShapeStyler == null)
            {
                return;
            }

            var renderContext = RenderOpen();
            var renderPen = GetRenderPen();
            renderContext.DrawGeometry(ShapeStyler.FillColor, renderPen, RenderGeometryGroup);
            AddTagText(renderContext, GetTagPosition());
            if (AreDragHandlesActive)
            {
                renderContext.DrawLine(
                    renderPen,
                    _topMiddleHandle.GeometryCenter,
                    _rotationHandle.GeometryCenter);
            }
            DrawDragHandles(renderContext);
            DrawText(renderContext);
            renderContext.Close();
        }

        private Pen GetRenderPen()
        {
            var styler = ShapeStyler
                ?? throw new InvalidOperationException("ShapeStyler must be available before rendering.");

            if (!_strokeThicknessOverride.HasValue)
            {
                return styler.SketchPen;
            }

            var scale = ViewportScale > 0 &&
                        !double.IsNaN(ViewportScale) &&
                        !double.IsInfinity(ViewportScale)
                ? ViewportScale
                : 1.0;
            var pen = new Pen(styler.SketchPen.Brush, _strokeThicknessOverride.Value / scale)
            {
                DashStyle = styler.SketchPen.DashStyle
            };
            return pen;
        }

        private void ApplyParameters(
            Point center,
            double phi,
            double length1,
            double length2,
            bool redraw)
        {
            ValidateParameters(center, phi, length1, length2);
            var normalizedPhi = phi;

            var centerChanged = Center != center;
            var phiChanged = !AreEqual(Phi, normalizedPhi);
            var length1Changed = !AreEqual(Length1, length1);
            var length2Changed = !AreEqual(Length2, length2);

            _center = center;
            _phi = normalizedPhi;
            _length1 = length1;
            _length2 = length2;

            if (centerChanged)
            {
                OnPropertyChanged(nameof(Center));
                OnPropertyChanged(nameof(Row));
                OnPropertyChanged(nameof(Column));
            }

            if (phiChanged)
            {
                OnPropertyChanged(nameof(Phi));
            }

            if (length1Changed)
            {
                OnPropertyChanged(nameof(Length1));
            }

            if (length2Changed)
            {
                OnPropertyChanged(nameof(Length2));
            }

            RebuildGeometry();
            if (redraw)
            {
                UpdateVisual();
            }
        }

        protected override bool TryUpdateMouseCursor(DragHandle handle)
        {
            if (ReferenceEquals(handle, _rotationHandle))
            {
                Mouse.SetCursor(Cursors.Hand);
                return true;
            }

            var grip = GetGrip(handle);
            if (!grip.HasValue)
            {
                return base.TryUpdateMouseCursor(handle);
            }

            var axis1 = Rectangle2Math.Axis1(Phi);
            var axis2 = Rectangle2Math.Axis2(Phi);
            var direction = new Vector(
                grip.Value.X * axis1.X + grip.Value.Y * axis2.X,
                grip.Value.X * axis1.Y + grip.Value.Y * axis2.Y);

            if (direction.Length < Rectangle2Math.MinimumHalfLength)
            {
                return base.TryUpdateMouseCursor(handle);
            }

            Mouse.SetCursor(GetResizeCursor(direction));
            return true;
        }

        private void RebuildGeometry()
        {
            var corners = Rectangle2Math.GetCorners(Center, Phi, Length1, Length2);
            _pathFigure.StartPoint = corners[0];
            _pathFigure.Segments.Clear();
            for (var i = 1; i < corners.Length; i++)
            {
                _pathFigure.Segments.Add(new LineSegment(corners[i], true));
            }

            UpdateHandleLocations();
        }

        private void UpdateHandleLocations()
        {
            var corners = Rectangle2Math.GetCorners(Center, Phi, Length1, Length2);
            _topLeftHandle.GeometryCenter = corners[0];
            _topRightHandle.GeometryCenter = corners[1];
            _bottomRightHandle.GeometryCenter = corners[2];
            _bottomLeftHandle.GeometryCenter = corners[3];

            _topMiddleHandle.GeometryCenter = Rectangle2Math.FromLocal(Center, Phi, 0, -Length2);
            _rightMiddleHandle.GeometryCenter = Rectangle2Math.FromLocal(Center, Phi, Length1, 0);
            _bottomMiddleHandle.GeometryCenter = Rectangle2Math.FromLocal(Center, Phi, 0, Length2);
            _leftMiddleHandle.GeometryCenter = Rectangle2Math.FromLocal(Center, Phi, -Length1, 0);

            var rotationGap = RotationGripScreenOffset / ViewportScale;
            _rotationHandle.GeometryCenter = Rectangle2Math.FromLocal(
                Center, Phi, 0, -Length2 - rotationGap);
        }

        private Point GetTagPosition()
        {
            return _tagPosition switch
            {
                TagPosition.Center => Center - new Vector(ShapeLayer.TagFontSize / 2, ShapeLayer.TagFontSize / 2),
                TagPosition.Top => Rectangle2Math.FromLocal(Center, Phi, 0, -Length2),
                TagPosition.Bottom => Rectangle2Math.FromLocal(Center, Phi, 0, Length2),
                _ => Center
            };
        }

        private static (int X, int Y)? GetGrip(DragHandle handle)
        {
            return handle.Id switch
            {
                TopLeftHandleId => (-1, -1),
                TopMiddleHandleId => (0, -1),
                TopRightHandleId => (1, -1),
                RightMiddleHandleId => (1, 0),
                BottomRightHandleId => (1, 1),
                BottomMiddleHandleId => (0, 1),
                BottomLeftHandleId => (-1, 1),
                LeftMiddleHandleId => (-1, 0),
                _ => null
            };
        }

        private static Cursor GetResizeCursor(Vector direction)
        {
            var angle = Math.Atan2(direction.Y, direction.X) % Math.PI;
            if (angle < 0)
            {
                angle += Math.PI;
            }

            if (angle < Math.PI / 8 || angle >= 7 * Math.PI / 8)
            {
                return Cursors.SizeWE;
            }

            if (angle < 3 * Math.PI / 8)
            {
                return Cursors.SizeNWSE;
            }

            if (angle < 5 * Math.PI / 8)
            {
                return Cursors.SizeNS;
            }

            return Cursors.SizeNESW;
        }

        private static void ValidateParameters(Point center, double phi, double length1, double length2)
        {
            if (!IsFinite(center.X) || !IsFinite(center.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(center), "The center must be finite.");
            }

            if (!IsFinite(phi))
            {
                throw new ArgumentOutOfRangeException(nameof(phi), "Phi must be finite.");
            }

            if (!IsFinite(length1) || length1 < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length1), "Length1 must be finite and non-negative.");
            }

            if (!IsFinite(length2) || length2 < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length2), "Length2 must be finite and non-negative.");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool AreEqual(double first, double second)
        {
            return Math.Abs(first - second) < 0.000000001;
        }
    }
}
