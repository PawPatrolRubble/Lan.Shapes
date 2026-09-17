using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lan.Shapes.Enums;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class RulerCrossTests
{
    [Fact]
    public void Creation_UsesImagePixelCenterAndCommitsOnMouseUp()
    {
        RunOnSta(() =>
        {
            var manager = new SketchBoardDataManager();
            manager.SetShapeLayer(TestShapeLayer.Create());
            var board = new SketchBoard
            {
                Width = 800,
                Height = 600,
                Image = BitmapSource.Create(320, 240, 192, 192, PixelFormats.Gray8,
                    null, new byte[320 * 240], 320),
                SketchBoardDataManager = manager
            };
            board.Measure(new Size(800, 600));
            board.Arrange(new Rect(0, 0, 800, 600));
            manager.SetGeometryType(typeof(RulerCross));

            var shape = Assert.IsType<RulerCross>(manager.CreateNewGeometry(new Point(12, 25)));
            shape.OnMouseLeftButtonDown(new Point(12, 25));
            shape.OnMouseMove(new Point(25, 35), MouseButtonState.Pressed);

            Assert.Equal(new Point(160, 120), shape.Center);
            Assert.Equal(new Rect(0, 0, 320, 240), shape.BoundsRect);
            Assert.False(shape.IsGeometryRendered);
            shape.OnMouseLeftButtonUp(new Point(25, 35));
            Assert.True(shape.IsGeometryRendered);
            Assert.Same(shape, Assert.Single(manager.Shapes));
            AssertAxes(shape, new Point(0, 120), new Point(320, 120),
                new Point(160, 0), new Point(160, 240));
        });
    }

    [Fact]
    public void DraggingEitherAxisOrOrigin_MovesIntersectionAndKeepsImageExtents()
    {
        foreach (var start in new[] { new Point(100, 120), new Point(160, 60), new Point(160, 120) })
        {
            var shape = CreateRuler();
            shape.State = ShapeVisualState.Selected;

            shape.OnMouseLeftButtonDown(start);
            shape.OnMouseMove(start + new Vector(10, 15), MouseButtonState.Pressed);
            shape.OnMouseMove(start + new Vector(20, 25), MouseButtonState.Pressed);
            shape.OnMouseLeftButtonUp(start + new Vector(20, 25));

            Assert.Equal(new Point(180, 145), shape.Center);
            AssertAxes(shape, new Point(0, 145), new Point(320, 145),
                new Point(180, 0), new Point(180, 240));
            Assert.Equal(new Point(0, 0), shape.GetRulerCoordinates(shape.Center));
            Assert.False(shape.IsBeingDraggedOrPanMoving);
        }
    }

    [Fact]
    public void Dragging_ClampsOriginToImageAndHonorsLock()
    {
        var shape = CreateRuler();
        shape.Lock();
        shape.OnMouseLeftButtonDown(shape.Center);
        shape.OnMouseMove(new Point(-500, 900), MouseButtonState.Pressed);
        shape.OnMouseLeftButtonUp(new Point(-500, 900));
        Assert.Equal(new Point(160, 120), shape.Center);

        shape.UnLock();
        shape.OnMouseLeftButtonDown(shape.Center);
        shape.OnMouseMove(new Point(-500, 900), MouseButtonState.Pressed);
        shape.OnMouseLeftButtonUp(new Point(-500, 900));
        Assert.Equal(new Point(0, 240), shape.Center);
    }

    [Fact]
    public void CoordinatesAndTicks_UseCalibrationAndFollowMovedOrigin()
    {
        var shape = CreateRuler();
        shape.ShapeLayer.Measurement.PixelPerUnit = 2000;
        shape.ShapeLayer.Measurement.UnitsPerMillimeter = 1000;
        shape.ShapeLayer.Measurement.UnitName = "um";
        shape.Center = new Point(155, 115);

        Assert.Equal(new Point(-10, 15), shape.GetRulerCoordinates(new Point(135, 145)));
        Assert.Equal(50, shape.MajorTickInterval);
        var group = Assert.IsType<GeometryGroup>(shape.RenderGeometry);
        var ticks = Assert.IsType<StreamGeometry>(group.Children[2]);
        var pen = new Pen(Brushes.Black, 0.1);
        Assert.True(ticks.StrokeContains(pen, new Point(55, 110)));
        Assert.True(ticks.StrokeContains(pen, new Point(255, 110)));
        Assert.True(ticks.StrokeContains(pen, new Point(150, 15)));
        Assert.True(ticks.StrokeContains(pen, new Point(75, 112.5)));
        Assert.False(ticks.StrokeContains(pen, new Point(75, 110)));
        Assert.True(ticks.StrokeContains(pen, new Point(65, 112.5)));
        Assert.True(ticks.StrokeContains(pen, new Point(152.5, 25)));
        Assert.True(ticks.StrokeContains(pen, new Point(105, 111.25)));
        Assert.False(ticks.StrokeContains(pen, new Point(105, 110)));
    }

    [Fact]
    public void Zoom_ChangesTickSpacingAndLengthWithoutMovingOrigin()
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        var shape = Assert.IsType<RulerCross>(manager.LoadShape<RulerCross, RulerCrossData>(
            new RulerCrossData { Center = new Point(160, 120), Width = 320, Height = 240 }));
        Assert.Equal(100, shape.MajorTickInterval);

        manager.OnImageViewerPropertyChanged(2);

        Assert.Equal(50, shape.MajorTickInterval);
        Assert.Equal(new Point(160, 120), shape.Center);
        var group = Assert.IsType<GeometryGroup>(shape.RenderGeometry);
        var ticks = Assert.IsType<StreamGeometry>(group.Children[2]);
        var pen = new Pen(Brushes.Black, 0.1);
        Assert.True(ticks.StrokeContains(pen, new Point(60, 117.5)));
        Assert.False(ticks.StrokeContains(pen, new Point(60, 115)));
    }

    [Fact]
    public void Metadata_RoundTripsMovedOriginAndDimensions()
    {
        var shape = CreateRuler();
        shape.Center = new Point(75, 190);
        var data = shape.GetMetaData();
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());

        var restored = Assert.IsType<RulerCross>(manager.LoadShape<RulerCross, RulerCrossData>(data));
        Assert.Equal(shape.Center, restored.Center);
        Assert.Equal(shape.BoundsRect, restored.BoundsRect);
        Assert.True(restored.IsGeometryRendered);
        restored.OnBoardContextAvailable(640, 480);
        Assert.Equal(new Point(75, 190), restored.Center);
        Assert.Equal(320, restored.Width);
    }

    private static RulerCross CreateRuler()
    {
        var shape = new RulerCross(TestShapeLayer.Create());
        shape.OnBoardContextAvailable(320, 240);
        shape.OnMouseLeftButtonUp(shape.Center);
        return shape;
    }

    private static void AssertAxes(RulerCross shape, Point horizontalStart, Point horizontalEnd,
        Point verticalStart, Point verticalEnd)
    {
        var group = Assert.IsType<GeometryGroup>(shape.RenderGeometry);
        var axes = group.Children.OfType<LineGeometry>().ToArray();
        Assert.Equal(2, axes.Length);
        Assert.Equal(horizontalStart, axes[0].StartPoint);
        Assert.Equal(horizontalEnd, axes[0].EndPoint);
        Assert.Equal(verticalStart, axes[1].StartPoint);
        Assert.Equal(verticalEnd, axes[1].EndPoint);
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
        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
