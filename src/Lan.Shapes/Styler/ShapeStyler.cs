using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace Lan.Shapes.Styler
{
    public class ShapeStyler : IShapeStyler, INotifyPropertyChanged
    {
        private Pen _sketchPen = new Pen();
        private string _dashStyle;
        public Pen SketchPen => _sketchPen;
        private Brush _fillColor;
        private double _dragHandleSize;
        private Brush _tagColor = Brushes.Red;
        public event PropertyChangedEventHandler PropertyChanged;
        public Brush FillColor
        {
            get => _fillColor;
            set
            {
                if (ReferenceEquals(_fillColor, value)) return;
                _fillColor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FillColor)));
            }
        }

        public string Name { get; set; }

        public IShapeStyler Clone()
        {
            var clone = new ShapeStyler(ToStylerParameter())
            {
                TagColor = TagColor?.Clone(),
                Name = Name
            };
            return clone;
        }

        public ShapeStylerParameter ToStylerParameter()
        {
            return new ShapeStylerParameter()
            {
                DashStyle = GetDashStyleName(_sketchPen.DashStyle),
                DragHandleSize = DragHandleSize,
                FillColor = FillColor?.Clone(),
                StrokeColor = _sketchPen.Brush?.Clone(),
                StrokeThickness = _sketchPen.Thickness,
                FillOpacity = FillColor?.Opacity ?? 0
            };
        }

        public void SetFillColor(Brush color)
        {
            FillColor = color;
        }

        public void SetStrokeColor(Brush color)
        {
            _sketchPen.Brush = color;
        }

        public void SetStrokeThickness(double thickness)
        {
            _sketchPen.Thickness = thickness;
        }

        public void SetPenDashStyle(DashStyle dashStyle)
        {
            _sketchPen.DashStyle = dashStyle;
            _dashStyle = GetDashStyleName(dashStyle);
        }

        private static string GetDashStyleName(DashStyle dashStyle)
        {
            return dashStyle == DashStyles.Dash ? "Dash"
                : dashStyle == DashStyles.Dot ? "Dot"
                : dashStyle == DashStyles.DashDot ? "DashDot"
                : dashStyle == DashStyles.DashDotDot ? "DashDotDot" : "Solid";
        }

        public double DragHandleSize
        {
            get => _dragHandleSize;
            set
            {
                if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                if (_dragHandleSize == value) return;
                _dragHandleSize = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DragHandleSize)));
            }
        }

        public Brush TagColor
        {
            get => _tagColor;
            set
            {
                if (ReferenceEquals(_tagColor, value)) return;
                _tagColor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TagColor)));
            }
        }

        public ShapeStyler()
        {
        }

        public ShapeStyler(ShapeStylerParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            FillColor = parameter.FillColor?.Clone();
            _sketchPen.Thickness = parameter.StrokeThickness > 0 ? parameter.StrokeThickness : 1;
            _sketchPen.Brush = parameter.StrokeColor?.Clone();
            _dashStyle = NormalizeDashStyle(parameter.DashStyle);
            _sketchPen.DashStyle = ConvertStringToDashStyle(_dashStyle);
            DragHandleSize = parameter.DragHandleSize;
            if (FillColor != null)
            {
                FillColor.Opacity = parameter.FillOpacity;
            }
        }

        private DashStyle ConvertStringToDashStyle(string dashStyleName)
        {
            DashStyle dashStyle;
            switch (dashStyleName)
            {
                case "Solid":
                    dashStyle = DashStyles.Solid;
                    break;
                case "Dash":
                    dashStyle = DashStyles.Dash;
                    break;
                case "Dot":
                    dashStyle = DashStyles.Dot;
                    break;
                case "DashDot":
                    dashStyle = DashStyles.DashDot;
                    break;
                case "DashDotDot":
                    dashStyle = DashStyles.DashDotDot;
                    break;
                default:
                    throw new ArgumentException("Invalid dash style name.");
            }

            return dashStyle;
        }

        public static string NormalizeDashStyle(string dashStyleName)
        {
            if (string.IsNullOrWhiteSpace(dashStyleName)) return "Solid";
            foreach (var name in new[] { "Solid", "Dash", "Dot", "DashDot", "DashDotDot" })
                if (string.Equals(name, dashStyleName.Trim(), StringComparison.OrdinalIgnoreCase)) return name;
            throw new ArgumentException("Invalid dash style name.", nameof(dashStyleName));
        }

        public ShapeStyler(Brush fillColor, Brush strokeColor, DashStyle dashStyle, double dragHandleSize)
        {
            this.FillColor = fillColor;
            _sketchPen.Thickness = 1;
            _sketchPen.Brush = strokeColor;
            _sketchPen.DashStyle = dashStyle;
            _dashStyle = GetDashStyleName(dashStyle);
            DragHandleSize = dragHandleSize;
        }
    }
}
