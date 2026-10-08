using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class BuiltinShapeContractTests
{
    [Theory]
    [InlineData("Line")]
    [InlineData("Rectangle")]
    [InlineData("Circle")]
    [InlineData("Ellipse")]
    [InlineData("Cross")]
    [InlineData("Polygon")]
    [InlineData("Rectangle2")]
    [InlineData("Angle")]
    [InlineData("RulerCross")]
    public void AddedText_IsDrawnOnceAndClearTextRemovesIt(string shapeType)
    {
        RunOnSta(() =>
        {
            var shape = CreateCompletedShape(shapeType);
            const string text = "contractlabel";
            shape.AddText(text, new Point(40, 45));
            Assert.Equal(1, CountOccurrences(ReadText(VisualTreeHelper.GetDrawing(shape)), text));

            shape.UpdateVisual();
            shape.State = ShapeVisualState.Selected;
            Assert.Equal(1, CountOccurrences(ReadText(VisualTreeHelper.GetDrawing(shape)), text));

            shape.ClearText();
            Assert.DoesNotContain(text, ReadText(VisualTreeHelper.GetDrawing(shape)));
        });
    }

    [Fact]
    public void PolygonDragging_PreservesSelectedHandlesAndReselectingRemainsUsable()
    {
        RunOnSta(() =>
        {
            var manager = new SketchBoardDataManager();
            manager.SetShapeLayer(TestShapeLayer.Create());
            var polygon = Assert.IsType<Polygon>(CreateCompletedShape("Polygon"));
            manager.AddShape(polygon);
            manager.SelectedGeometry = polygon;
            var vertex = polygon.GetMetaData().DataPoints[0];
            var movedVertex = vertex + new Vector(5, 5);

            polygon.OnMouseLeftButtonDown(vertex);
            polygon.OnMouseMove(movedVertex, MouseButtonState.Pressed);
            polygon.OnMouseLeftButtonUp(movedVertex);

            Assert.True(polygon.IsSelected);
            Assert.Equal(ShapeVisualState.Selected, polygon.State);
            Assert.True(polygon.HasDragHandleAt(movedVertex));
            manager.SelectedGeometry = polygon;
            Assert.True(polygon.HasDragHandleAt(movedVertex));
            Assert.False(polygon.IsBeingDraggedOrPanMoving);
        });
    }

    [Fact]
    public void PolygonMouseUp_LeavesStagedCreationOpenUntilItIsExplicitlyClosed()
    {
        RunOnSta(() =>
        {
            var polygon = new Polygon(TestShapeLayer.Create());
            polygon.OnMouseLeftButtonDown(new Point(10, 10));
            polygon.OnMouseLeftButtonUp(new Point(10, 10));
            Assert.False(polygon.IsGeometryRendered);
            polygon.OnMouseLeftButtonDown(new Point(100, 10));
            polygon.OnMouseLeftButtonUp(new Point(100, 10));
            Assert.False(polygon.IsGeometryRendered);

            polygon.OnMouseRightButtonUp(new Point(100, 10));
            Assert.True(polygon.IsGeometryRendered);
            Assert.Equal(2, polygon.GetMetaData().DataPoints.Count);
        });
    }

    [Fact]
    public void DirectRectangleImport_RemovesCreationHandlesWithoutAnotherRedraw()
    {
        RunOnSta(() =>
        {
            var rectangle = new Rectangle(TestShapeLayer.Create());
            rectangle.FromData(new PointsData(1, new List<Point> { new(10, 10), new(110, 90) }));

            Assert.True(rectangle.IsGeometryRendered);
            Assert.False(rectangle.HasDragHandleAt(rectangle.TopLeft));
            Assert.DoesNotContain(FlattenDrawing(VisualTreeHelper.GetDrawing(rectangle))
                .OfType<GeometryDrawing>(), drawing => drawing.Geometry is RectangleGeometry);
        });
    }

    [Fact]
    public void IncompleteLineScaleRefresh_UpdatesVisibleMeasurementText()
    {
        RunOnSta(() =>
        {
            var line = new Line(TestShapeLayer.Create());
            line.OnMouseLeftButtonDown(new Point(10, 10));
            line.OnMouseMove(new Point(110, 50), MouseButtonState.Pressed);
            Assert.False(line.IsGeometryRendered);
            Assert.Equal(15, ReadFontSize(VisualTreeHelper.GetDrawing(line)));

            line.ShapeLayer.Stylers[ShapeVisualState.Normal].DragHandleSize = 5;
            line.RefreshScaleDependentVisuals(2);

            Assert.Equal(7.5, ReadFontSize(VisualTreeHelper.GetDrawing(line)));
        });
    }

    [Theory]
    [InlineData("Line")]
    [InlineData("Rectangle")]
    [InlineData("Circle")]
    [InlineData("Ellipse")]
    [InlineData("Cross")]
    [InlineData("Polygon")]
    [InlineData("Rectangle2")]
    [InlineData("Angle")]
    [InlineData("RulerCross")]
    public void DirectLockedInteraction_DoesNotUnlockOrMoveGeometry(string shapeType)
    {
        RunOnSta(() =>
        {
            var shape = CreateCompletedShape(shapeType);
            shape.State = ShapeVisualState.Selected;
            shape.Lock();
            var originalBounds = shape.RenderGeometry.Bounds;
            var press = new Point(50, 50);

            shape.OnMouseLeftButtonDown(press);
            shape.OnMouseMove(press + new Vector(20, 20), MouseButtonState.Released);
            shape.OnMouseMove(press + new Vector(30, 30), MouseButtonState.Pressed);
            shape.OnMouseLeftButtonUp(press + new Vector(30, 30));

            Assert.True(shape.IsLocked);
            Assert.Equal(ShapeVisualState.Locked, shape.State);
            Assert.Equal(originalBounds, shape.RenderGeometry.Bounds);
            Assert.False(shape.IsBeingDraggedOrPanMoving);
        });
    }

    [Fact]
    public void EllipseBounds_FollowItsModelGeometry()
    {
        RunOnSta(() =>
        {
            var ellipse = Assert.IsType<Ellipse>(CreateCompletedShape("Ellipse"));
            Assert.Equal(new Rect(10, 20, 80, 60), ellipse.BoundsRect);
            Assert.True(ellipse.MatchesSelectionRectangle(new Rect(0, 0, 100, 100), crossing: false));

            ellipse.Center = new Point(70, 80);
            ellipse.RadiusX = 20;
            ellipse.RadiusY = 10;
            var drawing = Assert.Single(FlattenDrawing(VisualTreeHelper.GetDrawing(ellipse)).OfType<GeometryDrawing>());
            Assert.Equal(new Rect(50, 70, 40, 20), drawing.Geometry.Bounds);
        });
    }

    [Theory]
    [InlineData("Line")]
    [InlineData("Rectangle")]
    [InlineData("Circle")]
    [InlineData("Ellipse")]
    [InlineData("Cross")]
    [InlineData("Polygon")]
    [InlineData("Rectangle2")]
    [InlineData("Angle")]
    [InlineData("RulerCross")]
    public void CancelledInteraction_DoesNotResumeWithoutAnotherMouseDown(string shapeType)
    {
        RunOnSta(() =>
        {
            var shape = CreateCompletedShape(shapeType);
            shape.State = ShapeVisualState.Selected;
            shape.OnMouseLeftButtonDown(new Point(50, 50));
            shape.OnMouseMove(new Point(55, 55), MouseButtonState.Pressed);
            shape.CancelInteraction();
            var boundsAfterCancellation = shape.RenderGeometry.Bounds;

            shape.OnMouseMove(new Point(50, 50), MouseButtonState.Released);
            shape.OnMouseMove(new Point(70, 70), MouseButtonState.Pressed);
            shape.OnMouseMove(new Point(90, 90), MouseButtonState.Pressed);

            Assert.Equal(boundsAfterCancellation, shape.RenderGeometry.Bounds);
            Assert.False(shape.IsBeingDraggedOrPanMoving);
        });
    }

    [Theory]
    [InlineData("Line")]
    [InlineData("Rectangle")]
    [InlineData("Circle")]
    [InlineData("Ellipse")]
    [InlineData("Cross")]
    [InlineData("Polygon")]
    [InlineData("Rectangle2")]
    [InlineData("Angle")]
    [InlineData("RulerCross")]
    public void CancelledCreationGesture_DoesNotResumeWithoutAnotherMouseDown(string shapeType)
    {
        RunOnSta(() =>
        {
            var shape = CreateIncompleteShape(shapeType);
            if (shape is RulerCross ruler) ruler.OnBoardContextAvailable(200, 150);
            shape.OnMouseLeftButtonDown(new Point(50, 50));
            shape.OnMouseMove(new Point(90, 80), MouseButtonState.Pressed);
            shape.CancelInteraction();
            var boundsAfterCancellation = shape.RenderGeometry.Bounds;

            shape.OnMouseMove(new Point(120, 120), MouseButtonState.Pressed);
            shape.OnMouseMove(new Point(140, 130), MouseButtonState.Pressed);

            Assert.Equal(boundsAfterCancellation, shape.RenderGeometry.Bounds);
            Assert.False(shape.IsGeometryRendered);
            Assert.False(shape.IsBeingDraggedOrPanMoving);
        });
    }

    [Fact]
    public void CancellingStagedPointerTracking_PreservesPolygonVerticesAndAngleClickStage()
    {
        RunOnSta(() =>
        {
            var first = new Point(10, 10);
            var second = new Point(100, 80);
            var polygon = new Polygon(TestShapeLayer.Create());
            polygon.OnMouseLeftButtonDown(first);
            polygon.CancelInteraction();
            polygon.OnMouseLeftButtonDown(second);
            Assert.Equal(new[] { first, second }, polygon.GetMetaData().DataPoints);
            Assert.False(polygon.IsGeometryRendered);

            var angle = new Angle(TestShapeLayer.Create());
            angle.OnMouseLeftButtonDown(first);
            angle.CancelInteraction();
            angle.OnMouseLeftButtonDown(second);
            Assert.Equal(first, angle.FirstPoint);
            Assert.Equal(second, angle.Vertex);
            Assert.False(angle.IsGeometryRendered);
        });
    }

    private static ShapeVisualBase CreateIncompleteShape(string shapeType)
    {
        var layer = TestShapeLayer.Create();
        return shapeType switch
        {
            "Line" => new Line(layer),
            "Rectangle" => new Rectangle(layer),
            "Circle" => new Circle(layer),
            "Ellipse" => new Ellipse(layer),
            "Cross" => new Cross(layer),
            "Polygon" => new Polygon(layer),
            "Rectangle2" => new Rectangle2(layer),
            "Angle" => new Angle(layer),
            "RulerCross" => new RulerCross(layer),
            _ => throw new ArgumentOutOfRangeException(nameof(shapeType))
        };
    }

    private static ShapeVisualBase CreateCompletedShape(string shapeType)
    {
        var layer = TestShapeLayer.Create();
        var points = new PointsData(1, new List<Point> { new(10, 10), new(100, 100) });
        var ellipseData = new EllipseData { Center = new Point(50, 50), RadiusX = 40, RadiusY = 30 };
        switch (shapeType)
        {
            case "Line":
                var line = new Line(layer);
                line.FromData(points);
                return line;
            case "Rectangle":
                var rectangle = new Rectangle(layer);
                rectangle.FromData(points);
                return rectangle;
            case "Circle":
                var circle = new Circle(layer);
                circle.FromData(ellipseData);
                return circle;
            case "Ellipse":
                var ellipse = new Ellipse(layer);
                ellipse.FromData(ellipseData);
                return ellipse;
            case "Cross":
                var cross = new Cross(layer);
                cross.FromData(new CrossData { Center = new Point(50, 50), Width = 80, Height = 60 });
                return cross;
            case "Polygon":
                var polygon = new Polygon(layer);
                polygon.FromData(new PointsData(1, new List<Point>
                {
                    new(10, 10), new(100, 10), new(100, 100), new(10, 100)
                }));
                return polygon;
            case "Rectangle2":
                var rectangle2 = new Rectangle2(layer);
                rectangle2.FromData(new Rectangle2Data { Row = 50, Column = 50, Length1 = 40, Length2 = 30 });
                return rectangle2;
            case "Angle":
                var angle = new Angle(layer);
                angle.FromData(new PointsData(1, new List<Point> { new(10, 50), new(50, 50), new(50, 10) }));
                return angle;
            case "RulerCross":
                var ruler = new RulerCross(layer);
                ruler.FromData(new RulerCrossData { Center = new Point(50, 50), Width = 100, Height = 100 });
                return ruler;
            default:
                throw new ArgumentOutOfRangeException(nameof(shapeType));
        }
    }

    private static IEnumerable<Drawing> FlattenDrawing(Drawing? drawing)
    {
        if (drawing == null) yield break;
        if (drawing is DrawingGroup group)
        {
            foreach (var child in group.Children.SelectMany(FlattenDrawing)) yield return child;
        }
        else yield return drawing;
    }

    private static string ReadText(Drawing? drawing) => string.Concat(FlattenDrawing(drawing)
        .OfType<GlyphRunDrawing>().Select(glyphs => new string(glyphs.GlyphRun.Characters.ToArray())));

    private static double ReadFontSize(Drawing? drawing) => FlattenDrawing(drawing)
        .OfType<GlyphRunDrawing>().Select(glyphs => glyphs.GlyphRun.FontRenderingEmSize).FirstOrDefault();

    private static int CountOccurrences(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty).Length) / value.Length;

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
