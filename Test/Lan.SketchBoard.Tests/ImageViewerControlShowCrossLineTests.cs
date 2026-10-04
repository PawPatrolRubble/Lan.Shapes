using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Shapes;
using Lan.SketchBoard;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ImageViewerControlShowCrossLineTests
{
    [Fact]
    public void ShowCrossLine_DefaultsToTrue()
    {
        RunOnSta(() =>
        {
            var control = new ImageViewerControl();
            Assert.True(control.ShowCrossLine);

            var viewer = Assert.IsType<Lan.ImageViewer.ImageViewer>(control.FindName("ImageViewer"));
            Assert.True(viewer.ShowCrossLine);
        });
    }

    [Fact]
    public void ShowCrossLine_ChangingControlProperty_UpdatesInnerViewer()
    {
        RunOnSta(() =>
        {
            var control = new ImageViewerControl();
            var viewer = Assert.IsType<Lan.ImageViewer.ImageViewer>(control.FindName("ImageViewer"));

            control.ShowCrossLine = false;
            Assert.False(control.ShowCrossLine);
            Assert.False(viewer.ShowCrossLine);

            control.ShowCrossLine = true;
            Assert.True(control.ShowCrossLine);
            Assert.True(viewer.ShowCrossLine);
        });
    }

    [Fact]
    public void ShowCrossLine_SyncsWithViewModelBidirectionally()
    {
        RunOnSta(() =>
        {
            var control = new ImageViewerControl();
            var viewer = Assert.IsType<Lan.ImageViewer.ImageViewer>(control.FindName("ImageViewer"));
            var vm = CreateViewModel();

            control.DataContext = vm;

            // When VM changes ShowCrossLine, control and inner viewer update
            vm.ShowCrossLine = false;
            Assert.False(control.ShowCrossLine);
            Assert.False(viewer.ShowCrossLine);

            vm.ShowCrossLine = true;
            Assert.True(control.ShowCrossLine);
            Assert.True(viewer.ShowCrossLine);

            // When control changes ShowCrossLine, VM and inner viewer update
            control.ShowCrossLine = false;
            Assert.False(vm.ShowCrossLine);
            Assert.False(viewer.ShowCrossLine);
        });
    }

    [Fact]
    public void ShowCrossLine_ExplicitlySetOnControl_RetainedWhenDataContextAssigned()
    {
        RunOnSta(() =>
        {
            var control = new ImageViewerControl
            {
                ShowCrossLine = false
            };
            var viewer = Assert.IsType<Lan.ImageViewer.ImageViewer>(control.FindName("ImageViewer"));
            var vm = CreateViewModel();
            Assert.True(vm.ShowCrossLine); // VM defaults to true

            control.DataContext = vm;

            // Control retains false, and VM is synchronized to false
            Assert.False(control.ShowCrossLine);
            Assert.False(vm.ShowCrossLine);
            Assert.False(viewer.ShowCrossLine);
        });
    }

    [Fact]
    public void CrossLineToggle_OnTheToolbar_TogglesTheCrosshairBothWays()
    {
        RunOnSta(() =>
        {
            var vm = CreateViewModel();
            var control = new ImageViewerControl { DataContext = vm };
            Layout(control, new Size(800, 450));
            var viewer = Assert.IsType<Lan.ImageViewer.ImageViewer>(control.FindName("ImageViewer"));
            var toggle = Assert.IsType<ToggleButton>(control.FindName("BtnToggleCrossLine"));

            Assert.True(toggle.IsChecked);
            Assert.True(viewer.ShowCrossLine);

            // The user clicks the new button: view-model, control and inner viewer follow.
            toggle.IsChecked = false;
            Assert.False(vm.ShowCrossLine);
            Assert.False(control.ShowCrossLine);
            Assert.False(viewer.ShowCrossLine);

            // A host or view-model change keeps the button in sync.
            vm.ShowCrossLine = true;
            Assert.True(toggle.IsChecked);
            Assert.True(viewer.ShowCrossLine);

            control.ShowCrossLine = false;
            Assert.False(toggle.IsChecked);
            Assert.False(vm.ShowCrossLine);
            Assert.False(viewer.ShowCrossLine);
        });
    }

    [Fact]
    public void CrossLineToggle_ShowsACrosshairIconWithHintAndAccessibleName()
    {
        RunOnSta(() =>
        {
            var control = new ImageViewerControl { DataContext = CreateViewModel() };
            Layout(control, new Size(800, 450));
            var toggle = Assert.IsType<ToggleButton>(control.FindName("BtnToggleCrossLine"));

            Assert.False(string.IsNullOrWhiteSpace(toggle.ToolTip as string));
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(toggle)));

            var icon = Assert.IsType<System.Windows.Shapes.Path>(toggle.Content);
            Assert.NotNull(icon.Data);
            Assert.False(icon.Data.Bounds.IsEmpty);
        });
    }

    private static void Layout(FrameworkElement element, Size size)
    {
        element.Measure(size);
        element.Arrange(new Rect(size));
        element.UpdateLayout();
    }

    private static ImageViewerControlViewModel CreateViewModel()
    {
        var layer = TestShapeLayer.Create();
        var layerManager = new ShapeLayerManager();
        layerManager.Layers.Add(layer);

        var geometryTypeManager = new GeometryTypeManager();
        geometryTypeManager.RegisterGeometryType<Line>();

        var manager = new SketchBoardDataManager();
        return new ImageViewerControlViewModel(layerManager, manager, geometryTypeManager);
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
