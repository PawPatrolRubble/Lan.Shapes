#nullable enable
using System;
using System.Windows;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;

namespace Lan.ImageViewer.ViewModels.GeometryEditing;

public sealed class LineGeometryEditor : ShapeGeometryEditor
{
    private readonly Line _line;
    internal LineGeometryEditor(Line line, IShapeRepository repository) : base(line, repository)
    {
        _line = line;
        Initialize(new[]
        {
            new GeometryPropertyGroup("起点", string.Empty, new[] { Field("X", "线段起点 X"), Field("Y", "线段起点 Y") },
                () => new[] { line.Start.X, line.Start.Y }, values => line.SetEndpoints(new Point(values[0], values[1]), line.End)),
            new GeometryPropertyGroup("终点", string.Empty, new[] { Field("X", "线段终点 X"), Field("Y", "线段终点 Y") },
                () => new[] { line.End.X, line.End.Y }, values => line.SetEndpoints(line.Start, new Point(values[0], values[1]))),
            new GeometryPropertyGroup("长度", "固定起点，保持方向", new[] { Field(string.Empty, "线段长度") },
                () => new[] { Length }, values => SetLength(values[0]), () => double.IsFinite(Length) && Length > 0)
        });
    }

    private double Length
    {
        get
        {
            var delta = _line.End - _line.Start;
            var largest = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
            var smallest = Math.Min(Math.Abs(delta.X), Math.Abs(delta.Y));
            return largest == 0 ? 0 : largest * Math.Sqrt(1 + (smallest / largest) * (smallest / largest));
        }
    }

    private void SetLength(double length)
    {
        RequirePositive(length);
        var delta = _line.End - _line.Start;
        var maximum = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
        // Vector division uses a reciprocal; direct scalar division also handles subnormal components.
        var x = delta.X / maximum;
        var y = delta.Y / maximum;
        var magnitude = Math.Sqrt(x * x + y * y);
        var end = new Point(_line.Start.X + x / magnitude * length, _line.Start.Y + y / magnitude * length);
        RequirePoint(end);
        _line.SetEndpoints(_line.Start, end);
    }

    protected override double[] ReadSnapshot() => new[] { _line.Start.X, _line.Start.Y, _line.End.X, _line.End.Y };
}
