using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Lan.Shapes;
using Lan.Shapes.Custom;
using Lan.Shapes.DialogGeometry;
using Lan.Shapes.Handle;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class GeometrySnapshotStateTests
{
    [Fact]
    public void CircleEllipseAndCross_SnapshotsRestoreSizeAndHandlesAcrossRepeatedReplay() => BulkLayerFeatureTests.Sta(() =>
    {
        var circle = new Circle(TestShapeLayer.Create());
        circle.FromData(new EllipseData { Center = new Point(20.1, 30.2), RadiusX = 14.3 });
        AssertReplay(circle, () => circle.SetCircle(new Point(100.4, 200.5), 40.6),
            () => new object[] { circle.Center, circle.Radius });

        var ellipse = new Ellipse(TestShapeLayer.Create());
        ellipse.FromData(new EllipseData { Center = new Point(20.1, 30.2), RadiusX = 14.3, RadiusY = 18.7 });
        AssertReplay(ellipse, () => { ellipse.Center = new Point(100.4, 200.5); ellipse.RadiusX = 40.6; ellipse.RadiusY = 80.9; },
            () => new object[] { ellipse.Center, ellipse.RadiusX, ellipse.RadiusY });

        var cross = new Cross(TestShapeLayer.Create());
        cross.FromData(new CrossData { Center = new Point(20.1, 30.2), Width = 60, Height = 80 });
        AssertReplay(cross, () => { cross.Center = new Point(100.4, 200.5); cross.Width = 120; cross.Height = 160; },
            () => new object[] { cross.Center, cross.Width, cross.Height });
    });

    [Fact]
    public void Rectangle2_SnapshotRestoresRotationDimensionsAndAllControlPoints() => BulkLayerFeatureTests.Sta(() =>
    {
        var rectangle = new Rectangle2(TestShapeLayer.Create());
        rectangle.FromData(new Rectangle2Data { Row = 80.1, Column = 90.2, Phi = 0.37, Length1 = 31.7, Length2 = 19.8 });
        AssertReplay(rectangle, () =>
        {
            rectangle.Center = new Point(180.3, 190.4);
            rectangle.Phi = 1.19;
            rectangle.Length1 = 61.9;
            rectangle.Length2 = 42.8;
        }, () => new object[] { rectangle.Center, rectangle.Phi, rectangle.Length1, rectangle.Length2 });
    });

    [Fact]
    public void Fiber_SnapshotRestoresCornersRadiusTipAndRotationWithTranslationDisabled() => BulkLayerFeatureTests.Sta(() =>
    {
        var fiber = new Fiber(TestShapeLayer.Create());
        fiber.FromData(new FiberData { FilletCenter = new Point(100.1, 120.2), Width = 50.3, Height = 30.4,
            FiberAngleInDeg = 13.7, FilletRadius = 2.9, EnableTranslation = false });
        Assert.False(fiber.CanTranslate);
        AssertReplay(fiber, () =>
        {
            fiber.Width = 75.9;
            fiber.Height = 47.8;
            fiber.FilletRadius = 5.3;
            fiber.TriangleBottomEdgeAngleInDeg = 37.4;
            fiber.FiberAngle = 51.2;
        }, () => new object[] { fiber.RectTopLeft, fiber.RectTopRight, fiber.RectBottomLeft, fiber.RectBottomRight,
            fiber.FilletRadius, fiber.TriangleBottomEdgeAngleInDeg, fiber.FiberAngle, fiber.FilletCenter, fiber.TriangleApex });
        Assert.False(fiber.CanTranslate);
    });

    [Fact]
    public void FixedCenterCircle_ResizeSnapshotDoesNotRequireTranslationSupport() => BulkLayerFeatureTests.Sta(() =>
    {
        var circle = new FixedCenterCircle(TestShapeLayer.Create());
        circle.FromData(new EllipseData { Center = new Point(100.1, 120.2), RadiusX = 30.3 });
        Assert.False(circle.CanTranslate);
        AssertReplay(circle, () => circle.Radius = 45.6, () => new object[] { circle.Center, circle.Radius });
        Assert.False(circle.CanTranslate);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThickenedLineAndRectangle_SnapshotsRestoreModelAndThickness(bool rectangle) => BulkLayerFeatureTests.Sta(() =>
    {
        ShapeVisualBase shape = rectangle ? new ThickenedRectangle(TestShapeLayer.Create()) : new ThickenedLine(TestShapeLayer.Create());
        var original = new PointsData(8, new List<Point> { new(20, 30), new(120, 110) });
        var edited = new PointsData(19, new List<Point> { new(40, 50), new(180, 150) });
        if (shape is ThickenedRectangle rect)
        {
            rect.FromData(original);
            AssertReplay(rect, () => rect.FromData(edited), () =>
            {
                var data = rect.GetMetaData();
                return data.DataPoints.Cast<object>().Append(data.StrokeThickness).ToArray();
            });
        }
        else
        {
            var line = (ThickenedLine)shape;
            line.FromData(original);
            AssertReplay(line, () => line.FromData(edited), () =>
            {
                var data = line.GetMetaData();
                return data.DataPoints.Cast<object>().Append(data.StrokeThickness).ToArray();
            });
        }
    });

    [Theory]
    [InlineData(1)]
    [InlineData(99)]
    public void ThickenedCircle_SnapshotRestoresRadiusAndThicknessHandles(int handleId) => BulkLayerFeatureTests.Sta(() =>
    {
        var circle = new ThickenedCircle(TestShapeLayer.Create());
        circle.FromData(new EllipseData { Center = new Point(100, 120), RadiusX = 30, StrokeThickness = 8 });
        circle.SetSelectionAppearance(true, true);
        AssertReplay(circle, () =>
        {
            var start = Handles(circle).Single(handle => handle.Id == handleId).GeometryCenter;
            var end = start + (handleId == 99 ? new Vector(0, -10) : new Vector(15, 0));
            circle.OnMouseLeftButtonDown(start);
            circle.OnMouseMove(end, MouseButtonState.Pressed);
            circle.OnMouseLeftButtonUp(end);
        }, () =>
        {
            var data = circle.GetMetaData();
            return new object[] { data.Center, data.RadiusX, data.StrokeThickness };
        });
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DxfSnapshot_RestoresBakedScaleOrRotationAndExportedEntities(bool rotate) => BulkLayerFeatureTests.Sta(() =>
    {
        var document = new netDxf.DxfDocument();
        document.Entities.Add(new netDxf.Entities.Line(new netDxf.Vector2(10, 20), new netDxf.Vector2(80, 60)));
        var shape = new DxfGeometry(TestShapeLayer.Create(), new StubDxfService(document));
        typeof(DxfGeometry).GetMethod("ReadDxfFile", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(shape, new object[] { "drawing.dxf", new Point(160, 160), 1.0 });
        shape.OnMouseLeftButtonUp(new Point(160, 160));
        shape.SetSelectionAppearance(true, true);
        AssertReplay(shape, () =>
        {
            var handle = rotate ? Handles(shape).Single(item => item.Id == 100)
                : Handles(shape).First(item => item.GeometryCenter == shape.BoundsRect.BottomRight);
            var start = handle.GeometryCenter;
            var bounds = shape.BoundsRect;
            var center = new Point((bounds.Left + bounds.Right) / 2, (bounds.Top + bounds.Bottom) / 2);
            var end = rotate ? center + new Vector(center.Y - start.Y, start.X - center.X)
                : bounds.TopLeft + (bounds.BottomRight - bounds.TopLeft) * 1.4;
            shape.OnMouseLeftButtonDown(start);
            shape.OnMouseMove(end, MouseButtonState.Pressed);
            shape.OnMouseLeftButtonUp(end);
        }, () =>
        {
            var line = Assert.Single(shape.ExportToDxf(new Point(), reverseY: true).Entities.Lines);
            return new object[] { line.StartPoint, line.EndPoint };
        });
    });

    [Fact]
    public void GeometrySnapshot_RejectsAnotherOwnerAndIncompleteShape() => BulkLayerFeatureTests.Sta(() =>
    {
        var first = new Circle(TestShapeLayer.Create());
        first.FromData(new EllipseData { Center = new Point(20, 30), RadiusX = 10 });
        var second = new Circle(TestShapeLayer.Create());
        second.FromData(new EllipseData { Center = new Point(70, 80), RadiusX = 20 });
        Assert.Throws<ArgumentException>(() => second.RestoreGeometryState(first.CaptureGeometryState()));
        Assert.Equal(new Point(70, 80), second.Center);
        Assert.Throws<InvalidOperationException>(() => new Circle(TestShapeLayer.Create()).CaptureGeometryState());
    });

    private static void AssertReplay(ShapeVisualBase shape, Action edit, Func<object[]> model)
    {
        var before = shape.CaptureGeometryState();
        var original = model();
        var originalBounds = shape.BoundsRect;
        var originalHandles = Handles(shape).Select(handle => handle.GeometryCenter).ToArray();
        edit();
        var after = shape.CaptureGeometryState();
        var edited = model();
        var editedBounds = shape.BoundsRect;
        var editedHandles = Handles(shape).Select(handle => handle.GeometryCenter).ToArray();
        Assert.False(original.SequenceEqual(edited));
        Assert.False(before.HasSameContent(after));
        shape.Lock();
        for (var index = 0; index < 5; index++)
        {
            shape.RestoreGeometryState(before);
            Assert.Equal(original, model());
            Assert.Equal(originalBounds, shape.BoundsRect);
            Assert.Equal(originalHandles, Handles(shape).Select(handle => handle.GeometryCenter));
            Assert.True(before.HasSameContent(shape.CaptureGeometryState()));
            shape.RestoreGeometryState(after);
            Assert.Equal(edited, model());
            Assert.Equal(editedBounds, shape.BoundsRect);
            Assert.Equal(editedHandles, Handles(shape).Select(handle => handle.GeometryCenter));
            Assert.True(after.HasSameContent(shape.CaptureGeometryState()));
        }
        Assert.True(shape.IsLocked);
    }

    private static IReadOnlyList<DragHandle> Handles(ShapeVisualBase shape)
        => (IReadOnlyList<DragHandle>)typeof(ShapeVisualBase).GetField("Handles", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(shape)!;

    private sealed class StubDxfService : IDxfDocumentService
    {
        private readonly netDxf.DxfDocument _document;
        public StubDxfService(netDxf.DxfDocument document) => _document = document;
        public netDxf.DxfDocument Load(string path) => _document;
        public void Save(netDxf.DxfDocument document, string path) { }
    }
}
