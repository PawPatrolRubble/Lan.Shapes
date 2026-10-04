using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;
using Lan.Shapes.Enums;
using Lan.SketchBoard;
using Newtonsoft.Json;
using Xunit;
namespace Lan.SketchBoard.Tests;

/// <summary>
/// Phase 4 ISP: ViewModel uses repository selection for list/delete,
/// not the in-progress sketch pointer CurrentGeometryInEdit.
/// </summary>
public class ImageViewerViewModelTests
{
    [Fact]
    public void ThreeParameterConstructor_IsPreservedForBinaryCompatibility()
    {
        var constructor = typeof(ImageViewerControlViewModel).GetConstructor(
            [
                typeof(IShapeLayerManager),
                typeof(ISketchBoardDataManager),
                typeof(IGeometryTypeManager)
            ]);

        Assert.NotNull(constructor);
    }

    [Fact]
    public void SelectedShape_MapsToRepositorySelectedGeometry()
    {
        var (vm, manager, layer) = CreateViewModel();
        var shape = new Line(layer);
        manager.AddShape(shape);

        vm.SelectedShape = shape;

        Assert.Same(shape, manager.SelectedGeometry);
        Assert.Same(shape, vm.SelectedShape);
        Assert.Same(manager, vm.ShapeRepository);
        Assert.Same(manager.Shapes, vm.Shapes);
    }

