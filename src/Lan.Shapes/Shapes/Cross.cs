using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;

using Lan.Shapes.Handle;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;

namespace Lan.Shapes.Shapes
{
    public class Cross : ShapeVisualBase, IDataExport<CrossData>
    {


        private const int DefaultAxisLength = 40;

        private readonly LineGeometry _verticalLine = new LineGeometry();
        private readonly LineGeometry _horizontalLine = new LineGeometry();
        private readonly DragHandle _leftHandle;
        private readonly DragHandle _rightHandle;
        private readonly DragHandle _topHandle;
        private readonly DragHandle _bottomHandle;
        private readonly DragHandle _centerHandle;
        private Point _center;
        private int _height;
        private int _width;

        public Cross(ShapeLayer layer) : base(layer)
        {
            RenderGeometryGroup.Children.Add(_horizontalLine);
            RenderGeometryGroup.Children.Add(_verticalLine);
            _centerHandle = RegisterHandle(new RectDragHandle(DragHandleSize, default, DragLocation.Move));
            _leftHandle = RegisterHandle(new RectDragHandle(DragHandleSize, default, DragLocation.LeftMiddle));
            _rightHandle = RegisterHandle(new RectDragHandle(DragHandleSize, default, DragLocation.RightMiddle));
            _topHandle = RegisterHandle(new RectDragHandle(DragHandleSize, default, DragLocation.TopMiddle));
            _bottomHandle = RegisterHandle(new RectDragHandle(DragHandleSize, default, DragLocation.BottomMiddle));
        }

        public Point Center
        {
            get { return _center; }
            set
            {
                SetField(ref _center, value);
                UpdateVerticalAndHorizontalLine();
                UpdateVisual();
            }
        }


        public int Height
        {
            get { return _height; }
            set
            {
                SetField(ref _height, value);
                UpdateVerticalAndHorizontalLine();
                UpdateVisual();
            }
        }

        public int Width
        {
            get { return _width; }
            set
            {
                SetField(ref _width, value);
                UpdateVerticalAndHorizontalLine();
                UpdateVisual();
            }
        }


        private void UpdateVerticalAndHorizontalLine()
        {

            _horizontalLine.StartPoint = Center + new Vector(-Width * 1.0 / 2, 0);
            _horizontalLine.EndPoint = Center + new Vector(Width * 1.0 / 2, 0);

            _verticalLine.StartPoint = Center + new Vector(0, -Height * 1.0 / 2);
            _verticalLine.EndPoint = Center + new Vector(0, Height * 1.0 / 2);
            CreateHandles();
            UpdatePanSensitiveArea();
        }

        private void UpdatePanSensitiveArea()
        {
            if (ShapeStyler != null)
            {
                PanSensitiveArea.Geometry1 = RenderGeometry.GetWidenedPathGeometry(ShapeStyler.SketchPen);
            }
        }

        public override Rect BoundsRect => RenderGeometry.Bounds;

        protected override void CreateHandles()
        {
            _centerHandle.GeometryCenter = Center;
            _leftHandle.GeometryCenter = _horizontalLine.StartPoint;
            _rightHandle.GeometryCenter = _horizontalLine.EndPoint;
            _topHandle.GeometryCenter = _verticalLine.StartPoint;
            _bottomHandle.GeometryCenter = _verticalLine.EndPoint;
        }

        public override DragHandle FindDragHandleMouseOver(Point point)
        {
            if (!AreDragHandlesActive)
            {
                return null;
            }

            DragHandle nearest = null;
            var nearestDistance = double.PositiveInfinity;
            foreach (var handle in Handles)
            {
                var distance = (handle.GeometryCenter - point).LengthSquared;
                if (handle.FillContains(point) && distance < nearestDistance)
                {
                    nearest = handle;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        protected override void DrawGeometryInMouseMove(Point center, Point point)
        {
            Width = GetAxisLength(point.X - center.X);
            Height = GetAxisLength(point.Y - center.Y);
        }

        private static int GetAxisLength(double distance) => (int)Math.Round(2 * Math.Abs(distance));

        protected override void OnViewportScaleChanged(double viewportScale)
        {
            UpdatePanSensitiveArea();
        }

        protected override void HandleResizing(Point point)
        {
            if (ReferenceEquals(SelectedDragHandle, _centerHandle))
            {
                HandleTranslate(point);
            }
            else if (ReferenceEquals(SelectedDragHandle, _leftHandle) || ReferenceEquals(SelectedDragHandle, _rightHandle))
            {
                Width = GetAxisLength(point.X - Center.X);
            }
            else if (ReferenceEquals(SelectedDragHandle, _topHandle) || ReferenceEquals(SelectedDragHandle, _bottomHandle))
            {
                Height = GetAxisLength(point.Y - Center.Y);
            }
        }

        protected override void HandleTranslate(Point newPoint)
        {
            if (OldPointForTranslate.HasValue)
            {
                Center += newPoint - OldPointForTranslate.Value;
            }
        }

        public override void OnMouseLeftButtonDown(Point mousePoint)
        {
            base.OnMouseLeftButtonDown(mousePoint);
            if (!IsGeometryRendered)
            {
                Center = mousePoint;
                Width = DefaultAxisLength;
                Height = DefaultAxisLength;
            }
        }

        public override void OnMouseLeftButtonUp(Point point)
        {
            // A release can land on a position that no move event ever reported
            // (fast release, or a release outside the board). Apply the remaining
            // delta before the base implementation clears the drag state.
            if (IsGeometryRendered && OldPointForTranslate is Point previous && previous != point)
            {
                base.OnMouseMove(point, MouseButtonState.Pressed);
            }

            base.OnMouseLeftButtonUp(point);
            UpdateVisual();
        }

        public void FromData(CrossData data)
        {
            Center = data.Center;
            Width = data.Width;
            Height = data.Height;
            ShapeStyler?.SetStrokeThickness(data.StrokeThickness);
            IsGeometryRendered = true;
            UpdatePanSensitiveArea();
            UpdateVisual();
        }

        public CrossData GetMetaData()
        {
            return new CrossData()
            {
                Center = Center,
                Width = Width,
                Height = Height,
                StrokeThickness = ShapeStyler.SketchPen.Thickness,
            };
        }

        public override void AddText(string content, Point? location = null)
        {

        }


        public override void UpdateVisual()
        {
            var renderContext = RenderOpen();
            if (ShapeStyler != null)
            {
                renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, RenderGeometry);
                DrawDragHandles(renderContext);
                DrawText(renderContext);
            }

            renderContext.Close();
        }
    }
}
