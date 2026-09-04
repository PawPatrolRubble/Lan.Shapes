using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Models;
using Lan.Shapes.Scaling;
using Lan.Shapes.Shapes;
using Lan.Shapes.Styler;
using Lan.SketchBoard;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ViewportScalingTests
{
    [Fact]
    public void CalculateStrokeThickness_AtScaleTwo_HalvesBase()
    {
        var options = new ViewportScalingOptions(baseStrokeThickness: 2.0, baseDragHandleSize: 16.0);

        Assert.Equal(1.0, ViewportScalingService.CalculateStrokeThickness(2.0, options));
        Assert.Equal(8.0, ViewportScalingService.CalculateDragHandleSize(2.0, options));
    }

    [Fact]
    public void CalculateStrokeThickness_StaticDefaults_AtScaleTwo_HalvesBase()
    {
        Assert.Equal(
            ViewportScalingService.BaseStrokeThickness / 2.0,
            ViewportScalingService.CalculateStrokeThickness(2.0));
        Assert.Equal(
            ViewportScalingService.BaseDragHandleSize / 2.0,
            ViewportScalingService.CalculateDragHandleSize(2.0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NegativeInfinity)]
    public void CalculateStrokeThickness_NonPositiveScale_FallsBackToBase(double scale)
    {
        var options = new ViewportScalingOptions(baseStrokeThickness: 3.0, baseDragHandleSize: 12.0);

        Assert.Equal(3.0, ViewportScalingService.CalculateStrokeThickness(scale, options));
        Assert.Equal(12.0, ViewportScalingService.CalculateDragHandleSize(scale, options));
        Assert.Equal(ViewportScalingService.BaseStrokeThickness, ViewportScalingService.CalculateStrokeThickness(scale));
        Assert.Equal(ViewportScalingService.BaseDragHandleSize, ViewportScalingService.CalculateDragHandleSize(scale));
    }

    [Fact]
    public void TwoManagers_WithDifferentOptions_DoNotClobberEachOther()
    {
        var optionsA = new ViewportScalingOptions(baseStrokeThickness: 2.0, baseDragHandleSize: 10.0);
        var optionsB = new ViewportScalingOptions(baseStrokeThickness: 4.0, baseDragHandleSize: 20.0);

        var managerA = new SketchBoardDataManager(optionsA);
        var managerB = new SketchBoardDataManager(optionsB);

        var layerA = TestShapeLayer.CreateWithThickness(stroke: 1, handle: 8);
        var layerB = TestShapeLayer.CreateWithThickness(stroke: 1, handle: 8);

        managerA.SetShapeLayer(layerA);
        managerB.SetShapeLayer(layerB);

        managerA.OnImageViewerPropertyChanged(2.0);
        managerB.OnImageViewerPropertyChanged(2.0);

        var stylerA = managerA.CurrentShapeLayer!.Stylers[ShapeVisualState.Normal];
        var stylerB = managerB.CurrentShapeLayer!.Stylers[ShapeVisualState.Normal];

        Assert.Equal(1.0, stylerA.SketchPen.Thickness);
        Assert.Equal(5.0, stylerA.DragHandleSize);
        Assert.Equal(2.0, stylerB.SketchPen.Thickness);
        Assert.Equal(10.0, stylerB.DragHandleSize);

        // Changing A again must not affect B.
        managerA.OnImageViewerPropertyChanged(4.0);
        Assert.Equal(0.5, stylerA.SketchPen.Thickness);
        Assert.Equal(2.5, stylerA.DragHandleSize);
        Assert.Equal(2.0, stylerB.SketchPen.Thickness);
        Assert.Equal(10.0, stylerB.DragHandleSize);
    }

    [Fact]
    public void TwoManagers_SharingConfigLayer_DoNotClobberEachOtherOnZoom()
    {
        var sharedConfigLayer = TestShapeLayer.CreateWithThickness(stroke: 2, handle: 16);
        var originalThickness = sharedConfigLayer.Stylers[ShapeVisualState.Normal].SketchPen.Thickness;
        var originalHandle = sharedConfigLayer.Stylers[ShapeVisualState.Normal].DragHandleSize;

        var managerA = new SketchBoardDataManager(
            new ViewportScalingOptions(baseStrokeThickness: 2.0, baseDragHandleSize: 16.0));
        var managerB = new SketchBoardDataManager(
            new ViewportScalingOptions(baseStrokeThickness: 2.0, baseDragHandleSize: 16.0));

        managerA.SetShapeLayer(sharedConfigLayer);
        managerB.SetShapeLayer(sharedConfigLayer);

        Assert.False(ReferenceEquals(managerA.CurrentShapeLayer, sharedConfigLayer));
        Assert.False(ReferenceEquals(managerB.CurrentShapeLayer, sharedConfigLayer));
        Assert.False(ReferenceEquals(managerA.CurrentShapeLayer, managerB.CurrentShapeLayer));

        managerA.OnImageViewerPropertyChanged(2.0);
        managerB.OnImageViewerPropertyChanged(4.0);

        Assert.Equal(1.0, managerA.CurrentShapeLayer!.Stylers[ShapeVisualState.Normal].SketchPen.Thickness);
        Assert.Equal(8.0, managerA.CurrentShapeLayer.Stylers[ShapeVisualState.Normal].DragHandleSize);
        Assert.Equal(0.5, managerB.CurrentShapeLayer!.Stylers[ShapeVisualState.Normal].SketchPen.Thickness);
        Assert.Equal(4.0, managerB.CurrentShapeLayer.Stylers[ShapeVisualState.Normal].DragHandleSize);

        // Shared config layer remains at its base values.
        Assert.Equal(originalThickness, sharedConfigLayer.Stylers[ShapeVisualState.Normal].SketchPen.Thickness);
        Assert.Equal(originalHandle, sharedConfigLayer.Stylers[ShapeVisualState.Normal].DragHandleSize);
    }

    [Fact]
    public void OnImageViewerPropertyChanged_RefreshesExistingShapeHandleSize()
    {
        var options = new ViewportScalingOptions(baseStrokeThickness: 2.0, baseDragHandleSize: 16.0);
        var manager = new SketchBoardDataManager(options);
        manager.SetShapeLayer(TestShapeLayer.CreateWithThickness(stroke: 1, handle: 8));
        manager.InitializeVisualCollection(new ContainerVisual());

        var shape = manager.LoadShape<Line, PointsData>(
            new PointsData(1, new List<Point> { new(0, 0), new(10, 0) }));

        manager.OnImageViewerPropertyChanged(2.0);

        var styler = manager.CurrentShapeLayer!.Stylers[ShapeVisualState.Normal];
        Assert.Equal(1.0, styler.SketchPen.Thickness);
        Assert.Equal(8.0, styler.DragHandleSize);
        Assert.Equal(8.0, shape.ShapeStyler!.DragHandleSize);
    }

    [Fact]
    public void SetShapeLayer_AppliesCurrentScaleWithoutRequiringShapes()
    {
        var options = new ViewportScalingOptions(baseStrokeThickness: 4.0, baseDragHandleSize: 20.0);
        var manager = new SketchBoardDataManager(options);

        // Scale first (no layer yet), then attach layer — stylers should pick up scale.
        manager.OnImageViewerPropertyChanged(2.0);
        var layer = TestShapeLayer.CreateWithThickness(stroke: 99, handle: 99);
        manager.SetShapeLayer(layer);

        var styler = manager.CurrentShapeLayer!.Stylers[ShapeVisualState.Normal];
        Assert.Equal(2.0, styler.SketchPen.Thickness);
        Assert.Equal(10.0, styler.DragHandleSize);
        // Config layer left untouched.
        Assert.Equal(99, layer.Stylers[ShapeVisualState.Normal].SketchPen.Thickness);
    }

    [Fact]
    public void OnImageViewerPropertyChanged_ScalesShapesOnNonCurrentLayers()
    {
        var options = new ViewportScalingOptions(baseStrokeThickness: 4.0, baseDragHandleSize: 20.0);
        var manager = new SketchBoardDataManager(options);
        manager.InitializeVisualCollection(new ContainerVisual());

        var layerA = TestShapeLayer.CreateWithThickness(stroke: 4, handle: 20);
        var layerB = TestShapeLayer.CreateWithThickness(stroke: 4, handle: 20);
        // Give layerB a distinct id/name so copies stay distinguishable.
        layerB = new ShapeLayer(new ShapeLayerParameter
        {
            LayerId = 2,
            Name = "LayerB",
            Description = "B",
            MaximumThickenedShapeWidth = 100,
            TagFontSize = 12,
            TextForeground = new SolidColorBrush(Colors.Black),
            BorderBackground = new SolidColorBrush(Colors.LightBlue),
            StyleSchema = new Dictionary<ShapeVisualState, ShapeStylerParameter>
            {
                [ShapeVisualState.Normal] = new ShapeStylerParameter
                {
                    FillColor = new SolidColorBrush(Colors.Transparent),
                    StrokeColor = new SolidColorBrush(Colors.Red),
                    StrokeThickness = 4,
                    DashStyle = "Solid",
                    DragHandleSize = 20,
                    FillOpacity = 0
                },
                [ShapeVisualState.Selected] = new ShapeStylerParameter
                {
                    FillColor = new SolidColorBrush(Colors.Transparent),
                    StrokeColor = new SolidColorBrush(Colors.Blue),
                    StrokeThickness = 4,
                    DashStyle = "Solid",
                    DragHandleSize = 20,
                    FillOpacity = 0
                }
            }
        });

        manager.SetShapeLayer(layerA);
        var shapeOnA = manager.LoadShape<Line, PointsData>(
            new PointsData(1, new List<Point> { new(0, 0), new(10, 0) }));

        manager.SetShapeLayer(layerB);
        // Shape remains on layer A copy; current layer is B copy.
        Assert.Equal(1, shapeOnA.ShapeLayer.LayerId);
        Assert.Equal(2, manager.CurrentShapeLayer!.LayerId);

        manager.OnImageViewerPropertyChanged(2.0);

        Assert.Equal(2.0, shapeOnA.ShapeLayer.Stylers[ShapeVisualState.Normal].SketchPen.Thickness);
        Assert.Equal(10.0, shapeOnA.ShapeStyler!.DragHandleSize);
        Assert.Equal(2.0, manager.CurrentShapeLayer.Stylers[ShapeVisualState.Normal].SketchPen.Thickness);
    }
}
