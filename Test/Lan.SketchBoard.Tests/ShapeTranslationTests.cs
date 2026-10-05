using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Custom;
using Lan.Shapes.DialogGeometry;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeTranslationTests
{
    // WPF path/combined geometry bounds are computed through single-precision native
    // geometry operations. Exported model coordinates remain exact assertions below.
    private const double GeometryBoundsTolerance = 0.0001;

    public static IEnumerable<object[]> MovableTypes => new[]
    {
        typeof(Line), typeof(Rectangle), typeof(Rectangle2), typeof(Circle), typeof(Ellipse),
        typeof(Polygon), typeof(Cross), typeof(Angle), typeof(Fiber), typeof(ThickenedLine),
        typeof(ArrowedLine), typeof(ThickenedRectangle), typeof(ThickenedCircle),
        typeof(ThickenedCross), typeof(GridGeometry), typeof(GriddedRectangle), typeof(TextGeometry)
    }.Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(MovableTypes))]
    public void ModelTranslation_MovesGeometryAndExportedCoordinatesWithoutChangingVisualTransform(Type type)
    {
        Sta(() =>
        {
            var shape = Create(type);
            var visualTransform = new MatrixTransform(new Matrix(1.2, 0.1, 0.2, 0.8, 30, 40));
            shape.Transform = visualTransform;
            var before = shape.BoundsRect;
            var points = MetadataPoints(shape);
            var delta = new Vector(15, -7);

            Assert.True(CanTranslate(shape));
            Translate(shape, delta);

            Assert.Same(visualTransform, shape.Transform);
            AssertRectOffset(before, shape.BoundsRect, delta);
            Assert.Equal(points.Select(point => point + delta), MetadataPoints(shape));
        });
    }

    [Theory]
    [MemberData(nameof(MovableTypes))]
    public void ModelTranslation_MovesAttachedTextAndPreservesDimensions(Type type)
    {
        Sta(() =>
        {
            var shape = Create(type);
            shape.AddText("attached", new Point(60, 70));
            var before = shape.RenderGeometry.Bounds;
            Translate(shape, new Vector(-5, 8));
            var property = typeof(ShapeVisualBase).GetProperty("TextGeometries", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var text = Assert.Single((IReadOnlyList<(Point Location, string Content)>)property.GetValue(shape)!);
            Assert.Equal(new Point(55, 78), text.Location);
            AssertGeometryCoordinate(before.Width, shape.RenderGeometry.Bounds.Width);
            AssertGeometryCoordinate(before.Height, shape.RenderGeometry.Bounds.Height);
        });
    }

    [Fact]
    public void ModelTranslation_RejectsLockedIncompleteAndNonFiniteRequestsWithoutChangingModel()
    {
        Sta(() =>
        {
            var shape = (Line)Create(typeof(Line));
            var before = shape.GetMetaData().DataPoints.ToArray();
            Assert.Throws<ArgumentOutOfRangeException>(() => Translate(shape, new Vector(double.NaN, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Translate(shape, new Vector(1, double.PositiveInfinity)));
            shape.Lock();
            Assert.Throws<InvalidOperationException>(() => Translate(shape, new Vector(5, 6)));
            Assert.Equal(before, shape.GetMetaData().DataPoints);
            var incomplete = new Line(TestShapeLayer.Create());
            Assert.Throws<InvalidOperationException>(() => Translate(incomplete, new Vector(5, 6)));
        });
    }

    [Fact]
    public void ModelTranslation_ZeroDoesNotNotifyOrRedraw()
    {
        Sta(() =>
        {
            var shape = new CountingLine(TestShapeLayer.Create());
            shape.FromData(new PointsData(1, new List<Point> { new(10, 20), new(110, 120) }));
            shape.DrawCount = 0;
            var changes = 0;
            shape.PropertyChanged += (_, _) => changes++;
            Translate(shape, new Vector());
            Assert.Equal(0, changes);
            Assert.Equal(0, shape.DrawCount);
            Translate(shape, new Vector(4, 6));
            Assert.Equal(1, shape.DrawCount);
        });
    }

    [Fact]
    public void ReferenceAndUnadaptedShapes_ExplicitlyRejectModelTranslation()
    {
        Sta(() =>
        {
            var fixedCircle = new FixedCenterCircle(TestShapeLayer.Create());
            fixedCircle.FromData(new EllipseData { Center = new Point(50, 50), RadiusX = 20, RadiusY = 20 });
            var ruler = new RulerCross(TestShapeLayer.Create());
            ruler.FromData(new RulerCrossData { Center = new Point(50, 50), Width = 100, Height = 100 });
            foreach (var shape in new ShapeVisualBase[] { fixedCircle, ruler, new UnadaptedShape() })
            {
                Assert.False(CanTranslate(shape));
                Assert.Throws<NotSupportedException>(() => Translate(shape, new Vector(4, 6)));
            }
        });
    }

    [Fact]
    public void FiberReload_WithTranslationDisabledNotifiesItsCapability()
    {
        Sta(() =>
        {
            var fiber = (Fiber)Create(typeof(Fiber));
            var changes = new List<string?>();
            fiber.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
            var metadata = fiber.GetMetaData();
            metadata.EnableTranslation = false;
            fiber.FromData(metadata);
            Assert.False(CanTranslate(fiber));
            Assert.Contains("CanTranslate", changes);
            Assert.Throws<NotSupportedException>(() => Translate(fiber, new Vector(2, 3)));
        });
    }

    [Fact]
    public void DxfTranslation_MovesGeometryAndExportedEntities()
    {
        Sta(() =>
        {
            var document = new netDxf.DxfDocument();
            document.Entities.Add(new netDxf.Entities.Line(new netDxf.Vector2(10, 20), new netDxf.Vector2(40, 50)));
            var shape = new DxfGeometry(TestShapeLayer.Create(), new StubDxfService(document));
            typeof(DxfGeometry).GetMethod("ReadDxfFile", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(shape, new object[] { "drawing.dxf", new Point(100, 100), 1.0 });
            shape.OnMouseLeftButtonUp(new Point(100, 100));
            var beforeBounds = shape.BoundsRect;
            var beforeLine = Assert.Single(shape.ExportToDxf(new Point(), reverseY: true).Entities.Lines);
            var delta = new Vector(13, -9);
            Assert.True(CanTranslate(shape));
            Translate(shape, delta);
            var afterLine = Assert.Single(shape.ExportToDxf(new Point(), reverseY: true).Entities.Lines);
            AssertRectOffset(beforeBounds, shape.BoundsRect, delta);
            Assert.Equal(beforeLine.StartPoint.X + delta.X, afterLine.StartPoint.X, precision: 8);
            Assert.Equal(beforeLine.StartPoint.Y + delta.Y, afterLine.StartPoint.Y, precision: 8);
        });
    }

    private static bool CanTranslate(ShapeVisualBase shape) => shape.CanTranslate;

    private static void Translate(ShapeVisualBase shape, Vector delta) => shape.Translate(delta);

    private static ShapeVisualBase Create(Type type)
    {
        var shape = (ShapeVisualBase)Activator.CreateInstance(type, TestShapeLayer.Create())!;
        object metadata;
        if (shape is Fiber)
            metadata = new FiberData { Width = 40, Height = 20, FilletCenter = new Point(50, 50), FilletRadius = 3, EnableTranslation = true };
        else if (shape is TextGeometry)
            metadata = new TextGeometryData(new Point(20, 30), "Movable", 24);
        else if (shape is GridGeometry)
            metadata = new GridGeometryData { TopLeft = new Point(20, 30), BottomRight = new Point(140, 130), RowCount = 2, ColumnCount = 3 };
        else if (shape is Rectangle2)
            metadata = new Rectangle2Data { Row = 60, Column = 70, Phi = 0.3, Length1 = 40, Length2 = 20 };
        else if (shape is Circle || shape is Ellipse || shape is ThickenedCircle)
            metadata = new EllipseData { Center = new Point(80, 70), RadiusX = 30, RadiusY = 20 };
        else if (shape is Cross)
            metadata = new CrossData { Center = new Point(80, 70), Width = 60, Height = 40 };
        else if (shape is ThickenedCross)
            metadata = new PointsData(4, new List<Point> { new(70, 20), new(90, 120), new(20, 60), new(140, 80) });
        else if (shape is Angle || shape is Polygon)
            metadata = new PointsData(1, new List<Point> { new(20, 30), new(120, 30), new(120, 110) });
        else
            metadata = new PointsData(4, new List<Point> { new(20, 30), new(120, 110) });
        type.GetMethod("FromData", new[] { metadata.GetType() })!.Invoke(shape, new[] { metadata });
        return shape;
    }

    private static IReadOnlyList<Point> MetadataPoints(ShapeVisualBase shape)
    {
        var metadata = shape.GetType().GetMethod("GetMetaData")!.Invoke(shape, null);
        return metadata switch
        {
            PointsData points => points.DataPoints.ToArray(),
            EllipseData ellipse => new[] { ellipse.Center },
            CrossData cross => new[] { cross.Center },
            Rectangle2Data rectangle => new[] { new Point(rectangle.Column, rectangle.Row) },
            FiberData fiber => new[] { fiber.FilletCenter },
            GridGeometryData grid => new[] { grid.TopLeft, grid.BottomRight },
            TextGeometryData text => new[] { text.Location },
            _ => throw new InvalidOperationException("Missing translation metadata assertion.")
        };
    }

    private static void AssertRectOffset(Rect before, Rect after, Vector delta)
    {
        AssertGeometryCoordinate(before.X + delta.X, after.X);
        AssertGeometryCoordinate(before.Y + delta.Y, after.Y);
        AssertGeometryCoordinate(before.Width, after.Width);
        AssertGeometryCoordinate(before.Height, after.Height);
    }

    private static void AssertGeometryCoordinate(double expected, double actual)
        => Assert.InRange(Math.Abs(expected - actual), 0, GeometryBoundsTolerance);

    private sealed class CountingLine : Line
    {
        public CountingLine(ShapeLayer layer) : base(layer) { }
        public int DrawCount { get; set; }
        public override void UpdateVisual() { DrawCount++; base.UpdateVisual(); }
    }

    private sealed class UnadaptedShape : ShapeVisualBase
    {
        public UnadaptedShape() : base(TestShapeLayer.Create()) { }
        protected override void CreateHandles() { }
        protected override void HandleResizing(Point point) { }
        protected override void HandleTranslate(Point point) { }
    }

    private sealed class StubDxfService : IDxfDocumentService
    {
        private readonly netDxf.DxfDocument _document;
        public StubDxfService(netDxf.DxfDocument document) => _document = document;
        public netDxf.DxfDocument Load(string filePath) => _document;
        public void Save(netDxf.DxfDocument document, string filePath) { }
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
