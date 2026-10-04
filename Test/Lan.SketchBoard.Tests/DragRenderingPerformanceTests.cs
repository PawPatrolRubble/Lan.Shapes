using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;
using Xunit.Abstractions;
using RectangleShape = Lan.Shapes.Shapes.Rectangle;

namespace Lan.SketchBoard.Tests;

public class DragRenderingPerformanceTests
{
    private readonly ITestOutputHelper _output;
    public DragRenderingPerformanceTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("Line", true)]
    [InlineData("Line", false)]
    [InlineData("Circle", true)]
    [InlineData("Circle", false)]
    [InlineData("Rectangle", true)]
    [InlineData("Rectangle", false)]
    [InlineData("Rectangle2", true)]
    [InlineData("Rectangle2", false)]
    public void DragSamples_KeepGeometryAndMeasureRenderingWork(string kind, bool showLayerPanel)
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var layers = new ShapeLayerManager();
            layers.Layers.Add(TestShapeLayer.Create());
            var manager = new SketchBoardDataManager();
            manager.SetShapeLayer(layers.Layers[0]);
            for (var i = 0; i < 300; i++)
            {
                var line = new Line(manager.CurrentShapeLayer!);
                line.FromData(new PointsData(1, new List<Point> { new(10, i * 3 + 10), new(80, i * 3 + 10) }));
                manager.AddShape(line);
            }
            var shape = CreateShape(kind, manager.CurrentShapeLayer!);
            manager.AddShape(shape);
            var types = new GeometryTypeManager();
            types.RegisterGeometryType<Line>();
            var vm = new ImageViewerControlViewModel(layers, manager, types) { ShowSimpleCanvas = !showLayerPanel };
            var control = new ImageViewerControl { DataContext = vm };
            Layout(control);
            if (showLayerPanel)
            {
                var tree = (TreeView)control.FindName("LayerTree");
                ((TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0)).IsExpanded = true;
                Layout(control);
            }
            var board = Descendants<SketchBoard>(control).Single();
            var start = new Point(600, 200);
            Invoke(board, "HandleLeftButtonDown", new[] { typeof(Point), typeof(int), typeof(ModifierKeys) }, start, 1, ModifierKeys.None);
            Assert.Same(shape, manager.SelectedGeometry);
            var original = shape.BoundsRect;
            var renderCounts = (IRenderCounter)shape;
            renderCounts.RenderCount = 0;
            var rebuilds = 0;
            vm.LayerGroups[0].Shapes.CollectionChanged += (_, _) => rebuilds++;
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.StartNew();
            const int samples = 240;
            for (var i = 1; i <= samples; i++)
            {
                var point = start + new Vector(i * 0.1, i * 0.08);
                Invoke(board, "HandleMouseMove", new[] { typeof(Point), typeof(MouseButtonState) }, point, MouseButtonState.Pressed);
                Layout(control);
            }
            timer.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            _output.WriteLine($"{kind}, panel={showLayerPanel}, {samples} samples: {timer.Elapsed.TotalMilliseconds:F1} ms, " +
                $"{allocated / 1024d:F1} KiB allocated, {renderCounts.RenderCount} renders, {rebuilds} tree changes.");
            Invoke(board, "HandleLeftButtonUp", new[] { typeof(Point), typeof(ModifierKeys) }, start + new Vector(24, 19.2), ModifierKeys.None);
            // WPF path bounds use single-precision values internally.
            Assert.Equal(original.Left + 24, shape.BoundsRect.Left, 4);
            Assert.Equal(original.Top + 19.2, shape.BoundsRect.Top, 4);
            Assert.Equal(original.Width, shape.BoundsRect.Width, 4);
            Assert.Equal(original.Height, shape.BoundsRect.Height, 4);
            Assert.Equal(0, rebuilds);
            Assert.InRange(renderCounts.RenderCount, 1, samples);
        });
    }

    private static ShapeVisualBase CreateShape(string kind, ShapeLayer layer)
    {
        switch (kind)
        {
            case "Line":
                var line = new CountingLine(layer);
                line.FromData(new PointsData(1, new List<Point> { new(500, 200), new(700, 200) }));
                return line;
            case "Circle":
                var circle = new CountingCircle(layer);
                circle.FromData(new EllipseData { Center = new Point(600, 200), RadiusX = 80 });
                return circle;
            case "Rectangle":
                var rectangle = new CountingRectangle(layer);
                rectangle.FromData(new PointsData(1, new List<Point> { new(500, 120), new(700, 280) }));
                return rectangle;
            default:
                var rectangle2 = new CountingRectangle2(layer);
                rectangle2.FromData(new Rectangle2Data { Column = 600, Row = 200, Length1 = 100, Length2 = 80 });
                return rectangle2;
        }
    }

    private static void Invoke(SketchBoard board, string name, Type[] signature, params object[] args)
        => typeof(SketchBoard).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, null, signature, null)!.Invoke(board, args);

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(1400, 900));
        element.Arrange(new Rect(0, 0, 1400, 900));
        element.UpdateLayout();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private interface IRenderCounter { int RenderCount { get; set; } }
    private sealed class CountingLine : Line, IRenderCounter
    {
        public CountingLine(ShapeLayer layer) : base(layer) { }
        public int RenderCount { get; set; }
        public override void UpdateVisual() { RenderCount++; base.UpdateVisual(); }
    }
    private sealed class CountingCircle : Circle, IRenderCounter
    {
        public CountingCircle(ShapeLayer layer) : base(layer) { }
        public int RenderCount { get; set; }
        public override void UpdateVisual() { RenderCount++; base.UpdateVisual(); }
    }
    private sealed class CountingRectangle : RectangleShape, IRenderCounter
    {
        public CountingRectangle(ShapeLayer layer) : base(layer) { }
        public int RenderCount { get; set; }
        public override void UpdateVisual() { RenderCount++; base.UpdateVisual(); }
    }
    private sealed class CountingRectangle2 : Rectangle2, IRenderCounter
    {
        public CountingRectangle2(ShapeLayer layer) : base(layer) { }
        public int RenderCount { get; set; }
        public override void UpdateVisual() { RenderCount++; base.UpdateVisual(); }
    }
}
