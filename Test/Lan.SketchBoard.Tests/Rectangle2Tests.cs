using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Handle;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Lan.Shapes.Utilities;
using Lan.SketchBoard;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class Rectangle2Tests
{
    [Fact]
    public void Rectangle2Math_CreatesExpectedAxisAlignedCorners()
    {
        var corners = Rectangle2Math.GetCorners(new Point(20, 30), 0, 10, 5);

        Assert.Equal(new Point(10, 25), corners[0]);
        Assert.Equal(new Point(30, 25), corners[1]);
        Assert.Equal(new Point(30, 35), corners[2]);
        Assert.Equal(new Point(10, 35), corners[3]);
    }

    [Fact]
    public void LoadShape_RoundTripsRectangle2Parameters()
    {
        var manager = CreateManager();
        var data = new Rectangle2Data
        {
            Row = 60,
            Column = 80,
            Phi = 0.35,
            Length1 = 25,
            Length2 = 12,
            StrokeThickness = 3,
            Tag = "roi",
            TagPosition = TagPosition.Top
        };

        var shape = Assert.IsType<Rectangle2>(manager.LoadShape<Rectangle2, Rectangle2Data>(data));
        var result = shape.GetMetaData();

        Assert.Equal(data.Row, result.Row, 10);
        Assert.Equal(data.Column, result.Column, 10);
        Assert.Equal(data.Phi, result.Phi, 10);
        Assert.Equal(data.Length1, result.Length1, 10);
        Assert.Equal(data.Length2, result.Length2, 10);
        Assert.Equal(data.StrokeThickness, result.StrokeThickness, 10);
        Assert.Equal(data.Tag, result.Tag);
        Assert.Equal(data.TagPosition, result.TagPosition);
    }

    [Fact]
    public void Rectangle2_CreationUsesTwoPointDrag()
    {
        var shape = new Rectangle2(TestShapeLayer.Create());

        shape.OnMouseLeftButtonDown(new Point(10, 20));
        shape.OnMouseMove(new Point(50, 80), MouseButtonState.Pressed);
        shape.OnMouseLeftButtonUp(new Point(50, 80));

        Assert.True(shape.IsGeometryRendered);
        Assert.Equal(new Point(30, 50), shape.Center);
        Assert.Equal(20, shape.Length1, 8);
        Assert.Equal(30, shape.Length2, 8);
        Assert.Equal(0, shape.Phi, 8);
    }

    [Fact]
    public void Rectangle2_TranslationContinuesAfterLeavingMouseDownPoint()
    {
        var shape = new ProbeRectangle2(TestShapeLayer.Create());
        shape.FromData(new Rectangle2Data
        {
            Row = 100,
            Column = 100,
            Phi = 0,
            Length1 = 30,
            Length2 = 30
        });
        shape.State = ShapeVisualState.Selected;

        shape.OnMouseLeftButtonDown(shape.Center);
        shape.OnMouseMove(new Point(150, 150), MouseButtonState.Pressed);
        shape.OnMouseMove(new Point(200, 200), MouseButtonState.Pressed);
        shape.OnMouseLeftButtonUp(new Point(200, 200));

        Assert.Equal(new Point(200, 200), shape.Center);
    }

    [Fact]
    public void Rectangle2_CornerResizeUsesLocalAxesAndKeepsOppositeCornerFixed()
    {
        var shape = new ProbeRectangle2(TestShapeLayer.Create());
        shape.FromData(new Rectangle2Data
        {
            Row = 60,
            Column = 80,
            Phi = 0.4,
            Length1 = 20,
            Length2 = 10
        });
        shape.State = ShapeVisualState.Selected;

        var topLeft = shape.Handle(1).GeometryCenter;
        var fixedCorner = shape.Handle(5).GeometryCenter;
        var target = Rectangle2Math.FromLocal(shape.Center, shape.Phi, -30, -15);

        shape.OnMouseLeftButtonDown(topLeft);
        shape.OnMouseMove(target, MouseButtonState.Pressed);
        shape.OnMouseLeftButtonUp(target);

        Assert.Equal(25, shape.Length1, 8);
        Assert.Equal(12.5, shape.Length2, 8);
        Assert.Equal(fixedCorner.X, shape.Handle(5).GeometryCenter.X, 8);
        Assert.Equal(fixedCorner.Y, shape.Handle(5).GeometryCenter.Y, 8);
        Assert.Equal(0.4, shape.Phi, 8);
    }

    [Fact]
    public void Rectangle2_InwardCornerResizeCanApproachOppositeCorner()
    {
        var shape = new ProbeRectangle2(TestShapeLayer.Create());
        shape.FromData(new Rectangle2Data
        {
            Row = 60,
            Column = 80,
            Phi = 0.4,
            Length1 = 20,
            Length2 = 10
        });
        shape.State = ShapeVisualState.Selected;

        var topLeft = shape.Handle(1).GeometryCenter;
        var fixedCorner = shape.Handle(5).GeometryCenter;
        var target = Rectangle2Math.FromLocal(shape.Center, shape.Phi, 20, 10);

        shape.OnMouseLeftButtonDown(topLeft);
        shape.OnMouseMove(target, MouseButtonState.Pressed);
        shape.OnMouseLeftButtonUp(target);

        Assert.InRange(shape.Length1, Rectangle2Math.MinimumHalfLength, 0.001);
        Assert.InRange(shape.Length2, Rectangle2Math.MinimumHalfLength, 0.001);
        Assert.Equal(fixedCorner.X, shape.Handle(5).GeometryCenter.X, 8);
        Assert.Equal(fixedCorner.Y, shape.Handle(5).GeometryCenter.Y, 8);
    }

    [Fact]
    public void Rectangle2_RotationKeepsCenterAndLengths()
    {
        var shape = new ProbeRectangle2(TestShapeLayer.Create());
        shape.FromData(new Rectangle2Data
        {
            Row = 60,
            Column = 80,
            Phi = 0.2,
            Length1 = 20,
            Length2 = 10
        });
        shape.State = ShapeVisualState.Selected;

        var center = shape.Center;
        var rotationGrip = shape.Handle(9).GeometryCenter;
        var startAngle = Math.Atan2(rotationGrip.Y - center.Y, rotationGrip.X - center.X);
        var targetAngle = startAngle + Math.PI / 4;
        var radius = (rotationGrip - center).Length;
        var target = center + new Vector(Math.Cos(targetAngle), Math.Sin(targetAngle)) * radius;

        shape.OnMouseLeftButtonDown(rotationGrip);
        shape.OnMouseMove(target, MouseButtonState.Pressed);
        shape.OnMouseLeftButtonUp(target);

        Assert.Equal(center, shape.Center);
        Assert.Equal(20, shape.Length1, 8);
        Assert.Equal(10, shape.Length2, 8);
        Assert.Equal(Rectangle2Math.NormalizePhi(0.2 + Math.PI / 4), shape.Phi, 8);
    }

    [Fact]
    public void Rectangle2_RotationCrossingCanonicalBoundaryKeepsGripContinuous()
    {
        var shape = new ProbeRectangle2(TestShapeLayer.Create());
        var startPhi = Math.PI / 2 - 0.01;
        shape.FromData(new Rectangle2Data
        {
            Row = 60,
            Column = 80,
            Phi = startPhi,
            Length1 = 20,
            Length2 = 10
        });
        shape.State = ShapeVisualState.Selected;

        var center = shape.Center;
        var rotationGrip = shape.Handle(9).GeometryCenter;
        var startAngle = Math.Atan2(rotationGrip.Y - center.Y, rotationGrip.X - center.X);
        var targetAngle = startAngle + 0.02;
        var target = center + new Vector(Math.Cos(targetAngle), Math.Sin(targetAngle)) *
            (rotationGrip - center).Length;

        shape.OnMouseLeftButtonDown(rotationGrip);
        shape.OnMouseMove(target, MouseButtonState.Pressed);
        shape.OnMouseLeftButtonUp(target);

        var expectedPhi = startPhi + 0.02;
        Assert.Equal(expectedPhi, shape.Phi, 8);
        Assert.Equal(
            Rectangle2Math.NormalizePhi(expectedPhi),
            shape.GetMetaData().Phi,
            8);

        var expectedGrip = Rectangle2Math.FromLocal(
            center, expectedPhi, 0, -shape.Length2 - 24);
        Assert.Equal(expectedGrip.X, shape.Handle(9).GeometryCenter.X, 8);
        Assert.Equal(expectedGrip.Y, shape.Handle(9).GeometryCenter.Y, 8);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(4.0)]
    public void Rectangle2_RotationGapAndHandlesRemainConstantOnScreen(double scale)
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.CreateWithThickness(stroke: 1, handle: 10));
        var shape = new ProbeRectangle2(manager.CurrentShapeLayer!);
        shape.FromData(new Rectangle2Data
        {
            Row = 100,
            Column = 100,
            Phi = 0,
            Length1 = 30,
            Length2 = 15
        });
        manager.AddShape(shape);
        shape.State = ShapeVisualState.Selected;

        manager.OnImageViewerPropertyChanged(scale);

        var topMiddle = shape.Handle(2).GeometryCenter;
        var rotation = shape.Handle(9).GeometryCenter;
        Assert.Equal(24, (rotation - topMiddle).Length * scale, 8);
        Assert.Equal(10, shape.Handle(1).HandleSize.Width * scale, 8);
    }

    [Fact]
    public void Rectangle2_AddedAfterZoomUsesCurrentViewportScale()
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.CreateWithThickness(stroke: 1, handle: 10));
        manager.OnImageViewerPropertyChanged(4.0);

        var shape = new ProbeRectangle2(manager.CurrentShapeLayer!);
        shape.FromData(new Rectangle2Data
        {
            Row = 100,
            Column = 100,
            Phi = 0,
            Length1 = 30,
            Length2 = 15
        });
        manager.AddShape(shape);

        var topMiddle = shape.Handle(2).GeometryCenter;
        var rotation = shape.Handle(9).GeometryCenter;
        Assert.Equal(24, (rotation - topMiddle).Length * 4.0, 8);
    }

    [Fact]
    public void Rectangle2_ParameterlessScaleRefreshPreservesKnownScale()
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.CreateWithThickness(stroke: 1, handle: 10));
        var shape = new ProbeRectangle2(manager.CurrentShapeLayer!);
        shape.FromData(new Rectangle2Data
        {
            Row = 100,
            Column = 100,
            Phi = 0,
            Length1 = 30,
            Length2 = 15
        });
        manager.AddShape(shape);
        manager.OnImageViewerPropertyChanged(4.0);

        shape.RefreshScaleDependentVisuals();

        var topMiddle = shape.Handle(2).GeometryCenter;
        var rotation = shape.Handle(9).GeometryCenter;
        Assert.Equal(24, (rotation - topMiddle).Length * 4.0, 8);
    }

    [Fact]
    public void Rectangle2_StrokeMetadataDoesNotMutateSharedLayerStyler()
    {
        var manager = CreateManager();
        var shape = Assert.IsType<Rectangle2>(manager.LoadShape<Rectangle2, Rectangle2Data>(
            new Rectangle2Data
            {
                Row = 10,
                Column = 10,
                Phi = 0,
                Length1 = 5,
                Length2 = 3,
                StrokeThickness = 3
            }));

        Assert.Equal(1, manager.CurrentShapeLayer!.Stylers[ShapeVisualState.Normal].SketchPen.Thickness);
        Assert.Equal(3, shape.GetMetaData().StrokeThickness);

        manager.OnImageViewerPropertyChanged(4.0);
        Assert.Equal(3, shape.GetMetaData().StrokeThickness);
    }

    [Fact]
    public void Rectangle2_RejectsNegativeLengths()
    {
        var shape = new Rectangle2(TestShapeLayer.Create());

        Assert.Throws<ArgumentOutOfRangeException>(() => shape.FromData(new Rectangle2Data
        {
            Row = 0,
            Column = 0,
            Phi = 0,
            Length1 = -1,
            Length2 = 1
        }));
    }

    private static SketchBoardDataManager CreateManager()
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        manager.InitializeVisualCollection(new ContainerVisual());
        return manager;
    }

    private sealed class ProbeRectangle2 : Rectangle2
    {
        public ProbeRectangle2(ShapeLayer layer) : base(layer)
        {
        }

        public DragHandle Handle(int id)
        {
            return Handles.Single(handle => handle.Id == id);
        }
    }
}
