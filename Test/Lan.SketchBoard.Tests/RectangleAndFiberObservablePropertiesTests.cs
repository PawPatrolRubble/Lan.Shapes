using System.Collections.Generic;
using System.Windows;
using Lan.Shapes;
using Lan.Shapes.Custom;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class RectangleAndFiberObservablePropertiesTests
{
    [Fact]
    public void Rectangle_WidthAndHeight_AreObservableAndReflectChanges()
    {
        var layer = TestShapeLayer.CreateWithThickness(1, 8);
        var rectangle = new Rectangle(layer)
        {
            TopLeft = new Point(10, 20),
            BottomRight = new Point(110, 220)
        };

        Assert.Equal(100, rectangle.Width);
        Assert.Equal(200, rectangle.Height);

        var changedProperties = new List<string>();
        rectangle.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
            {
                changedProperties.Add(e.PropertyName);
            }
        };

        // Modify BottomRight
        rectangle.BottomRight = new Point(160, 320);
        Assert.Equal(150, rectangle.Width);
        Assert.Equal(300, rectangle.Height);
        Assert.Contains(nameof(Rectangle.Width), changedProperties);
        Assert.Contains(nameof(Rectangle.Height), changedProperties);

        changedProperties.Clear();

        // Two-way set Width
        rectangle.Width = 80;
        Assert.Equal(80, rectangle.Width);
        Assert.Equal(90, rectangle.BottomRight.X);
        Assert.Contains(nameof(Rectangle.Width), changedProperties);

        changedProperties.Clear();

        // Two-way set Height
        rectangle.Height = 120;
        Assert.Equal(120, rectangle.Height);
        Assert.Equal(140, rectangle.BottomRight.Y);
        Assert.Contains(nameof(Rectangle.Height), changedProperties);
    }

    [Fact]
    public void Fiber_AllProperties_AreObservableAndReflectChanges()
    {
        var layer = TestShapeLayer.CreateWithThickness(1, 8);
        var fiber = new Fiber(layer);
        fiber.FromData(new FiberData
        {
            Width = 40,
            Height = 30,
            FilletCenter = new Point(100, 100),
            FilletRadius = 15,
            FiberAngleInDeg = 0,
            EnableTranslation = true
        });

        Assert.Equal(15, fiber.Radius);
        Assert.Equal(15, fiber.FilletRadius);
        Assert.True(fiber.Width > 0);
        Assert.True(fiber.Height > 0);
        Assert.Equal(90, fiber.TipAngle); // Default triangle angle is 45, (90 - 45) * 2 = 90
        Assert.Equal(45, fiber.TriangleBottomEdgeAngleInDeg);

        var changedProperties = new List<string>();
        fiber.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
            {
                changedProperties.Add(e.PropertyName);
            }
        };

        // Change Radius -> notifies Radius and FilletCenter
        fiber.Radius = 25;
        Assert.Equal(25, fiber.Radius);
        Assert.Equal(25, fiber.FilletRadius);
        Assert.Contains(nameof(Fiber.Radius), changedProperties);
        Assert.Contains(nameof(Fiber.FilletCenter), changedProperties);

        changedProperties.Clear();

        // Change Width
        var initialWidth = fiber.Width;
        fiber.Width = initialWidth * 2;
        Assert.InRange(fiber.Width, initialWidth * 1.99, initialWidth * 2.01);
        Assert.Contains(nameof(Fiber.Width), changedProperties);

        changedProperties.Clear();

        // Change Height
        var initialHeight = fiber.Height;
        fiber.Height = initialHeight * 2;
        Assert.InRange(fiber.Height, initialHeight * 1.99, initialHeight * 2.01);
        Assert.Contains(nameof(Fiber.Height), changedProperties);

        changedProperties.Clear();

        // Change TipAngle
        fiber.TipAngle = 60;
        Assert.InRange(fiber.TipAngle, 59.99, 60.01);
        Assert.InRange(fiber.TriangleBottomEdgeAngleInDeg, 59.99, 60.01);
        Assert.Contains(nameof(Fiber.TipAngle), changedProperties);

        changedProperties.Clear();

        // Change TriangleBottomEdgeAngleInDeg directly
        fiber.TriangleBottomEdgeAngleInDeg = 30;
        Assert.InRange(fiber.TriangleBottomEdgeAngleInDeg, 29.99, 30.01);
        Assert.InRange(fiber.TipAngle, 119.99, 120.01); // (90 - 30) * 2 = 120
        Assert.Contains(nameof(Fiber.TriangleBottomEdgeAngleInDeg), changedProperties);
        Assert.Contains(nameof(Fiber.TipAngle), changedProperties);

        changedProperties.Clear();

        // Change FiberAngle
        fiber.FiberAngle = 45;
        Assert.InRange(fiber.FiberAngle, 44.9, 45.1);
        Assert.Contains(nameof(Fiber.FiberAngle), changedProperties);

        changedProperties.Clear();

        // Drag handle corner
        fiber.RectTopLeft = new Point(fiber.RectTopLeft.X + 10, fiber.RectTopLeft.Y + 10);
        Assert.Contains(nameof(Fiber.Width), changedProperties);
        Assert.Contains(nameof(Fiber.Height), changedProperties);
        Assert.Contains(nameof(Fiber.FiberAngle), changedProperties);
        Assert.Contains(nameof(Fiber.FilletCenter), changedProperties);
    }
}
