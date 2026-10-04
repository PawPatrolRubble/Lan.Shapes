using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class LayerControlsTests
{
    [Fact]
    public void CurrentLayerSelector_AssignsNewShapesAndReflectsViewModelChanges()
    {
        RunOnSta(() =>
        {
            var vm = CreateViewModel();
            var control = new ImageViewerControl { DataContext = vm };
            AddHostTextStyles(control.Resources);
            Layout(control);
            var selector = Assert.IsType<ComboBox>(control.FindName("CurrentLayerSelector"));
            Assert.Same(vm.SelectedShapeLayer, selector.SelectedItem);
            var menu = LayoutDropDown(selector);
            Assert.All(vm.Layers, layer => AssertReadable(
                Descendants<TextBlock>(menu).Single(text => text.Text == layer.Name).Foreground));
            Snapshot(menu, "current-layer-options");
            var lineDirection = Assert.IsType<ComboBox>(control.FindName("LineDirectionSelector"));
            var directions = LayoutDropDown(lineDirection);
            Assert.All(Descendants<TextBlock>(directions), text => AssertReadable(text.Foreground));

            selector.SelectedItem = vm.Layers[1];
            Assert.Same(vm.Layers[1], vm.SelectedShapeLayer);
            vm.ShapeRepository.SetGeometryType(typeof(Line));
            var shape = vm.ShapeRepository.CreateNewGeometry(new Point(10, 10));
            Assert.Equal(vm.Layers[1].LayerId, shape!.ShapeLayer.LayerId);

            vm.SelectedShapeLayer = vm.Layers[0];
            Assert.Same(vm.Layers[0], selector.SelectedItem);
        });
    }

    [Fact]
    public void DarkLayerTree_UsesReadableHeadersAndShapesWithHostTextStyles()
    {
        RunOnSta(() =>
        {
            var vm = CreateViewModel();
            vm.ShapeRepository.AddShape(new Line(vm.ShapeRepository.CurrentShapeLayer!));
            var control = new ImageViewerControl { DataContext = vm };
            AddHostTextStyles(control.Resources);
            Layout(control);
            var tree = Assert.IsType<TreeView>(control.FindName("LayerTree"));
            var group = Assert.IsType<TreeViewItem>(tree.ItemContainerGenerator.ContainerFromIndex(0));
            group.IsExpanded = true;
            Layout(control);
            Snapshot(control, "layer-viewer");

            var texts = Descendants<TextBlock>(tree).ToList();
            AssertReadable(texts.Single(x => x.Text == vm.Layers[0].Name).Foreground);
            AssertReadable(texts.Single(x => x.Text == nameof(Line)).Foreground);
            Assert.All(Descendants<Button>(tree).Where(x => Equals(x.Content, "编辑")),
                edit => AssertReadable(edit.Foreground));
            group.IsSelected = true;
            Layout(control);
            AssertReadable(texts.Single(x => x.Text == vm.Layers[0].Name).Foreground);
        });
    }

    [Fact]
    public void DarkLayerEditor_UsesReadableLabelsInputsAndLineTypeOptions()
    {
        RunOnSta(() =>
        {
            var editor = new LayerEditorWindow(TestShapeLayer.Create());
            var content = Assert.IsType<Grid>(editor.Content);
            Layout(content, new Size(358, 500));
            Snapshot(content, "layer-editor");
            Assert.All(content.Children.OfType<TextBlock>(),
                text => AssertReadable(text.Foreground));
            AssertReadable(Assert.IsType<TextBox>(editor.FindName("NameBox")).Foreground);
            var lineTypes = Assert.IsType<ComboBox>(editor.FindName("DashStyleBox"));
            lineTypes.ApplyTemplate();
            AssertReadable(lineTypes.Foreground);
            Assert.All(lineTypes.Items.Cast<ComboBoxItem>(), item => AssertReadable(item.Foreground));
            var lineTypeOptions = LayoutDropDown(lineTypes);
            Assert.All(Descendants<TextBlock>(lineTypeOptions), text => AssertReadable(text.Foreground));
            var states = Assert.IsType<ComboBox>(editor.FindName("StyleStateBox"));
            Assert.All(Descendants<TextBlock>(LayoutDropDown(states)), text => AssertReadable(text.Foreground));
            var picker = Assert.IsType<Xceed.Wpf.Toolkit.ColorPicker>(editor.FindName("StrokeColorPicker"));
            AssertReadable(picker.Foreground);
            var popup = Descendants<Popup>(picker).Single();
            var palette = Assert.IsAssignableFrom<FrameworkElement>(popup.Child);
            Layout(palette, new Size(300, 400));
            Snapshot(palette, "color-palette");
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LayerEditor_HoverFillEditsRefreshShapesAndSurviveSaving(bool hasHoverStyle)
    {
        RunOnSta(() =>
        {
            var vm = CreateViewModel();
            var layer = vm.Layers[0];
            if (hasHoverStyle)
            {
                var definition = layer.ToShapeLayerParameter();
                definition.StyleSchema[ShapeVisualState.MouseOver] = new ShapeStylerParameter
                {
                    FillColor = Brushes.Gold, FillOpacity = 0.2, StrokeColor = Brushes.Green,
                    StrokeThickness = 3, DashStyle = "Dash", DragHandleSize = 7
                };
                vm.UpdateLayerConfiguration(definition);
            }
            var circle = new Circle(vm.ShapeRepository.CurrentShapeLayer!);
            circle.FromData(new EllipseData { Center = new Point(100, 100), RadiusX = 50 });
            vm.ShapeRepository.AddShape(circle);
            var board = new HoverBoard { SketchBoardDataManager = (SketchBoardDataManager)vm.ShapeRepository };
            board.Hover(new Point(150, 100));
            Assert.Equal(ShapeVisualState.MouseOver, circle.State);
            var editor = new LayerEditorWindow(layer);
            var states = Assert.IsType<ComboBox>(editor.FindName("StyleStateBox"));
            var fill = Assert.IsType<Xceed.Wpf.Toolkit.ColorPicker>(editor.FindName("FillColorPicker"));
            var opacity = Assert.IsType<TextBox>(editor.FindName("FillOpacityBox"));
            states.SelectedItem = states.Items.Cast<ComboBoxItem>().Single(x => Equals(x.Tag, "MouseOver"));
            Assert.Equal(hasHoverStyle ? Colors.Gold : Colors.Transparent, fill.SelectedColor);
            fill.SelectedColor = Colors.MediumPurple;
            opacity.Text = "0.45";
            var content = Assert.IsType<Grid>(editor.Content);
            Layout(content, new Size(358, 500));
            Snapshot(content, "hover-layer-editor");
            states.SelectedIndex = 0;
            Assert.Equal(Colors.Transparent, fill.SelectedColor);
            Assert.Equal(hasHoverStyle, layer.Stylers.ContainsKey(ShapeVisualState.MouseOver));
            Assert.Equal(hasHoverStyle ? Colors.Gold : Colors.Transparent,
                Assert.IsType<SolidColorBrush>(layer.GetStyler(ShapeVisualState.MouseOver).FillColor).Color);
            var path = Path.GetTempFileName();
            try
            {
                vm.SaveLayerConfiguration(path);
                vm.UpdateLayerConfiguration(editor.EditedParameter);
                Assert.Equal(ShapeVisualState.MouseOver, circle.State);
                var hover = circle.ShapeStyler!;
                Assert.Equal(Colors.MediumPurple, Assert.IsType<SolidColorBrush>(hover.FillColor).Color);
                Assert.Equal(0.45, hover.FillColor.Opacity);
                Assert.Equal(hasHoverStyle ? 3 : 1, hover.SketchPen.Thickness);
                Assert.Equal(hasHoverStyle ? 7 : 10, hover.DragHandleSize);
                Assert.Equal(Colors.Transparent,
                    Assert.IsType<SolidColorBrush>(layer.GetStyler(ShapeVisualState.Normal).FillColor).Color);
                var loaded = new ShapeLayerManager();
                loaded.ReadConfiguration(path);
                var restored = loaded.Layers[0].GetStyler(ShapeVisualState.MouseOver);
                Assert.Equal(Colors.MediumPurple, Assert.IsType<SolidColorBrush>(restored.FillColor).Color);
                Assert.Equal(0.45, restored.FillColor.Opacity);
            }
            finally { File.Delete(path); }
        });
    }

    [Fact]
    public void LayerEditor_ViewingMissingStateKeepsNormalFallback()
    {
        RunOnSta(() =>
        {
            var editor = new LayerEditorWindow(TestShapeLayer.Create());
            var states = Assert.IsType<ComboBox>(editor.FindName("StyleStateBox"));
            states.SelectedItem = states.Items.Cast<ComboBoxItem>().Single(x => Equals(x.Tag, "MouseOver"));
            states.SelectedIndex = 0;
            Assert.False(editor.EditedParameter.StyleSchema.ContainsKey(ShapeVisualState.MouseOver));
        });
    }

    [Fact]
    public void ShapeCheckboxes_ToggleSharedSelectionAndDisableLockedShapes()
    {
        RunOnSta(() =>
        {
            var vm = CreateViewModel();
            var first = AddLine(vm, 50);
            var second = AddLine(vm, 100);
            vm.ShapeRepository.SetSelection(new[] { first, second });
            var control = new ImageViewerControl { DataContext = vm };
            Layout(control);
            var tree = Assert.IsType<TreeView>(control.FindName("LayerTree"));
            Assert.IsType<TreeViewItem>(tree.ItemContainerGenerator.ContainerFromIndex(0)).IsExpanded = true;
            Layout(control);
            var checkbox = Descendants<CheckBox>(tree).Single(x => ReferenceEquals(x.Tag, first));
            Assert.True(checkbox.IsChecked);
            checkbox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Same(second, Assert.Single(vm.SelectedShapes));
            Assert.False(checkbox.IsChecked);
            checkbox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal(2, vm.SelectedShapes.Count);
            Assert.True(checkbox.IsChecked);
            first.Lock();
            Assert.False(checkbox.IsEnabled);
        });
    }

    [Fact]
    public void SmallViewer_KeepsLayerPanelWithinViewportAndScrollsAllControls()
    {
        RunOnSta(() =>
        {
            var vm = CreateViewModel();
            vm.SelectedShape = AddLine(vm, 50);
            var control = new ImageViewerControl { DataContext = vm };
            AddHostTextStyles(control.Resources);
            Layout(control, new Size(800, 450));
            var tree = Assert.IsType<TreeView>(control.FindName("LayerTree"));
            Assert.IsType<TreeViewItem>(tree.ItemContainerGenerator.ContainerFromIndex(0)).IsExpanded = true;
            Layout(control, new Size(800, 450));
            var panel = Assert.IsType<Border>(control.FindName("LayerPanel"));
            Assert.InRange(panel.ActualHeight, 1, 380);
            var scroll = Assert.IsType<ScrollViewer>(panel.Child);
            Assert.True(scroll.ExtentHeight > scroll.ViewportHeight);
            scroll.ScrollToBottom();
            Layout(control, new Size(800, 450));
            Assert.True(scroll.VerticalOffset > 0);
            var selector = Assert.IsType<ComboBox>(control.FindName("AssignedLayerSelector"));
            AssertReadable(selector.Foreground);
            Assert.All(Descendants<TextBlock>(LayoutDropDown(selector)), text => AssertReadable(text.Foreground));
            Snapshot(control, "small-layer-viewer");
        });
    }

    private static Line AddLine(ImageViewerControlViewModel vm, double y)
    {
        var line = new Line(vm.ShapeRepository.CurrentShapeLayer!);
        line.FromData(new PointsData(line.ShapeLayer.LayerId, new List<Point> { new(40, y), new(100, y) }));
        vm.ShapeRepository.AddShape(line);
        return line;
    }

    private sealed class HoverBoard : SketchBoard
    {
        public void Hover(Point position)
            => HandleMouseMove(position, System.Windows.Input.MouseButtonState.Released);
    }

    private static ImageViewerControlViewModel CreateViewModel()
    {
        var layers = new ShapeLayerManager();
        layers.Layers.Add(TestShapeLayer.Create());
        var second = TestShapeLayer.Create().ToShapeLayerParameter();
        second.LayerId = 2;
        second.Name = "Second layer";
        layers.Layers.Add(new ShapeLayer(second));
        var types = new GeometryTypeManager();
        types.RegisterGeometryType<Line>();
        return new ImageViewerControlViewModel(layers, new SketchBoardDataManager(), types);
    }

    private static void AddHostTextStyles(ResourceDictionary resources)
    {
        foreach (var type in new[] { typeof(TextBlock), typeof(TreeViewItem), typeof(Button) })
        {
            var style = new Style(type);
            style.Setters.Add(new Setter(type == typeof(TextBlock)
                ? TextBlock.ForegroundProperty : Control.ForegroundProperty, Brushes.Black));
            resources[type] = style;
        }
    }

    private static void AssertReadable(Brush brush)
    {
        var color = Assert.IsType<SolidColorBrush>(brush).Color;
        Assert.True(color.R >= 140 && color.G >= 140 && color.B >= 140,
            $"Expected light text on the dark surface, got {color}.");
    }

    private static void Layout(FrameworkElement element, Size? size = null)
    {
        var bounds = size ?? new Size(1400, 900);
        element.Measure(bounds);
        element.Arrange(new Rect(bounds));
        element.UpdateLayout();
    }

    private static FrameworkElement LayoutDropDown(ComboBox selector)
    {
        selector.ApplyTemplate();
        var popup = Assert.IsType<Popup>(selector.Template.FindName("PART_Popup", selector));
        var menu = Assert.IsAssignableFrom<FrameworkElement>(popup.Child);
        Layout(menu, new Size(selector.ActualWidth, 200));
        Assert.NotEmpty(Descendants<TextBlock>(menu));
        return menu;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    internal static void Snapshot(FrameworkElement element, string name)
    {
        var directory = Environment.GetEnvironmentVariable("LAN_SHAPES_UI_SNAPSHOT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight,
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, name + ".png"));
        encoder.Save(file);
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
