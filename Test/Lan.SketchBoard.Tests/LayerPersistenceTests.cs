using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Shapes;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Lan.SketchBoard.Tests;

/// <summary>
/// Covers the app-level contract "edit a layer, press 保存, restart the app": the loaded
/// configuration file must contain the edit and reload into an equivalent layer definition.
/// </summary>
public class LayerPersistenceTests
{
    [Fact]
    public void EditingALayerThroughTheLayerPanelSurvivesAnApplicationRestart()
    {
        RunOnSta(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "LanShapesLayerPersistence",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "LanShapesConfig.json");
            try
            {
                SeedConfiguration(path, out var originalThickness);

                // Application startup (Lan.Shapes.TestApp / ImageViewerModule both do this).
                var manager = new ShapeLayerManager();
                manager.ReadConfiguration(path);
                var board = new SketchBoardDataManager();
                var types = new GeometryTypeManager();
                types.RegisterGeometryType<Line>();
                var vm = new ImageViewerControlViewModel(manager, board, types);
                var control = new ImageViewerControl { DataContext = vm };
                Layout(control, new Size(800, 450));

                var shape = new Line(board.CurrentShapeLayer!) { Start = new Point(20, 30), End = new Point(180, 30) };
                board.AddShape(shape);

                // "编辑" on the layer row: same construction as ImageViewerControl.EditLayer_Click.
                var node = vm.LayerGroups.Single(x => x.LayerId == 2);
                var editor = new LayerEditorWindow(node.Layer, false, vm.UpdateLayerConfiguration);
                Assert.Equal("layer2", Assert.IsType<TextBox>(editor.FindName("NameBox")).Text);
                Assert.IsType<TextBox>(editor.FindName("NameBox")).Text = "重命名图层";
                Assert.IsType<TextBox>(editor.FindName("StrokeThicknessBox")).Text = "5";
                ApplyEditor(editor);
                Assert.Equal("重命名图层", manager.Layers.Single(x => x.LayerId == 2).Name);

                // Panel "保存": known configuration path, so no file dialog is involved.
                ClickButton(control, "保存");

                // Next launch: a fresh manager reads the same file.
                var reloaded = new ShapeLayerManager();
                reloaded.ReadConfiguration(path);
                var restored = reloaded.Layers.Single(x => x.LayerId == 2);
                Assert.Equal("重命名图层", restored.Name);
                Assert.Equal(5, restored.ToShapeLayerParameter().StyleSchema[ShapeVisualState.Normal].StrokeThickness);

                var json = JToken.Parse(File.ReadAllText(path));
                var persisted = json["ShapeLayers"]!.Children().Single(x => (int)x["LayerId"]! == 2);
                Assert.Equal("重命名图层", (string)persisted["Name"]!);
                Assert.Equal(5.0, (double)persisted["StyleSchema"]!["Normal"]!["StrokeThickness"]!);
                Assert.Equal(originalThickness,
                    (double)json["ShapeLayers"]!.Children().Single(x => (int)x["LayerId"]! == 1)
                        ["StyleSchema"]!["Normal"]!["StrokeThickness"]!);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        });
    }

    private static void SeedConfiguration(string path, out double thickness)
    {
        var manager = new ShapeLayerManager();
        manager.Layers.Add(TestShapeLayer.CreateWithThickness(stroke: 2, handle: 8));
        var second = TestShapeLayer.CreateWithThickness(stroke: 3, handle: 8).ToShapeLayerParameter();
        second.LayerId = 2;
        second.Name = "layer2";
        manager.Layers.Add(new ShapeLayer(second));
        thickness = 2;
        manager.SaveConfiguration(path);
    }

    private static void ApplyEditor(Window editor)
    {
        // An unshown Window has no realized visual tree; walk its content instead.
        var apply = Descendants<Button>((DependencyObject)editor.Content)
            .Single(button => Equals(button.Content, "应用"));
        try
        {
            apply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        }
        catch (InvalidOperationException)
        {
            // DialogResult can only be set by a window shown with ShowDialog; the apply
            // already ran before the dialog result is assigned.
        }
    }

    private static void ClickButton(DependencyObject root, string content)
    {
        var button = Descendants<Button>(root).Single(x => Equals(x.Content, content));
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

    private static void Layout(FrameworkElement element, Size size)
    {
        element.Measure(size);
        element.Arrange(new Rect(size));
        element.UpdateLayout();
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
