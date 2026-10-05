#region

#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes.Handle;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;

#endregion

namespace Lan.Shapes.Shapes
{
    public class Ellipse : ShapeVisualBase, IDataExport<EllipseData>
    {
        #region fields

        private readonly EllipseGeometry _ellipseGeometry = new EllipseGeometry(default);

        private DragHandle _rightDragHandle; //= new RectDragHandle(10, default, 1);
        private DragHandle _topDragHandle;// = new RectDragHandle(10, default, 2);

        private Point _center;
        private double _radiusX;
        private double _radiusY;

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public override Rect BoundsRect => RenderGeometry.Bounds;

        public Point Center
        {
            get => _center;
            set
            {
                SetField(ref _center, value);
                _ellipseGeometry.Center = value;
                _rightDragHandle.GeometryCenter = value + new Vector(RadiusX, 0);
                _topDragHandle.GeometryCenter = value + new Vector(0, -RadiusY);
                RequestVisualUpdate();
            }
        }

        public double RadiusX
        {
            get => _radiusX;
            set
            {
                SetField(ref _radiusX, value);
                _ellipseGeometry.RadiusX = value;
                _rightDragHandle.GeometryCenter = Center + new Vector(value, 0);
                RequestVisualUpdate();
            }
        }

        public double RadiusY
        {
            get => _radiusY;
            set
            {
                SetField(ref _radiusY, value);
                _ellipseGeometry.RadiusY = value;
                _topDragHandle.GeometryCenter = Center + new Vector(0, -value);
                RequestVisualUpdate();
            }
        }

        #endregion

        #region Constructors

        public Ellipse(ShapeLayer shapeLayer) : base(shapeLayer)
        {
            RenderGeometryGroup.Children.Add(_ellipseGeometry);
            _rightDragHandle = RegisterHandle(new RectDragHandle(DragHandleSize, default, 1));
            _topDragHandle = RegisterHandle(new RectDragHandle(DragHandleSize, default, 2));
        }

        #endregion

        #region others

        protected override void CreateHandles()
        {
        }

        protected override void DrawGeometryInMouseMove(Point oldPoint, Point newPoint)
        {
            RadiusX = (newPoint.X - oldPoint.X) / 2;
            RadiusY = (newPoint.Y - oldPoint.Y) / 2;
        }

        protected override void HandleResizing(Point point)
        {
            if (MouseDownPoint != null && OldPointForTranslate != null)
            {
                switch (SelectedDragHandle!.Id)
                {
                    case 2:
                        RadiusY += OldPointForTranslate.Value.Y - point.Y;
                        break;

                    case 1:
                        RadiusX += point.X - OldPointForTranslate.Value.X;
                        break;
                }
            }

            OldPointForTranslate = point;
        }

        protected override void HandleTranslate(Point newPoint)
        {
            if (!MouseDownPoint.HasValue) return;

            Translate(newPoint - MouseDownPoint.Value);
            MouseDownPoint = newPoint;
        }

        public override bool CanTranslate => true;

        protected override void TranslateCore(Vector delta) => Center += delta;

        /// <summary>
        /// add geometries to group
        /// </summary>
        protected override void UpdateGeometryGroup()
        {
        }

        protected override void DrawShape(DrawingContext renderContext)
        {
            if (ShapeStyler != null && _rightDragHandle != null && _topDragHandle != null)
            {
                renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, RenderGeometry);

                AddTagText(renderContext, Center);
            }
        }

        #endregion

        public void FromData(EllipseData data)
        {
            Center = data.Center;
            RadiusX = data.RadiusX;
            RadiusY = data.RadiusY;
            IsGeometryRendered = true;
            RequestVisualUpdate();
        }

        public EllipseData GetMetaData()
        {

            return new EllipseData()
            {
                Center = Center,
                RadiusX = RadiusX,
                RadiusY = RadiusY,
            };
        }
    }
}
