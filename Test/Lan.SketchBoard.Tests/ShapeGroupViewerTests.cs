using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Custom;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeGroupViewerTests
{
    [Fact]
    public void GroupCommand_FollowsSelectionAndMemberLocks()
    {
        var (vm, board) = Viewer();
        using (vm)
        {
            var first = AddLine(board, 50);
            var second = AddLine(board, 100);
            var command = vm.GroupShapesCommand;
            Assert.False(command.CanExecute(null));
            board.SetSelection(new[] { first, second });
            Assert.True(command.CanExecute(null));
            Assert.True(vm.CanGroupSelectedShapes);
            second.Lock();
            Assert.False(command.CanExecute(null));
            second.UnLock();
            Assert.True(command.CanExecute(null));
            board.SelectedGeometry = first;
            Assert.False(command.CanExecute(null));
        }
    }

    [Fact]
    public void GroupCommand_DisablesForMixedLayersAndExplainsTheRequirement()
    {
        var (vm, board) = Viewer();
        using (vm)
        {
            var first = AddLine(board, 50);
            var target = BulkLayerFeatureTests.Layer(2, "Second");
            vm.Layers.Add(target);
            board.SetShapeLayer(target);
            var second = AddLine(board, 100);
            board.SetSelection(new[] { first, second });
            Assert.False(vm.GroupShapesCommand.CanExecute(null));
            Assert.Contains("同一图层", vm.GroupingPrompt);
        }
    }

    [Fact]
    public void GroupAndUngroupCommands_ExposeTheSelectedGroupAndKeepGeometrySelection()
    {
        var (vm, board) = Viewer();
        using (vm)
        {
            var first = AddLine(board, 50);
            var second = AddLine(board, 100);
            board.SetSelection(new[] { first, second });
            vm.GroupShapesCommand.Execute(null);
            var selectedGroup = vm.SelectedGroup;
            Assert.NotNull(selectedGroup);
            Assert.Contains(selectedGroup.Name, vm.SelectedGroupSummary);
            Assert.Contains("2", vm.SelectedGroupSummary);
            Assert.False(vm.GroupShapesCommand.CanExecute(null));
            Assert.True(vm.UngroupShapesCommand.CanExecute(null));
            Assert.Null(vm.PropertyShape);
            vm.UngroupShapesCommand.Execute(null);
            Assert.Empty(board.Groups);
            Assert.Equal(new[] { first, second }, vm.SelectedShapes);
            Assert.True(vm.GroupShapesCommand.CanExecute(null));
            Assert.False(vm.UngroupShapesCommand.CanExecute(null));
        }
    }

    [Fact]
    public void LockedGroupPrompts_ExplainHowToUnlockBeforeMoving()
    {
        var (vm, board) = Viewer();
        using (vm)
        {
            var first = AddLine(board, 50);
            var second = AddLine(board, 100);
            board.SetSelection(new[] { first, second });
            vm.GroupShapesCommand.Execute(null);
            second.Lock();
            const string expected = "组合包含锁定图形，请双击任一成员解锁后再移动。";
            Assert.Equal(expected, vm.GroupingPrompt);
            Assert.Contains(expected, vm.SelectionPrompt);
            Assert.True(vm.UngroupShapesCommand.CanExecute(null));
        }
    }

    [Fact]
    public void GroupCapabilityChange_RefreshesPromptsAndOffersUngrouping()
    {
        var (vm, board) = Viewer();
        using (vm)
        {
            var line = AddLine(board, 50);
            var fiber = (Fiber)board.LoadShape<Fiber, FiberData>(new FiberData
            {
                FilletCenter = new Point(150, 150), FilletRadius = 2,
                Width = 20, Height = 40, EnableTranslation = true
            });
            board.SetSelection(new ShapeVisualBase[] { line, fiber });
            vm.GroupShapesCommand.Execute(null);
            var notifications = new List<string?>();
            ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
            var data = fiber.GetMetaData();
            data.EnableTranslation = false;
            fiber.FromData(data);
            Assert.Contains("不支持整体移动", vm.GroupingPrompt);
            Assert.Contains("取消组合", vm.SelectionPrompt);
            Assert.Contains(nameof(ImageViewerControlViewModel.GroupingPrompt), notifications);
            Assert.True(vm.UngroupShapesCommand.CanExecute(null));
        }
    }

    [Fact]
    public void RepositoryGroupChanges_RefreshCommandAvailabilityWithoutChangingSelection()
    {
        var (vm, board) = Viewer();
        using (vm)
        {
            var first = AddLine(board, 50);
            var second = AddLine(board, 100);
            board.SetSelection(new[] { first, second });
            var command = vm.UngroupShapesCommand;
            var changes = 0;
            command.CanExecuteChanged += (_, _) => changes++;
            board.GroupShapes(new[] { first, second });
            Assert.True(command.CanExecute(null));
            Assert.True(changes > 0);
        }
    }

    [Fact]
    public void Dispose_DetachesGroupChangeNotifications()
    {
        var (vm, board) = Viewer();
        var first = AddLine(board, 50);
        var second = AddLine(board, 100);
        board.SetSelection(new[] { first, second });
        var command = vm.GroupShapesCommand;
        vm.Dispose();
        var changes = 0;
        command.CanExecuteChanged += (_, _) => changes++;
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, _) => changes++;
        board.GroupShapes(new[] { first, second });
        Assert.Equal(0, changes);
    }

    [Fact]
    public void TreeControlClick_RemovesEveryGroupMemberFromSelection()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var (vm, board) = Viewer();
            using (vm)
            {
                var first = AddLine(board, 50);
                var second = AddLine(board, 100);
                var independent = AddLine(board, 150);
                board.SetSelection(new[] { first, second });
                vm.GroupShapesCommand.Execute(null);
                board.SetSelection(new[] { first, second, independent });
                var control = new ImageViewerControl { DataContext = vm };
                SelectTree(control, first, ModifierKeys.Control);
                Assert.Same(independent, Assert.Single(board.SelectedGeometries));
                SelectTree(control, second, ModifierKeys.Control);
                Assert.Equal(3, board.SelectedGeometries.Count);
            }
        });
    }

    [Fact]
    public void TreeRangeSelection_ExpandsGroupedMembersOutsideTheRange()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var (vm, board) = Viewer();
            using (vm)
            {
                var first = AddLine(board, 50);
                var middle = AddLine(board, 100);
                var last = AddLine(board, 150);
                board.SetSelection(new[] { first, last });
                vm.GroupShapesCommand.Execute(null);
                var control = new ImageViewerControl { DataContext = vm };
                Layout(control);
                var tree = (TreeView)control.FindName("LayerTree");
                ((TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0)).IsExpanded = true;
                Layout(control);
                SelectTree(control, middle);
                SelectTree(control, last, ModifierKeys.Shift);
                Assert.Contains(first, board.SelectedGeometries);
                Assert.Contains(middle, board.SelectedGeometries);
                Assert.Contains(last, board.SelectedGeometries);
            }
        });
    }

    [Fact]
    public void GroupSelection_DeletesTheWholeGroupThroughTheExistingCommand()
    {
        var (vm, board) = Viewer();
        using (vm)
        {
            var first = AddLine(board, 50);
            var second = AddLine(board, 100);
            var independent = AddLine(board, 150);
            board.SetSelection(new[] { first, second });
            vm.GroupShapesCommand.Execute(null);
            vm.SelectedShape = second;
            Assert.Null(vm.PropertyShape);
            vm.DeleteShapeCommand.Execute(null);
            Assert.Same(independent, Assert.Single(board.Shapes));
            Assert.Empty(board.Groups);
        }
    }

    [Fact]
    public void GroupButtonsAndTreeShortcuts_UseTheSameCommands()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var (vm, board) = Viewer();
            using (vm)
            {
                var first = AddLine(board, 50);
                var second = AddLine(board, 100);
                board.SetSelection(new[] { first, second });
                var control = new ImageViewerControl { DataContext = vm };
                Layout(control);
                var groupButton = Assert.IsType<Button>(control.FindName("GroupShapesButton"));
                var ungroupButton = Assert.IsType<Button>(control.FindName("UngroupShapesButton"));
                Assert.Same(vm.GroupShapesCommand, groupButton.Command);
                Assert.Same(vm.UngroupShapesCommand, ungroupButton.Command);
                Assert.True(groupButton.IsEnabled);
                Assert.False(ungroupButton.IsEnabled);
                Assert.True(TreeKey(control, Key.G, ModifierKeys.Control));
                Assert.Single(board.Groups);
                Layout(control);
                Assert.False(groupButton.IsEnabled);
                Assert.True(ungroupButton.IsEnabled);
                var summary = Assert.IsType<TextBlock>(control.FindName("SelectedGroupSummaryText"));
                Assert.Equal(vm.SelectedGroupSummary, summary.Text);
                SaveScreenshot(control);
                Assert.True(TreeKey(control, Key.G, ModifierKeys.Control | ModifierKeys.Shift));
                Assert.Empty(board.Groups);
            }
        });
    }

    private static (ImageViewerControlViewModel Viewer, SketchBoardDataManager Board) Viewer()
    {
        var layers = new ShapeLayerManager();
        layers.Layers.Add(BulkLayerFeatureTests.Layer(1, "First"));
        var types = new GeometryTypeManager();
        types.RegisterGeometryType<Line>();
        var board = new SketchBoardDataManager();
        return (new ImageViewerControlViewModel(layers, board, types), board);
    }

    private static Line AddLine(SketchBoardDataManager board, double y)
        => (Line)board.LoadShape<Line, PointsData>(new PointsData(1,
            new List<Point> { new(40, y), new(100, y) }));

    private static void SelectTree(ImageViewerControl control, ShapeVisualBase shape,
        ModifierKeys modifiers = ModifierKeys.None)
        => typeof(ImageViewerControl).GetMethod("SelectTreeShape", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, new object[] { shape, modifiers });

    private static bool TreeKey(ImageViewerControl control, Key key, ModifierKeys modifiers)
    {
        var method = typeof(ImageViewerControl).GetMethod("HandleTreeSelectionKey", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(control, new object[] { key, modifiers }));
    }

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(1400, 1000));
        element.Arrange(new Rect(0, 0, 1400, 1000));
        element.UpdateLayout();
    }

    private static void SaveScreenshot(FrameworkElement element)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "src", "Lan.ImageViewer")))
            root = root.Parent;
        Assert.NotNull(root);
        var directory = Path.Combine(root.FullName, "TestResults", "ShapeGroup");
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight,
            96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, "group-viewer.png"));
        encoder.Save(file);
    }
}
