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

public class CrossInteractionTests
{
    [Fact]
    public void DrawingCross_PlacesCenterAndSizesAxesThroughFinalRelease()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(typeof(Cross));
            CrossData? committed = null;
            manager.NewShapeSketched += (_, shape) => committed = ((Cross)shape).GetMetaData();
            board.Press(new Point(100, 80));
            var cross = Assert.IsType<Cross>(manager.CurrentGeometryInEdit);
            board.Move(new Point(130, 100));
            board.Release(new Point(140, 110));

            Assert.Equal(new Point(100, 80), cross.Center);
            Assert.Equal(80, cross.Width);
            Assert.Equal(60, cross.Height);
            Assert.True(cross.IsGeometryRendered);
            Assert.False(cross.ContentBounds.IsEmpty);
            Assert.NotNull(committed);
            Assert.Equal(cross.Center, committed.Center);
            Assert.Equal(cross.Width, committed.Width);
            Assert.Equal(cross.Height, committed.Height);
            Assert.Single(manager.Shapes);
            Assert.Null(manager.CurrentGeometryType);
        });
    }

    [Theory]
    [InlineData(140, 110)]
    [InlineData(60, 110)]
    [InlineData(140, 50)]
    [InlineData(60, 50)]
    public void DrawingCross_InAnyQuadrantKeepsCenterAndPositiveDimensions(double x, double y)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(typeof(Cross));
            board.Press(new Point(100, 80));
            board.Release(new Point(x, y));

            var cross = Assert.IsType<Cross>(Assert.Single(manager.Shapes));
            Assert.Equal(new Point(100, 80), cross.Center);
            Assert.Equal(80, cross.Width);
            Assert.Equal(60, cross.Height);
            Assert.Equal(new Rect(60, 50, 80, 60), cross.BoundsRect);
            Assert.True(cross.IsGeometryRendered);
            Assert.Null(manager.CurrentGeometryType);
        });
    }

    [Fact]
    public void ClickingCrossTool_PlacesVisibleCrossAndFinishesSketch()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(typeof(Cross));
            board.Press(new Point(100, 80));
            board.Release(new Point(100, 80));

            var cross = Assert.IsType<Cross>(Assert.Single(manager.Shapes));
            Assert.Equal(new Point(100, 80), cross.Center);
            Assert.True(cross.Width > 0);
            Assert.True(cross.Height > 0);
            Assert.True(cross.IsGeometryRendered);
            Assert.False(cross.ContentBounds.IsEmpty);
            Assert.Null(manager.CurrentGeometryType);
        });
    }

    [Theory]
    [InlineData(20, 80, 10, 80, 180, 120)]
    [InlineData(180, 80, 190, 80, 180, 120)]
    [InlineData(100, 20, 100, 10, 160, 140)]
    [InlineData(100, 140, 100, 150, 160, 140)]
    public void ResizingAxisEndpoint_KeepsCenterAndOtherAxis(
        double startX, double startY, double endX, double endY, int width, int height)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var cross = LoadCross(manager);
            board.Press(new Point(startX, startY));
            board.Release(new Point(endX, endY));

            Assert.Same(cross, manager.SelectedGeometry);
            Assert.Equal(new Point(100, 80), cross.Center);
            Assert.Equal(width, cross.Width);
            Assert.Equal(height, cross.Height);
            Assert.False(cross.IsBeingDraggedOrPanMoving);
        });
    }

    [Theory]
    [InlineData(100, 80)]
    [InlineData(60, 80)]
    [InlineData(100, 40)]
    public void DraggingCenterOrAxisBody_TranslatesWithoutResizing(double x, double y)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var cross = LoadCross(manager);
            var start = new Point(x, y);
            board.Press(start);
            board.Move(start + new Vector(5, 10));
            board.Release(start + new Vector(20, 25));

            Assert.Same(cross, manager.SelectedGeometry);
            Assert.Equal(new Point(120, 105), cross.Center);
            Assert.Equal(160, cross.Width);
            Assert.Equal(120, cross.Height);
            Assert.Equal(new Rect(40, 45, 160, 120), cross.BoundsRect);
            Assert.False(cross.IsBeingDraggedOrPanMoving);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Fact]
    public void LockedCross_DoesNotMoveOrResize()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var cross = LoadCross(manager);
            cross.Lock();
            foreach (var start in new[] { cross.Center, new Point(180, 80) })
            {
                board.Press(start);
                board.Move(start + new Vector(20, 25));
                board.Release(start + new Vector(20, 25));
            }

            Assert.Equal(new Point(100, 80), cross.Center);
            Assert.Equal(160, cross.Width);
            Assert.Equal(120, cross.Height);
            Assert.True(cross.IsLocked);
        });
    }

    [Fact]
    public void DrawingCross_SnapsCenterAndDragPointToExistingLockedLine()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var target = Assert.IsType<Line>(manager.LoadShape<Line, PointsData>(new PointsData(1,
                new List<Point> { new(20, 20), new(100, 80) })));
            target.Lock();
            manager.SetGeometryType(typeof(Cross));
            board.Press(new Point(22, 23));
            var cross = Assert.IsType<Cross>(manager.CurrentGeometryInEdit);
            board.Move(new Point(97, 82));

            Assert.Equal(new Point(20, 20), cross.Center);
            Assert.Equal(160, cross.Width);
            Assert.Equal(120, cross.Height);
            Assert.False(board.MarkerBounds.IsEmpty);
            board.Release(new Point(98, 83));

            Assert.True(cross.IsGeometryRendered);
            Assert.Null(manager.CurrentGeometryType);
            Assert.True(board.MarkerBounds.IsEmpty);
            Assert.True(target.IsLocked);
            Assert.Equal(new Point(20, 20), target.Start);
            Assert.Equal(new Point(100, 80), target.End);
        });
    }

    [Fact]
    public void LoadedCross_UsesSameAxesAndExcludesHandlesFromBounds()
    {
        RunOnSta(() =>
        {
            CreateBoard(out var manager);
            var cross = LoadCross(manager);
            cross.State = ShapeVisualState.Selected;
            Assert.Equal(new Rect(20, 20, 160, 120), cross.BoundsRect);
            Assert.True(cross.RenderGeometry.StrokeContains(new Pen(Brushes.Black, 1), new Point(100, 20)));
            Assert.True(cross.RenderGeometry.StrokeContains(new Pen(Brushes.Black, 1), new Point(180, 80)));

            cross.FromData(new CrossData { Center = new Point(150, 120), Width = 40, Height = 60, StrokeThickness = 1 });
            Assert.Equal(new Rect(130, 90, 40, 60), cross.BoundsRect);
            Assert.False(cross.RenderGeometry.StrokeContains(new Pen(Brushes.Black, 1), new Point(100, 20)));
            Assert.Equal(new Point(150, 120), cross.GetMetaData().Center);
        });
    }

    private static Cross LoadCross(SketchBoardDataManager manager)
        => Assert.IsType<Cross>(manager.LoadShape<Cross, CrossData>(new CrossData
        {
            Center = new Point(100, 80), Width = 160, Height = 120, StrokeThickness = 1
        }));

    private static TestBoard CreateBoard(out SketchBoardDataManager manager)
    {
        manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        var board = new TestBoard { Width = 320, Height = 240, Background = Brushes.Transparent, SketchBoardDataManager = manager };
        board.Measure(new Size(320, 240));
        board.Arrange(new Rect(0, 0, 320, 240));
        return board;
    }

    private sealed class TestBoard : SketchBoard
    {
        public Rect MarkerBounds => ((DrawingVisual)GetVisualChild(VisualChildrenCount - 1)).ContentBounds;
        public void Press(Point point) => HandleLeftButtonDown(point, 1);
        public void Move(Point point) => HandleMouseMove(point, MouseButtonState.Pressed);
        public void Release(Point point) => HandleLeftButtonUp(point);
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
