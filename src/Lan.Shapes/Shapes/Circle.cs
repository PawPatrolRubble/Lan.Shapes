using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using Lan.Shapes.Handle;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;
using Lan.Shapes.Utilities;

namespace Lan.Shapes.Shapes
{
    public class Circle : ShapeVisualBase, IDataExport<EllipseData>
    {
        #region constructor

        private DragHandle _dragHandle;

        #region Constructors

        public Circle(ShapeLayer shapeLayer) : base(shapeLayer)
        {
            DragHandleSize = ShapeStyler.DragHandleSize;
            _dragHandle = RegisterHandle(new RectDragHandle(DragHandleSize, default, 1));
            RenderGeometryGroup.Children.Add(_ellipseGeometry);
        }

        #endregion

        #endregion

        #region interface implementation

        public void FromData(EllipseData data)
        {
            Center = data.Center;
            Radius = data.RadiusX;
            IsGeometryRendered = true;
            RequestVisualUpdate();
        }

        public EllipseData GetMetaData()
        {
            return new EllipseData
            {
                Center = Center,
                RadiusX = Radius
            };
        }

        #endregion

        #region fields

        private readonly EllipseGeometry _ellipseGeometry = new EllipseGeometry(default);
        private readonly LineGeometry _verticalLine = new LineGeometry();
        private readonly LineGeometry _horizontalLine = new LineGeometry();

        private readonly int crossSize = 40;
        private Point _center;

        #endregion

        #region Properties

        /// <summary>
        /// </summary>
        public override Rect BoundsRect
        {
            get { return _ellipseGeometry.Bounds; }
        }

        public override IEnumerable<Point> GetSnapPoints() => new[]
        {
            Center,
            Center + new Vector(Radius, 0),
            Center + new Vector(0, -Radius),
            Center + new Vector(-Radius, 0),
            Center + new Vector(0, Radius)
        };

        public override Point? MoveSnapPoint => IsGeometryRendered && !IsLocked
            && SelectedDragHandle == null ? Center : null;

        public double X
        {
            get => Center.X;
            set => Center = new Point(value, Center.Y);
        }

        public double Y
        {
            get => Center.Y;
            set => Center = new Point(Center.X, value);
        }



        public Point Center
        {
            get { return _center; }
            set
            {
                if (!SetField(ref _center, value)) return;
                UpdateGeometryGroup();
                OnPropertyChanged(nameof(X));
                OnPropertyChanged(nameof(Y));
            }
        }


        #region Overrides of Object

        public override string ToString()
        {
            return $"Circle: {Center.X:f0}, {Center.Y:f0}, Radius: {Radius}";
        }

        #endregion


        private double _radius;

        public double Radius
        {
            get { return _radius; }
            set
            {
                SetField(ref _radius, value);
                UpdateGeometryGroup();
            }
        }

        /// <summary>Atomically updates the center and radius of a completed, unlocked circle.</summary>
        public void SetCircle(Point center, double radius)
        {
            GeometryEditValidation.EnsureEditable(this);
            GeometryEditValidation.EnsureFinite(center, nameof(center));
            if (!double.IsFinite(radius) || radius <= 0 || !double.IsFinite(radius * 2))
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be finite, positive and have a finite diameter.");
            var left = center.X - radius;
            var right = center.X + radius;
            var top = center.Y - radius;
            var bottom = center.Y + radius;
            if (!double.IsFinite(left) || !double.IsFinite(right) || !double.IsFinite(top) || !double.IsFinite(bottom)
                || left >= right || top >= bottom)
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must define finite, distinct circle extents.");
            GeometryEditValidation.EnsureBounds(new Rect(left, top, right - left, bottom - top), nameof(radius));

            var centerChanged = _center != center;
            var radiusChanged = _radius != radius;
            if (!centerChanged && !radiusChanged) return;

            using (DeferVisualUpdates())
            {
                _center = center;
                _radius = radius;
                UpdateCircleGeometry();
                if (centerChanged)
                {
                    OnPropertyChanged(nameof(Center));
                    OnPropertyChanged(nameof(X));
                    OnPropertyChanged(nameof(Y));
                }
                if (radiusChanged) OnPropertyChanged(nameof(Radius));
                OnPropertyChanged(nameof(BoundsRect));
                OnPropertyChanged(nameof(SelectionBounds));
            }
        }

        #endregion

        #region others

        protected override void CreateHandles()
        {

        }

