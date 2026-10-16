using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Lan.Shapes;
using Lan.Shapes.Custom;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeGeometryStateTests
{
    public static IEnumerable<object[]> EditableTypes => ShapeTranslationTests.MovableTypes;

    [Theory]
    [MemberData(nameof(EditableTypes))]
    public void GeometrySnapshot_RestoresEachBuiltInModelAfterTranslation(Type type)
    {
        Sta(() =>
        {
            var factory = typeof(ShapeTranslationTests).GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static)!;
            var shape = (ShapeVisualBase)factory.Invoke(null, new object[] { type })!;
            shape.AddText("attached", new Point(15.2, 35.4));
            var before = shape.CaptureGeometryState();
            shape.Translate(new Vector(0.7, -0.3));
            Assert.False(before.HasSameContent(shape.CaptureGeometryState()));
            shape.RestoreGeometryState(before);
            Assert.True(before.HasSameContent(shape.CaptureGeometryState()));
        });
    }

    [Fact]
    public void GeometrySnapshot_RestoresRotationDimensionsAndCircleRadiiExactly()
    {
        Sta(() =>
        {
            var rectangle = new Rectangle2(TestShapeLayer.Create());
            rectangle.FromData(new Rectangle2Data { Row = 30.2, Column = 20.1, Phi = 0.3, Length1 = 40.4, Length2 = 20.5 });
            var circle = new Circle(TestShapeLayer.Create());
            circle.FromData(new EllipseData { Center = new Point(20, 30), RadiusX = 15.3 });
            var ellipse = new Ellipse(TestShapeLayer.Create());
            ellipse.FromData(new EllipseData { Center = new Point(40, 50), RadiusX = 12.4, RadiusY = 8.6 });
            var cross = new Cross(TestShapeLayer.Create());
            cross.FromData(new CrossData { Center = new Point(20, 30), Width = 80, Height = 40 });
            Check(rectangle, () => { rectangle.Center += new Vector(10, 5); rectangle.Phi = 1.2; rectangle.Length1 = 75; rectangle.Length2 = 35; });
            Check(circle, () => circle.Radius = 37.8);
            Check(ellipse, () => { ellipse.RadiusX = 35.1; ellipse.RadiusY = 29.2; });
            Check(cross, () => { cross.Width = 120; cross.Height = 100; });
        });
    }

    [Fact]
    public void GeometrySnapshot_RestoresFiberFilletAndTriangleWithCorners()
    {
        Sta(() =>
        {
            var fiber = new Fiber(TestShapeLayer.Create());
            fiber.FromData(new FiberData { Width = 80, Height = 40, FilletCenter = new Point(100, 100), FilletRadius = 3 });
            var angle = fiber.TriangleBottomEdgeAngleInDeg;
            Check(fiber, () => { fiber.Width = 120; fiber.Height = 50; fiber.FiberAngle = 20; fiber.FilletRadius = 6; fiber.TriangleBottomEdgeAngleInDeg = 35; });
            Assert.Equal(angle, fiber.TriangleBottomEdgeAngleInDeg);
        });
    }

    [Fact]
    public void GeometrySnapshot_RestoresFixedCircleAndRulerWithoutEnablingTranslation()
    {
        Sta(() =>
        {
            var fixedCircle = new FixedCenterCircle(TestShapeLayer.Create());
            fixedCircle.FromData(new EllipseData { Center = new Point(100, 100), RadiusX = 40 });
            Check(fixedCircle, () => fixedCircle.Radius = 80);
            Assert.False(fixedCircle.CanTranslate);
            var ruler = new RulerCross(TestShapeLayer.Create());
            ruler.FromData(new RulerCrossData { Center = new Point(100, 100), Width = 300, Height = 200 });
            Check(ruler, () => ruler.Center = new Point(180, 70));
            Assert.False(ruler.CanTranslate);
        });
    }

    [Theory]
    [InlineData(typeof(ThickenedRectangle))]
    [InlineData(typeof(ThickenedLine))]
    [InlineData(typeof(ThickenedCircle))]
    [InlineData(typeof(ThickenedCross))]
    public void GeometrySnapshot_RestoresThicknessAndAllHandleLocations(Type type)
    {
        Sta(() =>
        {
            var factory = typeof(ShapeTranslationTests).GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static)!;
            var shape = (ShapeVisualBase)factory.Invoke(null, new object[] { type })!;
            var handlesProperty = typeof(ShapeVisualBase).GetField("Handles", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var handles = ((IEnumerable<Lan.Shapes.Handle.DragHandle>)handlesProperty.GetValue(shape)!).ToArray();
            var originalHandles = handles.Select(handle => handle.GeometryCenter).ToArray();
            var thickness = typeof(CustomGeometryBase).GetProperty("StrokeThickness", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var originalThickness = (double)thickness.GetValue(shape)!;
            var original = shape.CaptureGeometryState();
            thickness.SetValue(shape, originalThickness + 5);
            var changed = shape.CaptureGeometryState();
            Assert.False(original.HasSameContent(changed));
            shape.RestoreGeometryState(original);
            Assert.Equal(originalThickness, thickness.GetValue(shape));
            Assert.Equal(originalHandles, handles.Select(handle => handle.GeometryCenter));
            shape.RestoreGeometryState(changed);
            Assert.Equal(originalThickness + 5, thickness.GetValue(shape));
        });
    }

    [Fact]
    public void GeometrySnapshot_RejectsAnotherOwnerAndIncompleteShape()
    {
        Sta(() =>
        {
            var first = new Circle(TestShapeLayer.Create());
            var second = new Circle(TestShapeLayer.Create());
            Assert.Throws<InvalidOperationException>(() => first.CaptureGeometryState());
            first.FromData(new EllipseData { Center = new Point(20, 30), RadiusX = 10 });
            second.FromData(new EllipseData { Center = new Point(70, 80), RadiusX = 20 });
            Assert.Throws<ArgumentException>(() => second.RestoreGeometryState(first.CaptureGeometryState()));
            Assert.Equal(new Point(70, 80), second.Center);
        });
    }

    private static void Check(ShapeVisualBase shape, Action mutate)
    {
        var original = shape.CaptureGeometryState();
        var originalPoints = shape.GetSnapPoints().ToArray();
        mutate();
        var changed = shape.CaptureGeometryState();
        Assert.False(original.HasSameContent(changed));
        shape.RestoreGeometryState(original);
        Assert.True(original.HasSameContent(shape.CaptureGeometryState()));
        Assert.Equal(originalPoints, shape.GetSnapPoints());
        shape.RestoreGeometryState(changed);
        Assert.True(changed.HasSameContent(shape.CaptureGeometryState()));
        shape.RestoreGeometryState(original);
    }

    private static void Sta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}

