#nullable enable
using System.Windows;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;

namespace Lan.ImageViewer.ViewModels.GeometryEditing;

public sealed class CircleGeometryEditor : ShapeGeometryEditor
{
    private readonly Circle _circle;
    internal CircleGeometryEditor(Circle circle, IShapeRepository repository) : base(circle, repository)
    {
        _circle = circle;
        Initialize(new[]
        {
            new GeometryPropertyGroup("圆心", "移动位置，保持半径", new[] { Field("X", "圆心 X"), Field("Y", "圆心 Y") },
                () => new[] { circle.Center.X, circle.Center.Y }, MoveTo),
            new GeometryPropertyGroup("半径", string.Empty, new[] { Field(string.Empty, "圆半径") },
                () => new[] { circle.Radius }, values => circle.SetCircle(circle.Center, values[0]))
        }, new GeometryReadout("直径", () => Format(circle.Radius * 2)));
    }

    private void MoveTo(double[] values)
    {
        RequirePositive(_circle.Radius);
        var center = new Point(values[0], values[1]);
        RequireBounds(new Rect(new Point(center.X - _circle.Radius, center.Y - _circle.Radius),
            new Size(_circle.Radius * 2, _circle.Radius * 2)));
        var delta = center - _circle.Center;
        var translated = _circle.Center + delta;
        RequireRepresentable(translated.X, center.X);
        RequireRepresentable(translated.Y, center.Y);
        Move(delta);
    }

    protected override double[] ReadSnapshot() => new[] { _circle.Center.X, _circle.Center.Y, _circle.Radius };
}
