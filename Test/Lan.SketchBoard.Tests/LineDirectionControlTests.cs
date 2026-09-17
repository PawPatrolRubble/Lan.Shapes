using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Lan.ImageViewer;
using Lan.Shapes.Enums;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class LineDirectionControlTests
{
    [Theory]
    [InlineData("ImageViewer.Compact")]
    [InlineData("ImageViewer.Wide")]
    public void ViewerTemplate_ForwardsDirectionChangesToBoard(string styleName)
    {
        RunOnSta(() =>
        {
            var resources = new ResourceDictionary
            {
                Source = new Uri("/Lan.ImageViewer;component/Style.xaml", UriKind.Relative)
            };
            var viewer = new Lan.ImageViewer.ImageViewer
            {
                Style = (Style)resources[styleName],
                LineDirectionMode = LineDirectionMode.Vertical
            };
            viewer.ApplyTemplate();
            var board = Assert.IsType<SketchBoard>(viewer.Template.FindName("SketchBoard", viewer));
            Assert.Equal(LineDirectionMode.Vertical, board.LineDirectionMode);
            viewer.LineDirectionMode = LineDirectionMode.Horizontal;
            Assert.Equal(LineDirectionMode.Horizontal, board.LineDirectionMode);
            viewer.LineDirectionMode = LineDirectionMode.Free;
            Assert.Equal(LineDirectionMode.Free, board.LineDirectionMode);
        });
    }

    [Fact]
    public void ToolbarSelector_ChangesBoardModeAndReflectsExternalChanges()
    {
        RunOnSta(() =>
        {
            var control = new ImageViewerControl();
            var selector = Assert.IsType<ComboBox>(control.FindName("LineDirectionSelector"));
            var viewer = Assert.IsType<Lan.ImageViewer.ImageViewer>(control.FindName("ImageViewer"));
            viewer.ApplyTemplate();
            var board = Assert.IsType<SketchBoard>(viewer.Template.FindName("SketchBoard", viewer));
            Assert.Equal(LineDirectionMode.Free, selector.SelectedValue);
            Assert.Equal(LineDirectionMode.Free, board.LineDirectionMode);

            selector.SelectedValue = LineDirectionMode.Horizontal;
            Assert.Equal(LineDirectionMode.Horizontal, control.LineDirectionMode);
            Assert.Equal(LineDirectionMode.Horizontal, board.LineDirectionMode);
            selector.SelectedValue = LineDirectionMode.Vertical;
            Assert.Equal(LineDirectionMode.Vertical, board.LineDirectionMode);

            control.LineDirectionMode = LineDirectionMode.Free;
            Assert.Equal(LineDirectionMode.Free, selector.SelectedValue);
            Assert.Equal(LineDirectionMode.Free, board.LineDirectionMode);
        });
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
