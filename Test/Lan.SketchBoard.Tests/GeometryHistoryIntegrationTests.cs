using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.ImageViewer.ViewModels.GeometryEditing;
using Lan.Shapes;
using Lan.Shapes.Custom;
using Lan.Shapes.DialogGeometry;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class GeometryHistoryIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VertexHandle_ReplaysExactEndpointsAndVertices(bool polygon) => BulkLayerFeatureTests.Sta(() =>
    {
        var board = Board(out var manager);
        ShapeVisualBase shape;
        if (polygon)
        {
            var model = new Polygon(manager.CurrentShapeLayer!);
            model.FromData(new PointsData(1, new List<Point> { new(40, 50), new(140, 50), new(140, 140) }));
            shape = model;
        }
        else
        {
            var model = new Line(manager.CurrentShapeLayer!);
            model.FromData(new PointsData(1, new List<Point> { new(40, 50), new(140, 50) }));
            shape = model;
        }
        manager.AddShape(shape);
        manager.SetSelection(new[] { shape });
        var before = shape.CaptureGeometryState();
        board.Press(new Point(140, 50));
        board.Move(new Point(160, 70));
        board.Release(new Point(160, 70));
        var after = shape.CaptureGeometryState();
        Assert.False(before.HasSameContent(after));
        Assert.True(manager.CanUndo);
        manager.Undo();
        Assert.True(before.HasSameContent(shape.CaptureGeometryState()));
        Assert.False(manager.CanUndo);
        manager.Redo();
        Assert.True(after.HasSameContent(shape.CaptureGeometryState()));
    });

    [Fact]
    public void ActiveGeometryScope_DisablesReplayUntilEditCompletes() => BulkLayerFeatureTests.Sta(() =>
    {
        _ = Board(out var manager);
        var shape = new Circle(manager.CurrentShapeLayer!);
        shape.FromData(new EllipseData { Center = new Point(150, 150), RadiusX = 40 });
        manager.AddShape(shape);
        shape.Translate(new Vector(10, 0));
        using (shape.BeginGeometryEdit())
        {
            shape.SetCircle(shape.Center, 60);
            Assert.False(manager.CanUndo);
            manager.Undo();
            Assert.Equal(new Point(160, 150), shape.Center);
            Assert.Equal(60, shape.Radius);
        }
        Assert.True(manager.CanUndo);
        manager.Undo();
        Assert.Equal(40, shape.Radius);
        manager.Undo();
        Assert.Equal(new Point(150, 150), shape.Center);
    });

    [Fact]
    public void ResizeReturningToOriginal_PreservesExistingRedoBranch() => BulkLayerFeatureTests.Sta(() =>
    {
        var board = Board(out var manager);
        var shape = new Ellipse(manager.CurrentShapeLayer!);
        shape.FromData(new EllipseData { Center = new Point(150, 150), RadiusX = 40, RadiusY = 30 });
        manager.AddShape(shape);
        shape.Translate(new Vector(10, 0));
        manager.Undo();
        manager.SetSelection(new[] { shape });
        board.Press(new Point(190, 150));
        board.Move(new Point(210, 150));
        board.Move(new Point(190, 150));
        board.Release(new Point(190, 150));
        Assert.Equal(40, shape.RadiusX);
        Assert.False(manager.CanUndo);
        Assert.True(manager.CanRedo);
        manager.Redo();
        Assert.Equal(new Point(160, 150), shape.Center);
    });

    [Theory]
    [InlineData("line")]
    [InlineData("rectangle")]
    [InlineData("circle")]
    public void PropertyPanelCommit_ReplaysGeometryAndRejectsInvalidInput(string kind) => BulkLayerFeatureTests.Sta(() =>
    {
        _ = Board(out var manager);
        ShapeVisualBase shape;
        if (kind == "line")
        {
            var line = new Line(manager.CurrentShapeLayer!);
            line.FromData(new PointsData(1, new List<Point> { new(40, 50), new(140, 50) }));
            shape = line;
        }
        else if (kind == "rectangle")
        {
            var rectangle = new Rectangle(manager.CurrentShapeLayer!);
            rectangle.FromData(new PointsData(1, new List<Point> { new(40, 50), new(140, 120) }));
            shape = rectangle;
        }
        else
        {
            var circle = new Circle(manager.CurrentShapeLayer!);
            circle.FromData(new EllipseData { Center = new Point(100, 100), RadiusX = 40 });
            shape = circle;
        }
        manager.AddShape(shape);
        manager.SetSelection(new[] { shape });
        using var editor = ShapeGeometryEditor.Create(shape, manager)!;
        var before = shape.CaptureGeometryState();
        var group = editor.Groups[kind == "line" ? 2 : 1];
        group.Fields[0].Text = "80";
        Assert.True(editor.Commit(group));
        var after = shape.CaptureGeometryState();
        Assert.False(before.HasSameContent(after));
        Assert.True(manager.CanUndo);
        manager.Undo();
        Assert.True(before.HasSameContent(shape.CaptureGeometryState()));
        Assert.False(manager.CanUndo);
        manager.Redo();
        Assert.True(after.HasSameContent(shape.CaptureGeometryState()));
        manager.ClearHistory();
        using var refreshedEditor = ShapeGeometryEditor.Create(shape, manager)!;
        var invalidGroup = refreshedEditor.Groups[kind == "line" ? 2 : 1];
        invalidGroup.Fields[0].Text = "-1";
        Assert.False(refreshedEditor.Commit(invalidGroup));
        Assert.True(after.HasSameContent(shape.CaptureGeometryState()));
        Assert.False(manager.CanUndo);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rectangle2_HandleGestureRestoresRotationAndDimensions(bool rotate) => BulkLayerFeatureTests.Sta(() =>
    {
        var board = Board(out var manager);
        var shape = new Rectangle2(manager.CurrentShapeLayer!);
        shape.FromData(new Rectangle2Data { Row = 150, Column = 150, Length1 = 50, Length2 = 30 });
        manager.AddShape(shape);
        manager.SetSelection(new[] { shape });
        var before = shape.CaptureGeometryState();
        var handle = rotate ? new Point(150, 96) : new Point(200, 180);
        var target = rotate ? new Point(204, 150) : new Point(225, 195);
        board.Press(handle);
        board.Move(target);
        Assert.False(manager.CanUndo);
        board.Release(target);
        var after = shape.CaptureGeometryState();
        Assert.False(before.HasSameContent(after));
        if (rotate) Assert.NotEqual(0, shape.Phi);
        else Assert.True(shape.Length1 > 50 && shape.Length2 > 30);
        for (var repeat = 0; repeat < 3; repeat++)
        {
            manager.Undo();
            Assert.True(before.HasSameContent(shape.CaptureGeometryState()));
            Assert.False(manager.CanUndo);
            manager.Redo();
            Assert.True(after.HasSameContent(shape.CaptureGeometryState()));
            Assert.False(manager.CanRedo);
        }
    });

    [Theory]
    [InlineData("escape")]
    [InlineData("capture")]
    [InlineData("unload")]
    [InlineData("rebind")]
    public void InterruptedResize_PreservesOneUndoStepAndStopsFurtherEdits(string interruption) => BulkLayerFeatureTests.Sta(() =>
    {
        var board = Board(out var manager);
        var shape = new Ellipse(manager.CurrentShapeLayer!);
        shape.FromData(new EllipseData { Center = new Point(150, 150), RadiusX = 40, RadiusY = 30 });
        manager.AddShape(shape);
        manager.SetSelection(new[] { shape });
        board.Press(new Point(190, 150));
        board.Move(new Point(210, 150));
        Assert.Equal(60, shape.RadiusX);
        switch (interruption)
        {
            case "escape": board.Key(Key.Escape); break;
            case "capture": board.LoseCapture(); break;
            case "unload": board.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent, board)); break;
            case "rebind": board.SketchBoardDataManager = new SketchBoardDataManager(); break;
        }
        board.Move(new Point(240, 150));
        board.Release(new Point(240, 150));
        Assert.Equal(60, shape.RadiusX);
        Assert.True(manager.CanUndo);
        manager.Undo();
        Assert.Equal(40, shape.RadiusX);
        Assert.Equal(30, shape.RadiusY);
        Assert.False(manager.CanUndo);
        manager.Redo();
        Assert.Equal(60, shape.RadiusX);
    });

    [Fact]
    public void ThicknessHandle_RestoresThicknessAndCornerGeometry() => BulkLayerFeatureTests.Sta(() =>
    {
        var board = Board(out var manager);
        var shape = new ThickenedRectangle(manager.CurrentShapeLayer!);
        shape.FromData(new PointsData(10, new List<Point> { new(80, 100), new(220, 200) }));
        manager.AddShape(shape);
        manager.SetSelection(new[] { shape });
        var before = shape.CaptureGeometryState();
        board.Press(new Point(150, 95));
        board.Move(new Point(150, 85));
        board.Release(new Point(150, 85));
        Assert.Equal(20, shape.GetMetaData().StrokeThickness);
        manager.Undo();
        Assert.Equal(10, shape.GetMetaData().StrokeThickness);
        Assert.True(before.HasSameContent(shape.CaptureGeometryState()));
        Assert.False(manager.CanUndo);
        manager.Redo();
        Assert.Equal(20, shape.GetMetaData().StrokeThickness);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DxfHandle_EditReplaysBakedGeometryAndExport(bool rotate) => BulkLayerFeatureTests.Sta(() =>
    {
        var board = Board(out var manager);
        var document = new netDxf.DxfDocument();
        document.Entities.Add(new netDxf.Entities.Line(new netDxf.Vector2(10, 20), new netDxf.Vector2(140, 150)));
        var shape = new DxfGeometry(manager.CurrentShapeLayer!, new StubDxfService(document));
        typeof(DxfGeometry).GetMethod("ReadDxfFile", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(shape, new object[] { "drawing.dxf", new Point(100, 100), 1.0 });
        shape.OnMouseLeftButtonUp(new Point(100, 100));
        manager.AddShape(shape);
        manager.SetSelection(new[] { shape });
        var bounds = shape.BoundsRect;
        var before = shape.CaptureGeometryState();
        var beforeLine = ExportLine(shape);
        var handle = rotate ? new Point(bounds.Left + bounds.Width / 2, bounds.Top - 30) : bounds.BottomRight;
        // Read the actual rotation handle so the test follows its configured screen offset.
        if (rotate) handle = (Point)typeof(DxfGeometry).GetMethod("GetRotationHandleCenter", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(shape, new object[] { bounds })!;
        var target = rotate ? new Point(bounds.Right + 30, bounds.Top + bounds.Height / 2) : handle + new Vector(30, 30);
        board.Press(handle);
        board.Move(target);
        board.Release(target);
        var after = shape.CaptureGeometryState();
        var afterLine = ExportLine(shape);
        Assert.False(before.HasSameContent(after));
        Assert.NotEqual(beforeLine, afterLine);
        for (var repeat = 0; repeat < 3; repeat++)
        {
            manager.Undo();
            Assert.Equal(beforeLine, ExportLine(shape));
            Assert.False(manager.CanUndo);
            manager.Redo();
            Assert.Equal(afterLine, ExportLine(shape));
            Assert.False(manager.CanRedo);
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DxfUnchangedGesture_PreservesUnbakedStateAndRedo(bool moveBack) => BulkLayerFeatureTests.Sta(() =>
    {
        var board = Board(out var manager);
        var document = new netDxf.DxfDocument();
        document.Entities.Add(new netDxf.Entities.Line(new netDxf.Vector2(10, 20), new netDxf.Vector2(140, 150)));
        var shape = new DxfGeometry(manager.CurrentShapeLayer!, new StubDxfService(document));
        typeof(DxfGeometry).GetMethod("ReadDxfFile", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(shape, new object[] { "drawing.dxf", new Point(100, 100), 1.0 });
        shape.OnMouseLeftButtonUp(new Point(100, 100));
        manager.AddShape(shape);
        var original = shape.CaptureGeometryState();
        shape.Translate(new Vector(10, 0));
        var before = shape.CaptureGeometryState();
        shape.Translate(new Vector(10, 0));
        var after = shape.CaptureGeometryState();
        manager.Undo();
        manager.SetSelection(new[] { shape });
        var bounds = shape.BoundsRect;
        var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        board.Press(center);
        if (moveBack)
        {
            board.Move(center + new Vector(20, 0));
            board.Move(center);
        }
        board.Release(center);
        Assert.True(before.HasSameContent(shape.CaptureGeometryState()));
        Assert.True(manager.CanRedo);
        manager.Undo();
        Assert.True(original.HasSameContent(shape.CaptureGeometryState()));
        Assert.False(manager.CanUndo);
        manager.Redo();
        manager.Redo();
        Assert.True(after.HasSameContent(shape.CaptureGeometryState()));
        var exported = ExportLine(shape);
        shape.RestoreGeometryState(after);
        Assert.Equal(exported, ExportLine(shape));
    });

    private static (netDxf.Vector3 Start, netDxf.Vector3 End) ExportLine(DxfGeometry shape)
    {
        var line = Assert.Single(shape.ExportToDxf(new Point(), true).Entities.Lines);
        return (line.StartPoint, line.EndPoint);
    }

    private static Probe Board(out SketchBoardDataManager manager)
    {
        manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        var board = new Probe { Width = 500, Height = 400, Background = Brushes.Transparent,
            SketchBoardDataManager = manager, IsSnappingEnabled = false };
        board.Measure(new Size(500, 400));
        board.Arrange(new Rect(0, 0, 500, 400));
        return board;
    }
    private sealed class Probe : SketchBoard
    {
        public void Press(Point point) => HandleLeftButtonDown(point, 1);
        public void Move(Point point) => HandleMouseMove(point, MouseButtonState.Pressed);
        public void Release(Point point) => HandleLeftButtonUp(point);
        public void Key(Key key) => HandleSelectionKey(key, ModifierKeys.None);
        public void LoseCapture() => OnLostMouseCapture(new MouseEventArgs(Mouse.PrimaryDevice, 0)
            { RoutedEvent = Mouse.LostMouseCaptureEvent, Source = this });
    }
    private sealed class StubDxfService : IDxfDocumentService
    {
        private readonly netDxf.DxfDocument _document;
        public StubDxfService(netDxf.DxfDocument document) => _document = document;
        public netDxf.DxfDocument Load(string path) => _document;
        public void Save(netDxf.DxfDocument document, string path) { }
    }
}
