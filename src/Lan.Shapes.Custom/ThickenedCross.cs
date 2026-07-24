#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes.Handle;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;

#endregion

namespace Lan.Shapes.Custom
{
    public class ThickenedCross : CustomGeometryBase, IDataExport<PointsData>
    {
        #region fields

        //ˮƽ��
        private const int MinPixelDistance = 1;

        private readonly bool _isSquare;
        private readonly CombinedGeometry? _combinedGeometry;
        private readonly RectangleGeometry _hRectangleGeometry = new RectangleGeometry();
        private readonly RectangleGeometry _vRectangleGeometry = new RectangleGeometry();

        private Point _centerPoint;
        private Dictionary<DragLocations, DragHandle> _dragHandles = new Dictionary<DragLocations, DragHandle>();
        private Point _horizontalTopLeft;
        private Point _verticalBottomRight;
        private Point _verticalTopLeft;

        #endregion

        #region Properties

        public override Geometry RenderGeometry
        {
            get => _combinedGeometry;
        }

        private Point CenterPoint
        {
            get => _centerPoint;
            set
            {
                _centerPoint = value;
                UpdateHorizontalGeometryCornerPoints();
            }
        }


        public Point VerticalTopLeft
        {
            get => _verticalTopLeft;
            set
            {
                _verticalTopLeft = value;
                if (!IsGeometryRendered)
                {
                    CreateGeometry(_verticalTopLeft);
                }
                else
                {
                    HandleDragging(_verticalTopLeft, DragLocations.VTopLeft);
                    UpdateCenter();
                }
            }
        }


        public Point VerticalBottomRight
        {
            get => _verticalBottomRight;
            set
            {
                _verticalBottomRight = value;
                HandleDragging(_verticalBottomRight, DragLocations.VBottomRight);
                UpdateCenter();
            }
        }


        public Point HorizontalTopLeft
        {
            get => _horizontalTopLeft;
            set
            {
                _horizontalTopLeft = value;
                HandleDragging(_horizontalTopLeft, DragLocations.HTopLeft);
            }
        }


        private Point _horizontalBottomRight;

        public Point HorizontalBottomRight
        {
            get => _horizontalBottomRight;
            set
            {
                _horizontalBottomRight = value;
                HandleDragging(_horizontalBottomRight, DragLocations.HBottomRight);
            }
        }


        #endregion

        #region Constructors

        public ThickenedCross(ShapeLayer shapeLayer) : base(shapeLayer)
        {
            _combinedGeometry = new CombinedGeometry(_vRectangleGeometry, _hRectangleGeometry);

        }

        #endregion

        #region local methods

        private void CreateGeometry(Point topLeft)
        {
            _vRectangleGeometry.Rect = new Rect(topLeft, new Size());
        }


        private void HandleDragging(Point mousePoint, DragLocations location)
        {
            switch (location)
            {
                case DragLocations.None:
                    break;
                case DragLocations.VTopLeft:

                    _vRectangleGeometry.Rect = new Rect(mousePoint, _vRectangleGeometry.Rect.BottomRight);

                    UpdateHTopLeft();
                    UpdateHBottomRight();
                    _hRectangleGeometry.Rect = new Rect(HorizontalTopLeft, HorizontalBottomRight);

                    break;
                case DragLocations.VTopRight:
                    break;
                case DragLocations.VBottomLeft:
                    break;
                case DragLocations.VBottomRight:

                    _vRectangleGeometry.Rect = new Rect(_vRectangleGeometry.Rect.Location, mousePoint);
                    UpdateCenter();

                    CreateHandles();

                    break;
                case DragLocations.HTopLeft:

                    //
                    if (SelectedDragHandle?.Id == (int)DragLocations.HTopLeft)
                    {
                        //change bottom right also
                        var vectorToCenter = mousePoint - CenterPoint;
                        HorizontalBottomRight = CenterPoint - vectorToCenter;
                    }

                    _hRectangleGeometry.Rect = new Rect(mousePoint, HorizontalBottomRight);

                    break;
                case DragLocations.HTopRight:
                    break;
                case DragLocations.HBottomLeft:
                    break;
                case DragLocations.HBottomRight:

                    if (SelectedDragHandle?.Id == (int)DragLocations.HBottomRight)
                    {
                        //change top left also
                        var vectorToCenter = mousePoint - CenterPoint;
                        HorizontalTopLeft = CenterPoint - vectorToCenter;
                    }

                    _hRectangleGeometry.Rect = new Rect(HorizontalTopLeft, mousePoint);

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(location), location, null);
            }

