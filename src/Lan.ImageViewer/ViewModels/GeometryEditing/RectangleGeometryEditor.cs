#nullable enable
using System.Windows;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;

namespace Lan.ImageViewer.ViewModels.GeometryEditing;

public sealed class RectangleGeometryEditor : ShapeGeometryEditor
{
    private readonly Rectangle _rectangle;
    private Rect Bounds => new(_rectangle.TopLeft, _rectangle.BottomRight);
    internal RectangleGeometryEditor(Rectangle rectangle, IShapeRepository repository) : base(rectangle, repository)
    {
        _rectangle = rectangle;
        Initialize(new[]
        {
            new GeometryPropertyGroup("左上角", "移动位置，保持大小", new[] { Field("X", "矩形左上角 X"), Field("Y", "矩形左上角 Y") },
                () => new[] { Bounds.Left, Bounds.Top }, MoveTo),
            new GeometryPropertyGroup("大小", "固定左上角", new[] { Field("宽", "矩形宽度"), Field("高", "矩形高度") },
                () => new[] { Bounds.Width, Bounds.Height }, values =>
                {
                    RequirePositive(values[0]);
                    RequirePositive(values[1]);
                    var bounds = new Rect(Bounds.TopLeft, new Size(values[0], values[1]));
                    RequireBounds(bounds);
                    rectangle.SetBounds(bounds);
                })
        }, new GeometryReadout("右下角", () => $"X {Format(Bounds.Right)}   Y {Format(Bounds.Bottom)}"));
    }

    private void MoveTo(double[] values)
    {
        var bounds = new Rect(new Point(values[0], values[1]), Bounds.Size);
        RequireBounds(bounds);
        var delta = bounds.TopLeft - Bounds.TopLeft;
        var translated = new Rect(_rectangle.TopLeft + delta, _rectangle.BottomRight + delta);
        RequireBounds(translated);
        RequireRepresentable(translated.Left, bounds.Left);
        RequireRepresentable(translated.Top, bounds.Top);
        RequireRepresentable(translated.Width, Bounds.Width);
        RequireRepresentable(translated.Height, Bounds.Height);
        Move(delta);
    }

    protected override double[] ReadSnapshot() => new[]
        { _rectangle.TopLeft.X, _rectangle.TopLeft.Y, _rectangle.BottomRight.X, _rectangle.BottomRight.Y };
}