    [Fact]
    public void SelectedShape_RaisesWhenBoardSelectionChanges()
    {
        var (vm, manager, layer) = CreateViewModel();
        var shape = new Line(layer);
        manager.AddShape(shape);

        var raised = 0;
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IImageViewerViewModel.SelectedShape))
            {
                raised++;
            }
        };

        // Board-driven selection (mouse path) should notify the VM property.
        manager.SelectedGeometry = shape;

        Assert.Same(shape, vm.SelectedShape);
        Assert.True(raised >= 1);
    }

    [Fact]
    public void DeleteShapeCommand_RemovesSelectedShape_NotCurrentGeometryInEdit()
    {
        var (vm, manager, layer) = CreateViewModel();
        var selected = new Line(layer);
        var sketching = new Line(layer);
        manager.AddShape(selected);
        manager.AddShape(sketching);

        // List selection vs in-progress sketch are distinct.
        manager.SelectedGeometry = selected;
        manager.CurrentGeometryInEdit = sketching;

        Assert.True(vm.DeleteShapeCommand.CanExecute(null));
        vm.DeleteShapeCommand.Execute(null);

        Assert.DoesNotContain(selected, manager.Shapes);
        Assert.Contains(sketching, manager.Shapes);
        Assert.Same(sketching, manager.CurrentGeometryInEdit);
        Assert.Null(manager.SelectedGeometry);
        Assert.Null(vm.SelectedShape);
    }

    [Fact]
    public void ShapeRepository_DoesNotRequireVisualHostForSelection()
    {
        // Confirms VM shape logic never needs VisualCollection / InitializeVisualCollection.
        var (vm, manager, layer) = CreateViewModel();
        var shape = new Line(layer);
        manager.AddShape(shape);

        IShapeRepository repo = vm.ShapeRepository;
        repo.SelectedGeometry = shape;

        Assert.Same(shape, repo.SelectedGeometry);
        Assert.Single(repo.Shapes);
        // Accessing VisualCollection without host must still throw — VM must not touch it.
        Assert.Throws<System.InvalidOperationException>(() => _ = manager.VisualCollection);
    }

    [Fact]
    public void GeometryTypeList_MirrorsRegisteredGeometryTypesOnly()
    {
        var layer = TestShapeLayer.Create();
        var layerManager = new ShapeLayerManager();
        layerManager.Layers.Add(layer);

        var geometryTypeManager = new GeometryTypeManager();
        GeometryTypeRegistration.RegisterGeometryTypes(
            geometryTypeManager,
            new[] { nameof(Line), nameof(Circle) });

        var vm = new ImageViewerControlViewModel(
            layerManager,
            new SketchBoardDataManager(),
            geometryTypeManager);

        Assert.Equal(
            new[] { nameof(Line), nameof(Circle) },
            vm.GeometryTypeList.Select(x => x.Name).ToArray());
    }

    [Fact]
    public void ShowCrossLine_DefaultsToTrue_AndNotifiesOnPropertyChange()
    {
        var (vm, _, _) = CreateViewModel();

        Assert.True(vm.ShowCrossLine);

        var propertyChangedFired = false;
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IImageViewerViewModel.ShowCrossLine))
            {
                propertyChangedFired = true;
            }
        };

        vm.ShowCrossLine = false;
        Assert.False(vm.ShowCrossLine);
        Assert.True(propertyChangedFired);

        propertyChangedFired = false;
        vm.ShowCrossLine = true;
        Assert.True(vm.ShowCrossLine);
        Assert.True(propertyChangedFired);
    }

    [Fact]
    public void ShowGeometries_DefaultsToTrue_AndNotifiesOnToggle()
    {
        var (vm, _, _) = CreateViewModel();

        Assert.True(vm.ShowGeometries);

        var propertyChangedFired = false;
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IImageViewerViewModel.ShowGeometries))
            {
                propertyChangedFired = true;
            }
        };

        vm.ShowGeometries = false;
        Assert.False(vm.ShowGeometries);
        Assert.True(propertyChangedFired);

        propertyChangedFired = false;
        vm.ShowGeometries = true;
        Assert.True(vm.ShowGeometries);
        Assert.True(propertyChangedFired);
    }

    [Fact]
    public void ChooseGeometryType_RestoresShowGeometriesToTrue()
    {
        var (vm, _, _) = CreateViewModel();
        vm.ShowGeometries = false;

        var lineType = vm.GeometryTypeList.FirstOrDefault();
        Assert.NotNull(lineType);

        vm.ChooseGeometryTypeCommand.Execute(lineType);

        Assert.True(vm.ShowGeometries);
    }

    [Fact]
    public void LayerTree_HidesAndRestoresAllShapesIncludingNewOnes()
    {
        var (vm, manager, _) = CreateViewModel();
        manager.InitializeVisualCollection(new ContainerVisual());
        var first = new Line(manager.CurrentShapeLayer!);
        manager.AddShape(first);
        manager.SelectedGeometry = first;

        var group = Assert.Single(vm.LayerGroups);
        Assert.Single(group.Shapes);
        group.IsVisible = false;

        Assert.Null(manager.SelectedGeometry);
        Assert.Empty(manager.VisualCollection.Cast<Visual>());
        Assert.Single(manager.Shapes);

        manager.AddShape(new Line(manager.CurrentShapeLayer!));
        Assert.Equal(2, group.Shapes.Count);
        Assert.Empty(manager.VisualCollection.Cast<Visual>());

        group.IsVisible = true;
        Assert.Equal(2, manager.VisualCollection.Count);

        manager.SetLayerVisibility(group.LayerId, false);
        Assert.False(group.IsVisible);
    }

    [Fact]
    public void EditingLayer_RefreshesOwnedShapesAndKeepsWidthThroughZoom()
    {
        var (vm, manager, layer) = CreateViewModel();
        var shape = new Line(manager.CurrentShapeLayer!);
        manager.AddShape(shape);
        var parameter = layer.ToShapeLayerParameter();
        parameter.Name = "Edited";
        parameter.StyleSchema[ShapeVisualState.Normal].StrokeColor = Brushes.Red;
        parameter.StyleSchema[ShapeVisualState.Normal].StrokeThickness = 3;
        parameter.StyleSchema[ShapeVisualState.Normal].DashStyle = "Dash";

        vm.UpdateLayerConfiguration(parameter);
        manager.OnImageViewerPropertyChanged(2);

        Assert.Equal("Edited", Assert.Single(vm.LayerGroups).Name);
        Assert.Equal("Edited", shape.ShapeLayer.Name);
        Assert.Equal(1.5, shape.ShapeStyler!.SketchPen.Thickness);
        Assert.Equal(DashStyles.Dash, shape.ShapeStyler.SketchPen.DashStyle);
        Assert.Equal(Colors.Red, ((SolidColorBrush)shape.ShapeStyler.SketchPen.Brush).Color);
        Assert.Equal(3, layer.ToShapeLayerParameter()
            .StyleSchema[ShapeVisualState.Normal].StrokeThickness);
    }

    [Fact]
    public void HidingOneLayer_LeavesOtherLayerOnCanvas()
    {
        var (vm, manager, firstLayer) = CreateViewModel();
        manager.InitializeVisualCollection(new ContainerVisual());
        manager.AddShape(new Line(manager.CurrentShapeLayer!));

        var secondParameter = firstLayer.ToShapeLayerParameter();
        secondParameter.LayerId = 2;
        secondParameter.Name = "Second";
        var secondLayer = new ShapeLayer(secondParameter);
        vm.Layers.Add(secondLayer);
        vm.SelectedShapeLayer = secondLayer;
        var secondShape = new Line(manager.CurrentShapeLayer!);
        manager.AddShape(secondShape);

        vm.LayerGroups.Single(x => x.LayerId == firstLayer.LayerId).IsVisible = false;

        Assert.Equal(2, manager.Shapes.Count);
        Assert.Single(manager.VisualCollection.Cast<Visual>());
        Assert.Same(secondShape, manager.VisualCollection[0]);
        Assert.Single(vm.LayerGroups.Single(x => x.LayerId == 2).Shapes);
    }

    private static (ImageViewerControlViewModel Vm, SketchBoardDataManager Manager, ShapeLayer Layer)
        CreateViewModel()
    {
        var layer = TestShapeLayer.Create();
        var layerManager = new ShapeLayerManager();
        layerManager.Layers.Add(layer);

        var geometryTypeManager = new GeometryTypeManager();
        geometryTypeManager.RegisterGeometryType<Line>();

        var manager = new SketchBoardDataManager();
        var vm = new ImageViewerControlViewModel(layerManager, manager, geometryTypeManager);
        return (vm, manager, layer);
    }
}