            UpdateHandleLocation();
        }


        private void UpdateCenter()
        {
            //update center point
            CenterPoint = new Point(
                (_vRectangleGeometry.Rect.TopLeft.X + _vRectangleGeometry.Rect.BottomRight.X) / 2,
                (_vRectangleGeometry.Rect.TopLeft.Y + _vRectangleGeometry.Rect.BottomRight.Y) / 2);
        }

        private void UpdateHandleLocation()
        {
            if (Handles.Count > 0)
            {
                _dragHandles[DragLocations.VTopLeft].GeometryCenter = _vRectangleGeometry.Rect.Location;
                _dragHandles[DragLocations.VBottomRight].GeometryCenter = _vRectangleGeometry.Rect.BottomRight;

                _dragHandles[DragLocations.HTopLeft].GeometryCenter = _hRectangleGeometry.Rect.Location;
                _dragHandles[DragLocations.HBottomRight].GeometryCenter = _hRectangleGeometry.Rect.BottomRight;
                _dragHandles[DragLocations.ResizeHandle].GeometryCenter = GetResizeHandleLocation();
            }
        }


        private void UpdateHBottomRight()
        {
            if (_isSquare)
            {
                var width = _vRectangleGeometry.Rect.Width;
                HorizontalBottomRight = HorizontalTopLeft + new Vector(_vRectangleGeometry.Rect.Height, width);
            }

            if (!IsGeometryRendered)
            {
                HorizontalBottomRight = VerticalBottomRight + new Vector(15, -_vRectangleGeometry.Rect.Width);
            }
        }


        /// <summary>
        ///     update horizontal top left and bottom right when center changes
        ///     as it must be symmetric to the center
        /// </summary>
        private void UpdateHorizontalGeometryCornerPoints()
        {
            //get width and height of horizontal geometry
            var width = _hRectangleGeometry.Rect.Width / 2;
            var height = _hRectangleGeometry.Rect.Height / 2;

            if (!IsGeometryRendered)
            {
                width = _vRectangleGeometry.Rect.Height / 2;
                height = _vRectangleGeometry.Rect.Width / 2;
            }


            HorizontalTopLeft = CenterPoint -
                                new Vector(width, height);
            HorizontalBottomRight = CenterPoint +
                                    new Vector(width, height);
        }

        private void UpdateHTopLeft()
        {
            if (_isSquare)
            {
                var width = _vRectangleGeometry.Rect.Width;
                var height = _vRectangleGeometry.Rect.Height;
                HorizontalTopLeft = _vRectangleGeometry.Rect.Location +
                                    new Vector(-(height - width) / 2, (height - width) / 2);
            }

            //set default size of 
            if (!IsGeometryRendered)
            {
                HorizontalTopLeft = VerticalTopLeft + new Vector(-15, _vRectangleGeometry.Rect.Width);
            }
        }

        #endregion

        #region implementations

        /// <summary>
        /// Loads a thickened cross from four corners:
        /// [0] VerticalTopLeft, [1] VerticalBottomRight,
        /// [2] HorizontalTopLeft, [3] HorizontalBottomRight.
        /// </summary>
        public void FromData(PointsData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.DataPoints == null || data.DataPoints.Count != 4)
            {
                throw new ArgumentException(
                    $"{nameof(PointsData)} must have exactly 4 elements in {nameof(PointsData.DataPoints)} " +
                    "(VerticalTopLeft, VerticalBottomRight, HorizontalTopLeft, HorizontalBottomRight).",
                    nameof(data));
            }

