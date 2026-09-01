using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;
using Lan.SketchBoard;
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
