#nullable enable

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes.Enums;
using Lan.Shapes.ExtensionMethods;
using Lan.Shapes.Handle;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;

#endregion

namespace Lan.Shapes.Shapes
{
    public class Rectangle : ShapeVisualBase, IDataExport<PointsData>
    {
        #region fields

        private RectangleGeometry? _rectangleGeometry;
        private Point _bottomRight;
        private Point _topLeft;

        #endregion

        private TagPosition _tagPosition;

        #region Properties

        public Point BottomRight
        {
            get => _bottomRight;
            set
            {
                SetField(ref _bottomRight, value);

                if (_rectangleGeometry != null)
                {
                    _rectangleGeometry.Rect = new Rect(TopLeft, value);
                    UpdateHandleLocation();
                    RequestVisualUpdate();
                }

                OnPropertyChanged(nameof(Width));
                OnPropertyChanged(nameof(Height));
            }
        }

        public double Width
        {
            get => Math.Abs(BottomRight.X - TopLeft.X);
            set
            {
                if (Math.Abs(Width - value) > 0.000001 && value >= 0)
                {
                    var sign = BottomRight.X >= TopLeft.X ? 1 : -1;
                    BottomRight = new Point(TopLeft.X + sign * value, BottomRight.Y);
                    OnPropertyChanged();
                }
            }
        }

