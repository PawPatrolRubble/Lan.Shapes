using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes.Enums;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class LineClickInteractionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TwoClicks_PreviewWithoutHoldingAndCommitOnce(int finalClickCount)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(typeof(Line));
            var commits = 0;
            manager.NewShapeSketched += (_, shape) =>
            {
                Assert.True(shape.IsGeometryRendered);
                commits++;
            };

            board.Press(new Point(0, 0));
            var line = Assert.IsType<Line>(manager.CurrentGeometryInEdit);
            Assert.Equal(line.Start, line.End);
            board.Release(new Point(0, 0));
            board.LoseCapture();
            Assert.False(line.IsGeometryRendered);
            Assert.Equal(0, commits);

            board.Hover(new Point(100, 80));
            Assert.Equal(new Point(0, 0), line.Start);
            Assert.Equal(new Point(100, 80), line.End);
            board.Press(new Point(120, 90), clickCount: finalClickCount);
            board.Release(new Point(120, 90));
            board.Hover(new Point(160, 130));

            Assert.True(line.IsGeometryRendered);
            Assert.Equal(new Point(120, 90), line.End);
            Assert.Equal(1, commits);
            Assert.Single(manager.Shapes);
            Assert.Null(manager.CurrentGeometryInEdit);
            Assert.Null(manager.CurrentGeometryType);
        });
    }

    [Fact]
    public void DraggingFirstClick_DoesNotCompleteAndFinalClickFixesEndpoint()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(typeof(Line));
            board.Press(new Point(20, 30));
            var line = Assert.IsType<Line>(manager.CurrentGeometryInEdit);
            board.Drag(new Point(80, 90));
            board.Release(new Point(90, 100));
            Assert.False(line.IsGeometryRendered);

            board.Hover(new Point(110, 120));
            board.Press(new Point(130, 140));
            board.Drag(new Point(150, 160));
            board.Release(new Point(170, 180));

            Assert.True(line.IsGeometryRendered);
            Assert.Equal(new Point(20, 30), line.Start);
            Assert.Equal(new Point(130, 140), line.End);
        });
    }

    [Fact]
    public void HoverPreview_UpdatesWhenShiftAndFixedModeChangeWithoutMovingPointer()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(typeof(Line));
            board.Press(new Point(40, 60));
            board.Release(new Point(40, 60));
            var line = Assert.IsType<Line>(manager.CurrentGeometryInEdit);

            board.Hover(new Point(140, 100));
            board.ModifiersChanged(ModifierKeys.Shift);
            Assert.Equal(new Point(140, 60), line.End);
            board.ModifiersChanged(ModifierKeys.None);
            Assert.Equal(new Point(140, 100), line.End);
            board.LineDirectionMode = LineDirectionMode.Vertical;
            Assert.Equal(new Point(40, 100), line.End);
            board.LineDirectionMode = LineDirectionMode.Free;
            Assert.Equal(new Point(140, 100), line.End);
            Assert.False(line.IsGeometryRendered);
        });
    }

    [Theory]
    [InlineData(LineDirectionMode.Free, ModifierKeys.Shift, 140, 60)]
    [InlineData(LineDirectionMode.Horizontal, ModifierKeys.None, 140, 60)]
    [InlineData(LineDirectionMode.Vertical, ModifierKeys.Shift, 40, 100)]
    public void FinalClick_AppliesDirectionWithoutAPrecedingMove(
        LineDirectionMode mode, ModifierKeys modifiers, double x, double y)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            board.LineDirectionMode = mode;
            manager.SetGeometryType(typeof(Line));
            board.Press(new Point(40, 60));
            board.Release(new Point(40, 60));
            var line = Assert.IsType<Line>(manager.CurrentGeometryInEdit);

            board.Press(new Point(140, 100), modifiers);
            board.Release(new Point(140, 100), modifiers);

            Assert.True(line.IsGeometryRendered);
            Assert.Equal(new Point(x, y), line.End);
        });
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(4)]
    public void HoverAndFinalClick_SnapToCompatibleAnchorAtViewportScale(double scale)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var target = new Line(manager.CurrentShapeLayer!);
            target.FromData(new PointsData(1, new List<Point> { new(100, 60), new(200, 60) }));
            manager.AddShape(target);
            manager.OnImageViewerPropertyChanged(scale);
            board.LineDirectionMode = LineDirectionMode.Horizontal;
            manager.SetGeometryType(typeof(Line));
            board.Press(new Point(40, 60));
            board.Release(new Point(40, 60));
            var line = Assert.IsType<Line>(manager.CurrentGeometryInEdit);

            board.Hover(new Point(100 + 3 / scale, 60 + 2 / scale));
            Assert.Equal(new Point(100, 60), line.End);
            Assert.False(board.MarkerBounds.IsEmpty);
            board.Press(new Point(100 + 4 / scale, 60 + 3 / scale));
            board.Release(new Point(100 + 4 / scale, 60 + 3 / scale));

            Assert.True(line.IsGeometryRendered);
            Assert.Equal(new Point(100, 60), line.End);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Fact]
    public void ClickingStartAgain_KeepsPreviewOpenUntilANonzeroEndpointIsClicked()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(typeof(Line));
            var commits = 0;
            manager.NewShapeSketched += (_, _) => commits++;
            var start = new Point(40, 60);
            board.Press(start);
            board.Release(start);
            board.Press(start, clickCount: 2);
            board.Release(start);
            var line = Assert.IsType<Line>(manager.CurrentGeometryInEdit);
            Assert.False(line.IsGeometryRendered);
            Assert.Equal(0, commits);

            board.Hover(new Point(140, 100));
            Assert.Equal(new Point(140, 100), line.End);
            board.Press(new Point(140, 100));
            board.Release(new Point(140, 100));
            Assert.True(line.IsGeometryRendered);
            Assert.Equal(1, commits);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelBetweenClicks_RemovesPreviewWithoutCommitting(bool escape)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(typeof(Line));
            var commits = 0;
            manager.NewShapeSketched += (_, _) => commits++;
            board.Press(new Point(40, 60));
            board.Release(new Point(40, 60));
            board.Hover(new Point(140, 100));
            if (escape) board.Escape();
            else board.RightClick(new Point(140, 100));
            board.Hover(new Point(160, 120));

            Assert.Empty(manager.Shapes);
            Assert.Null(manager.CurrentGeometryInEdit);
            Assert.Null(manager.CurrentGeometryType);
            Assert.Equal(0, commits);
        });
    }

    private static TestBoard CreateBoard(out SketchBoardDataManager manager)
    {
        manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        var board = new TestBoard
        {
            Width = 320, Height = 240, Background = Brushes.Transparent, SketchBoardDataManager = manager
        };
        board.Measure(new Size(320, 240));
        board.Arrange(new Rect(0, 0, 320, 240));
        return board;
    }

    private sealed class TestBoard : SketchBoard
    {
        public Rect MarkerBounds => ((DrawingVisual)GetVisualChild(VisualChildrenCount - 1)).ContentBounds;
        public void Press(Point point, ModifierKeys modifiers = ModifierKeys.None, int clickCount = 1)
            => HandleLeftButtonDown(point, clickCount, modifiers);
        public void Release(Point point, ModifierKeys modifiers = ModifierKeys.None) => HandleLeftButtonUp(point, modifiers);
        public void Hover(Point point, ModifierKeys modifiers = ModifierKeys.None)
            => HandleMouseMove(point, MouseButtonState.Released, modifiers);
        public void Drag(Point point) => HandleMouseMove(point, MouseButtonState.Pressed);
        public void ModifiersChanged(ModifierKeys modifiers) => HandleModifierKeysChanged(modifiers);
        public void RightClick(Point point) => HandleRightButtonUp(point);
        public void Escape() => HandleSelectionKey(Key.Escape, ModifierKeys.None);
        public void LoseCapture() => OnLostMouseCapture(new MouseEventArgs(Mouse.PrimaryDevice, 0)
        {
            RoutedEvent = Mouse.LostMouseCaptureEvent, Source = this
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
