using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class BulkLayerFeatureTests
{
    [Fact]
    public void Selection_DeduplicatesAndLegacySingleSelectionCollapsesTheSet()
    {
        var manager = Manager();
        var first = AddLine(manager, 50);
        var second = AddLine(manager, 100);
        manager.SetSelection(new[] { first, second, first });
        Assert.Equal(new[] { first, second }, manager.SelectedGeometries);
        Assert.Same(second, manager.SelectedGeometry);
        Assert.True(first.IsSelected);
        manager.SelectedGeometry = second;
        Assert.Same(second, Assert.Single(manager.SelectedGeometries));
        Assert.False(first.IsSelected);
        manager.RemoveShape(second);
        Assert.Empty(manager.SelectedGeometries);
        Assert.False(second.IsSelected);
    }

    [Fact]
    public void InvalidSelection_PreservesThePreviousSet()
    {
        var manager = Manager();
        var first = AddLine(manager, 50);
        manager.SetSelection(new[] { first });
        var outside = new Line(manager.CurrentShapeLayer!);
        Assert.Throws<ArgumentException>(() => manager.SetSelection(new[] { first, outside }));
        Assert.Same(first, Assert.Single(manager.SelectedGeometries));
    }

    [Fact]
    public void BatchMove_PreservesGeometryAndIdentityAndUsesIndependentScaledStyles()
    {
        var manager = Manager();
        manager.InitializeVisualCollection(new ContainerVisual());
        var first = AddLine(manager, 50);
        var second = AddLine(manager, 100);
        var firstId = first.Id;
        var firstPoints = first.GetMetaData().DataPoints.ToArray();
        manager.SetSelection(new[] { first, second });
        manager.OnImageViewerPropertyChanged(2);
        var target = Layer(2, "Target", 6);
        var changes = 0;
        manager.LayerAssignmentsChanged += (_, _) => changes++;
        Assert.Equal(2, manager.AssignShapesToLayer(new[] { first, second, first }, target));
        Assert.All(manager.Shapes, shape => Assert.Equal(2, shape.ShapeLayer.LayerId));
        Assert.Same(first.ShapeLayer, second.ShapeLayer);
        Assert.NotSame(target, first.ShapeLayer);
        Assert.Equal(3, first.ShapeStyler!.SketchPen.Thickness);
        Assert.Equal(6, target.GetStyler(ShapeVisualState.Selected).SketchPen.Thickness);
        Assert.Equal(firstId, first.Id);
        Assert.Equal(firstPoints, first.GetMetaData().DataPoints);
        Assert.Equal(2, manager.SelectedGeometries.Count);
        Assert.Equal(1, manager.CurrentShapeLayer!.LayerId);
        Assert.Equal(1, changes);
        Assert.Equal(0, manager.AssignShapesToLayer(new[] { first, second }, target));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void BatchMove_WithLockedMemberChangesNothing()
    {
        var manager = Manager();
        var first = AddLine(manager, 50);
        var second = AddLine(manager, 100);
        second.Lock();
        Assert.Throws<ArgumentException>(() => manager.AssignShapesToLayer(new[] { first, second }, Layer(2, "Target")));
        Assert.All(manager.Shapes, shape => Assert.Equal(1, shape.ShapeLayer.LayerId));
        Assert.True(second.IsLocked);
    }

    [Fact]
    public void BatchMove_RenderFailureRestoresLayersSelectionAndVisuals()
    {
        var manager = Manager();
        manager.InitializeVisualCollection(new ContainerVisual());
        var first = AddLine(manager, 50);
        var second = new ThrowingLine(manager.CurrentShapeLayer!);
        second.FromData(new PointsData(1, new List<Point> { new(40, 100), new(100, 100) }));
        manager.AddShape(second);
        manager.SetSelection(new ShapeVisualBase[] { first, second });
        second.FailNext = true;
        Assert.Throws<InvalidOperationException>(() => manager.AssignShapesToLayer(new ShapeVisualBase[] { first, second }, Layer(2, "Target")));
        Assert.All(manager.Shapes, shape => Assert.Equal(1, shape.ShapeLayer.LayerId));
        Assert.Equal(new ShapeVisualBase[] { first, second }, manager.SelectedGeometries);
        Assert.Equal(new Visual[] { first, second }, manager.VisualCollection.Cast<Visual>());
    }

    [Fact]
    public void HiddenTarget_RemovesMigratedShapesFromSelectionAndCanvas()
    {
        var manager = Manager();
        manager.InitializeVisualCollection(new ContainerVisual());
        var first = AddLine(manager, 50);
        var second = AddLine(manager, 100);
        manager.SetSelection(new[] { first, second });
        manager.SetLayerVisibility(2, false);
        manager.AssignShapesToLayer(new[] { first }, Layer(2, "Hidden"));
        Assert.Same(second, Assert.Single(manager.SelectedGeometries));
        Assert.Same(second, Assert.Single(manager.VisualCollection.Cast<Visual>()));
        Assert.Equal(2, manager.Shapes.Count);
        manager.SetLayerVisibility(1, false);
        Assert.Empty(manager.SelectedGeometries);
    }

    [Fact]
    public void ControlClick_TogglesSelectionWithoutDraggingGeometry()
    {
        Sta(() =>
        {
            var manager = Manager();
            var board = new ProbeBoard { SketchBoardDataManager = manager };
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 100);
            board.Press(new Point(70, 50), ModifierKeys.Control);
            board.Move(new Point(170, 150));
            board.Release(new Point(170, 150));
            Assert.Equal(new Point(40, 50), first.Start);
            board.Click(new Point(70, 100), ModifierKeys.Control);
            Assert.Equal(new[] { first, second }, manager.SelectedGeometries);
            board.Click(new Point(70, 50), ModifierKeys.Control);
            Assert.Same(second, Assert.Single(manager.SelectedGeometries));
        });
    }

    [Theory]
    [InlineData(false, 0.5)]
    [InlineData(false, 2)]
    [InlineData(true, 0.5)]
    [InlineData(true, 2)]
    public void Marquee_DirectionControlsContainmentOrCrossing(bool crossing, double scale)
    {
        Sta(() =>
        {
            var manager = Manager();
            var board = new ProbeBoard { SketchBoardDataManager = manager };
            manager.OnImageViewerPropertyChanged(scale);
            var first = AddLine(manager, 50);
            var partial = AddLine(manager, 80, 180, 280);
            var locked = AddLine(manager, 110);
            locked.Lock();
            var start = crossing ? new Point(220, 210) : new Point(10, 10);
            var end = crossing ? new Point(10, 10) : new Point(220, 210);
            board.Press(start);
            board.Move(end);
            board.Release(end);
            Assert.Contains(first, manager.SelectedGeometries);
            Assert.Equal(crossing, manager.SelectedGeometries.Contains(partial));
            Assert.DoesNotContain(locked, manager.SelectedGeometries);
        });
    }

    [Fact]
    public void AdditiveMarqueeAndKeyboardSelectionShareTheSameCollection()
    {
        Sta(() =>
        {
            var manager = Manager();
            var board = new ProbeBoard { SketchBoardDataManager = manager };
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 240);
            manager.SetSelection(new[] { second });
            board.Press(new Point(10, 10), ModifierKeys.Control);
            board.Release(new Point(140, 140), ModifierKeys.Control);
            Assert.Equal(new[] { second, first }, manager.SelectedGeometries);
            board.Key(Key.Escape);
            Assert.Empty(manager.SelectedGeometries);
            board.Key(Key.A, ModifierKeys.Control);
            Assert.Equal(2, manager.SelectedGeometries.Count);
            board.Key(Key.Delete);
            Assert.Empty(manager.Shapes);
            Assert.Empty(manager.SelectedGeometries);
        });
    }

    [Fact]
    public void CrossingSelection_UsesActualGeometryAndVisualTransform()
    {
        var manager = Manager();
        var line = new Line(manager.CurrentShapeLayer!);
        line.FromData(new PointsData(1, new List<Point> { new(0, 0), new(100, 100) }));
        Assert.False(line.MatchesSelectionRectangle(new Rect(0, 80, 10, 10), crossing: true));
        line.Transform = new TranslateTransform(100, 50);
        Assert.True(line.MatchesSelectionRectangle(new Rect(95, 45, 110, 110), crossing: false));
        Assert.True(line.MatchesSelectionRectangle(new Rect(140, 90, 20, 20), crossing: true));
    }

    [Fact]
    public void CrossingSelection_ExcludesEmptyInteriorsAndIncludesVisibleFills()
    {
        var layer = TestShapeLayer.Create();
        var circle = new Circle(layer);
        circle.FromData(new EllipseData { Center = new Point(100, 100), RadiusX = 50 });
        var inside = new Rect(95, 95, 10, 10);
        Assert.False(circle.MatchesSelectionRectangle(inside, crossing: true));
        Assert.True(circle.MatchesSelectionRectangle(new Rect(145, 95, 10, 10), crossing: true));
        circle.State = ShapeVisualState.Selected;
        layer.GetStyler(ShapeVisualState.Selected).SetFillColor(Brushes.Blue);
        Assert.False(circle.MatchesSelectionRectangle(inside, crossing: true));
        layer.GetStyler(ShapeVisualState.Normal).SetFillColor(Brushes.Red);
        Assert.True(circle.MatchesSelectionRectangle(inside, crossing: true));
    }

    [Fact]
    public void TreeMultiSelectionAndAssignmentSelectorUpdateGroupsAndCanvasSelection()
    {
        Sta(() =>
        {
            var layers = new ShapeLayerManager();
            layers.Layers.Add(Layer(1, "First"));
            layers.Layers.Add(Layer(2, "Second"));
            var manager = Manager();
            var vm = Viewer(layers, manager);
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 100);
            var third = AddLine(manager, 150);
            var control = new ImageViewerControl { DataContext = vm };
            Layout(control);
            var tree = (TreeView)control.FindName("LayerTree");
            ((TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0)).IsExpanded = true;
            Layout(control);
            SelectTree(control, first);
            SelectTree(control, third, ModifierKeys.Shift);
            Assert.Equal(new[] { first, second, third }, manager.SelectedGeometries);
            SelectTree(control, second, ModifierKeys.Control);
            Assert.Equal(new[] { first, third }, manager.SelectedGeometries);
            var selector = (ComboBox)control.FindName("AssignedLayerSelector");
            selector.SelectedItem = layers.Layers[1];
            Assert.True(vm.CanAssignSelectedLayer);
            Assert.Equal(2, vm.AssignSelectedShapesToLayer());
            Assert.Equal(new[] { first, third }, vm.LayerGroups.Single(x => x.LayerId == 2).Shapes);
            Assert.Same(second, Assert.Single(vm.LayerGroups.Single(x => x.LayerId == 1).Shapes));
            Assert.Equal("Second", vm.SelectedLayerSummary);
            Assert.Null(vm.PropertyShape);
            manager.SetSelection(new[] { first, second });
            Assert.Equal("混合图层", vm.SelectedLayerSummary);
            Assert.Null(vm.TargetShapeLayer);
            Layout(control);
            LayerControlsTests.Snapshot(control, "multi-selection-viewer");
            SelectTree(control, third);
            Assert.Same(third, Assert.Single(manager.SelectedGeometries));
        });
    }

    [Fact]
    public void LayerDefinitionChange_RefreshesViewersAtTheirOwnScale()
    {
        Sta(() =>
        {
            var layers = new ShapeLayerManager();
            layers.Layers.Add(Layer(1, "First"));
            var first = Manager();
            var second = Manager();
            var firstVm = Viewer(layers, first);
            var secondVm = Viewer(layers, second);
            var a = AddLine(first, 50);
            var b = AddLine(second, 50);
            first.OnImageViewerPropertyChanged(2);
            second.OnImageViewerPropertyChanged(4);
            var definition = layers.Layers[0].ToShapeLayerParameter();
            definition.Name = "Renamed";
            definition.StyleSchema[ShapeVisualState.Normal].StrokeThickness = 8;
            firstVm.UpdateLayerConfiguration(definition);
            Assert.Equal(4, a.ShapeStyler!.SketchPen.Thickness);
            Assert.Equal(2, b.ShapeStyler!.SketchPen.Thickness);
            Assert.Equal("Renamed", secondVm.LayerGroups.Single().Name);
            Assert.Equal(8, layers.Layers[0].GetStyler(ShapeVisualState.Normal).SketchPen.Thickness);
        });
    }

    internal static ShapeLayer Layer(int id, string name, double width = 1)
    {
        var parameter = TestShapeLayer.CreateWithThickness(width, 10).ToShapeLayerParameter();
        parameter.LayerId = id;
        parameter.Name = name;
        return new ShapeLayer(parameter);
    }
    private static SketchBoardDataManager Manager()
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(Layer(1, "First"));
        return manager;
    }
    private static Line AddLine(SketchBoardDataManager manager, double y, double start = 40, double end = 100)
    {
        var line = new Line(manager.CurrentShapeLayer!);
        line.FromData(new PointsData(1, new List<Point> { new(start, y), new(end, y) }));
        manager.AddShape(line);
        return line;
    }
    private static ImageViewerControlViewModel Viewer(ShapeLayerManager layers, SketchBoardDataManager manager)
    {
        var types = new GeometryTypeManager();
        types.RegisterGeometryType<Line>();
        return new ImageViewerControlViewModel(layers, manager, types);
    }
    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(1400, 1000));
        element.Arrange(new Rect(0, 0, 1400, 1000));
        element.UpdateLayout();
    }
    internal static void Sta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
    private sealed class ProbeBoard : SketchBoard
    {
        public void Press(Point p, ModifierKeys modifiers = ModifierKeys.None) => HandleLeftButtonDown(p, 1, modifiers);
        public void Move(Point p) => HandleMouseMove(p, MouseButtonState.Pressed);
        public void Release(Point p, ModifierKeys modifiers = ModifierKeys.None) => HandleLeftButtonUp(p, modifiers);
        public void Click(Point p, ModifierKeys modifiers) { Press(p, modifiers); Release(p, modifiers); }
        public void Key(Key key, ModifierKeys modifiers = ModifierKeys.None) => HandleSelectionKey(key, modifiers);
    }
    private static void SelectTree(ImageViewerControl control, ShapeVisualBase shape, ModifierKeys modifiers = ModifierKeys.None)
        => typeof(ImageViewerControl).GetMethod("SelectTreeShape", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, new object[] { shape, modifiers });
    private sealed class ThrowingLine : Line
    {
        public bool FailNext { get; set; }
        public ThrowingLine(ShapeLayer layer) : base(layer) { }
        protected override void OnViewportScaleChanged(double scale)
        {
            if (FailNext && ShapeLayer.LayerId == 2)
            {
                FailNext = false;
                throw new InvalidOperationException("Injected render failure");
            }
            base.OnViewportScaleChanged(scale);
        }
    }
}
