using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes.Custom;
using Lan.Shapes.Enums;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Lan.Shapes.Styler;
using Lan.SketchBoard;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeLifecycleTests
{
    [Fact]
    public void LoadShape_RoundTrips_Line()
    {
        var manager = CreateManagerWithHost();
        var data = new PointsData(1, new List<Point> { new(10, 20), new(40, 60) });

        var shape = Assert.IsType<Line>(manager.LoadShape<Line, PointsData>(data));
        var meta = shape.GetMetaData();

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(new Point(10, 20), shape.Start);
        Assert.Equal(new Point(40, 60), shape.End);
        Assert.Equal(2, meta.DataPoints.Count);
        Assert.Equal(new Point(10, 20), meta.DataPoints[0]);
        Assert.Equal(new Point(40, 60), meta.DataPoints[1]);
        Assert.Single(manager.Shapes);
    }

    [Fact]
    public void LoadShape_RoundTrips_Rectangle()
    {
        var manager = CreateManagerWithHost();
        var data = new PointsData(1, new List<Point> { new(5, 5), new(50, 40) });

        var shape = Assert.IsType<Rectangle>(manager.LoadShape<Rectangle, PointsData>(data));
        var meta = shape.GetMetaData();

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(new Point(5, 5), shape.TopLeft);
        Assert.Equal(new Point(50, 40), shape.BottomRight);
        Assert.Equal(new Point(5, 5), meta.DataPoints[0]);
        Assert.Equal(new Point(50, 40), meta.DataPoints[1]);
    }

    [Fact]
    public void LoadShape_RoundTrips_Circle()
    {
        var manager = CreateManagerWithHost();
        var data = new EllipseData
        {
            Center = new Point(100, 120),
            RadiusX = 30,
            RadiusY = 30,
            StrokeThickness = 1
        };

        var shape = Assert.IsType<Circle>(manager.LoadShape<Circle, EllipseData>(data));
        var meta = shape.GetMetaData();

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(new Point(100, 120), shape.Center);
        Assert.Equal(30, shape.Radius);
        Assert.Equal(new Point(100, 120), meta.Center);
        Assert.Equal(30, meta.RadiusX);
    }

    [Fact]
    public void LoadShape_RoundTrips_Ellipse()
    {
        var manager = CreateManagerWithHost();
        var data = new EllipseData
        {
            Center = new Point(15, 25),
            RadiusX = 12,
            RadiusY = 18,
            StrokeThickness = 1
        };

        var shape = Assert.IsType<Ellipse>(manager.LoadShape<Ellipse, EllipseData>(data));
        var meta = shape.GetMetaData();

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(new Point(15, 25), shape.Center);
        Assert.Equal(12, shape.RadiusX);
        Assert.Equal(18, shape.RadiusY);
        Assert.Equal(12, meta.RadiusX);
        Assert.Equal(18, meta.RadiusY);
    }

    [Fact]
    public void LoadShape_RoundTrips_Polygon()
    {
        var manager = CreateManagerWithHost();
        var points = new List<Point>
        {
            new(0, 0),
            new(40, 0),
            new(40, 30),
            new(0, 30)
        };
        var data = new PointsData(1, points);

        var shape = Assert.IsType<Polygon>(manager.LoadShape<Polygon, PointsData>(data));
        var meta = shape.GetMetaData();

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(4, meta.DataPoints.Count);
        Assert.Equal(points, meta.DataPoints);
    }

    [Fact]
    public void LoadShape_RoundTrips_Cross()
    {
        var manager = CreateManagerWithHost();
        var data = new CrossData
        {
            Center = new Point(80, 90),
            Width = 40,
            Height = 50,
            StrokeThickness = 2
        };

        var shape = Assert.IsType<Cross>(manager.LoadShape<Cross, CrossData>(data));
        var meta = shape.GetMetaData();

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(new Point(80, 90), shape.Center);
        Assert.Equal(40, shape.Width);
        Assert.Equal(50, shape.Height);
        Assert.Equal(new Point(80, 90), meta.Center);
        Assert.Equal(40, meta.Width);
        Assert.Equal(50, meta.Height);
    }

    [Fact]
    public void LoadShape_RoundTrips_GridGeometry()
    {
        var manager = CreateManagerWithHost();
        var data = new GridGeometryData
        {
            TopLeft = new Point(10, 20),
            BottomRight = new Point(110, 220),
            RowCount = 4,
            ColumnCount = 5,
            StrokeThickness = 2,
            Tag = "grid"
        };

        var shape = Assert.IsType<Lan.Shapes.DialogGeometry.GridGeometry>(
            manager.LoadShape<Lan.Shapes.DialogGeometry.GridGeometry, GridGeometryData>(data));
        var meta = shape.GetMetaData();

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(new Point(10, 20), shape.TopLeft);
        Assert.Equal(new Point(110, 220), shape.BottomRight);
        Assert.Equal(4, shape.RowCount);
        Assert.Equal(5, shape.ColumnCount);
        Assert.Equal("grid", meta.Tag);
        Assert.Equal(4, meta.RowCount);
        Assert.Equal(5, meta.ColumnCount);
        Assert.Equal(new Point(10, 20), meta.TopLeft);
        Assert.Equal(new Point(110, 220), meta.BottomRight);
    }

    [Fact]
    public void LoadShape_RoundTrips_TextGeometry()
    {
        var manager = CreateManagerWithHost();
        var data = new TextGeometryData(new Point(40, 50), "hello", 24)
        {
            StrokeThickness = 3
        };

        var shape = Assert.IsType<Lan.Shapes.Custom.TextGeometry>(
            manager.LoadShape<Lan.Shapes.Custom.TextGeometry, TextGeometryData>(data));
        var meta = shape.GetMetaData();

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(new Point(40, 50), meta.Location);
        Assert.Equal("hello", meta.Content);
        Assert.Equal(24, meta.FontSize);
        Assert.Equal(3, meta.StrokeThickness);
    }

    [Fact]
    public void LoadShape_RoundTrips_ThickenedCircle()
    {
        var manager = CreateManagerWithHost();
        var data = new EllipseData
        {
            Center = new Point(30, 40),
            RadiusX = 15,
            RadiusY = 15,
            StrokeThickness = 4
        };

        var shape = Assert.IsType<ThickenedCircle>(
            manager.LoadShape<ThickenedCircle, EllipseData>(data));
        var meta = shape.GetMetaData();

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(new Point(30, 40), meta.Center);
        Assert.Equal(15, meta.RadiusX);
        Assert.Equal(15, meta.RadiusY);
        Assert.Equal(4, meta.StrokeThickness);
    }

    [Fact]
    public void LoadShape_RoundTrips_ThickenedRectangle()
    {
        var manager = CreateManagerWithHost();
        var data = new PointsData(3, new List<Point> { new(5, 6), new(50, 60) });

        var shape = Assert.IsType<ThickenedRectangle>(
            manager.LoadShape<ThickenedRectangle, PointsData>(data));
        var meta = shape.GetMetaData();

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(2, meta.DataPoints.Count);
        Assert.Equal(new Point(5, 6), meta.DataPoints[0]);
        Assert.Equal(new Point(50, 60), meta.DataPoints[1]);
        Assert.Equal(3, meta.StrokeThickness);
    }

    [Fact]
    public void LoadShape_RoundTrips_ThickenedCross()
    {
        var manager = CreateManagerWithHost();
        var data = new PointsData(
            2,
            new List<Point>
            {
                new(40, 10), // VerticalTopLeft
                new(60, 90), // VerticalBottomRight
                new(20, 40), // HorizontalTopLeft
                new(80, 60)  // HorizontalBottomRight
            });

        var shape = Assert.IsType<ThickenedCross>(
            manager.LoadShape<ThickenedCross, PointsData>(data));
        var meta = shape.GetMetaData();

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(4, meta.DataPoints.Count);
        Assert.Equal(new Point(40, 10), meta.DataPoints[0]);
        Assert.Equal(new Point(60, 90), meta.DataPoints[1]);
        Assert.Equal(new Point(20, 40), meta.DataPoints[2]);
        Assert.Equal(new Point(80, 60), meta.DataPoints[3]);
        Assert.Equal(2, meta.StrokeThickness);

        // Reload from exported meta — contract must be stable.
        var reloaded = Assert.IsType<ThickenedCross>(
            manager.LoadShape<ThickenedCross, PointsData>(meta));
        var meta2 = reloaded.GetMetaData();
        Assert.Equal(meta.DataPoints, meta2.DataPoints);
    }

    [Fact]
    public void LoadShape_RoundTrips_FixedCenterCircle()
    {
        var manager = CreateManagerWithHost();
        var data = new EllipseData
        {
            Center = new Point(100, 120),
            RadiusX = 25,
            RadiusY = 25
        };

        var shape = Assert.IsType<FixedCenterCircle>(
            manager.LoadShape<FixedCenterCircle, EllipseData>(data));
        var meta = shape.GetMetaData();

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(new Point(100, 120), meta.Center);
        Assert.Equal(25, meta.RadiusX);
        Assert.Equal(25, meta.RadiusY);
    }
    [Fact]
    public void LoadShape_RoundTrips_Fiber()
    {
        var manager = CreateManagerWithHost();
        var data = new FiberData
        {
            FilletCenter = new Point(50, 50),
            FiberAngleInDeg = 0,
            FilletRadius = 5,
            Width = 40,
            Height = 80,
            EnableTranslation = true
        };

        var shape = Assert.IsType<Fiber>(
            manager.LoadShape<Fiber, FiberData>(data));
        var meta = shape.GetMetaData();

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(40, meta.Width, 3);
        Assert.Equal(80, meta.Height, 3);
        Assert.Equal(5, meta.FilletRadius, 3);
        Assert.InRange(meta.FilletCenter.X, 49, 51);
        Assert.InRange(meta.FilletCenter.Y, 49, 51);
    }

    [Fact]
    public void SelectedGeometry_DoesNotThrow_ForBasicShapes()
    {
        var manager = CreateManagerWithHost();
        var line = manager.LoadShape<Line, PointsData>(
            new PointsData(1, new List<Point> { new(0, 0), new(10, 10) }));
        var circle = manager.LoadShape<Circle, EllipseData>(
            new EllipseData { Center = new Point(20, 20), RadiusX = 5, RadiusY = 5 });
        var cross = manager.LoadShape<Cross, CrossData>(
            new CrossData { Center = new Point(1, 1), Width = 10, Height = 10, StrokeThickness = 1 });

        var exception = Record.Exception(() =>
        {
            manager.SelectedGeometry = line;
            Assert.Equal(ShapeVisualState.Selected, line.State);

            manager.SelectedGeometry = circle;
            Assert.Equal(ShapeVisualState.Normal, line.State);
            Assert.Equal(ShapeVisualState.Selected, circle.State);

            manager.SelectedGeometry = cross;
            Assert.Equal(ShapeVisualState.Normal, circle.State);
            Assert.Equal(ShapeVisualState.Selected, cross.State);

            manager.SelectedGeometry = null;
            Assert.Equal(ShapeVisualState.Normal, cross.State);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void CreateNewGeometry_RequiresGeometryType()
    {
        var manager = CreateManagerWithHost();

        Assert.Null(manager.CreateNewGeometry(new Point(1, 1)));
    }

    [Fact]
    public void CreateNewGeometry_RequiresShapeLayer()
    {
        var manager = new SketchBoardDataManager();
        manager.SetGeometryType(typeof(Line));
        manager.InitializeVisualCollection(new ContainerVisual());

        Assert.Null(manager.CreateNewGeometry(new Point(1, 1)));
    }

    [Fact]
    public void ShapeStylerFactory_DottedLineDoesNotCorruptSelected()
    {
        var factory = new ShapeStylerFactory();

        var selected = factory.ShapeSelectedVisualState();
        var dotted = factory.DottedLineStyler();
        var selectedAgain = factory.ShapeSelectedVisualState();

        Assert.NotSame(selected, dotted);
        Assert.Same(selected, selectedAgain);
        Assert.Equal(DashStyles.Dash, dotted.SketchPen.DashStyle);
        Assert.NotEqual(DashStyles.Dash, selected.SketchPen.DashStyle);
    }

    private static SketchBoardDataManager CreateManagerWithHost()
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        manager.InitializeVisualCollection(new ContainerVisual());
        return manager;
    }
}
