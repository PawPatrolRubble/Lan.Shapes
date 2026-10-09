using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Handle;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeGeometryEditingTests
{
    [Fact]
    public void SetEndpoints_UpdatesModelGeometryHandlesAndExportBeforeNotification()
    {
        Sta(() =>
        {
            var line = CreateLine();
            var start = new Point(-10, 15);
            var end = new Point(70, 100);
            var changed = Observe(line, () =>
            {
                Assert.Equal(start, line.Start);
                Assert.Equal(end, line.End);
                Assert.Equal(new[] { start, end }, line.GetMetaData().DataPoints);
                var geometry = Assert.IsType<LineGeometry>(Assert.Single(((GeometryGroup)line.RenderGeometry).Children));
                Assert.Equal(start, geometry.StartPoint);
                Assert.Equal(end, geometry.EndPoint);
                Assert.Equal(new[] { start, end, start + (end - start) / 2 }, Handles(line).Select(handle => handle.GeometryCenter));
                AssertBounds(new Rect(start, end), line.BoundsRect);
                AssertBounds(new Rect(start, end), line.SelectionBounds);
            });

            Edit(line, "SetEndpoints", start, end);

            Assert.Contains(nameof(Line.Start), changed);
            Assert.Contains(nameof(Line.End), changed);
            Assert.Contains(nameof(ShapeVisualBase.BoundsRect), changed);
            Assert.Contains(nameof(ShapeVisualBase.SelectionBounds), changed);
            Assert.Equal(1, line.DrawCount);
        });
    }

    [Fact]
    public void SetBounds_NormalizesReversedRectangleAndUpdatesAllStateBeforeNotification()
    {
        Sta(() =>
        {
            var rectangle = CreateRectangle();
            var bounds = new Rect(-20, 30, 150, 80);
            var changed = Observe(rectangle, () =>
            {
                Assert.Equal(bounds.TopLeft, rectangle.TopLeft);
                Assert.Equal(bounds.BottomRight, rectangle.BottomRight);
                Assert.Equal(bounds.Width, rectangle.Width);
                Assert.Equal(bounds.Height, rectangle.Height);
                Assert.Equal(new[] { bounds.TopLeft, bounds.BottomRight }, rectangle.GetMetaData().DataPoints);
                var geometry = Assert.IsType<RectangleGeometry>(Assert.Single(((GeometryGroup)rectangle.RenderGeometry).Children));
                Assert.Equal(bounds, geometry.Rect);
                Assert.Equal(new[] { bounds.TopLeft, bounds.TopRight, bounds.BottomRight, bounds.BottomLeft },
                    Handles(rectangle).Select(handle => handle.GeometryCenter));
                AssertBounds(bounds, rectangle.BoundsRect);
                AssertBounds(bounds, rectangle.SelectionBounds);
            });

            Edit(rectangle, "SetBounds", bounds);

            foreach (var property in new[] { nameof(Rectangle.TopLeft), nameof(Rectangle.BottomRight), nameof(Rectangle.Width),
                         nameof(Rectangle.Height), nameof(ShapeVisualBase.BoundsRect), nameof(ShapeVisualBase.SelectionBounds) })
                Assert.Contains(property, changed);
            Assert.Equal(1, rectangle.DrawCount);
        });
    }

    [Fact]
    public void SetCircle_UpdatesModelGeometryHandlesAndDerivedCoordinatesBeforeNotification()
    {
        Sta(() =>
        {
            var circle = CreateCircle();
            var center = new Point(-10, 80);
            const double radius = 25;
            var changed = Observe(circle, () =>
            {
                Assert.Equal(center, circle.Center);
                Assert.Equal(center.X, circle.X);
                Assert.Equal(center.Y, circle.Y);
                Assert.Equal(radius, circle.Radius);
                Assert.Equal(center, circle.GetMetaData().Center);
                Assert.Equal(radius, circle.GetMetaData().RadiusX);
                var geometry = Assert.IsType<EllipseGeometry>(Assert.Single(((GeometryGroup)circle.RenderGeometry).Children));
                Assert.Equal(center, geometry.Center);
                Assert.Equal(radius, geometry.RadiusX);
                Assert.Equal(radius, geometry.RadiusY);
                Assert.Equal(center + new Vector(radius, 0), Assert.Single(Handles(circle)).GeometryCenter);
                var bounds = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
                AssertBounds(bounds, circle.BoundsRect);
                AssertBounds(bounds, circle.SelectionBounds);
            });

            Edit(circle, "SetCircle", center, radius);

            foreach (var property in new[] { nameof(Circle.Center), nameof(Circle.X), nameof(Circle.Y), nameof(Circle.Radius),
                         nameof(ShapeVisualBase.BoundsRect), nameof(ShapeVisualBase.SelectionBounds) })
                Assert.Contains(property, changed);
            Assert.Equal(1, circle.DrawCount);
        });
    }

    [Fact]
    public void GeometryEdits_RejectInvalidAndOverflowingRequestsWithoutAnyChanges()
    {
        Sta(() =>
        {
            var line = CreateLine();
            var rectangle = CreateRectangle();
            var circle = CreateCircle();
            var lineChanges = Observe(line, () => { });
            var rectangleChanges = Observe(rectangle, () => { });
            var circleChanges = Observe(circle, () => { });

            foreach (var endpoints in new[]
                     {
                         (new Point(double.NaN, 20), new Point(40, 60)),
                         (new Point(10, 20), new Point(double.PositiveInfinity, 60)),
                         (new Point(10, 20), new Point(10, 20)),
                         (new Point(-double.MaxValue, 0), new Point(double.MaxValue, 0)),
                         (new Point(0, 0), new Point(double.MaxValue, double.MaxValue))
                     })
                Assert.Throws<ArgumentOutOfRangeException>(() => Edit(line, "SetEndpoints", endpoints.Item1, endpoints.Item2));

            foreach (var bounds in new[]
                     {
                         Rect.Empty, new Rect(10, 20, 0, 20), new Rect(10, 20, 20, 0),
                         new Rect(double.NaN, 20, 20, 20), new Rect(10, 20, double.PositiveInfinity, 20),
                         new Rect(double.MaxValue, 20, double.MaxValue, 20), new Rect(1e20, 20, 1, 20)
                     })
                Assert.Throws<ArgumentOutOfRangeException>(() => Edit(rectangle, "SetBounds", bounds));

            foreach (var request in new[]
                     {
                         (new Point(10, 20), 0.0), (new Point(10, 20), -1.0),
                         (new Point(10, 20), double.NaN), (new Point(10, 20), double.PositiveInfinity),
                         (new Point(double.NegativeInfinity, 20), 30.0),
                         (new Point(double.MaxValue, 20), double.MaxValue), (new Point(1e20, 20), 1.0)
                     })
                Assert.Throws<ArgumentOutOfRangeException>(() => Edit(circle, "SetCircle", request.Item1, request.Item2));

            Assert.Equal(new Point(10, 20), line.Start);
            Assert.Equal(new Point(40, 60), line.End);
            Assert.Equal(new Point(110, 70), rectangle.TopLeft);
            Assert.Equal(new Point(10, 20), rectangle.BottomRight);
            Assert.Equal(new Point(10, 20), circle.Center);
            Assert.Equal(30, circle.Radius);
            Assert.Empty(lineChanges);
            Assert.Empty(rectangleChanges);
            Assert.Empty(circleChanges);
            Assert.Equal(0, line.DrawCount);
            Assert.Equal(0, rectangle.DrawCount);
            Assert.Equal(0, circle.DrawCount);
        });
    }

    [Fact]
    public void SetEndpoints_UsesStableLengthForFiniteLargeAndSmallLines()
    {
        Sta(() =>
        {
            var line = CreateLine();
            foreach (var endpoint in new[] { new Point(1e200, 1e200), new Point(1e-200, 1e-200), new Point(double.Epsilon, 0) })
            {
                Edit(line, "SetEndpoints", new Point(), endpoint);
                Assert.Equal(new Point(), line.Start);
                Assert.Equal(endpoint, line.End);
                Assert.Equal(new[] { new Point(), endpoint }, line.GetMetaData().DataPoints);
                Assert.Equal(new Point() + (endpoint - new Point()) / 2, Handles(line)[2].GeometryCenter);
            }
        });
    }

    [Fact]
    public void GeometryEdits_RejectLockedAndIncompleteShapes()
    {
        Sta(() =>
        {
            foreach (var shape in new ShapeVisualBase[] { CreateLine(), CreateRectangle(), CreateCircle() })
            {
                shape.IsLocked = true;
                Assert.Throws<InvalidOperationException>(() => EditValidGeometry(shape));
            }

            var layer = TestShapeLayer.Create();
            foreach (var shape in new ShapeVisualBase[] { new Line(layer), new Rectangle(layer), new Circle(layer) })
                Assert.Throws<InvalidOperationException>(() => EditValidGeometry(shape));
        });
    }

    [Fact]
    public void GeometryEdits_UnchangedRequestsDoNotNotifyOrRedraw()
    {
        Sta(() =>
        {
            var line = CreateLine();
            var rectangle = CreateRectangle();
            Edit(rectangle, "SetBounds", new Rect(rectangle.TopLeft, rectangle.BottomRight));
            rectangle.DrawCount = 0;
            var circle = CreateCircle();
            var changes = new List<string?>();
            foreach (var shape in new ShapeVisualBase[] { line, rectangle, circle })
                shape.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

            Edit(line, "SetEndpoints", line.Start, line.End);
            Edit(rectangle, "SetBounds", new Rect(rectangle.TopLeft, rectangle.BottomRight));
            Edit(circle, "SetCircle", circle.Center, circle.Radius);

            Assert.Empty(changes);
            Assert.Equal(0, line.DrawCount);
            Assert.Equal(0, rectangle.DrawCount);
            Assert.Equal(0, circle.DrawCount);
        });
    }

    [Fact]
    public void Circle_CenterAndCoordinateWrappersStaySynchronizedWithGeometryAndExport()
    {
        Sta(() =>
        {
            var circle = CreateCircle();
            var changed = new List<string?>();
            circle.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            circle.Center = new Point(80, -25);

            Assert.Equal(80, circle.X);
            Assert.Equal(-25, circle.Y);
            Assert.Contains(nameof(Circle.Center), changed);
            Assert.Contains(nameof(Circle.X), changed);
            Assert.Contains(nameof(Circle.Y), changed);
            circle.X = 100;
            circle.Y = 5;
            Assert.Equal(new Point(100, 5), circle.Center);
            Assert.Equal(circle.Center, circle.GetMetaData().Center);
            var geometry = Assert.IsType<EllipseGeometry>(Assert.Single(((GeometryGroup)circle.RenderGeometry).Children));
            Assert.Equal(circle.Center, geometry.Center);
            circle.Translate(new Vector(5, -3));
            Assert.Equal(new Point(105, 2), circle.Center);
            Assert.Equal(105, circle.X);
            Assert.Equal(2, circle.Y);
        });
    }

    [Fact]
    public void Circle_MouseCreationStillAllowsZeroRadiusAndUsesTheSelectedCenter()
    {
        Sta(() =>
        {
            var circle = new Circle(TestShapeLayer.Create());
            var center = new Point(80, 90);
            circle.OnMouseLeftButtonDown(center);
            circle.OnMouseMove(center, MouseButtonState.Pressed);
            Assert.Equal(0, circle.Radius);
            Assert.False(circle.IsGeometryRendered);
            Assert.Equal(center.X, circle.X);
            Assert.Equal(center.Y, circle.Y);
            circle.OnMouseMove(new Point(83, 94), MouseButtonState.Pressed);
            circle.OnMouseLeftButtonUp(new Point(83, 94));
            Assert.True(circle.IsGeometryRendered);
            Assert.Equal(5, circle.Radius);
            Assert.Equal(center, circle.GetMetaData().Center);
        });
    }

    private static List<string?> Observe(ShapeVisualBase shape, Action assertFinalState)
    {
        var properties = new List<string?>();
        shape.PropertyChanged += (_, args) =>
        {
            assertFinalState();
            properties.Add(args.PropertyName);
        };
        return properties;
    }

    private static void EditValidGeometry(ShapeVisualBase shape)
    {
        switch (shape)
        {
            case Line: Edit(shape, "SetEndpoints", new Point(20, 30), new Point(60, 80)); break;
            case Rectangle: Edit(shape, "SetBounds", new Rect(20, 30, 60, 80)); break;
            case Circle: Edit(shape, "SetCircle", new Point(20, 30), 40.0); break;
        }
    }

    private static void Edit(ShapeVisualBase shape, string methodName, params object[] parameters)
    {
        var method = shape.GetType().GetMethod(methodName, parameters.Select(parameter => parameter.GetType()).ToArray());
        Assert.NotNull(method);
        try { method.Invoke(shape, parameters); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }

    private static IReadOnlyList<DragHandle> Handles(ShapeVisualBase shape)
        => (IReadOnlyList<DragHandle>)typeof(ShapeVisualBase).GetField("Handles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shape)!;

    private static void AssertBounds(Rect expected, Rect actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 4);
        Assert.Equal(expected.Y, actual.Y, precision: 4);
        Assert.Equal(expected.Width, actual.Width, precision: 4);
        Assert.Equal(expected.Height, actual.Height, precision: 4);
    }

    private static CountingLine CreateLine()
    {
        var line = new CountingLine(TestShapeLayer.Create());
        line.FromData(new PointsData(1, new List<Point> { new(10, 20), new(40, 60) }));
        line.DrawCount = 0;
        return line;
    }

    private static CountingRectangle CreateRectangle()
    {
        var rectangle = new CountingRectangle(TestShapeLayer.Create());
        rectangle.FromData(new PointsData(0, new List<Point> { new(110, 70), new(10, 20) }));
        rectangle.DrawCount = 0;
        return rectangle;
    }

    private static CountingCircle CreateCircle()
    {
        var circle = new CountingCircle(TestShapeLayer.Create());
        circle.FromData(new EllipseData { Center = new Point(10, 20), RadiusX = 30 });
        circle.DrawCount = 0;
        return circle;
    }

    private sealed class CountingLine : Line
    {
        public CountingLine(ShapeLayer layer) : base(layer) { }
        public int DrawCount { get; set; }
        public override void UpdateVisual() { DrawCount++; base.UpdateVisual(); }
    }

    private sealed class CountingRectangle : Rectangle
    {
        public CountingRectangle(ShapeLayer layer) : base(layer) { }
        public int DrawCount { get; set; }
        public override void UpdateVisual() { DrawCount++; base.UpdateVisual(); }
    }

    private sealed class CountingCircle : Circle
    {
        public CountingCircle(ShapeLayer layer) : base(layer) { }
        public int DrawCount { get; set; }
        public override void UpdateVisual() { DrawCount++; base.UpdateVisual(); }
    }

    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception exception) { failure = exception; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
