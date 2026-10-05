using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Lan.SketchBoard;

namespace Lan.Shapes.InteractionProbe;

/// <summary>
/// Interactive checks that require an HWND and an accessible desktop input stream.
/// Run with a physical click on the button; these checks deliberately stay outside CI.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var application = new Application();
        var host = new Border { Background = Brushes.White, Height = 240 };
        var captureTarget = new Button { Content = "Capture target", Width = 140, Height = 28 };
        var results = new TextBox
        {
            Text = "Click Run capture checks to exercise WPF capture release and transfer.",
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 180,
            Margin = new Thickness(0, 8, 0, 8)
        };
        var run = new Button { Content = "Run capture checks", Height = 32 };
        var content = new StackPanel { Margin = new Thickness(12) };
        content.Children.Add(host);
        content.Children.Add(captureTarget);
        content.Children.Add(results);
        content.Children.Add(run);
        var window = new Window
        {
            Title = "Lan.Shapes native capture probe",
            Content = content,
            Width = 580,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        run.Click += (_, _) =>
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            var lines = new List<string> { $"HWND DPI: {dpi.PixelsPerInchX} x {dpi.PixelsPerInchY}" };
            var passed = 0;
            foreach (var creating in new[] { false, true })
            foreach (var transfer in new[] { false, true })
            {
                var label = $"{(creating ? "Creating" : "Editing")} / {(transfer ? "Transfer" : "Release")}";
                try
                {
                    CheckCaptureLoss(host, captureTarget, creating, transfer);
                    lines.Add($"PASS: {label}: capture acquired; cancellation blocks stale move/up; new down resumes.");
                    passed++;
                }
                catch (Exception exception)
                {
                    lines.Add($"FAIL: {label}: {exception.Message}");
                }
            }
            lines.Add($"Result: {passed}/4 passed");
            results.Text = string.Join(Environment.NewLine, lines);
        };
        application.Run(window);
    }

    private static void CheckCaptureLoss(Border host, Button captureTarget, bool creating, bool transfer)
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(CreateLayer());
        var board = new ProbeBoard { SketchBoardDataManager = manager };
        host.Child = board;
        host.UpdateLayout();
        var completions = 0;
        manager.NewShapeSketched += (_, _) => completions++;
        var start = new Point(100, 100);

        try
        {
            ShapeVisualBase shape;
            if (creating)
            {
                manager.SetGeometryType(typeof(Line));
                board.Press(start);
                shape = manager.CurrentGeometryInEdit ?? throw new InvalidOperationException("Creation did not start.");
            }
            else
            {
                var cross = new Cross(manager.CurrentShapeLayer!);
                cross.FromData(new CrossData { Center = start, Width = 80, Height = 60 });
                manager.AddShape(cross);
                shape = cross;
                board.Press(start);
                Require(ReferenceEquals(manager.SelectedGeometry, shape), "Shape was not selected.");
            }

            Require(board.CaptureMouse() && ReferenceEquals(Mouse.Captured, board), "Board could not acquire native capture.");
            board.MovePressed(start + new Vector(20, 0));
            if (!creating) Require(shape.IsBeingDraggedOrPanMoving, "Editing did not start.");

            if (transfer)
                Require(captureTarget.CaptureMouse() && ReferenceEquals(Mouse.Captured, captureTarget), "Capture transfer failed.");
            else
                board.ReleaseMouseCapture();

            Require(!board.IsMouseCaptured, "Board retained capture.");
            Require(board.LostCaptureCount == 1, "WPF did not deliver exactly one capture-loss event to the board.");
            Require(!shape.IsBeingDraggedOrPanMoving, "Capture loss retained the drag flag.");
            var bounds = shape.BoundsRect;
            board.MovePressed(start + new Vector(50, 30));
            board.Release(start + new Vector(50, 30));
            Require(shape.BoundsRect == bounds, "Stale move/up changed the shape after cancellation.");
            Require(shape.IsGeometryRendered == !creating, "Stale release completed creation.");
            Require(completions == 0, "Stale release raised a completion event.");

            if (captureTarget.IsMouseCaptured) captureTarget.ReleaseMouseCapture();
            board.Press(creating ? start : start + new Vector(20, 0));
            board.MovePressed(start + new Vector(60, 30));
            board.Release(start + new Vector(60, 30));
            Require(shape.BoundsRect != bounds, "Fresh mouse down did not resume editing.");
            Require(shape.IsGeometryRendered, "Fresh gesture did not complete.");
            Require(completions == (creating ? 1 : 0), "Unexpected completion event count.");
            Require(!shape.IsBeingDraggedOrPanMoving, "Fresh release retained the drag flag.");
        }
        finally
        {
            if (board.IsMouseCaptured) board.ReleaseMouseCapture();
            if (captureTarget.IsMouseCaptured) captureTarget.ReleaseMouseCapture();
            host.Child = null;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static ShapeLayer CreateLayer()
    {
        var schema = new Dictionary<ShapeVisualState, ShapeStylerParameter>();
        foreach (var state in new[] { ShapeVisualState.Normal, ShapeVisualState.Selected })
            schema[state] = new ShapeStylerParameter
            {
                FillColor = Brushes.Transparent,
                FillOpacity = 0,
                StrokeColor = state == ShapeVisualState.Selected ? Brushes.Blue : Brushes.Red,
                StrokeThickness = 1,
                DashStyle = "Solid",
                DragHandleSize = 10
            };
        return new ShapeLayer(new ShapeLayerParameter
        {
            LayerId = 1,
            Name = "Native capture probe",
            StyleSchema = schema
        });
    }

    private sealed class ProbeBoard : global::Lan.SketchBoard.SketchBoard
    {
        public int LostCaptureCount { get; private set; }

        public void Press(Point point) => HandleLeftButtonDown(point, 1);
        public void MovePressed(Point point) => HandleMouseMove(point, MouseButtonState.Pressed);
        public void Release(Point point) => HandleLeftButtonUp(point);

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            if (ReferenceEquals(e.OriginalSource, this)) LostCaptureCount++;
            base.OnLostMouseCapture(e);
        }
    }
}
