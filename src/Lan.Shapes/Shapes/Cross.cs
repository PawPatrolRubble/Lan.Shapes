using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using Lan.Shapes.Enums;
using Lan.Shapes.ExtensionMethods;
using Lan.Shapes.Handle;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;

namespace Lan.Shapes.Shapes
{
    public class Cross : ShapeVisualBase, IDataExport<CrossData>
    {


        private readonly LineGeometry _verticalLine = new LineGeometry();
        private readonly LineGeometry _horizontalLine = new LineGeometry();
        private Point _center;
        private int _height;
        private int _width;

        public Cross(ShapeLayer layer) : base(layer)
        {

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
        }


        public override Rect BoundsRect { get; }
        protected override void CreateHandles()
        {
            //throw new NotImplementedException();
        }

        protected override void HandleResizing(Point point)
        {
            //throw new NotImplementedException();
        }

        protected override void HandleTranslate(Point newPoint)
        {
            //throw new NotImplementedException();
        }

        public override void OnDeselected()
        {
            //throw new NotImplementedException();
        }

        public override void OnSelected()
        {
            //throw new NotImplementedException();
        }


        public override void OnMouseLeftButtonDown(Point mousePoint)
        {
            //base.OnMouseLeftButtonDown(mousePoint);
            //if (!IsGeometryRendered)
            //{
                
            //}
        }
        
        public void FromData(CrossData data)
        {
            Center = data.Center;
            Width = data.Width;
            Height = data.Height;
            ShapeStyler.SetStrokeThickness(data.StrokeThickness);
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
                renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, _verticalLine);
                renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, _horizontalLine);
                DrawText(renderContext);
            }

            renderContext.Close();
        }
    }
}