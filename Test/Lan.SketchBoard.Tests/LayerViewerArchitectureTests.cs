using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Newtonsoft.Json;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class LayerViewerArchitectureTests
{
    [Fact]
    public void ReplacingLayerCollectionIsRejectedBeforeChangingViewerState()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var (vm, layers, board) = Viewer();
            var originalCollection = vm.Layers;
            var current = vm.SelectedShapeLayer;
            var replacement = new ObservableCollection<ShapeLayer> { BulkLayerFeatureTests.Layer(99, "External") };

            Assert.Throws<ArgumentException>(() => vm.Layers = replacement);
            Assert.Same(originalCollection, vm.Layers);
            Assert.Same(current, vm.SelectedShapeLayer);
            Assert.Equal(current.LayerId, board.CurrentShapeLayer!.LayerId);
            vm.Layers = layers.Layers;

            var definition = current.ToShapeLayerParameter();
            definition.Name = "Created";
            var created = vm.CreateLayer(definition);
            Assert.Contains(created, vm.Layers);
            Assert.Contains(vm.LayerGroups, node => node.LayerId == created.LayerId);
        });
    }

    [Fact]
    public void CurrentLayerResolvesToItsCatalogueDefinitionAndRejectsUnknownIds()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var (vm, layers, board) = Viewer();
            var external = BulkLayerFeatureTests.Layer(2, "External second");

            vm.SelectedShapeLayer = external;

            Assert.Same(layers.Layers[1], vm.SelectedShapeLayer);
            Assert.Equal("Second", board.CurrentShapeLayer!.Name);
            Assert.Throws<ArgumentException>(() =>
                vm.SelectedShapeLayer = BulkLayerFeatureTests.Layer(99, "Unknown"));
            Assert.Same(layers.Layers[1], vm.SelectedShapeLayer);
            Assert.Equal(2, board.CurrentShapeLayer.LayerId);
        });
    }

    [Fact]
    public void ReloadSynchronizesExistingCalibrationAndPreservesConfiguredCurrentLayer()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var (vm, layers, board) = Viewer();
            vm.SelectedShapeLayer = layers.Layers[1];
            var shape = AddLine(board, 20);
            var control = new ImageViewerControl { DataContext = vm };
            Layout(control);
            var selector = (ComboBox)control.FindName("CurrentLayerSelector");
            var path = Path.GetTempFileName();
            try
            {
                var configuration = new LanShapesConfiguration
                {
                    Measurement = new ShapeMeasurementSettings { PixelPerUnit = 4, UnitsPerMillimeter = 10, UnitName = "mm" },
                    ShapeLayers = layers.Layers.Select(layer => layer.ToShapeLayerParameter()).ToList()
                };
                configuration.ShapeLayers[1].Name = "Reloaded second";
                File.WriteAllText(path, JsonConvert.SerializeObject(configuration));
                layers.ReadConfiguration(path);

                Assert.Equal(2, vm.SelectedShapeLayer.LayerId);
                Assert.Same(layers.Layers[1], vm.SelectedShapeLayer);
                Assert.Same(layers.Layers[1], selector.SelectedItem);
                Assert.Equal(2, board.CurrentShapeLayer!.LayerId);
                Assert.Equal(4, shape.ShapeLayer.Measurement.PixelPerUnit);
                Assert.Equal("Reloaded second", shape.ShapeLayer.Name);
                Assert.Same(layers.Configuration.Measurement, shape.ShapeLayer.Measurement);
                Assert.Equal("Reloaded second", vm.LayerGroups.Single(node => node.LayerId == 2).Name);
            }
            finally { File.Delete(path); }
        });
    }

    [Fact]
    public void RemovingConfiguredLayerKeepsOrphanShapesButDisablesTheirEditor()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var (vm, layers, board) = Viewer();
            vm.SelectedShapeLayer = layers.Layers[1];
            var orphan = AddLine(board, 20);
            board.SetSelection(new[] { orphan });
            layers.Layers.RemoveAt(1);

            Assert.Equal(1, vm.SelectedShapeLayer.LayerId);
            Assert.Equal(1, board.CurrentShapeLayer!.LayerId);
            Assert.Null(vm.TargetShapeLayer);
            var orphanNode = vm.LayerGroups.Single(node => node.LayerId == 2);
            Assert.Same(orphan, Assert.Single(orphanNode.Shapes));
            var control = new ImageViewerControl { DataContext = vm };
            Layout(control);
            var tree = (TreeView)control.FindName("LayerTree");
            var container = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(orphanNode);
            var editor = Descendants<Button>(container).Single(button => Equals(button.Content, "编辑"));
            Assert.False(editor.IsEnabled);
        });
    }

    [Fact]
    public void NativeTreeSelectionCancelsUnfinishedSketchAndActiveTool()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var (vm, _, board) = Viewer();
            var finished = AddLine(board, 20);
            vm.ChooseGeometryTypeCommand.Execute(vm.GeometryTypeList.First());
            var unfinished = board.CreateNewGeometry(new Point(100, 100));
            Assert.NotNull(unfinished);
            Assert.False(unfinished.IsGeometryRendered);
            var control = new ImageViewerControl { DataContext = vm };
            Layout(control);
            var tree = (TreeView)control.FindName("LayerTree");
            var group = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(vm.LayerGroups[0]);
            group.IsExpanded = true;
            Layout(control);
            var item = (TreeViewItem)group.ItemContainerGenerator.ContainerFromItem(finished);

            item.IsSelected = true;

            Assert.Same(finished, vm.SelectedShape);
            Assert.DoesNotContain(unfinished, board.Shapes);
            Assert.Null(board.CurrentGeometryInEdit);
            Assert.Null(board.CurrentGeometryType);
            Assert.Null(vm.SelectedGeometryType);
        });
    }

    [Fact]
    public void ShapeCollectionChangesPreservePerLayerOrderAndNodeIdentity()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var (vm, _, board) = Viewer();
            var firstNode = vm.LayerGroups[0];
            var secondNode = vm.LayerGroups[1];
            var first = AddLine(board, 10);
            var last = AddLine(board, 30);
            var inserted = new Line(board.CurrentShapeLayer!);
            inserted.FromData(new PointsData(1, new List<Point> { new(10, 20), new(50, 20) }));
            board.AddShape(inserted, 1);
            Assert.Equal(new[] { first, inserted, last }, firstNode.Shapes);

            board.Shapes.Move(2, 0);
            Assert.Equal(new[] { last, first, inserted }, firstNode.Shapes);
            board.RemoveShape(first);
            Assert.Equal(new[] { last, inserted }, firstNode.Shapes);
            board.AssignShapesToLayer(new[] { inserted }, vm.Layers[1]);
            Assert.Same(last, Assert.Single(firstNode.Shapes));
            Assert.Same(inserted, Assert.Single(secondNode.Shapes));
            Assert.Same(firstNode, vm.LayerGroups[0]);
            Assert.Same(secondNode, vm.LayerGroups[1]);
            board.ClearAllShapes();
            Assert.Empty(firstNode.Shapes);
            Assert.Empty(secondNode.Shapes);
        });
    }

    [Fact]
    public void DisposedViewerCanBeCollectedWhileSharedManagerAndBoardRemainAlive()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var (weakViewer, layers, board) = CreateDisposedViewer();
            Collect();
            Assert.False(weakViewer.IsAlive);
            var definition = layers.Layers[0].ToShapeLayerParameter();
            definition.Name = "After disposal";
            layers.UpdateLayer(definition);
            Assert.Equal("First", board.CurrentShapeLayer!.Name);
            GC.KeepAlive(layers);
            GC.KeepAlive(board);
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Viewer, ShapeLayerManager Layers, SketchBoardDataManager Board) CreateDisposedViewer()
    {
        var (vm, layers, board) = Viewer();
        AddLine(board, 20);
        var disposable = Assert.IsAssignableFrom<IDisposable>(vm);
        var weakViewer = new WeakReference(vm);
        disposable.Dispose();
        disposable.Dispose();
        return (weakViewer, layers, board);
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static (ImageViewerControlViewModel Vm, ShapeLayerManager Layers, SketchBoardDataManager Board) Viewer()
    {
        var layers = new ShapeLayerManager();
        layers.Layers.Add(BulkLayerFeatureTests.Layer(1, "First"));
        layers.Layers.Add(BulkLayerFeatureTests.Layer(2, "Second"));
        var types = new GeometryTypeManager();
        types.RegisterGeometryType<Line>();
        var board = new SketchBoardDataManager();
        return (new ImageViewerControlViewModel(layers, board, types), layers, board);
    }

    private static Line AddLine(SketchBoardDataManager board, double y)
    {
        var shape = new Line(board.CurrentShapeLayer!);
        shape.FromData(new PointsData(1, new List<Point> { new(10, y), new(50, y) }));
        board.AddShape(shape);
        return shape;
    }

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(1400, 1000));
        element.Arrange(new Rect(0, 0, 1400, 1000));
        element.UpdateLayout();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
