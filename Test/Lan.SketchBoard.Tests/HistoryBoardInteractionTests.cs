using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class HistoryBoardInteractionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DragAndRelease_AreOneUndoStep(bool grouped) => BulkLayerFeatureTests.Sta(() =>
    {
        var board = Board(out var manager);
        var first = Add(manager, 50);
        var second = Add(manager, 120);
        if (grouped) manager.GroupShapes(new[] { first, second });
        board.Press(new Point(80, 50));
        board.Move(new Point(90, 60));
        board.Move(new Point(100, 70));
        board.Release(new Point(110, 90));
        Assert.True(manager.CanUndo);
        board.Key(Key.Z, ModifierKeys.Control);
        Assert.Equal(new Point(40, 50), first.Start);
        Assert.Equal(new Point(40, 120), second.Start);
        Assert.False(manager.CanUndo);
        board.Key(Key.Y, ModifierKeys.Control);
        Assert.Equal(grouped ? new Point(70, 90) : new Point(60, 70), first.Start);
        Assert.Equal(new Point(40 + (grouped ? 30 : 0), 120 + (grouped ? 40 : 0)), second.Start);
        board.Key(Key.Z, ModifierKeys.Control);
        board.Key(Key.Z, ModifierKeys.Control | ModifierKeys.Shift);
        Assert.Equal(grouped ? new Point(70, 90) : new Point(60, 70), first.Start);
    });

    [Theory]
    [InlineData("escape")]
    [InlineData("capture")]
    [InlineData("unload")]
    [InlineData("rebind")]
    public void InterruptedDrag_CommitsOneStepAndStopsFurtherMovement(string interruption) => BulkLayerFeatureTests.Sta(() =>
    {
        var board = Board(out var manager);
        var line = Add(manager, 50);
        board.Press(new Point(80, 50));
        board.Move(new Point(90, 60));
        board.Move(new Point(100, 70));
        switch (interruption)
        {
            case "escape": board.Key(Key.Escape, ModifierKeys.None); break;
            case "capture": board.LoseCapture(); break;
            case "unload": board.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent, board)); break;
            case "rebind": board.SketchBoardDataManager = new SketchBoardDataManager(); break;
        }
        Assert.True(manager.CanUndo);
        board.Move(new Point(120, 90));
        board.Release(new Point(120, 90));
        Assert.Equal(new Point(60, 70), line.Start);
        manager.Undo();
        Assert.Equal(new Point(40, 50), line.Start);
        Assert.False(manager.CanUndo);
    });

    [Fact]
    public void DeleteShortcutAndClickWithoutMovement_RespectStepBoundaries() => BulkLayerFeatureTests.Sta(() =>
    {
        var board = Board(out var manager);
        var first = Add(manager, 50);
        var second = Add(manager, 120);
        board.Press(new Point(80, 50));
        board.Release(new Point(80, 50));
        Assert.False(manager.CanUndo);
        manager.SetSelection(new[] { first, second });
        board.Key(Key.Delete, ModifierKeys.None);
        Assert.Empty(manager.Shapes);
        board.Key(Key.Z, ModifierKeys.Control);
        Assert.Equal(new[] { first, second }, manager.Shapes);
        Assert.False(manager.CanUndo);
    });

    private static Probe Board(out SketchBoardDataManager manager)
    {
        manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        var board = new Probe { Width = 400, Height = 300, Background = Brushes.Transparent,
            SketchBoardDataManager = manager, IsSnappingEnabled = false };
        board.Measure(new Size(400, 300));
        board.Arrange(new Rect(0, 0, 400, 300));
        return board;
    }
    private static Line Add(SketchBoardDataManager manager, double y)
    {
        var line = new Line(manager.CurrentShapeLayer!);
        line.FromData(new PointsData(1, new List<Point> { new(40, y), new(140, y) }));
        manager.AddShape(line);
        return line;
    }
    private sealed class Probe : SketchBoard
    {
        public void Press(Point point) => HandleLeftButtonDown(point, 1);
        public void Move(Point point) => HandleMouseMove(point, MouseButtonState.Pressed);
        public void Release(Point point) => HandleLeftButtonUp(point);
        public void Key(Key key, ModifierKeys modifiers) => HandleSelectionKey(key, modifiers);
        public void LoseCapture() => OnLostMouseCapture(new MouseEventArgs(Mouse.PrimaryDevice, 0)
            { RoutedEvent = Mouse.LostMouseCaptureEvent, Source = this });
    }
}
