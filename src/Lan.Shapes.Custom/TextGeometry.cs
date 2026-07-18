using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;

namespace Lan.Shapes.Custom
{
    public class TextGeometry : ShapeVisualBase, IDataExport<TextGeometryData>
    {
        private TextGeometryData? _textGeometryData;
        private Geometry? _geometry;

        public TextGeometry(ShapeLayer shapeLayer) : base(shapeLayer)
        {
        }

        public void FromData(TextGeometryData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            _textGeometryData = new TextGeometryData(data.Location, data.Content, data.FontSize)
            {
                StrokeThickness = data.StrokeThickness
            };
            IsGeometryRendered = true;
            UpdateVisual();
        }

        public TextGeometryData GetMetaData()
        {
            if (_textGeometryData == null)
            {
                return new TextGeometryData(default, string.Empty, 12)
                {
                    StrokeThickness = ShapeStyler?.SketchPen.Thickness ?? 1
                };
            }

            return new TextGeometryData(
                _textGeometryData.Location,
                _textGeometryData.Content,
                _textGeometryData.FontSize)
            {
                StrokeThickness = ShapeStyler?.SketchPen.Thickness
                    ?? _textGeometryData.StrokeThickness
            };
        }

        public override Rect BoundsRect => _geometry?.Bounds ?? Rect.Empty;

        protected override void CreateHandles()
        {
        }

        protected override void HandleResizing(Point point)
        {
        }

        protected override void HandleTranslate(Point newPoint)
        {
        }

        protected override void UpdateGeometryGroup()
        {
        }

        public override void OnDeselected()
        {
        }

        public override void OnMouseLeftButtonDown(Point mousePoint)
        {
        }

        public override void OnSelected()
        {
        }

        public override void UpdateVisual()
        {
            if (ShapeStyler == null)
            {
                return;
            }

            if (_textGeometryData == null || string.IsNullOrWhiteSpace(_textGeometryData.Content))
            {
                return;
            }

            var render = RenderOpen();
            try
            {
                _geometry = GetTextGeometry(_textGeometryData);

                _geometry.Transform = new TransformGroup();
                var scaleTransform = new ScaleTransform(
                    -1,
                    1,
                    _geometry.Bounds.TopLeft.X,
                    _geometry.Bounds.TopLeft.Y);

                ((TransformGroup)_geometry.Transform).Children.Add(scaleTransform);
                ((TransformGroup)_geometry.Transform).Children.Add(new TranslateTransform(700, 0));

                if (_textGeometryData.StrokeThickness > 0)
                {
                    ShapeStyler.SetStrokeThickness(_textGeometryData.StrokeThickness);
                }

                render.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, _geometry);
            }
            finally
            {
                render.Close();
            }
        }

        public static string Convert(Geometry geometry)
        {
            if (geometry == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var pathGeometry = geometry.GetFlattenedPathGeometry();

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(
                -1,
                1,
                pathGeometry.Bounds.TopLeft.X,
                pathGeometry.Bounds.TopLeft.Y));
            transformGroup.Children.Add(new TranslateTransform(pathGeometry.Bounds.Width, 0));
            pathGeometry.Transform = transformGroup;

            foreach (PathFigure figure in pathGeometry.Figures)
            {
                sb.Append('M');
                sb.Append(figure.StartPoint.X.ToString("F2", CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(figure.StartPoint.Y.ToString("F2", CultureInfo.InvariantCulture));

                foreach (PathSegment segment in figure.Segments)
                {
                    if (segment is LineSegment lineSegment)
                    {
                        sb.Append(" L");
                        sb.Append(lineSegment.Point.X.ToString("F2", CultureInfo.InvariantCulture));
                        sb.Append(',');
                        sb.Append(lineSegment.Point.Y.ToString("F2", CultureInfo.InvariantCulture));
                    }
                    else if (segment is ArcSegment arcSegment)
                    {
                        sb.Append(" A");
                        sb.Append(arcSegment.Size.Width.ToString("F2", CultureInfo.InvariantCulture));
                        sb.Append(',');
                        sb.Append(arcSegment.Size.Height.ToString("F2", CultureInfo.InvariantCulture));
                        sb.Append(' ');
                        sb.Append(arcSegment.RotationAngle.ToString("F2", CultureInfo.InvariantCulture));
                        sb.Append(' ');
                        sb.Append(arcSegment.IsLargeArc ? '1' : '0');
                        sb.Append(',');
                        sb.Append(arcSegment.SweepDirection == SweepDirection.Clockwise ? '1' : '0');
                        sb.Append(' ');
                        sb.Append(arcSegment.Point.X.ToString("F2", CultureInfo.InvariantCulture));
                        sb.Append(',');
                        sb.Append(arcSegment.Point.Y.ToString("F2", CultureInfo.InvariantCulture));
                    }
                }
            }

            return sb.ToString();
        }

        private Geometry GetTextGeometry(TextGeometryData geometryData)
        {
            var formattedText = new FormattedText(
                geometryData.Content,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("song"),
                geometryData.FontSize,
                ShapeStyler!.SketchPen.Brush,
                96);

            return formattedText.BuildGeometry(geometryData.Location);
        }
    }
}
