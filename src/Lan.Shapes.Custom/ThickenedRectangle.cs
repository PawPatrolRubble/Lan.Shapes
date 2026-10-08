#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes.Handle;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;

#endregion

namespace Lan.Shapes.Custom
{
    public class ThickenedRectangle : CustomGeometryBase, IDataExport<PointsData>
    {
        #region fields

        private readonly RectangleGeometry _middleRectangleGeometry = new RectangleGeometry();

        private Point _bottomRight;

        private Dictionary<DragLocation, DragHandle> _dragHandleDict;
        private Vector _offsetVector;
        private Point _topLeft;

        #endregion

        #region Properties

        public override IEnumerable<Point> GetSnapPoints()
        {
            var rectangle = new Rect(TopLeft, BottomRight);
            return new[] { rectangle.TopLeft, rectangle.TopRight, rectangle.BottomRight, rectangle.BottomLeft };
        }

        public Point BottomRight
        {
            get => _bottomRight;
            set
            {
                _bottomRight = value;
                if (_bottomRight != default)
                {
                    ResizeByCornerPoint(DragLocation.BottomRight, _bottomRight);
                    RequestVisualUpdate();
                }
            }
        }

        public Point TopLeft
        {
            get => _topLeft;
            set
            {
                _topLeft = value;
                if (_topLeft != default)
                {
                    if (!IsGeometryRendered)
                        CreateGeometry(_topLeft);
                    else
                        ResizeByCornerPoint(DragLocation.TopLeft, _topLeft);

                    RequestVisualUpdate();
                }
            }
        }

        #endregion

        #region Constructors

        public ThickenedRectangle(ShapeLayer shapeLayer) : base(shapeLayer)
        {
            RenderGeometryGroup.Children.Add(_middleRectangleGeometry);
            RegisterHandle(DistanceResizeHandle);
        }

        #endregion

        #region implementations

        public void FromData(PointsData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.DataPoints == null || data.DataPoints.Count != 2)
            {
                throw new ArgumentException(
                    $"{nameof(PointsData)} must have exactly 2 elements in {nameof(PointsData.DataPoints)} (TopLeft, BottomRight).",
                    nameof(data));
            }

            CreateHandles();
            TopLeft = data.DataPoints[0];
            BottomRight = data.DataPoints[1];
            StrokeThickness = data.StrokeThickness;
            IsGeometryRendered = true;
            RequestVisualUpdate();
        }

        /// <summary>
        /// 输出左上，右下两个点，及stroke thickness
        /// </summary>
        /// <returns></returns>
        public PointsData GetMetaData()
        {
            return new PointsData(StrokeThickness, new List<Point>() { TopLeft, BottomRight });
        }

        public override bool CanTranslate => true;

        protected override void TranslateCore(Vector delta)
        {
            SetField(ref _topLeft, TopLeft + delta, nameof(TopLeft));
            SetField(ref _bottomRight, BottomRight + delta, nameof(BottomRight));
            _middleRectangleGeometry.Rect = new Rect(TopLeft, BottomRight);
            ResizeByCornerPoint(DragLocation.BottomRight, BottomRight);
        }

        #endregion

        #region others

        private void CreateGeometry(Point topLeft)
        {
            _middleRectangleGeometry.Rect = new Rect(topLeft, new Size());
        }

        protected override void CreateHandles()
        {
            if (Handles.Count > 1)
            {
                return;
            }

            RegisterHandle(new RectDragHandle(new Size(ShapeStyler.DragHandleSize, ShapeStyler.DragHandleSize), default, 10, (int)DragLocation.TopLeft));
            RegisterHandle(new RectDragHandle(new Size(ShapeStyler.DragHandleSize, ShapeStyler.DragHandleSize), default, 10, (int)DragLocation.TopRight));
            RegisterHandle(new RectDragHandle(new Size(ShapeStyler.DragHandleSize, ShapeStyler.DragHandleSize), default, 10, (int)DragLocation.BottomLeft));
            RegisterHandle(new RectDragHandle(new Size(ShapeStyler.DragHandleSize, ShapeStyler.DragHandleSize), default, 10, (int)DragLocation.BottomRight));

            _dragHandleDict = Handles.ToDictionary(x => (DragLocation)x.Id);
        }


        protected override void DrawGeometryInMouseMove(Point oldPoint, Point newPoint)
        {
        }

        public override void FindSelectedHandle(Point p)
        {
            base.FindSelectedHandle(p);
        }


