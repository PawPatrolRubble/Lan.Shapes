using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using Lan.Shapes.Handle;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;

namespace Lan.Shapes.Shapes
{
    public class Line : ShapeVisualBase, IDataExport<PointsData>
    {
        #region constructor

        public Line(ShapeLayer layer) : base(layer)
        {

            DragHandleSize = ShapeStyler.DragHandleSize;
            _leftDragHandle = RegisterHandle(new RectDragHandle(DragHandleSize, default, 1));
            _rightDragHandle = RegisterHandle(new RectDragHandle(DragHandleSize, default, 2));
            _panHandle = RegisterHandle(new RectDragHandle(DragHandleSize, default, 2));

            RenderGeometryGroup.Children.Add(_lineGeometry);

        }

        #endregion

        #region private fields

        private readonly DragHandle _panHandle;
        private readonly DragHandle _leftDragHandle; //= new RectDragHandle(10, default, 1);
        private readonly LineGeometry _lineGeometry = new LineGeometry();
        private readonly DragHandle _rightDragHandle; // = new RectDragHandle(10, default, 2);

        private Point _end;
        private Point _start;

        #endregion

        #region properties

        public Point Start
        {
            get { return _start; }
            set
            {
                SetField(ref _start, value);
                UpdateGeometry();
            }
        }

        public Point End
        {
            get { return _end; }
            set
            {
                SetField(ref _end, value);
                UpdateGeometry();
            }
        }


        public override Rect BoundsRect
        {
            get { return RenderGeometryGroup.Bounds; }
        }

        #endregion

        #region other members

        private void UpdateGeometry()
        {
            _lineGeometry.StartPoint = Start;
            _lineGeometry.EndPoint = End;

            _leftDragHandle.GeometryCenter = Start;
            _rightDragHandle.GeometryCenter = End;
            _panHandle.GeometryCenter = new Point((Start.X + End.X) / 2, (Start.Y + End.Y) / 2);

            UpdateVisual();
        }

        protected override void CreateHandles()
        {

        }

        protected override void HandleResizing(Point point)
        {

        }

        protected override void HandleTranslate(Point newPoint)
        {
            if (OldPointForTranslate.HasValue)
            {
                Start += newPoint - OldPointForTranslate.Value;
                End += newPoint - OldPointForTranslate.Value;
                UpdateVisual();
                OldPointForTranslate = newPoint;
            }
        }

        public override void OnMouseLeftButtonDown(Point mousePoint)
        {
            base.OnMouseLeftButtonDown(mousePoint);
            if (!IsGeometryRendered)
            {
                Start = mousePoint;
                End = mousePoint + new Vector(10, 10);
            }
            else
            {
                FindSelectedHandle(mousePoint);
            }

            OldPointForTranslate = mousePoint;
        }

        public override void FindSelectedHandle(Point p)
        {
            base.FindSelectedHandle(p);
        }

        public override void OnMouseMove(Point point, MouseButtonState buttonState)
        {
            var oldPointForTranslate = OldPointForTranslate;

            base.OnMouseMove(point, buttonState);

            if (buttonState == MouseButtonState.Pressed)
            {
                if (!IsGeometryRendered)
                {
                    End = point;
                }
                else // handle reallocation of start and end points of line
                {

                    if (SelectedDragHandle != null)
                    {
                        switch (SelectedDragHandle)
                        {
                            case var handle when handle == _leftDragHandle:
                                Start = point;
                                break;
                            case var handle when handle == _rightDragHandle:
                                End = point;
                                break;
                            default:
                                OldPointForTranslate = oldPointForTranslate;
                                HandleTranslate(point);
                                break;
                        }
                    }
                }
            }
        }

        public override void OnMouseLeftButtonUp(Point newPoint)
        {
            base.OnMouseLeftButtonUp(newPoint);
            SelectedDragHandle = null;
        }

        #endregion

        private void DrawLengthText(DrawingContext renderContext)
        {
            // Draw the length text
            var length = Math.Sqrt(Math.Pow(End.X - Start.X, 2) + Math.Pow(End.Y - Start.Y, 2));
            var lengthInMm = 0.0;
            var measurement = ShapeLayer.Measurement;

            if (measurement.UnitsPerMillimeter != 0 && measurement.PixelPerUnit != 0)
            {
                lengthInMm = length * measurement.UnitsPerMillimeter / measurement.PixelPerUnit;
            }

            var formattedText = CreateFormattedText(
                $"{lengthInMm:f4} {measurement.UnitName}, {length:f4} px",
                ShapeStyler?.TagColor ?? Brushes.Red);

            renderContext.DrawText(formattedText, new Point((Start.X + End.X) / 2, (Start.Y + End.Y) / 2));
        }

        #region Overrides of ShapeVisualBase

        protected override void UpdateVisualOnLocked()
        {
            UpdateVisual();
        }

        public override void UpdateVisual()
        {
            if (ShapeStyler == null)
            {
                return;
            }

            var renderContext = RenderOpen();
            renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, RenderGeometryGroup);
            DrawDragHandles(renderContext);
            DrawLengthText(renderContext);
            renderContext.Close();
        }

        #endregion


        public void FromData(PointsData data)
        {
            if (data.DataPoints.Count != 2)
            {
                throw new Exception($"{nameof(PointsData)} must have 2 elements in  DataPoints");
            }

            Start = data.DataPoints[0];
            End = data.DataPoints[1];
            IsGeometryRendered = true;
            UpdateVisual();
        }

        public PointsData GetMetaData()
        {
            return new PointsData(1, new List<Point>()
            {
                Start,
                End
            });
        }
    }
}