            // Assign fields directly — property setters run drag-side-effect geometry rebuilds.
            _verticalTopLeft = data.DataPoints[0];
            _verticalBottomRight = data.DataPoints[1];
            _horizontalTopLeft = data.DataPoints[2];
            _horizontalBottomRight = data.DataPoints[3];

            _vRectangleGeometry.Rect = new Rect(_verticalTopLeft, _verticalBottomRight);
            _hRectangleGeometry.Rect = new Rect(_horizontalTopLeft, _horizontalBottomRight);

            // Write center field only; CenterPoint setter rewrites horizontal corners.
            _centerPoint = new Point(
                (_verticalTopLeft.X + _verticalBottomRight.X) / 2,
                (_verticalTopLeft.Y + _verticalBottomRight.Y) / 2);

            // Handles must exist before StrokeThickness (OnStrokeThicknessChanges indexes them).
            CreateHandles();
            StrokeThickness = data.StrokeThickness;
            UpdateHandleLocation();

            IsGeometryRendered = true;
            UpdateVisual();
        }

        /// <summary>
        /// Exports four corners matching <see cref="FromData"/>:
        /// VerticalTopLeft, VerticalBottomRight, HorizontalTopLeft, HorizontalBottomRight.
        /// </summary>
        public PointsData GetMetaData()
        {
            return new PointsData(
                StrokeThickness,
                new List<Point>
                {
                    VerticalTopLeft,
                    VerticalBottomRight,
                    HorizontalTopLeft,
                    HorizontalBottomRight
                });
        }

        #endregion

        #region others

        protected override void OnStrokeThicknessChanges(double strokeThickness)
        {
            if (_dragHandles.TryGetValue(DragLocations.ResizeHandle, out var resizeHandle))
            {
                resizeHandle.GeometryCenter = GetResizeHandleLocation();
            }
        }

        protected override void CreateHandles()
        {
            if (_dragHandles.Count == 0)
            {
                RegisterHandle(new RectDragHandle(DragHandleSize, _hRectangleGeometry.Rect.Location,
                    (int)DragLocations.HTopLeft));
                RegisterHandle(new RectDragHandle(DragHandleSize, _hRectangleGeometry.Rect.BottomRight,
                    (int)DragLocations.HBottomRight));

                RegisterHandle(new RectDragHandle(DragHandleSize, _vRectangleGeometry.Rect.BottomRight,
                    (int)DragLocations.VBottomRight));
                RegisterHandle(new RectDragHandle(DragHandleSize, _vRectangleGeometry.Rect.TopLeft,
                    (int)DragLocations.VTopLeft));

                RegisterHandle(new RectDragHandle(DragHandleSize, GetResizeHandleLocation(),
                    (int)DragLocations.ResizeHandle));

                _dragHandles = Handles.ToDictionary(x => (DragLocations)x.Id);
            }
        }

        private Point GetResizeHandleLocation()
        {
            return _vRectangleGeometry.Rect.Location +
                   new Vector(_vRectangleGeometry.Rect.Width / 2, -StrokeThickness / 2);
        }


        protected override void HandleTranslate(Point newPoint)
        {
            if (OldPointForTranslate.HasValue)
            {
                var offset = newPoint - OldPointForTranslate.Value;
                VerticalTopLeft += offset;
                VerticalBottomRight += offset;

                HorizontalBottomRight += offset;
                HorizontalTopLeft += offset;

                UpdateHandleLocation();
                UpdateVisual();
                OldPointForTranslate = newPoint;
            }
        }


        /// <summary>
        ///     left mouse button down event
        /// </summary>
        /// <param name="mousePoint"></param>
        public override void OnMouseLeftButtonDown(Point mousePoint)
        {
            if (!IsGeometryRendered)
            {
                VerticalTopLeft = mousePoint;
            }
            else
            {
                FindSelectedHandle(mousePoint);
            }

            OldPointForTranslate = mousePoint;
        }


        /// <summary>
        ///     ������ƶ�
        /// </summary>
        public override void OnMouseMove(Point point, MouseButtonState buttonState)
        {
            if (buttonState == MouseButtonState.Pressed)
            {
                if (!IsGeometryRendered)
                {
                    VerticalBottomRight = GetValidValueFromPoint(DragLocations.VBottomRight, point);
                    UpdateVisual();
                }
                else if (SelectedDragHandle != null)
                {
                    IsBeingDraggedOrPanMoving = true;

                    switch ((DragLocations)SelectedDragHandle.Id)
                    {
                        case DragLocations.VTopLeft:
                            VerticalTopLeft = GetValidValueFromPoint(DragLocations.VTopLeft, point);
                            break;
                        case DragLocations.VBottomRight:

                            VerticalBottomRight = GetValidValueFromPoint(DragLocations.VBottomRight, point);

                            break;
                        case DragLocations.HTopLeft:

                            HorizontalTopLeft = GetValidValueFromPoint(DragLocations.HTopLeft, point);

                            break;
                        case DragLocations.HBottomRight:

                            HorizontalBottomRight = GetValidValueFromPoint(DragLocations.HBottomRight, point);

                            break;
                        case DragLocations.ResizeHandle:

                            if (OldPointForTranslate != null)
                            {
                                var delta = point - OldPointForTranslate.Value;
                                StrokeThickness += -delta.Y;
                                OldPointForTranslate = point;
                            }

                            break;
                    }

                    UpdateHandleLocation();
                    UpdateVisual();
                }
                else
                {
                    Mouse.SetCursor(Cursors.Hand);
                    HandleTranslate(point);
                }
            }
        }


        private Point GetValidValueFromPoint(DragLocations dragLocation, Point point)
        {
            switch (dragLocation)
            {
                case DragLocations.VTopLeft:
                    return ForcePointInRange(point,
                        HorizontalTopLeft.X + MinPixelDistance,
                        VerticalBottomRight.X - MinPixelDistance,
                        0,
                        HorizontalTopLeft.Y - MinPixelDistance);

                case DragLocations.VBottomRight:

                    return ForcePointInRange(
                        point,
                        VerticalTopLeft.X + MinPixelDistance,
                        HorizontalBottomRight.X - MinPixelDistance,
                        HorizontalBottomRight.Y + MinPixelDistance,
                        point.Y);

                case DragLocations.HTopLeft:
                    return ForcePointInRange(
                        point,
                        0,
                        VerticalTopLeft.X - MinPixelDistance,
                        VerticalTopLeft.Y + MinPixelDistance,
                        HorizontalBottomRight.Y - MinPixelDistance);
                case DragLocations.HBottomRight:

                    return ForcePointInRange(
                        point,
                        VerticalBottomRight.X + MinPixelDistance,
                        point.X,
                        HorizontalTopLeft.Y + MinPixelDistance,
                        VerticalBottomRight.Y - MinPixelDistance);
                default:
                    return point;
            }
        }




        public override void UpdateVisual()
        {
            if (DistanceResizeHandle == null)
            {
                return;
            }


            var renderContext = RenderOpen();

            Pen ??= ShapeStyler?.SketchPen.CloneCurrentValue();

            if (ShapeStyler != null && Pen != null)
            {
                Pen.Brush.Opacity = 0.5;
                Pen.Thickness = StrokeThickness;
                renderContext.DrawGeometry(ShapeStyler.FillColor, Pen, RenderGeometry);
            }

            AddTagText(renderContext, VerticalTopLeft - new Vector(0, ShapeLayer.TagFontSize + StrokeThickness));
            DrawDragHandles(renderContext);
            renderContext.Close();
        }

        private enum DragLocations
        {
            None = 0,
            VTopLeft,
            VTopRight,
            VBottomLeft,
            VBottomRight,
            HTopLeft,
            HTopRight,
            HBottomLeft,
            HBottomRight,
            ResizeHandle
        }

        #endregion



        #region constructor

        #endregion
    }
}
