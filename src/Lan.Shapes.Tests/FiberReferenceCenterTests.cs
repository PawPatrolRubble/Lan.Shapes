using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes.Custom;
using Lan.Shapes.Enums;
using Lan.Shapes.Styler;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lan.Shapes.Tests;

[TestClass]
public class FiberReferenceCenterTests
{
    [DataTestMethod]
    [DataRow(0.0)]
    [DataRow(90.0)]
    [DataRow(135.0)]
    [DataRow(270.0)]
    public void FromData_ZeroRadius_PreservesTheRequestedCenterAndPlacesTheTipThere(double angle)
    {
        var center = new Point(1050, 1777);
        var fiber = CreateFiber(center, angle, 0);

        AssertPoint(center, fiber.FilletCenter);
        AssertPoint(center, fiber.RectTopLeft + (fiber.RectTopRight - fiber.RectTopLeft) / 2);
        Assert.AreEqual(125 * 3.410, fiber.Width, 1e-6);
        Assert.AreEqual(450 * 3.410, fiber.Height, 1e-6);

        var corner = fiber.RectTopLeft;
        fiber.FiberAngle = angle + 37;
        AssertPoint(center, fiber.FilletCenter);
        Assert.AreEqual((corner - center).Length, (fiber.RectTopLeft - center).Length, 1e-6);
    }

    [DataTestMethod]
    [DataRow(0.0)]
    [DataRow(90.0)]
    [DataRow(135.0)]
    [DataRow(270.0)]
    public void RadiusChanges_PreserveTheBodyAndRecomputeTheTangentCenter_RotationPreservesThatCenter(double angle)
    {
        var center = new Point(1050, 1777);
        var fiber = CreateFiber(center, angle, 30);
        foreach (var radius in new[] { 0.0, 50.0, 10.0, 0.0, 30.0 })
        {
            var topLeft = fiber.RectTopLeft;
            var topRight = fiber.RectTopRight;
            var bottomLeft = fiber.RectBottomLeft;
            var bottomRight = fiber.RectBottomRight;
            var radians = fiber.FiberAngle * Math.PI / 180;
            var towardTip = new Vector(Math.Cos(radians), -Math.Sin(radians));
            var apex = topLeft + (topRight - topLeft) / 2;
            var expectedCenter = apex - towardTip * (radius / Math.Sin(Math.PI / 4));

            fiber.Radius = radius;
            AssertPoint(topLeft, fiber.RectTopLeft);
            AssertPoint(topRight, fiber.RectTopRight);
            AssertPoint(bottomLeft, fiber.RectBottomLeft);
            AssertPoint(bottomRight, fiber.RectBottomRight);
            AssertPoint(expectedCenter, fiber.FilletCenter);
            var corner = fiber.RectTopRight;
            center = fiber.FilletCenter;
            fiber.FiberAngle += 19;
            AssertPoint(center, fiber.FilletCenter);
            Assert.AreEqual((corner - center).Length, (fiber.RectTopRight - center).Length, 1e-6);
            Assert.AreEqual(125 * 3.410, fiber.Width, 1e-6);
            Assert.AreEqual(450 * 3.410, fiber.Height, 1e-6);
        }
    }

    [TestMethod]
    public void ZeroRadius_RoundTripAndReload_PreserveGeometryAndTheReferenceCenter()
    {
        var center = new Point(1050, 1777);
        var fiber = CreateFiber(center, 135, 30);
        fiber.FilletRadius = 0;
        center = fiber.RectTopLeft + (fiber.RectTopRight - fiber.RectTopLeft) / 2;
        var data = fiber.GetMetaData();
        AssertPoint(center, data.FilletCenter);
        var restored = CreateFiber(new Point(400, 500), 90, 20);
        restored.FromData(data);

        AssertPoint(center, restored.FilletCenter);
        AssertPoint(fiber.RectTopLeft, restored.RectTopLeft);
        AssertPoint(fiber.RectTopRight, restored.RectTopRight);
        AssertPoint(fiber.RectBottomLeft, restored.RectBottomLeft);
        AssertPoint(fiber.RectBottomRight, restored.RectBottomRight);
        Assert.AreEqual(0, restored.FilletRadius);
    }

