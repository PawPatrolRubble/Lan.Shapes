using System.Windows;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Custom;
using Lan.Shapes.Enums;
using Lan.Shapes.Handle;
using Lan.Shapes.Models;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class FiberTests
{
    [Fact]
    public void FiberHandlesAreRenderedWithoutAFill()
    {
        var fiber = new ProbeFiber(TestShapeLayer.CreateWithThickness(stroke: 1, handle: 8));
        fiber.FromData(new FiberData
        {
            Width = 20,
            Height = 10,
            FilletCenter = new Point(50, 50),
            FilletRadius = 3,
            FiberAngleInDeg = 0,
            EnableTranslation = true
        });
        fiber.State = ShapeVisualState.Selected;

        var drawing = VisualTreeHelper.GetDrawing(fiber);
        var handleDrawing = FindGeometryDrawing(drawing, fiber.FirstHandleGeometry);

        Assert.NotNull(handleDrawing);
        Assert.Null(handleDrawing!.Brush);
    }

    private static GeometryDrawing? FindGeometryDrawing(Drawing? drawing, Geometry target)
    {
        if (drawing is GeometryDrawing geometryDrawing &&
            ReferenceEquals(geometryDrawing.Geometry, target))
        {
            return geometryDrawing;
        }

        if (drawing is DrawingGroup group)
        {
            foreach (var child in group.Children)
            {
                var match = FindGeometryDrawing(child, target);
                if (match != null)
                {
                    return match;
                }
            }
        }

        return null;
    }

    private sealed class ProbeFiber : Fiber
    {
        public ProbeFiber(ShapeLayer layer) : base(layer)
        {
        }

        public Geometry FirstHandleGeometry => Handles[0].HandleGeometry!;
    }
}
