using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Custom;
using Lan.Shapes.DialogGeometry;
using Lan.Shapes.Models;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class CustomShapeContractTests
{
    [Fact]
    public void TextGeometry_UsesRenderedTextForCrossingAndContainmentSelection()
    {
        RunOnSta(() =>
        {
            var shape = new TextGeometry(TestShapeLayer.Create());
            shape.FromData(new TextGeometryData(new Point(20, 30), "Text outline", 24)
            {
                StrokeThickness = 1
            });
            var bounds = shape.BoundsRect;
            Assert.False(bounds.IsEmpty);
            bounds.Inflate(2, 2);

            Assert.False(shape.RenderGeometry.IsEmpty());
            Assert.Equal(shape.RenderGeometry.Bounds, shape.BoundsRect);
            Assert.True(shape.MatchesSelectionRectangle(bounds, crossing: false));
            Assert.True(shape.MatchesSelectionRectangle(bounds, crossing: true));
        });
    }

    [Fact]
    public void ThickenedCross_MouseReleaseCompletesCreationAndUsesModelBounds()
    {
        RunOnSta(() =>
        {
            var shape = new ThickenedCross(TestShapeLayer.Create());
            shape.OnMouseLeftButtonDown(new Point(40, 30));
            shape.OnMouseMove(new Point(60, 110), MouseButtonState.Pressed);
            shape.OnMouseLeftButtonUp(new Point(60, 110));

            Assert.False(shape.RenderGeometry.IsEmpty());
            Assert.True(shape.IsGeometryRendered);
            Assert.Equal(shape.RenderGeometry.Bounds, shape.BoundsRect);
            Assert.True(shape.MatchesSelectionRectangle(shape.BoundsRect, crossing: false));
        });
    }

    [Fact]
    public void ThickenedLine_CrossingSelectionIncludesActualStrokeWidth()
    {
        RunOnSta(() =>
        {
            var layer = TestShapeLayer.Create();
            layer.GetStyler(Lan.Shapes.Enums.ShapeVisualState.Normal).SetFillColor(Brushes.Red.CloneCurrentValue());
            var shape = new ThickenedLine(layer);
            shape.FromData(new PointsData(20, new List<Point> { new(20, 50), new(120, 50) }));

            Assert.Equal(shape.RenderGeometry.Bounds, shape.BoundsRect);
            Assert.True(shape.MatchesSelectionRectangle(new Rect(55, 57, 5, 2), crossing: true));
            Assert.False(shape.MatchesSelectionRectangle(new Rect(55, 65, 5, 2), crossing: true));
        });
    }

    [Fact]
    public void FixedCenterCircle_BoundsFollowLoadedGeometry()
    {
        RunOnSta(() =>
        {
            var shape = new FixedCenterCircle(TestShapeLayer.Create());
            shape.FromData(new EllipseData { Center = new Point(100, 80), RadiusX = 25, RadiusY = 25 });

            Assert.Equal(new Rect(75, 55, 50, 50), shape.BoundsRect);
            Assert.True(shape.MatchesSelectionRectangle(new Rect(70, 50, 60, 60), crossing: false));
        });
    }

    [Fact]
    public void FixedCenterCircle_CrossingSelectionUsesItsLayerStroke()
    {
        RunOnSta(() =>
        {
            var shape = new FixedCenterCircle(TestShapeLayer.Create());
            shape.FromData(new EllipseData { Center = new Point(100, 80), RadiusX = 25, RadiusY = 25 });
            Assert.False(shape.MatchesSelectionRectangle(new Rect(127, 79, 1, 2), crossing: true));
            Assert.True(shape.MatchesSelectionRectangle(new Rect(124.75, 79, 1, 2), crossing: true));
        });
    }

    [Fact]
    public void TextGeometry_WithoutStrokeOverrideUsesItsLayerStrokeForSelection()
    {
        RunOnSta(() =>
        {
            var shape = new TextGeometry(TestShapeLayer.Create());
            shape.FromData(new TextGeometryData(new Point(20, 30), "I", 40) { StrokeThickness = 0 });
            var bounds = shape.BoundsRect;
            Assert.False(shape.MatchesSelectionRectangle(
                new Rect(bounds.Right + 2, bounds.Top + bounds.Height / 2, 1, 1), crossing: true));
        });
    }

    [Fact]
    public void ThickenedLine_DirectMouseInteractionPreservesLockedGeometry()
    {
        RunOnSta(() =>
        {
            var shape = new ThickenedLine(TestShapeLayer.Create());
            shape.FromData(new PointsData(20, new List<Point> { new(20, 50), new(120, 50) }));
            var original = shape.GetMetaData();
            shape.Lock();

            shape.OnMouseLeftButtonDown(new Point(60, 50));
            shape.OnMouseMove(new Point(80, 80), MouseButtonState.Pressed);
            shape.OnMouseLeftButtonUp(new Point(80, 80));

            Assert.True(shape.IsLocked);
            Assert.Equal(original.DataPoints, shape.GetMetaData().DataPoints);
            Assert.Equal(original.StrokeThickness, shape.GetMetaData().StrokeThickness);
        });
    }

    [Fact]
    public void TextGeometry_MetadataUpdatesSelectionDuringDeferredRendering()
    {
        RunOnSta(() =>
        {
            var layer = TestShapeLayer.Create();
            var shape = new TextGeometry(layer);
            using (shape.DeferVisualUpdates())
            {
                shape.FromData(new TextGeometryData(new Point(20, 30), "Deferred text", 24)
                {
                    StrokeThickness = 8
                });

                Assert.False(shape.RenderGeometry.IsEmpty());
                var bounds = shape.RenderGeometry.Bounds;
                bounds.Inflate(5, 5);
                Assert.True(shape.MatchesSelectionRectangle(bounds, crossing: true));
            }

            var modelDrawing = Assert.IsType<GeometryDrawing>(
                VisualTreeHelper.GetDrawing(shape).Children.First());
            Assert.Equal(8, modelDrawing.Pen.Thickness);
            Assert.Equal(8, shape.GetMetaData().StrokeThickness);
            Assert.Equal(1, layer.GetStyler(Lan.Shapes.Enums.ShapeVisualState.Normal).SketchPen.Thickness);
        });
    }

    [Fact]
    public void DxfGeometry_LockingSelectionHidesBothHandlesAndRotationConnector()
    {
        RunOnSta(() =>
        {
            var document = new netDxf.DxfDocument();
            document.Entities.Add(new netDxf.Entities.Line(new netDxf.Vector2(10, 20), new netDxf.Vector2(40, 50)));
            var shape = new DxfGeometry(TestShapeLayer.Create(), new StubDxfService(document));
            typeof(DxfGeometry).GetMethod("ReadDxfFile", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(shape, new object[] { "drawing.dxf", new Point(100, 100), 1.0 });
            shape.OnMouseLeftButtonUp(new Point(100, 100));
            shape.SetSelectionAppearance(selected: true, showHandles: true);
            shape.Lock();

            Assert.True(shape.IsSelected);
            Assert.True(shape.IsLocked);
            var drawing = Assert.Single(VisualTreeHelper.GetDrawing(shape).Children);
            Assert.Equal(shape.RenderGeometry.Bounds, Assert.IsType<GeometryDrawing>(drawing).Geometry.Bounds);
        });
    }

    [Theory]
    [InlineData(typeof(ArrowedLine))]
    [InlineData(typeof(Fiber))]
    [InlineData(typeof(FixedCenterCircle))]
    [InlineData(typeof(ThickenedCircle))]
    [InlineData(typeof(ThickenedCross))]
    [InlineData(typeof(ThickenedLine))]
    [InlineData(typeof(ThickenedRectangle))]
    [InlineData(typeof(GridGeometry))]
    [InlineData(typeof(DxfGeometry))]
    public void CancelInteraction_CompletedCustomShapeRequiresFreshMouseDown(Type shapeType)
    {
        RunOnSta(() =>
        {
            var shape = CreateCompletedShape(shapeType);
            shape.SetSelectionAppearance(selected: true, showHandles: true);
            var bounds = shape.BoundsRect;
            var point = shape is Fiber fiber
                ? new Point((fiber.RectTopLeft.X + fiber.RectBottomRight.X) / 2,
                    (fiber.RectTopLeft.Y + fiber.RectBottomRight.Y) / 2)
                : new Point(bounds.Right, bounds.Top + bounds.Height / 2);
            shape.OnMouseLeftButtonDown(point);
            shape.CancelInteraction();
            var geometry = GetGeometrySnapshot(shape);

            shape.OnMouseMove(point + new Vector(40, 30), MouseButtonState.Pressed);

            Assert.Equal(geometry, GetGeometrySnapshot(shape));
            Assert.False(shape.IsBeingDraggedOrPanMoving);
            Assert.True(shape.IsGeometryRendered);

            // GridGeometry has no completed-shape editing gestures.
            if (shape is not GridGeometry)
            {
                shape.OnMouseLeftButtonDown(point);
                shape.OnMouseMove(point + new Vector(40, 30), MouseButtonState.Pressed);
                Assert.NotEqual(geometry, GetGeometrySnapshot(shape));
            }
        });
    }

    [Theory]
    [InlineData(typeof(ArrowedLine))]
    [InlineData(typeof(Fiber))]
    [InlineData(typeof(FixedCenterCircle))]
    [InlineData(typeof(ThickenedCircle))]
    [InlineData(typeof(ThickenedCross))]
    [InlineData(typeof(ThickenedLine))]
    [InlineData(typeof(ThickenedRectangle))]
    [InlineData(typeof(GridGeometry))]
    public void CancelInteraction_UnfinishedCustomShapeRequiresFreshMouseDown(Type shapeType)
    {
        RunOnSta(() =>
        {
            var shape = (ShapeVisualBase)Activator.CreateInstance(shapeType, TestShapeLayer.Create())!;
            shape.OnMouseLeftButtonDown(new Point(40, 30));
            shape.OnMouseMove(new Point(70, 110), MouseButtonState.Pressed);
            shape.CancelInteraction();
            var geometry = GetGeometrySnapshot(shape);

            shape.OnMouseMove(new Point(90, 130), MouseButtonState.Pressed);

            Assert.Equal(geometry, GetGeometrySnapshot(shape));
            Assert.False(shape.IsBeingDraggedOrPanMoving);
            Assert.False(shape.IsGeometryRendered);
        });
    }

    [Theory]
    [InlineData(typeof(ArrowedLine))]
    [InlineData(typeof(Fiber))]
    [InlineData(typeof(FixedCenterCircle))]
    [InlineData(typeof(TextGeometry))]
    [InlineData(typeof(ThickenedCircle))]
    [InlineData(typeof(ThickenedCross))]
    [InlineData(typeof(ThickenedLine))]
    [InlineData(typeof(ThickenedRectangle))]
    [InlineData(typeof(DxfGeometry))]
    [InlineData(typeof(GriddedRectangle))]
    [InlineData(typeof(GridGeometry))]
    public void AddTextAndClearText_ApplyToEveryCustomShape(Type shapeType)
    {
        RunOnSta(() =>
        {
            var shape = (ShapeVisualBase)Activator.CreateInstance(shapeType, TestShapeLayer.Create())!;
            shape.UpdateVisual();
            var originalTextRuns = CountTextRuns(VisualTreeHelper.GetDrawing(shape));

            shape.AddText("attached annotation", new Point(200, 200));

            Assert.Equal(originalTextRuns + 1, CountTextRuns(VisualTreeHelper.GetDrawing(shape)));
            shape.ClearText();
            Assert.Equal(originalTextRuns, CountTextRuns(VisualTreeHelper.GetDrawing(shape)));
        });
    }

    private static int CountTextRuns(Drawing? drawing) => drawing switch
    {
        GlyphRunDrawing => 1,
        DrawingGroup group => group.Children.Sum(CountTextRuns),
        _ => 0
    };

    private static string GetGeometrySnapshot(ShapeVisualBase shape)
        => shape.RenderGeometry.Bounds.ToString(CultureInfo.InvariantCulture) + ";"
            + shape.RenderGeometry.GetFlattenedPathGeometry().ToString(CultureInfo.InvariantCulture);

    private static ShapeVisualBase CreateCompletedShape(Type shapeType)
    {
        var shape = (ShapeVisualBase)Activator.CreateInstance(shapeType, TestShapeLayer.Create())!;
        switch (shape)
        {
            case ThickenedLine line:
                line.FromData(new PointsData(12, new List<Point> { new(20, 50), new(120, 50) }));
                break;
            case Fiber fiber:
                fiber.FromData(new FiberData { FilletCenter = new Point(100, 80), Width = 60, Height = 40, FilletRadius = 10 });
                break;
            case FixedCenterCircle fixedCircle:
                fixedCircle.FromData(new EllipseData { Center = new Point(100, 80), RadiusX = 25, RadiusY = 25 });
                break;
            case ThickenedCircle thickCircle:
                thickCircle.FromData(new EllipseData { Center = new Point(100, 80), RadiusX = 25, RadiusY = 25, StrokeThickness = 12 });
                break;
            case ThickenedCross cross:
                cross.FromData(new PointsData(12, new List<Point> { new(40, 20), new(60, 100), new(10, 40), new(100, 60) }));
                break;
            case ThickenedRectangle rectangle:
                rectangle.FromData(new PointsData(12, new List<Point> { new(20, 20), new(120, 100) }));
                break;
            case GridGeometry grid:
                grid.FromData(new GridGeometryData { TopLeft = new Point(20, 20), BottomRight = new Point(120, 100), RowCount = 2, ColumnCount = 2 });
                break;
            case DxfGeometry:
                var document = new netDxf.DxfDocument();
                document.Entities.Add(new netDxf.Entities.Line(new netDxf.Vector2(10, 20), new netDxf.Vector2(40, 50)));
                var dxf = new DxfGeometry(TestShapeLayer.Create(), new StubDxfService(document));
                typeof(DxfGeometry).GetMethod("ReadDxfFile", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(dxf, new object[] { "drawing.dxf", new Point(100, 100), 1.0 });
                dxf.OnMouseLeftButtonUp(new Point(100, 100));
                return dxf;
            default:
                throw new ArgumentOutOfRangeException(nameof(shapeType));
        }
        return shape;
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null) ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private sealed class StubDxfService : IDxfDocumentService
    {
        private readonly netDxf.DxfDocument _document;
        public StubDxfService(netDxf.DxfDocument document) => _document = document;
        public netDxf.DxfDocument Load(string filePath) => _document;
        public void Save(netDxf.DxfDocument document, string filePath) => throw new NotSupportedException();
    }
}