        /// <summary>
        /// left mouse button down event
        /// </summary>
        /// <param name="mousePoint"></param>
        public override void OnMouseLeftButtonDown(Point mousePoint)
        {
            if (IsLocked) return;
            if (!IsGeometryRendered)
            {
                TopLeft = mousePoint;
            }
            else
            {
                FindSelectedHandle(mousePoint);
                OldPointForTranslate = mousePoint;
            }

            MouseDownPoint = mousePoint;
        }


        /// <summary>
        /// 鼠标点击
        /// </summary>
        public override void OnMouseMove(Point point, MouseButtonState buttonState)
        {
            if (IsLocked || !HasPointerInteraction) return;
            if (!IsGeometryRendered)
            {
                if (buttonState == MouseButtonState.Pressed) BottomRight = point;
            }
            else
            {
                if (buttonState == MouseButtonState.Pressed)
                {

                    if (SelectedDragHandle != null)
                    {
                        IsBeingDraggedOrPanMoving = true;
                        if (SelectedDragHandle.Id == 99)
                        {
                            if (OldPointForTranslate != null)
                            {
                                StrokeThickness += -(point - OldPointForTranslate.Value).Y;
                                OldPointForTranslate = point;
                            }
                        }
                        else
                        {
                            switch ((DragLocation)SelectedDragHandle.Id)
                            {
                                case DragLocation.TopLeft:
                                    TopLeft = point;
                                    break;
                                case DragLocation.TopRight:
                                    BottomRight = new Point(point.X, BottomRight.Y);
                                    TopLeft = new Point(TopLeft.X, point.Y);
                                    break;
                                case DragLocation.BottomRight:
                                    BottomRight = point;
                                    break;
                                case DragLocation.BottomLeft:
                                    TopLeft = new Point(point.X, TopLeft.Y);
                                    BottomRight = new Point(BottomRight.X, point.Y);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }
                    }
                    else
                    {
                        //handle translate
                        if (OldPointForTranslate != null)
                        {
                            SetMouseCursorToHand();
                            TopLeft += point - OldPointForTranslate.Value;
                            BottomRight += point - OldPointForTranslate.Value;
                            OldPointForTranslate = point;
                        }
                    }
                }
            }
        }


        protected override void OnStrokeThicknessChanges(double strokeThickness)
        {
            _offsetVector.X = StrokeThickness / 2;
            _offsetVector.Y = StrokeThickness / 2;

            //update location of handle
            DistanceResizeHandle.GeometryCenter = _middleRectangleGeometry.Rect.Location +
                                                  new Vector(_middleRectangleGeometry.Rect.Width / 2,
                                                      -StrokeThickness / 2);

            RequestVisualUpdate();
        }


        private void ResizeByCornerPoint(DragLocation location, Point point)
        {
            switch (location)
            {
                case DragLocation.TopLeft:
                    _middleRectangleGeometry.Rect = new Rect(point, BottomRight);
                    break;
                case DragLocation.BottomRight:
                    _middleRectangleGeometry.Rect = new Rect(TopLeft, point);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(location), location, null);
            }

            CreateHandles();

            //update location of handle
            DistanceResizeHandle.GeometryCenter = _middleRectangleGeometry.Rect.Location +
                                                  new Vector(_middleRectangleGeometry.Rect.Width / 2,
                                                      -StrokeThickness / 2);

            _dragHandleDict[DragLocation.TopLeft].GeometryCenter = TopLeft;
            _dragHandleDict[DragLocation.TopRight].GeometryCenter = new Point(BottomRight.X, TopLeft.Y);
            _dragHandleDict[DragLocation.BottomRight].GeometryCenter = BottomRight;
            _dragHandleDict[DragLocation.BottomLeft].GeometryCenter = new Point(TopLeft.X, BottomRight.Y);
        }


        #region local field

        protected override void DrawShape(DrawingContext renderContext)
        {
            if (DistanceResizeHandle == null)
            {
                return;
            }
            Pen ??= ShapeStyler?.SketchPen.CloneCurrentValue();

            if (ShapeStyler != null && Pen != null)
            {
                Pen.Brush.Opacity = 0.5;
                Pen.Thickness = StrokeThickness;
                renderContext.DrawGeometry(ShapeStyler.FillColor, Pen, RenderGeometry);
            }


            AddTagText(renderContext, TopLeft - new Vector(0, AnnotationFontSize + StrokeThickness));
        }

        #endregion

        #endregion
    }
}
