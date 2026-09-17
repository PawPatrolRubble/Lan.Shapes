using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Lan.ImageViewer;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ImageViewerPanTests
{
    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(2)]
    public void MiddleDrag_PansOnFirstGestureInScreenCoordinatesWithoutChangingZoom(double scale)
    {
        RunOnSta(() =>
        {
            var viewer = CreateViewer();
            viewer.Matrix = new Matrix(scale, 0, 0, scale, 7, 11);

            Assert.True(viewer.Press(new Point(40, 60), MouseButton.Middle));
            Assert.True(viewer.Move(new Point(50, 80), middle: MouseButtonState.Pressed));
            Assert.True(viewer.Move(new Point(70, 90), middle: MouseButtonState.Pressed));
            Assert.True(viewer.Release(MouseButton.Middle));

            Assert.Equal(new Matrix(scale, 0, 0, scale, 37, 41), viewer.Matrix);
            Assert.False(viewer.Move(new Point(100, 120)));
            Assert.Equal(new Matrix(scale, 0, 0, scale, 37, 41), viewer.Matrix);
        });
    }

    [Fact]
    public void ControlLeftDrag_UsesSamePanBehavior()
    {
        RunOnSta(() =>
        {
            var viewer = CreateViewer();
            Assert.True(viewer.Press(new Point(10, 20), MouseButton.Left, ModifierKeys.Control));
            Assert.True(viewer.Move(new Point(35, 50), left: MouseButtonState.Pressed));
            Assert.True(viewer.Release(MouseButton.Left));

            Assert.Equal(25, viewer.Matrix.OffsetX);
            Assert.Equal(30, viewer.Matrix.OffsetY);
        });
    }

    [Theory]
    [InlineData(MouseButton.Left, ModifierKeys.None)]
    [InlineData(MouseButton.Left, ModifierKeys.Alt)]
    [InlineData(MouseButton.Right, ModifierKeys.None)]
    public void OtherGestures_DoNotPan(MouseButton button, ModifierKeys modifiers)
    {
        RunOnSta(() =>
        {
            var viewer = CreateViewer();
            Assert.False(viewer.Press(new Point(10, 20), button, modifiers));
            Assert.False(viewer.Move(new Point(35, 50), MouseButtonState.Pressed, MouseButtonState.Pressed));
            Assert.Equal(Matrix.Identity, viewer.Matrix);
        });
    }

    [Fact]
    public void ReleasingAnotherButton_DoesNotInterruptMiddlePan()
    {
        RunOnSta(() =>
        {
            var viewer = CreateViewer();
            viewer.Press(new Point(10, 20), MouseButton.Middle);
            // A second press must not replace the middle button's gesture origin.
            viewer.Press(new Point(100, 100), MouseButton.Left, ModifierKeys.Control);
            Assert.False(viewer.Release(MouseButton.Left));
            Assert.True(viewer.Move(new Point(35, 50), middle: MouseButtonState.Pressed));

            Assert.Equal(25, viewer.Matrix.OffsetX);
            Assert.Equal(30, viewer.Matrix.OffsetY);
            Assert.True(viewer.Release(MouseButton.Middle));
        });
    }

    [Fact]
    public void LostCapture_StopsPanAndNextGestureStartsAtNewPosition()
    {
        RunOnSta(() =>
        {
            var viewer = CreateViewer();
            viewer.Press(new Point(10, 20), MouseButton.Middle);
            viewer.LoseCapture(viewer);
            Assert.False(viewer.Move(new Point(100, 100), middle: MouseButtonState.Pressed));
            viewer.Press(new Point(100, 100), MouseButton.Middle);
            Assert.True(viewer.Move(new Point(105, 110), middle: MouseButtonState.Pressed));

            Assert.Equal(5, viewer.Matrix.OffsetX);
            Assert.Equal(10, viewer.Matrix.OffsetY);
        });
    }

    [Fact]
    public void ChildLostCapture_DoesNotCancelViewerPan()
    {
        RunOnSta(() =>
        {
            var viewer = CreateViewer();
            viewer.Press(new Point(10, 20), MouseButton.Middle);
            viewer.LoseCapture(viewer.Canvas);

            Assert.True(viewer.Move(new Point(35, 50), middle: MouseButtonState.Pressed));
            Assert.Equal(25, viewer.Matrix.OffsetX);
            Assert.Equal(30, viewer.Matrix.OffsetY);
        });
    }

    [Fact]
    public void MissingMouseUp_StopsPanWhenStartingButtonIsNoLongerPressed()
    {
        RunOnSta(() =>
        {
            var viewer = CreateViewer();
            viewer.Press(new Point(10, 20), MouseButton.Middle);

            Assert.False(viewer.Move(new Point(35, 50), left: MouseButtonState.Pressed));
            Assert.False(viewer.Move(new Point(45, 60), middle: MouseButtonState.Pressed));
            Assert.Equal(Matrix.Identity, viewer.Matrix);
        });
    }

    [Fact]
    public void PreviewMiddleDown_IsConsumedBeforeReachingCanvas()
    {
        RunOnSta(() =>
        {
            var viewer = CreateViewer();
            var canvasDownCount = 0;
            viewer.Canvas.PreviewMouseDown += (_, _) => canvasDownCount++;
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Middle)
            {
                RoutedEvent = Mouse.PreviewMouseDownEvent
            };

            viewer.Canvas.RaiseEvent(args);

            Assert.True(args.Handled);
            Assert.Equal(0, canvasDownCount);
            viewer.Release(MouseButton.Middle);
        });
    }

    private static TestViewer CreateViewer()
    {
        var viewer = new TestViewer
        {
            Width = 320,
            Height = 240,
            Template = (ControlTemplate)XamlReader.Parse(@"
                <ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                                 xmlns:viewer='clr-namespace:Lan.ImageViewer;assembly=Lan.ImageViewer'
                                 TargetType='{x:Type viewer:ImageViewerBasic}'>
                    <Border x:Name='BorderContainer' Padding='15'>
                        <Canvas x:Name='containerCanvas' Background='Transparent'>
                            <Grid x:Name='GridContainer'>
                                <Image x:Name='ImageViewer'/>
                            </Grid>
                        </Canvas>
                    </Border>
                </ControlTemplate>")
        };
        viewer.ApplyTemplate();
        viewer.Measure(new Size(320, 240));
        viewer.Arrange(new Rect(0, 0, 320, 240));
        return viewer;
    }

    private sealed class TestViewer : ImageViewerBasic
    {
        public Canvas Canvas => (Canvas)GetTemplateChild("containerCanvas");
        private MatrixTransform ViewportTransform => (MatrixTransform)
            ((TransformGroup)((Grid)GetTemplateChild("GridContainer")).RenderTransform).Children[0];
        public Matrix Matrix { get => ViewportTransform.Matrix; set => ViewportTransform.Matrix = value; }
        public bool Press(Point point, MouseButton button, ModifierKeys modifiers = ModifierKeys.None)
            => HandlePanMouseDown(point, button, modifiers);
        public bool Move(Point point, MouseButtonState left = MouseButtonState.Released,
            MouseButtonState middle = MouseButtonState.Released) => HandlePanMouseMove(point, left, middle);
        public bool Release(MouseButton button) => HandlePanMouseUp(button);
        public void LoseCapture(object source) => OnLostMouseCapture(new MouseEventArgs(Mouse.PrimaryDevice, 0)
        {
            RoutedEvent = Mouse.LostMouseCaptureEvent,
            Source = source
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