        public double Height
        {
            get => Math.Abs(BottomRight.Y - TopLeft.Y);
            set
            {
                if (Math.Abs(Height - value) > 0.000001 && value >= 0)
                {
                    var sign = BottomRight.Y >= TopLeft.Y ? 1 : -1;
                    BottomRight = new Point(BottomRight.X, TopLeft.Y + sign * value);
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public override Rect BoundsRect
        {
            get => RenderGeometry.Bounds;
        }

        public override IEnumerable<Point> GetSnapPoints()
        {
            var rectangle = new Rect(TopLeft, BottomRight);
            return new[] { rectangle.TopLeft, rectangle.TopRight, rectangle.BottomRight, rectangle.BottomLeft };
        }

        public Point TopLeft
        {
            get => _topLeft;
            set
            {
                SetField(ref _topLeft, value);

                if (_rectangleGeometry == null)
                {
                    _rectangleGeometry = new RectangleGeometry();
                    RenderGeometryGroup.Children.Add(_rectangleGeometry);
                    _rectangleGeometry.Rect = new Rect(value, value);
                }
                else
                {
                    _rectangleGeometry.Rect = new Rect(value, BottomRight);
                }

                UpdateHandleLocation();
                RequestVisualUpdate();
                OnPropertyChanged(nameof(Width));
                OnPropertyChanged(nameof(Height));
            }
        }

        #endregion


        #region Implementations

        public void FromData(PointsData data)
        {
            if (data.DataPoints.Count != 2)
            {
                throw new Exception($"{nameof(PointsData)} must have 2 elements in  DataPoints");
            }

            //create handles

            CreateHandles();
            TopLeft = data.DataPoints[0];
            BottomRight = data.DataPoints[1];
            IsGeometryRendered = true;

            Tag = data.Tag;
            _tagPosition = data.TagPosition;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public PointsData GetMetaData()
        {
            return new PointsData(0,
                new List<Point> { _rectangleGeometry.Rect.TopLeft, _rectangleGeometry.Rect.BottomRight });
        }

        #endregion

        #region others

        protected override void CreateHandles()
        {
            if (Handles.Count > 0)
            {
                return;
            }

            foreach (var id in Enumerable.Range(1, 4))
            {
                RegisterHandle(new RectDragHandle(DragHandleSize, default, id));
            }
        }

        protected override void HandleResizing(Point point)
        {
            if (SelectedDragHandle != null)
                switch (SelectedDragHandle.Id)
                {
                    case 1:
                        TopLeft = ForcePointInRange(point, 0, BottomRight.X, 0, BottomRight.Y);
                        break;
                    case 2:

                        var validPointTopRight = ForcePointInRange(point, TopLeft.X, point.X, 0, BottomRight.Y);
                        TopLeft = new Point(TopLeft.X, validPointTopRight.Y);
                        BottomRight = new Point(validPointTopRight.X, BottomRight.Y);
                        break;
                    case 3:
                        BottomRight = ForcePointInRange(point, TopLeft.X, point.X, TopLeft.Y, point.Y);
                        break;
                    case 4:
                        var validPointBottomLeft = ForcePointInRange(point, 0, BottomRight.X, TopLeft.Y, point.Y);

                        TopLeft = new Point(validPointBottomLeft.X, TopLeft.Y);
                        BottomRight = new Point(BottomRight.X, validPointBottomLeft.Y);
                        break;
                }
        }

        protected override void HandleTranslate(Point newPoint)
        {
            if (OldPointForTranslate.HasValue)
            {
                Translate(newPoint - OldPointForTranslate.Value);
                OldPointForTranslate = newPoint;
            }
        }

        public override bool CanTranslate => true;

        protected override void TranslateCore(Vector delta)
        {
            SetField(ref _topLeft, TopLeft + delta, nameof(TopLeft));
            SetField(ref _bottomRight, BottomRight + delta, nameof(BottomRight));
            if (_rectangleGeometry != null) _rectangleGeometry.Rect = new Rect(TopLeft, BottomRight);
            UpdateHandleLocation();
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
                CreateHandles();
            }
            else
            {
                FindSelectedHandle(mousePoint);
            }

            OldPointForTranslate = mousePoint;
            MouseDownPoint = mousePoint;
        }


        /// <summary>
        /// 鼠标点击移动
        /// </summary>
        public override void OnMouseMove(Point point, MouseButtonState buttonState)
        {
            if (IsLocked || (buttonState == MouseButtonState.Pressed && !MouseDownPoint.HasValue)) return;

            if (buttonState == MouseButtonState.Pressed)
            {
                if (!IsGeometryRendered)
                {
                    BottomRight = ForcePointInRange(point, TopLeft.X, point.X, TopLeft.Y, point.Y);
                }
                else if (SelectedDragHandle != null)
                {
                    IsBeingDraggedOrPanMoving = true;
                    HandleResizing(point);
                }
                else if (IsGeometryRendered && _rectangleGeometry != null)
                {
                    // If already dragging, continue. Otherwise check if mouse down was inside the rectangle
                    if (IsBeingDraggedOrPanMoving || (MouseDownPoint.HasValue && _rectangleGeometry.FillContains(MouseDownPoint.Value)))
                    {
                        IsBeingDraggedOrPanMoving = true;
                        HandleTranslate(point);
                    }
                }
            }
        }

        private void UpdateHandleLocation()
        {
            for (var i = 0; i < Handles.Count + 1; i++)
                switch (i)
                {
                    case 1:
                        Handles[i - 1].GeometryCenter = TopLeft;
                        break;
                    case 2:
                        Handles[i - 1].GeometryCenter = new Point(BottomRight.X, TopLeft.Y);
                        break;
                    case 3:
                        Handles[i - 1].GeometryCenter = BottomRight;
                        break;
                    case 4:
                        Handles[i - 1].GeometryCenter = new Point(TopLeft.X, BottomRight.Y);
                        break;
                }
        }

        protected override void DrawShape(DrawingContext renderContext)
        {
            if (_rectangleGeometry == null)
            {
                return;
            }

            if (ShapeStyler != null)
            {
                AddTagText(renderContext, GetTagPosition());

                renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, RenderGeometryGroup);
            }
        }

        private Point GetTagPosition()
        {
            switch (_tagPosition)
            {
                case TagPosition.Center:
                    return TopLeft.MiddleWith(BottomRight) - new Vector(AnnotationFontSize / 2, AnnotationFontSize / 2);

                case TagPosition.Top:
                    return TopLeft - new Vector(0, AnnotationFontSize);

                case TagPosition.Bottom:
                    return TopLeft + new Vector(0, BottomRight.Y - TopLeft.Y + AnnotationFontSize);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        public Rectangle(ShapeLayer layer) : base(layer)
        {
        }
    }
}
