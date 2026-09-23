using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.ImageViewer.Prism;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class AngleInteractionTests
{
    [Fact]
    public void ThreeClicks_CommitOnceAndDisplayMeasuredAngle()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(typeof(Angle));
            var commits = 0;
            manager.NewShapeSketched += (_, _) => commits++;

            board.Press(new Point(100, 50));
            board.Release(new Point(100, 50));
            var angle = Assert.IsType<Angle>(manager.CurrentGeometryInEdit);
            Assert.False(angle.IsGeometryRendered);
            Assert.Equal(0, commits);

            board.Hover(new Point(100, 100));
            Assert.False(angle.ContentBounds.IsEmpty);
            board.Press(new Point(100, 100), 2);
            board.Release(new Point(100, 100));
            Assert.False(angle.IsGeometryRendered);
            Assert.Equal(0, commits);

            board.Hover(new Point(150, 100));
            board.Press(new Point(150, 100));
            board.Release(new Point(150, 100));

            Assert.True(angle.IsGeometryRendered);
            Assert.Equal(90, angle.AngleDegrees, 6);
            Assert.Equal(1, commits);
            Assert.Null(manager.CurrentGeometryType);
            Assert.Single(manager.Shapes);
            Assert.Equal(new[] { new Point(100, 50), new Point(100, 100), new Point(150, 100) },
                angle.GetMetaData().DataPoints);
        });
    }

    [Fact]
    public void DraggingVerticesAndEdges_UpdatesPointsAndAngle()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var angle = Assert.IsType<Angle>(manager.LoadShape<Angle, PointsData>(new PointsData(1, new List<Point>
            {
                new(100, 50), new(100, 100), new(150, 100)
            })));

            board.Press(new Point(100, 50));
            board.Release(new Point(100, 60));
            Assert.Equal(new Point(100, 60), angle.FirstPoint);

            board.Press(new Point(100, 100));
            board.Release(new Point(110, 100));
            Assert.Equal(new Point(110, 100), angle.Vertex);
            Assert.NotEqual(90, angle.AngleDegrees);

            board.Press(new Point(130, 100));
            board.Release(new Point(140, 110));
            Assert.Equal(new Point(110, 70), angle.FirstPoint);
            Assert.Equal(new Point(120, 110), angle.Vertex);
            Assert.Equal(new Point(160, 110), angle.SecondPoint);
        });
    }

    [Fact]
    public void CancelIncompleteAngle_RemovesItAndClearsTool()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(typeof(Angle));
            board.Press(new Point(10, 10));
            board.Release(new Point(10, 10));
            board.RightClick(new Point(20, 20));

            Assert.Empty(manager.Shapes);
            Assert.Null(manager.CurrentGeometryInEdit);
            Assert.Null(manager.CurrentGeometryType);
        });
    }

    [Fact]
    public void AngleDegrees_UsesTheSmallerAngleForObtuseRays()
    {
        RunOnSta(() =>
        {
            CreateBoard(out var manager);
            var angle = Assert.IsType<Angle>(manager.LoadShape<Angle, PointsData>(
                new PointsData(1, new List<Point>
                {
                    new(100, 50), new(100, 100), new(50, 150)
                })));

            Assert.Equal(135, angle.AngleDegrees, 6);
            Assert.Equal(3, angle.GetMetaData().DataPoints.Count);
        });
    }

    [Fact]
    public void Catalog_ContainsAngle()
    {
        Assert.Equal(typeof(Angle), GeometryTypeRegistration.Catalog[nameof(Angle)]);
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

    private sealed class TestBoard : SketchBoard
    {
        public void Press(Point point, int clickCount = 1) => HandleLeftButtonDown(point, clickCount);
        public void Release(Point point) => HandleLeftButtonUp(point);
        public void Hover(Point point) => HandleMouseMove(point, MouseButtonState.Released);
        public void RightClick(Point point) => HandleRightButtonUp(point);
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
