using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class HistoryViewerTests
{
    [Fact]
    public void HistoryCommands_RestoreMultiSelectionDeletionInOneStep_AndNotifyAvailability()
    {
        var (vm, board) = Viewer();
        using (vm)
        {
            Assert.False(vm.UndoCommand.CanExecute(null));
            Assert.False(vm.RedoCommand.CanExecute(null));
            var undoNotifications = 0;
            var redoNotifications = 0;
            vm.UndoCommand.CanExecuteChanged += (_, _) => undoNotifications++;
            vm.RedoCommand.CanExecuteChanged += (_, _) => redoNotifications++;
            var first = AddLine(board, 50);
            var second = AddLine(board, 100);
            board.SetSelection(new[] { first, second });

            vm.DeleteShapeCommand.Execute(null);
            Assert.Empty(board.Shapes);
            Assert.True(vm.UndoCommand.CanExecute(null));
            vm.UndoCommand.Execute(null);
            Assert.Equal(new[] { first, second }, board.Shapes);
            Assert.False(vm.UndoCommand.CanExecute(null));
            Assert.True(vm.RedoCommand.CanExecute(null));
            vm.RedoCommand.Execute(null);
            Assert.Empty(board.Shapes);
            Assert.False(vm.RedoCommand.CanExecute(null));
            Assert.True(undoNotifications > 0);
            Assert.True(redoNotifications > 0);
        }
    }

    [Fact]
    public void ToolbarAndTreeShortcuts_UseHistoryCommandsAndTrackAvailability()
        => BulkLayerFeatureTests.Sta(() =>
        {
            var (vm, board) = Viewer();
            using (vm)
            {
                var shape = AddLine(board, 50);
                board.SetSelection(new[] { shape });
                vm.DeleteShapeCommand.Execute(null);
                var control = new ImageViewerControl { DataContext = vm };
                Layout(control);
                var undo = Assert.IsType<Button>(control.FindName("UndoButton"));
                var redo = Assert.IsType<Button>(control.FindName("RedoButton"));
                Assert.Same(vm.UndoCommand, undo.Command);
                Assert.Same(vm.RedoCommand, redo.Command);
                Assert.True(undo.IsEnabled);
                Assert.False(redo.IsEnabled);

                Assert.True(TreeKey(control, Key.Z, ModifierKeys.Control));
                Assert.Same(shape, Assert.Single(board.Shapes));
                Layout(control);
                Assert.True(redo.IsEnabled);
                Assert.True(TreeKey(control, Key.Y, ModifierKeys.Control));
                Assert.Empty(board.Shapes);
                Assert.True(TreeKey(control, Key.Z, ModifierKeys.Control));
                Assert.True(TreeKey(control, Key.Z, ModifierKeys.Control | ModifierKeys.Shift));
                Assert.Empty(board.Shapes);
                Assert.False(TreeKey(control, Key.Z, ModifierKeys.Control | ModifierKeys.Alt));
            }
        });

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

    private static bool TreeKey(ImageViewerControl control, Key key, ModifierKeys modifiers)
        => Assert.IsType<bool>(typeof(ImageViewerControl)
            .GetMethod("HandleTreeSelectionKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, new object[] { key, modifiers }));

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(1600, 1000));
        element.Arrange(new Rect(0, 0, 1600, 1000));
        element.UpdateLayout();
    }
}