    [TestMethod]
    public void ZeroRadius_ExplicitTranslation_MovesTheReferenceCenterWithTheShape()
    {
        var fiber = CreateFiber(new Point(0, 0), 135, 0);
        fiber.TranslationX = 40;
        fiber.TranslationY = -25;

        AssertPoint(new Point(40, -25), fiber.FilletCenter);
        AssertPoint(fiber.FilletCenter, fiber.RectTopLeft + (fiber.RectTopRight - fiber.RectTopLeft) / 2);
        fiber.FiberAngle = 90;
        AssertPoint(new Point(40, -25), fiber.FilletCenter);
    }

    [TestMethod]
    public void RadiusAssignment_RejectsNegativeValuesWithoutChangingGeometry()
    {
        var center = new Point(1050, 1777);
        var fiber = CreateFiber(center, 135, 30);
        var topLeft = fiber.RectTopLeft;

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => fiber.FilletRadius = -1);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => fiber.Radius = -1);

        Assert.AreEqual(30, fiber.FilletRadius);
        AssertPoint(center, fiber.FilletCenter);
        AssertPoint(topLeft, fiber.RectTopLeft);
    }

    [TestMethod]
    public void FromData_RejectsNegativeRadiusBeforeChangingTheExistingShape()
    {
        var center = new Point(1050, 1777);
        var fiber = CreateFiber(center, 135, 30);
        var topLeft = fiber.RectTopLeft;
        var data = fiber.GetMetaData();
        data.FilletRadius = -1;
        data.FilletCenter = new Point(100, 200);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => fiber.FromData(data));

        Assert.AreEqual(30, fiber.FilletRadius);
        AssertPoint(center, fiber.FilletCenter);
        AssertPoint(topLeft, fiber.RectTopLeft);
    }

    [DataTestMethod]
    [DataRow(90.0)]
    [DataRow(135.0)]
    public void RadiusHandle_DraggingBeyondZero_StopsAtZero(double angle)
    {
        var fiber = CreateFiber(new Point(1050, 1777), angle, 30);
        fiber.SetSelectionAppearance(true, true);
        var radians = angle * Math.PI / 180;
        var towardTip = new Vector(Math.Cos(radians), -Math.Sin(radians));
        var handlePoint = fiber.FilletCenter - towardTip * fiber.FilletRadius;
        Assert.AreEqual(9, fiber.FindDragHandleMouseOver(handlePoint)!.Id);
        var topLeft = fiber.RectTopLeft;

        fiber.OnMouseLeftButtonDown(handlePoint);
        fiber.OnMouseMove(handlePoint + towardTip * 60, MouseButtonState.Pressed);

        Assert.AreEqual(0, fiber.FilletRadius);
        AssertPoint(topLeft, fiber.RectTopLeft);
        AssertPoint(fiber.TriangleApex, fiber.FilletCenter);
    }

    private static void AssertPoint(Point expected, Point actual)
    {
        Assert.AreEqual(expected.X, actual.X, 1e-6, "Reference center X changed.");
        Assert.AreEqual(expected.Y, actual.Y, 1e-6, "Reference center Y changed.");
    }

    private static Fiber CreateFiber(Point center, double angle, double radius)
    {
        var style = new ShapeStylerParameter
        {
            FillColor = Brushes.Transparent, StrokeColor = Brushes.Lime,
            StrokeThickness = 1, DragHandleSize = 8, DashStyle = "Solid"
        };
        var layer = new ShapeLayer(new ShapeLayerParameter
        {
            LayerId = 1, Name = "Fiber", TextForeground = Brushes.Black,
            BorderBackground = Brushes.Transparent,
            StyleSchema = new Dictionary<ShapeVisualState, ShapeStylerParameter>
            {
                [ShapeVisualState.Normal] = style, [ShapeVisualState.Selected] = style
            }
        }, new ShapeMeasurementSettings
        {
            UnitsPerMillimeter = 1000, PixelPerUnit = 3410, UnitName = "um"
        }, new ShapeStylerFactory());
        var fiber = new Fiber(layer);
        fiber.FromData(new FiberData
        {
            FilletCenter = center, FiberAngleInDeg = angle, FilletRadius = radius,
            Width = 125, Height = 450, EnableTranslation = false
        });
        return fiber;
    }
}
