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

public class OverlapSelectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClickingCircleOutlineInsideRectangle_SelectsCircleRegardlessOfDrawingOrder(bool rectangleOnTop)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var circle = CreateCircle(manager);
            var rectangle = CreateRectangle(manager);
            manager.AddShape(rectangleOnTop ? circle : rectangle);
            manager.AddShape(rectangleOnTop ? rectangle : circle);

            board.Click(new Point(160, 90));

            Assert.Same(circle, manager.SelectedGeometry);
        });
    }

    [Fact]
    public void TransformedCircleOutline_RemainsSelectableUnderRectangle()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var circle = CreateCircle(manager);
            circle.Transform = new TranslateTransform(20, 10);
            manager.AddShape(circle);
            manager.AddShape(CreateRectangle(manager));

            board.Click(new Point(180, 100));

            Assert.Same(circle, manager.SelectedGeometry);
        });
    }

    [Fact]
    public void HoveringAndClickingCircleOutline_OverridesSelectedRectangleInterior()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var circle = CreateCircle(manager);
            var rectangle = CreateRectangle(manager);
            manager.AddShape(circle);
            manager.AddShape(rectangle);
            manager.SelectedGeometry = rectangle;
            var point = new Point(160, 90);

            board.Move(point, MouseButtonState.Released);
            Assert.Equal(ShapeVisualState.MouseOver, circle.State);
            Assert.Equal(ShapeVisualState.Selected, rectangle.State);
            board.Click(point);

            Assert.Same(circle, manager.SelectedGeometry);
            Assert.Equal(ShapeVisualState.Normal, rectangle.State);
        });
    }

    [Fact]
    public void ClickingInteriors_KeepsTopmostSelection_AndAltClickReachesCircleUnderneath()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var circle = CreateCircle(manager);
            var rectangle = CreateRectangle(manager);
            manager.AddShape(circle);
            manager.AddShape(rectangle);
            var point = new Point(170, 110);

            board.Click(point);
            Assert.Same(rectangle, manager.SelectedGeometry);
            board.Click(point, ModifierKeys.Alt);
            Assert.Same(circle, manager.SelectedGeometry);
            board.Click(point, ModifierKeys.Alt);
            Assert.Same(rectangle, manager.SelectedGeometry);
        });
    }

    [Fact]
    public void AltClick_CyclesCoincidentOutlinesAndWrapsInVisualOrder()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var bottom = CreateCircle(manager);
            var middle = CreateCircle(manager);
            var top = CreateCircle(manager);
            manager.AddShape(bottom);
            manager.AddShape(middle);
            manager.AddShape(top);
            var point = new Point(160, 90);

            board.Click(point, ModifierKeys.Alt);
            Assert.Same(top, manager.SelectedGeometry);
            board.Click(point, ModifierKeys.Alt);
            Assert.Same(middle, manager.SelectedGeometry);
            board.Click(point, ModifierKeys.Alt);
            Assert.Same(bottom, manager.SelectedGeometry);
            board.Click(point, ModifierKeys.Alt);
            Assert.Same(top, manager.SelectedGeometry);
        });
    }

    [Fact]
    public void AltClick_SkipsLockedShapesAndDoesNotDragOrToggleLocks()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var circle = CreateCircle(manager);
            var rectangle = CreateRectangle(manager);
            manager.AddShape(circle);
            manager.AddShape(rectangle);
            rectangle.Lock();
            var point = new Point(160, 90);

            board.Press(point, ModifierKeys.Alt, clickCount: 2);
            board.Move(new Point(200, 150), MouseButtonState.Pressed);
            board.Release(new Point(200, 150));

            Assert.Same(circle, manager.SelectedGeometry);
            Assert.Equal(new Point(160, 120), circle.Center);
            Assert.Equal(30, circle.Radius);
            Assert.False(circle.IsLocked);
            Assert.True(rectangle.IsLocked);
            board.Click(point, ModifierKeys.Alt);
            Assert.Same(circle, manager.SelectedGeometry);
        });
    }

    [Fact]
    public void AltClick_EmptyCanvasClearsSelectionWithoutCreatingGeometry()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var circle = CreateCircle(manager);
            manager.AddShape(circle);
            manager.SelectedGeometry = circle;

            board.Click(new Point(310, 230), ModifierKeys.Alt);

            Assert.Null(manager.SelectedGeometry);
            Assert.Null(manager.CurrentGeometryInEdit);
            Assert.Single(manager.Shapes);
        });
    }

    [Fact]
    public void ActiveHandleDetectionRegion_RemainsReachableOverAnotherOutline()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var circle = CreateCircle(manager);
            var rectangle = new Rectangle(manager.CurrentShapeLayer!);
            rectangle.FromData(new PointsData(1, new List<Point> { new(197, 20), new(300, 220) }));
            manager.AddShape(circle);
            manager.AddShape(rectangle);
            manager.SelectedGeometry = circle;
            var point = new Point(197, 120);
            Assert.True(circle.HasDragHandleAt(point));

            board.Press(point);
            board.Move(new Point(207, 120), MouseButtonState.Pressed);
            board.Release(new Point(207, 120));

            Assert.Same(circle, manager.SelectedGeometry);
            Assert.Equal(40, circle.Radius);
            Assert.Equal(new Point(197, 20), rectangle.TopLeft);
        });
    }

    [Fact]
    public void ThickenedCircleStroke_UsesActualStrokeWidthUnderRectangle()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var circle = new ThickenedCircle(manager.CurrentShapeLayer!);
            circle.FromData(new EllipseData { Center = new Point(160, 120), RadiusX = 30, StrokeThickness = 20 });
            manager.AddShape(circle);
            manager.AddShape(CreateRectangle(manager));

            board.Click(new Point(160, 84));

            Assert.Same(circle, manager.SelectedGeometry);
        });
    }

    [Fact]
    public void DoubleClick_CircleUnderRectangleTogglesCircleLock()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var circle = CreateCircle(manager);
            var rectangle = CreateRectangle(manager);
            manager.AddShape(circle);
            manager.AddShape(rectangle);
            var point = new Point(160, 90);

            board.Click(point, clickCount: 2);
            Assert.True(circle.IsLocked);
            Assert.False(rectangle.IsLocked);
            board.Click(point, clickCount: 2);
            Assert.False(circle.IsLocked);
            Assert.Same(circle, manager.SelectedGeometry);
        });
    }

    [Fact]
    public void AltClick_WithActiveDrawingToolContinuesDrawingInsteadOfCyclingSelection()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var circle = CreateCircle(manager);
            manager.AddShape(circle);
            manager.AddShape(CreateRectangle(manager));
            manager.SetGeometryType(typeof(Line));
            var start = new Point(160, 90);
            var end = new Point(230, 170);

            board.Press(start, ModifierKeys.Alt);
            board.Move(end, MouseButtonState.Pressed);
            board.Release(end);

            var line = Assert.IsType<Line>(manager.Shapes[2]);
            Assert.Equal(start, line.Start);
            Assert.Equal(end, line.End);
            Assert.True(line.IsGeometryRendered);
            Assert.Null(manager.CurrentGeometryType);
            Assert.False(circle.IsLocked);
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
            SketchBoardDataManager = manager,
            IsSnappingEnabled = false
        };
        board.Measure(new Size(320, 240));
        board.Arrange(new Rect(0, 0, 320, 240));
        return board;
    }

    private static Circle CreateCircle(SketchBoardDataManager manager)
    {
        var circle = new Circle(manager.CurrentShapeLayer!);
        circle.FromData(new EllipseData { Center = new Point(160, 120), RadiusX = 30 });
        return circle;
    }

    private static Rectangle CreateRectangle(SketchBoardDataManager manager)
    {
        var rectangle = new Rectangle(manager.CurrentShapeLayer!);
        rectangle.FromData(new PointsData(1, new List<Point> { new(20, 20), new(300, 220) }));
        return rectangle;
    }

    private sealed class TestBoard : SketchBoard
    {
        public void Press(Point position, ModifierKeys modifiers = ModifierKeys.None, int clickCount = 1)
            => HandleLeftButtonDown(position, clickCount, modifiers);

        public void Move(Point position, MouseButtonState state) => HandleMouseMove(position, state);

        public void Release(Point position) => HandleLeftButtonUp(position);

        public void Click(Point position, ModifierKeys modifiers = ModifierKeys.None, int clickCount = 1)
        {
            Press(position, modifiers, clickCount);
            Release(position);
        }
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