        private void AddRadiusText(DrawingContext renderContext)
        {
            var lengthInMm = 0.0;
            var measurement = ShapeLayer.Measurement;
            if (measurement.UnitsPerMillimeter != 0 && measurement.PixelPerUnit != 0)
            {
                lengthInMm = Radius * measurement.UnitsPerMillimeter / measurement.PixelPerUnit;
            }

            DrawCachedText(renderContext,
                $"{lengthInMm:f3} {measurement.UnitName}",
                ShapeStyler?.TagColor ?? Brushes.Red,
                Center);
        }

        protected override void DrawGeometryInMouseMove(Point oldPoint, Point newPoint)
        {
            Radius = (newPoint.X - oldPoint.X) / 2;
        }

        protected override void HandleResizing(Point point)
        {
            if (MouseDownPoint.HasValue)
            {
                switch (SelectedDragHandle!.Id)
                {
                    case 2:
                        Radius += MouseDownPoint.Value.Y - point.Y;
                        break;

                    case 1:
                        Radius += point.X - MouseDownPoint.Value.X;
                        break;
                }
            }

            MouseDownPoint = point;
        }

        protected override void HandleTranslate(Point newPoint)
        {
            if (!OldPointForTranslate.HasValue)
            {
                return;
            }

            Translate(newPoint - OldPointForTranslate.Value);
            OldPointForTranslate = newPoint;
        }

        public override bool CanTranslate => true;

        protected override void TranslateCore(Vector delta)
        {
            Center += delta;
        }

        /// <summary>
        ///     left mouse button down event
        /// </summary>
        /// <param name="mousePoint"></param>
        public override void OnMouseLeftButtonDown(Point mousePoint)
        {
            if (IsLocked) return;

            if (!IsGeometryRendered)
            {
                Center = mousePoint;
            }
            else
            {
                FindSelectedHandle(mousePoint);
            }

            OldPointForTranslate = mousePoint;
            MouseDownPoint = mousePoint;
        }

        public override void FindSelectedHandle(Point p)
        {
            base.FindSelectedHandle(p);
        }


        /// <summary>
        ///     鼠标点击移动
        /// </summary>
        public override void OnMouseMove(Point point, MouseButtonState buttonState)
        {
            if (IsLocked) return;

            if (buttonState == MouseButtonState.Pressed)
            {
                if (!IsGeometryRendered && OldPointForTranslate.HasValue)
                {
                    // Calculate radius as distance from center to current point
                    var dx = point.X - OldPointForTranslate.Value.X;
                    var dy = point.Y - OldPointForTranslate.Value.Y;
                    Radius = Math.Sqrt(dx * dx + dy * dy);
                    // Don't update OldPointForTranslate during drawing - we need the center point
                }
                else if (SelectedDragHandle != null)
                {
                    IsBeingDraggedOrPanMoving = true;
                    HandleResizing(point);
                    // HandleResizing updates OldPointForTranslate internally
                }
                else if (IsGeometryRendered)
                {
                    // If already dragging, continue. Otherwise check if mouse down was inside the circle
                    if (IsBeingDraggedOrPanMoving || (MouseDownPoint.HasValue && _ellipseGeometry.FillContains(MouseDownPoint.Value)))
                    {
                        IsBeingDraggedOrPanMoving = true;
                        HandleTranslate(point);
                        // HandleTranslate updates OldPointForTranslate internally
                    }
                }
            }
        }

        /// <summary>
        ///     add geometries to group
        /// </summary>
        protected void UpdateGeometryGroup([CallerMemberName] string propertyName = "")
        {
            if (propertyName == nameof(Center) || propertyName == nameof(Radius)) UpdateCircleGeometry();
        }

        private void UpdateCircleGeometry()
        {
            _ellipseGeometry.Center = Center;
            if (Radius > 0)
            {
                _ellipseGeometry.RadiusX = Radius;
                _ellipseGeometry.RadiusY = Radius;
            }
            _dragHandle.GeometryCenter = Center + new Vector(Radius, 0);
            _verticalLine.StartPoint = Center + new Vector(0, -crossSize / 2.0);
            _verticalLine.EndPoint = Center + new Vector(0, crossSize / 2.0);
            _horizontalLine.StartPoint = Center + new Vector(-crossSize / 2.0, 0);
            _horizontalLine.EndPoint = Center + new Vector(crossSize / 2.0, 0);
            RequestVisualUpdate();
        }

        protected override void DrawShape(DrawingContext renderContext)
        {
            if (ShapeStyler != null)
            {
                renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, RenderGeometry);
                renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, _verticalLine);
                renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, _horizontalLine);

                AddTagText(renderContext, Center);
                AddRadiusText(renderContext);
            }
        }

        #endregion
    }
}
