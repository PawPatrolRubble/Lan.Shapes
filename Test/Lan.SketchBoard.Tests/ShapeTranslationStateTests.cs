using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using Lan.Shapes;
using Lan.Shapes.Custom;
using Lan.Shapes.DialogGeometry;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeTranslationStateTests
{
    public static IEnumerable<object[]> MovableTypes => ShapeTranslationTests.MovableTypes;

    [Fact]
    public void TranslationEvent_ContainsExactSnapshotsAndMarksModelNotificationsAsTranslation()
    {
        Sta(() =>
        {
            var shape = new Circle(TestShapeLayer.Create());
            shape.FromData(new EllipseData { Center = new Point(20.1, 30.2), RadiusX = 10 });
            ShapeTranslationEventArgs? translation = null;
            var notificationCount = 0;
            shape.PropertyChanged += (_, _) => { Assert.True(shape.IsTranslating); notificationCount++; };
            shape.TranslationCompleted += (_, args) =>
            {
                Assert.False(shape.IsTranslating);
                translation = args;
            };
            var before = shape.CaptureTranslationState();
            shape.Translate(new Vector(0.7, 0.3));
            Assert.True(notificationCount > 0);
            Assert.NotNull(translation);
            Assert.True(before.HasSameContent(translation!.Before!));
            Assert.True(shape.CaptureTranslationState().HasSameContent(translation.After!));
            Assert.False(translation.Before!.HasSameContent(translation.After!));
        });
    }

    [Theory]
    [MemberData(nameof(MovableTypes))]
    public void Snapshot_RestoresAllMovableModelsExactly(Type type)
    {
        Sta(() =>
        {
            var factory = typeof(ShapeTranslationTests).GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static)!;
            var shape = (ShapeVisualBase)factory.Invoke(null, new object[] { type })!;
            var before = shape.GetSnapPoints().ToArray();
            var state = shape.CaptureTranslationState();
            shape.Translate(new Vector(0.7, -0.3));
            shape.RestoreTranslationState(state);
            Assert.Equal(before, shape.GetSnapPoints());
            shape.Translate(new Vector(0.7, -0.3));
            shape.Lock();
            shape.RestoreTranslationState(state);
            Assert.True(shape.IsLocked);
            Assert.Equal(before, shape.GetSnapPoints());
        });
    }

    [Fact]
    public void Snapshot_RejectsStateFromAnotherShape()
    {
        Sta(() =>
        {
            var first = new Circle(TestShapeLayer.Create());
            var second = new Circle(TestShapeLayer.Create());
            first.FromData(new EllipseData { Center = new Point(20, 30), RadiusX = 10 });
            second.FromData(new EllipseData { Center = new Point(70, 80), RadiusX = 20 });
            Assert.Throws<ArgumentException>(() => second.RestoreTranslationState(first.CaptureTranslationState()));
            Assert.Equal(new Point(70, 80), second.Center);
        });
    }

    [Fact]
    public void Snapshot_RestoresDxfGeometryAndExportAfterMouseUpBakesTransform()
    {
        Sta(() =>
        {
            var document = new netDxf.DxfDocument();
            document.Entities.Add(new netDxf.Entities.Line(new netDxf.Vector2(10, 20), new netDxf.Vector2(40, 50)));
            var shape = new DxfGeometry(TestShapeLayer.Create(), new StubDxfService(document));
            typeof(DxfGeometry).GetMethod("ReadDxfFile", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(shape, new object[] { "drawing.dxf", new Point(100, 100), 1.0 });
            shape.OnMouseLeftButtonUp(new Point(100, 100));
            var original = shape.CaptureTranslationState();
            var before = shape.BoundsRect;
            var beforeLine = Assert.Single(shape.ExportToDxf(new Point(), reverseY: true).Entities.Lines);
            shape.Translate(new Vector(0.7, -0.3));
            shape.OnMouseLeftButtonUp(new Point(100.7, 99.7));
            var moved = shape.CaptureTranslationState();
            var after = shape.BoundsRect;
            var afterLine = Assert.Single(shape.ExportToDxf(new Point(), reverseY: true).Entities.Lines);
            for (var index = 0; index < 4; index++)
            {
                shape.RestoreTranslationState(original);
                Assert.Equal(before, shape.BoundsRect);
                Assert.Equal(beforeLine.StartPoint, Assert.Single(shape.ExportToDxf(new Point(), reverseY: true).Entities.Lines).StartPoint);
                shape.RestoreTranslationState(moved);
                Assert.Equal(after, shape.BoundsRect);
                Assert.Equal(afterLine.StartPoint, Assert.Single(shape.ExportToDxf(new Point(), reverseY: true).Entities.Lines).StartPoint);
            }
        });
    }

    [Fact]
    public void Snapshot_RestoresExactCoordinatesAndAttachedTextAcrossRepeatedReplay()
    {
        Sta(() =>
        {
            var shape = new Line(TestShapeLayer.Create());
            shape.FromData(new PointsData(1, new List<Point> { new(0.1, 0.2), new(13.7, 24.9) }));
            shape.AddText("annotation", new Point(0.3, 0.7));
            var before = shape.GetMetaData().DataPoints.ToArray();
            var capture = typeof(ShapeVisualBase).GetMethod("CaptureTranslationState");
            Assert.NotNull(capture);
            var original = capture!.Invoke(shape, null);
            shape.Translate(new Vector(0.7, -0.3));
            var moved = capture.Invoke(shape, null);
            var after = shape.GetMetaData().DataPoints.ToArray();
            var restore = typeof(ShapeVisualBase).GetMethod("RestoreTranslationState");
            Assert.NotNull(restore);
            for (var i = 0; i < 10; i++)
            {
                restore!.Invoke(shape, new[] { original });
                Assert.Equal(before, shape.GetMetaData().DataPoints);
                restore.Invoke(shape, new[] { moved });
                Assert.Equal(after, shape.GetMetaData().DataPoints);
            }
            restore!.Invoke(shape, new[] { original });
            var texts = typeof(ShapeVisualBase).GetProperty("TextGeometries", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.Equal(new Point(0.3, 0.7), Assert.Single((IReadOnlyList<(Point Location, string Content)>)texts.GetValue(shape)!).Location);
        });
    }

    [Fact]
    public void Snapshot_RestoresPolygonWithoutAppendingVerticesAndFiberWithoutReconstruction()
    {
        Sta(() =>
        {
            var polygon = new Polygon(TestShapeLayer.Create());
            polygon.FromData(new PointsData(1, new List<Point> { new(0.1, 0.2), new(100.3, 0.2), new(100.3, 100.9) }));
            var fiber = new Fiber(TestShapeLayer.Create());
            fiber.FromData(new FiberData { FilletCenter = new Point(10.1, 20.2), Width = 50, Height = 30, FiberAngleInDeg = 13, FilletRadius = 2 });
            var capture = typeof(ShapeVisualBase).GetMethod("CaptureTranslationState");
            var restore = typeof(ShapeVisualBase).GetMethod("RestoreTranslationState");
            Assert.NotNull(capture);
            Assert.NotNull(restore);
            var points = polygon.GetMetaData().DataPoints.ToArray();
            var corners = new[] { fiber.RectTopLeft, fiber.RectTopRight, fiber.RectBottomLeft, fiber.RectBottomRight };
            var polygonState = capture!.Invoke(polygon, null);
            var fiberState = capture.Invoke(fiber, null);
            polygon.Translate(new Vector(0.7, 0.3));
            fiber.Translate(new Vector(0.7, 0.3));
            restore!.Invoke(polygon, new[] { polygonState });
            restore.Invoke(fiber, new[] { fiberState });
            Assert.Equal(points, polygon.GetMetaData().DataPoints);
            Assert.Equal(corners, new[] { fiber.RectTopLeft, fiber.RectTopRight, fiber.RectBottomLeft, fiber.RectBottomRight });
        });
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

    private sealed class StubDxfService : IDxfDocumentService
    {
        private readonly netDxf.DxfDocument _document;
        public StubDxfService(netDxf.DxfDocument document) => _document = document;
        public netDxf.DxfDocument Load(string path) => _document;
        public void Save(netDxf.DxfDocument document, string path) { }
    }
}
