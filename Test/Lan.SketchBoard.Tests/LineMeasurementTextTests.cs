using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes.Enums;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class LineMeasurementTextTests
{
    [Fact]
    public void CompletedUnselectedLine_ShowsLengthAndAngleAndUpdatesAfterMovingEndpoint()
    {
        RunOnSta(() =>
        {
            var line = new Line(TestShapeLayer.Create());
            line.FromData(new PointsData(1, new List<Point>
            {
                new(10, 10), new(30, 30)
            }));

            line.State = ShapeVisualState.Selected;
            line.State = ShapeVisualState.Normal;
            line.OnMouseLeftButtonUp(line.End);

            var label = ReadText(VisualTreeHelper.GetDrawing(line));
            Assert.Contains("px", label);
            Assert.Contains("45°", label);
            Assert.Equal(15, ReadFontSize(VisualTreeHelper.GetDrawing(line)));

            line.ShapeLayer.Stylers[ShapeVisualState.Normal].DragHandleSize = 5;
            line.RefreshScaleDependentVisuals(2);
            Assert.Equal(7.5, ReadFontSize(VisualTreeHelper.GetDrawing(line)));

            line.End = new Point(30, 10);
            Assert.Contains("0°", ReadText(VisualTreeHelper.GetDrawing(line)));

            line.End = new Point(10, 30);
            Assert.Contains("90°", ReadText(VisualTreeHelper.GetDrawing(line)));
        });
    }

    private static string ReadText(Drawing? drawing)
    {
        if (drawing is GlyphRunDrawing glyphs)
            return new string(glyphs.GlyphRun.Characters.ToArray());
        if (drawing is DrawingGroup group)
            return string.Concat(group.Children.Select(ReadText));
        return string.Empty;
    }

    private static double ReadFontSize(Drawing? drawing)
    {
        if (drawing is GlyphRunDrawing glyphs)
            return glyphs.GlyphRun.FontRenderingEmSize;
        if (drawing is DrawingGroup group)
            return group.Children.Select(ReadFontSize).FirstOrDefault(size => size > 0);
        return 0;
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
