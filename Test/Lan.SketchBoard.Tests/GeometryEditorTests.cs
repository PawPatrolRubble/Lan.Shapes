using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Lan.ImageViewer.ViewModels.GeometryEditing;
using Lan.Shapes;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class GeometryEditorTests
{
    [Theory]
    [InlineData("line")]
    [InlineData("rectangle")]
    [InlineData("circle")]
    public void PixelParameters_DisplayIntegersWithoutRoundingTheModel(string shapeType)
        => Sta(() =>
        {
            var board = Board();
            ShapeVisualBase shape;
            string[] expectedFields;
            string[] expectedReadouts;
            switch (shapeType)
            {
                case "line":
                    shape = board.LoadShape<Line, PointsData>(new PointsData(1,
                        new List<Point> { new(-10.25, -20.75), new(19.75, 19.25) }));
                    expectedFields = new[] { "-10", "-21", "20", "19", "50" };
                    expectedReadouts = Array.Empty<string>();
                    break;
                case "rectangle":
                    shape = board.LoadShape<Rectangle, PointsData>(new PointsData(1,
                        new List<Point> { new(10.25, 20.75), new(110.65, 71.05) }));
                    expectedFields = new[] { "10", "21", "100", "50" };
                    expectedReadouts = new[] { "X 111   Y 71" };
                    break;
                default:
                    shape = board.LoadShape<Circle, EllipseData>(new EllipseData
                        { Center = new Point(50.25, 60.75), RadiusX = 10.3, RadiusY = 10.3 });
                    expectedFields = new[] { "50", "61", "10" };
                    expectedReadouts = new[] { "21" };
                    break;
            }
            board.SelectedGeometry = shape;
            var originalPoints = shape.GetSnapPoints().ToArray();
            var originalBounds = shape.RenderGeometry.Bounds;
            using var editor = Create(shape, board);

            Assert.Equal(expectedFields, editor.Groups.SelectMany(group => group.Fields).Select(field => field.Text));
            Assert.Equal(expectedReadouts, editor.Readouts.Select(readout => readout.Value));
            foreach (var group in editor.Groups) Assert.True(editor.Commit(group));
            Assert.Equal(originalPoints, shape.GetSnapPoints());
            Assert.Equal(originalBounds, shape.RenderGeometry.Bounds);
        });

    [Fact]
    public void TinyDiagonalLengthEdit_PreservesItsUnitDirection()
        => Sta(() =>
        {
            var (board, line) = LineBoard();
            line.Start = new Point();
            line.End = new Point(double.Epsilon, double.Epsilon);
            using var editor = Create(line, board);
            editor.Groups[2].Fields[0].Text = "1";
            Assert.True(editor.Commit(editor.Groups[2]));
            Assert.Equal(1 / Math.Sqrt(2), line.End.X, 12);
            Assert.Equal(1 / Math.Sqrt(2), line.End.Y, 12);
        });

    [Theory]
    [InlineData(1e16, 100, 1)]
    [InlineData(9007199254740992, 2, 9007199254740991)]
    public void UnrepresentableRectangleMove_DoesNotChangePositionOrDimensions(double x, double width, double target)
        => Sta(() =>
        {
            var board = Board();
            var rectangle = (Rectangle)board.LoadShape<Rectangle, PointsData>(new PointsData(1,
                new List<Point> { new(x, 20), new(x + width, 70) }));
            board.SelectedGeometry = rectangle;
            using var editor = Create(rectangle, board);
            editor.Groups[0].Fields[0].Text = target.ToString("R", CultureInfo.CurrentCulture);
            Assert.False(editor.Commit(editor.Groups[0]));
            Assert.Equal(new Point(x, 20), rectangle.TopLeft);
            Assert.Equal(width, rectangle.Width);
        });

    [Fact]
    public void UnrepresentableCircleMove_DoesNotSilentlyChangeRequestedCenter()
        => Sta(() =>
        {
            var board = Board();
            var circle = (Circle)board.LoadShape<Circle, EllipseData>(new EllipseData
                { Center = new Point(1e16, 20), RadiusX = 10, RadiusY = 10 });
            board.SelectedGeometry = circle;
            using var editor = Create(circle, board);
            editor.Groups[0].Fields[0].Text = "1";
            Assert.False(editor.Commit(editor.Groups[0]));
            Assert.Equal(new Point(1e16, 20), circle.Center);
        });

    [Fact]
    public void ChangingIndependentTransformBackToIdentity_ReenablesGeometryInputs()
        => Sta(() =>
        {
            var (board, line) = LineBoard();
            var transform = new ScaleTransform(2, 2);
            line.Transform = transform;
            using var editor = Create(line, board);
            Assert.False(editor.CanEdit);
            transform.ScaleX = 1;
            transform.ScaleY = 1;
            Pump();
            Assert.True(editor.CanEdit);
            Assert.True(editor.Groups[0].IsEnabled);
        });

    [Fact]
    public void LengthCommit_FixesStartAndDirectionAndRefreshesEnd()
        => Sta(() =>
        {
            var (board, line) = LineBoard();
            using var editor = Create(line, board);
            var length = editor.Groups[2];
            length.Fields[0].Text = "100";
            Assert.Equal(new Point(40, 60), line.End);
            Assert.True(editor.Commit(length));
            Assert.Equal(new Point(10, 20), line.Start);
            Assert.Equal(new Point(70, 100), line.End);
            Assert.Equal("70", editor.Groups[1].Fields[0].Text);
        });

    [Fact]
    public void EndpointGroup_CommitsBothCoordinatesAndKeepsOtherEndpoint()
        => Sta(() =>
        {
            var (board, line) = LineBoard();
            using var editor = Create(line, board);
            var group = editor.Groups[0];
            group.Fields[0].Text = "-10.125";
            group.Fields[1].Text = "25.5";
            Assert.True(editor.Commit(group));
            Assert.Equal(new Point(-10.125, 25.5), line.Start);
            Assert.Equal(new Point(40, 60), line.End);
            Assert.Equal("-10", group.Fields[0].Text);
            Assert.Equal("26", group.Fields[1].Text);
        });

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e999")]
    public void InvalidCoordinate_DoesNotPartiallyCommitTheValidCoordinate(string invalid)
        => Sta(() =>
        {
            var (board, line) = LineBoard();
            using var editor = Create(line, board);
            var group = editor.Groups[0];
            group.Fields[0].Text = "25";
            group.Fields[1].Text = invalid;
            Assert.False(editor.Commit(group));
            Assert.Equal(new Point(10, 20), line.Start);
            Assert.True(group.HasError);
            Assert.Equal(invalid, group.Fields[1].Text);
        });

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("NaN")]
    public void InvalidLength_PreservesTheLine(string text)
        => Sta(() =>
        {
            var (board, line) = LineBoard();
            using var editor = Create(line, board);
            var length = editor.Groups[2];
            length.Fields[0].Text = text;
            Assert.False(editor.Commit(length));
            Assert.Equal(new Point(40, 60), line.End);
        });

    [Fact]
    public void RectanglePosition_PreservesSizeAndMovesAttachedText()
        => Sta(() =>
        {
            var board = Board();
            var rectangle = (Rectangle)board.LoadShape<Rectangle, PointsData>(new PointsData(1,
                new List<Point> { new(110, 70), new(10, 20) }));
            rectangle.AddText("label", new Point(30, 40));
            board.SelectedGeometry = rectangle;
            using var editor = Create(rectangle, board);
            Assert.Equal("10", editor.Groups[0].Fields[0].Text);
            var position = editor.Groups[0];
            position.Fields[0].Text = "30";
            position.Fields[1].Text = "40";
            Assert.True(editor.Commit(position));
            Assert.Equal(new Point(30, 40), rectangle.GetMetaData().DataPoints[0]);
            Assert.Equal(100, rectangle.Width);
            Assert.Equal(50, rectangle.Height);
            Assert.Equal(new Point(50, 60), AttachedText(rectangle).Location);
        });

    [Fact]
    public void RectangleSize_FixesNormalizedTopLeftOfReverseCorners()
        => Sta(() =>
        {
            var board = Board();
            var rectangle = (Rectangle)board.LoadShape<Rectangle, PointsData>(new PointsData(1,
                new List<Point> { new(110, 70), new(10, 20) }));
            board.SelectedGeometry = rectangle;
            using var editor = Create(rectangle, board);
            var size = editor.Groups[1];
            size.Fields[0].Text = "80";
            size.Fields[1].Text = "120";
            Assert.True(editor.Commit(size));
            Assert.Equal(new[] { new Point(10, 20), new Point(90, 140) }, rectangle.GetMetaData().DataPoints);
        });

    [Fact]
    public void CirclePositionAndRadius_HaveIndependentSemanticsAndRefreshDiameter()
        => Sta(() =>
        {
            var board = Board();
            var circle = (Circle)board.LoadShape<Circle, EllipseData>(new EllipseData
                { Center = new Point(50, 60), RadiusX = 10, RadiusY = 10 });
            circle.AddText("label", new Point(52, 62));
            board.SelectedGeometry = circle;
            using var editor = Create(circle, board);
            var center = editor.Groups[0];
            center.Fields[0].Text = "70";
            center.Fields[1].Text = "80";
            Assert.True(editor.Commit(center));
            Assert.Equal(10, circle.Radius);
            Assert.Equal(new Point(72, 82), AttachedText(circle).Location);
            var radius = editor.Groups[1];
            radius.Fields[0].Text = "25";
            Assert.True(editor.Commit(radius));
            Assert.Equal(new Point(70, 80), circle.Center);
            Assert.Equal("50", editor.Readouts[0].Value);
        });

    [Fact]
    public void CommitOneCoordinate_PreservesUneditedCoordinatePrecision()
        => Sta(() =>
        {
            var (board, line) = LineBoard();
            line.Start = new Point(10.1234567890123, 20.9876543210987);
            using var editor = Create(line, board);
            var start = editor.Groups[0];
            Assert.Equal("10", start.Fields[0].Text);
            Assert.Equal("21", start.Fields[1].Text);
            start.Fields[0].Text = "11";
            Assert.True(editor.Commit(start));
            Assert.Equal(20.9876543210987, line.Start.Y);
        });

    [Fact]
    public void ResetGroup_RestoresModelAndInvalidatesQueuedCommitVersion()
        => Sta(() =>
        {
            var (board, line) = LineBoard();
            using var editor = Create(line, board);
            var start = editor.Groups[0];
            start.Fields[0].Text = "99";
            var revision = start.Revision;
            editor.Reset(start);
            Assert.Equal("10", start.Fields[0].Text);
            Assert.True(start.Revision > revision);
            Assert.False(start.IsDirty);
        });

    [Fact]
    public void ExternalGeometryChange_DiscardsOldDraftAndReadsFinalState()
        => Sta(() =>
        {
            var (board, line) = LineBoard();
            using var editor = Create(line, board);
            editor.Groups[0].Fields[0].Text = "999";
            line.Start = new Point(5, 6);
            line.End = new Point(35, 46);
            Pump();
            Assert.Equal("5", editor.Groups[0].Fields[0].Text);
            Assert.Equal("35", editor.Groups[1].Fields[0].Text);
            Assert.Contains("图形已更新", editor.StatusMessage);
            Assert.Equal(new Point(5, 6), line.Start);
        });

    [Fact]
    public void OwnCommit_DoesNotReportExternalConflictWhenNotificationsArePumped()
        => Sta(() =>
        {
            var (board, line) = LineBoard();
            using var editor = Create(line, board);
            editor.Groups[2].Fields[0].Text = "100";
            Assert.True(editor.Commit(editor.Groups[2]));
            Pump();
            Assert.Equal(string.Empty, editor.StatusMessage);
        });

    [Theory]
    [InlineData("lock")]
    [InlineData("hidden")]
    [InlineData("remove")]
    [InlineData("selection")]
    [InlineData("transform")]
    public void EligibilityChanges_RejectPendingCommit(string change)
        => Sta(() =>
        {
            var (board, line) = LineBoard();
            using var editor = Create(line, board);
            var start = editor.Groups[0];
            start.Fields[0].Text = "99";
            switch (change)
            {
                case "lock": line.Lock(); break;
                case "hidden": board.SetLayerVisibility(line.ShapeLayer.LayerId, false); break;
                case "remove": board.RemoveShape(line); break;
                case "selection": board.UnselectGeometry(); break;
                case "transform": line.Transform = new ScaleTransform(2, 2); break;
            }
            Assert.False(editor.Commit(start));
            Assert.Equal(new Point(10, 20), line.Start);
        });

    [Fact]
    public void Dispose_RejectsOldDraftAndDetachesModelNotifications()
        => Sta(() =>
        {
            var (board, line) = LineBoard();
            using var editor = Create(line, board);
            var start = editor.Groups[0];
            start.Fields[0].Text = "99";
            editor.Dispose();
            line.Start = new Point(3, 4);
            Pump();
            Assert.False(editor.Commit(start));
            Assert.Equal(new Point(3, 4), line.Start);
        });

    [Fact]
    public void DegenerateLoadedLine_CanRecoverThroughEndpointsBeforeLengthEditing()
        => Sta(() =>
        {
            var (board, line) = LineBoard();
            line.End = line.Start;
            using var editor = Create(line, board);
            Assert.False(editor.Groups[2].IsEnabled);
            editor.Groups[1].Fields[0].Text = "30";
            Assert.True(editor.Commit(editor.Groups[1]));
            Assert.True(editor.Groups[2].IsEnabled);
        });

    [Fact]
    public void CurrentCultureParsing_AcceptsDecimalCommaWithoutRoundingUneditedValues()
        => Sta(() =>
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var (board, line) = LineBoard();
            using var editor = Create(line, board);
            editor.Groups[0].Fields[0].Text = "12,5";
            Assert.True(editor.Commit(editor.Groups[0]));
            Assert.Equal(12.5, line.Start.X);
        });

    private static ShapeGeometryEditor Create(ShapeVisualBase shape, IShapeRepository repository)
        => Assert.IsAssignableFrom<ShapeGeometryEditor>(ShapeGeometryEditor.Create(shape, repository));
    private static (SketchBoardDataManager Board, Line Line) LineBoard()
    {
        var board = Board();
        var line = (Line)board.LoadShape<Line, PointsData>(new PointsData(1,
            new List<Point> { new(10, 20), new(40, 60) }));
        board.SelectedGeometry = line;
        return (board, line);
    }

    private static SketchBoardDataManager Board()
    {
        var board = new SketchBoardDataManager();
        board.SetShapeLayer(TestShapeLayer.Create());
        return board;
    }

    private static (Point Location, string Content) AttachedText(ShapeVisualBase shape)
        => Assert.Single((IReadOnlyList<(Point Location, string Content)>)typeof(ShapeVisualBase)
            .GetProperty("TextGeometries", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shape)!);

    private static void Sta(Action action) => BulkLayerFeatureTests.Sta(action);

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
