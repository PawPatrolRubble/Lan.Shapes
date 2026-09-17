using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Custom;
using Lan.Shapes.Enums;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class GeometryLockInteractionTests
{
    [Fact]
    public void DoubleClick_TogglesRulerLockAndDraggingResumesAfterUnlock()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var ruler = LoadRuler(manager);
            var center = ruler.Center;

            board.Press(center, clickCount: 2);
            Assert.True(ruler.IsLocked);
            Assert.Equal(ShapeVisualState.Locked, ruler.State);
            board.Move(center + new Vector(20, 30), MouseButtonState.Pressed);
            board.Release();
            Assert.Equal(center, ruler.Center);

            board.Move(center, MouseButtonState.Released);
            Assert.True(ruler.IsLocked);
            board.Press(center, clickCount: 1);
            board.Release();
            Assert.True(ruler.IsLocked);
            board.Press(center, clickCount: 2);
            Assert.False(ruler.IsLocked);
            Assert.Equal(ShapeVisualState.Selected, ruler.State);
            board.Move(center + new Vector(20, 30), MouseButtonState.Pressed);
            board.Release();
            Assert.Equal(center, ruler.Center);

            board.Press(center, clickCount: 1);
            board.Move(center + new Vector(20, 30), MouseButtonState.Pressed);
            Assert.Equal(center + new Vector(20, 30), ruler.Center);
            Assert.Single(manager.Shapes);
        });
    }

    [Fact]
    public void ClickingLockedShape_DoesNotCreateGeometryOrDragAnotherSelection()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var rectangle = new TrackingRectangle(manager.CurrentShapeLayer!);
            rectangle.FromData(new PointsData(1, new List<Point> { new(10, 10), new(80, 70) }));
            manager.AddShape(rectangle);
            manager.SelectedGeometry = rectangle;
            var ruler = LoadRuler(manager);
            ruler.Lock();

            board.Press(ruler.Center, clickCount: 1);
            board.Move(new Point(200, 180), MouseButtonState.Pressed);
            board.Release();

            Assert.Equal(2, manager.ShapeCount);
            Assert.True(ruler.IsLocked);
            Assert.Same(rectangle, manager.SelectedGeometry);
            Assert.Equal(0, rectangle.MoveCount);
            Assert.Null(manager.CurrentGeometryInEdit);
        });
    }

    [Fact]
    public void DoubleClick_UsesLockGestureForCompletedShapesWithCustomDoubleClickHooks()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var rectangle = new TrackingRectangle(manager.CurrentShapeLayer!);
            rectangle.FromData(new PointsData(1, new List<Point> { new(20, 20), new(100, 80) }));
            manager.AddShape(rectangle);
            var point = new Point(60, 20);

            board.Press(point, clickCount: 2);
            board.Release();
            Assert.True(rectangle.IsLocked);
            board.Press(point, clickCount: 2);
            board.Release();
            Assert.False(rectangle.IsLocked);
            Assert.Equal(0, rectangle.DoubleClickCount);
        });
    }

    [Fact]
    public void DoubleClick_UnfinishedShapeKeepsCreationHookAndDoesNotLock()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var rectangle = new TrackingRectangle(manager.CurrentShapeLayer!);
            manager.AddShape(rectangle);
            manager.CurrentGeometryInEdit = rectangle;

            board.Press(new Point(250, 180), clickCount: 2);

            Assert.False(rectangle.IsLocked);
            Assert.False(rectangle.IsGeometryRendered);
            Assert.Equal(1, rectangle.DoubleClickCount);
            Assert.Single(manager.Shapes);
        });
    }

    [Fact]
    public void SelectionHooks_CannotUnlockLockedGeometry()
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        var circle = new FixedCenterCircle(manager.CurrentShapeLayer!);
        circle.FromData(new EllipseData { Center = new Point(100, 100), RadiusX = 30, RadiusY = 30 });
        manager.AddShape(circle);
        circle.Lock();

        manager.SelectedGeometry = circle;
        Assert.True(circle.IsLocked);
        manager.UnselectGeometry();
        Assert.True(circle.IsLocked);
        Assert.Equal(ShapeVisualState.Locked, circle.State);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public void ActiveDrawingTool_StartsLineAtExistingCornerWithoutSelectingOrTogglingIt(bool locked, int clickCount)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var rectangle = new TrackingRectangle(manager.CurrentShapeLayer!);
            rectangle.FromData(new PointsData(1, new List<Point> { new(20, 20), new(100, 80) }));
            manager.AddShape(rectangle);
            if (locked) rectangle.Lock();
            var existingSelections = 0;
            manager.ShapeSelected += (_, shape) => { if (ReferenceEquals(shape, rectangle)) existingSelections++; };
            manager.SetGeometryType(typeof(Line));

            board.Move(rectangle.TopLeft, MouseButtonState.Released);
            Assert.Equal(locked ? ShapeVisualState.Locked : ShapeVisualState.Normal, rectangle.State);
            board.Press(rectangle.TopLeft, clickCount);

            var line = Assert.IsType<Line>(manager.CurrentGeometryInEdit);
            Assert.Equal(rectangle.TopLeft, line.Start);
            Assert.Same(line, manager.SelectedGeometry);
            board.Move(new Point(150, 160), MouseButtonState.Pressed);
            board.Release();
            Assert.Equal(new Point(150, 160), line.End);
            Assert.True(line.IsGeometryRendered);
            Assert.Equal(locked, rectangle.IsLocked);
            Assert.Equal(0, existingSelections);
            Assert.Equal(0, rectangle.MoveCount);
            Assert.Equal(0, rectangle.DoubleClickCount);
            Assert.Equal(2, manager.ShapeCount);
            Assert.Null(manager.CurrentGeometryType);
            Assert.Null(manager.CurrentGeometryInEdit);

            board.Press(rectangle.BottomRight, clickCount: 1);
            if (!locked) Assert.Same(rectangle, manager.SelectedGeometry);
            board.Press(rectangle.BottomRight, clickCount: 2);
            Assert.Equal(!locked, rectangle.IsLocked);
        });
    }

    [Fact]
    public void InProgressSketch_ReceivesClicksAndRightClickOverExistingShape()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var rectangle = new TrackingRectangle(manager.CurrentShapeLayer!);
            rectangle.FromData(new PointsData(1, new List<Point> { new(20, 20), new(100, 80) }));
            manager.AddShape(rectangle);
            manager.SetGeometryType(typeof(Polygon));
            board.Press(new Point(200, 180), clickCount: 1);
            board.Release();
            var polygon = Assert.IsType<Polygon>(manager.CurrentGeometryInEdit);
            Assert.False(polygon.IsGeometryRendered);
            manager.UnselectGeometryType();

            board.Press(rectangle.TopLeft, clickCount: 1);
            board.Release();
            Assert.Same(polygon, manager.SelectedGeometry);
            Assert.Equal(new Point(20, 20), polygon.GetMetaData().DataPoints[1]);
            Assert.Equal(0, rectangle.LeftDownCount);
            board.Finish(rectangle.TopLeft);

            Assert.True(polygon.IsGeometryRendered);
            Assert.Equal(0, rectangle.RightUpCount);
            Assert.Null(manager.CurrentGeometryInEdit);
            Assert.Null(manager.CurrentGeometryType);
        });
    }

    [Fact]
    public void SelectingDrawingTool_ClearsHoverFeedbackOnNextMouseMove()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var ruler = LoadRuler(manager);
            board.Move(ruler.Center, MouseButtonState.Released);
            Assert.Equal(ShapeVisualState.MouseOver, ruler.State);

            manager.SetGeometryType(typeof(Line));
            board.Move(ruler.Center, MouseButtonState.Released);

            Assert.Equal(ShapeVisualState.Normal, ruler.State);
            Assert.Null(manager.SelectedGeometry);
            Assert.Single(manager.Shapes);
        });
    }

    [Fact]
    public void RightClick_FinishesSketchAndClearsActiveDrawingTool()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var ruler = LoadRuler(manager);
            manager.SetGeometryType(typeof(Polygon));
            board.Press(new Point(200, 180), clickCount: 1);
            var polygon = Assert.IsType<Polygon>(manager.CurrentGeometryInEdit);

            board.Finish(ruler.Center);

            Assert.True(polygon.IsGeometryRendered);
            Assert.Null(manager.CurrentGeometryInEdit);
            Assert.Null(manager.CurrentGeometryType);
            board.Press(ruler.Center, clickCount: 2);
            Assert.True(ruler.IsLocked);
        });
    }

    private static TestBoard CreateBoard(out SketchBoardDataManager manager)
    {
        manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        var board = new TestBoard
        {
            Width = 320,
            Height = 240,
            Background = Brushes.Transparent,
            SketchBoardDataManager = manager
        };
        board.Measure(new Size(320, 240));
        board.Arrange(new Rect(0, 0, 320, 240));
        return board;
    }

    private static RulerCross LoadRuler(SketchBoardDataManager manager)
    {
        return Assert.IsType<RulerCross>(manager.LoadShape<RulerCross, RulerCrossData>(
            new RulerCrossData { Center = new Point(160, 120), Width = 320, Height = 240 }));
    }

    private sealed class TestBoard : SketchBoard
    {
        private Point _position;
        public void Press(Point point, int clickCount) { _position = point; HandleLeftButtonDown(point, clickCount); }
        public void Move(Point point, MouseButtonState buttonState) { _position = point; HandleMouseMove(point, buttonState); }
        public void Release() => HandleLeftButtonUp(_position);
        public void Finish(Point point) => HandleRightButtonUp(point);
    }

    private sealed class TrackingRectangle : Rectangle
    {
        public TrackingRectangle(ShapeLayer layer) : base(layer) { }
        public int MoveCount { get; private set; }
        public int DoubleClickCount { get; private set; }
        public int LeftDownCount { get; private set; }
        public int RightUpCount { get; private set; }
        public override void OnMouseMove(Point point, MouseButtonState buttonState) => MoveCount++;
        public override void OnMouseLeftButtonDoubleClick(Point point) => DoubleClickCount++;
        public override void OnMouseLeftButtonDown(Point point) { LeftDownCount++; base.OnMouseLeftButtonDown(point); }
        public override void OnMouseRightButtonUp(Point point) { RightUpCount++; base.OnMouseRightButtonUp(point); }
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
