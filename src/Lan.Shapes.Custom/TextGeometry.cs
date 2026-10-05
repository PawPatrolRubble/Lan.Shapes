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
            _geometry = BuildGeometry(_textGeometryData);
            IsGeometryRendered = true;
            RequestVisualUpdate();
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
                StrokeThickness = _textGeometryData.StrokeThickness > 0
                    ? _textGeometryData.StrokeThickness
                    : ShapeStyler?.SketchPen.Thickness ?? 1
            };
        }

        public override Geometry RenderGeometry => _geometry ?? Geometry.Empty;

        public override Rect BoundsRect => RenderGeometry.Bounds;

        public override bool CanTranslate => true;

        protected override void TranslateCore(Vector delta)
        {
            _textGeometryData!.Location += delta;
            _geometry = BuildGeometry(_textGeometryData);
        }

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
            base.OnDeselected();
        }

        public override void OnMouseLeftButtonDown(Point mousePoint)
        {
        }

        public override void OnSelected()
        {
        }

        protected override Pen? GetSelectionPen()
        {
            var pen = base.GetSelectionPen()?.CloneCurrentValue();
            if (pen != null && _textGeometryData?.StrokeThickness > 0)
            {
                pen.Thickness = _textGeometryData.StrokeThickness;
            }
            return pen;
        }

        protected override void DrawShape(DrawingContext render)
        {
            if (ShapeStyler == null)
            {
                return;
            }

            if (_textGeometryData == null || string.IsNullOrWhiteSpace(_textGeometryData.Content))
            {
                return;
            }

            var fill = ShapeStyler.FillColor;
            var pen = ShapeStyler.SketchPen.CloneCurrentValue();
            if (_textGeometryData.StrokeThickness > 0)
            {
                pen.Thickness = _textGeometryData.StrokeThickness;
            }

            if (IsLocked)
            {
                pen.Brush = GetTextForeground(pen.Brush);
                if (fill != null)
                {
                    var grayFill = GetTextForeground(fill).CloneCurrentValue();
                    // Preserve transparency when text is drawn as an outline.
                    grayFill.Opacity = fill.Opacity * (fill is SolidColorBrush solid ? solid.Color.A / 255.0 : 1);
                    fill = grayFill;
                }
            }
            render.DrawGeometry(fill, pen, RenderGeometry);
        }

        private Geometry BuildGeometry(TextGeometryData data)
        {
            if (string.IsNullOrWhiteSpace(data.Content)) return Geometry.Empty;

            var geometry = GetTextGeometry(data);
            var transforms = new TransformGroup();
            transforms.Children.Add(new ScaleTransform(-1, 1, geometry.Bounds.Left, geometry.Bounds.Top));
            transforms.Children.Add(new TranslateTransform(700, 0));
            geometry.Transform = transforms;
            return geometry;
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
