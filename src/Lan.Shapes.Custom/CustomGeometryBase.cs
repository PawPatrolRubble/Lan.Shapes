#region

using System;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes.Handle;

#endregion

namespace Lan.Shapes.Custom
{
    public abstract class CustomGeometryBase : ShapeVisualBase
    {
        #region fields

        protected double MaxStrokeThickness { get; private set; }

        #endregion

        #region fields

        protected readonly DragHandle
            DistanceResizeHandle; //= new RectDragHandle(new Size(10, 10), new Point(), 10, 99);

        protected readonly SolidColorBrush DragHandleFillColor = Brushes.Aquamarine;
        protected readonly Pen DragHandlePen; // = new Pen(Brushes.Red, 1);
        private double _strokeThickness = 15;
        protected Pen? Pen;

        #endregion

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public override Rect BoundsRect { get; }

        protected double StrokeThickness
        {
            get => _strokeThickness;
            set
            {
                _strokeThickness = value;
                _strokeThickness = Math.Min(MaxStrokeThickness, _strokeThickness);
                _strokeThickness = Math.Max(0, _strokeThickness);

                if (Pen != null)
                {
                    Pen.Thickness = StrokeThickness;
                }

                //update handle position, when stroke thickness changes
                OnStrokeThicknessChanges(_strokeThickness);
            }
        }

        #endregion

        #region Constructors

        public CustomGeometryBase(ShapeLayer shapeLayer) : base(shapeLayer)
        {
            DistanceResizeHandle = new RectDragHandle(new Size(DragHandleSize, DragHandleSize), new Point(), 10, 99);
            DragHandlePen = ShapeStyler?.SketchPen ?? new Pen(Brushes.Red, 1);
            MaxStrokeThickness = shapeLayer.MaximumThickenedShapeWidth;
        }

        #endregion

        #region others

        protected override void CreateHandles()
        {
        }

        protected override void HandleResizing(Point point)
        {
        }

        protected override void HandleTranslate(Point newPoint)
        {
        }

        /// <summary>
        /// 未选择状态 — base no-op; selection styling is driven by State.
        /// </summary>
        public override void OnDeselected()
        {
        }


        /// <summary>
        /// 选择时 — base no-op; selection styling is driven by State.
        /// </summary>
        public override void OnSelected()
        {
        }

        protected override Brush? GetDragHandleFill() => DragHandleFillColor;

        protected override Pen? GetDragHandlePen() => DragHandlePen;

        protected abstract void OnStrokeThicknessChanges(double strokeThickness);

        #endregion
    }
}
