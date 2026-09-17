using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Custom;
using Lan.Shapes.Enums;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class LineDirectionInteractionTests
{
    public static IEnumerable<object[]> LineTypesAndAxes()
    {
        foreach (var type in new[] { typeof(Line), typeof(ThickenedLine), typeof(ArrowedLine) })
        foreach (var horizontal in new[] { true, false })
            yield return new object[] { type, horizontal };
    }

    [Theory]
    [MemberData(nameof(LineTypesAndAxes))]
    public void ShiftDrawing_ConstrainsNearestAxisIncludingFinalRelease(Type type, bool horizontal)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(type);
            board.Press(new Point(60, 60));
            var line = manager.CurrentGeometryInEdit!;
            board.Move(horizontal ? new Point(180, 100) : new Point(100, 180), ModifierKeys.Shift);
            Assert.Equal(horizontal ? new Point(180, 60) : new Point(60, 180), Data(line).DataPoints[1]);
            board.Release(horizontal ? new Point(200, 110) : new Point(110, 200), ModifierKeys.Shift);

            Assert.Equal(horizontal ? new Point(200, 60) : new Point(60, 200), Data(line).DataPoints[1]);
            Assert.Equal(new Point(60, 60), Data(line).DataPoints[0]);
            Assert.True(line.IsGeometryRendered);
        });
    }

    [Theory]
    [InlineData(-60, 20, -20, 40)]
    [InlineData(20, -60, 40, -20)]
    [InlineData(60, 60, 100, 40)]
    public void ShiftDrawing_SupportsNegativeDirectionsAndDiagonalTie(double dx, double dy, double x, double y)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(typeof(Line));
            board.Press(new Point(40, 40));
            var line = manager.CurrentGeometryInEdit!;
            var pointer = new Point(40 + dx, 40 + dy);
            board.Move(pointer, ModifierKeys.Shift);
            board.Release(pointer, ModifierKeys.Shift);
            Assert.Equal(new Point(x, y), Data(line).DataPoints[1]);
        });
    }

    [Theory]
    [InlineData(typeof(Line))]
    [InlineData(typeof(ThickenedLine))]
    [InlineData(typeof(ArrowedLine))]
    public void ShiftChanges_UpdateStationaryPreviewAndReleaseRestoresFreeDrawing(Type type)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(type);
            board.Press(new Point(60, 60));
            var line = manager.CurrentGeometryInEdit!;
            board.Move(new Point(180, 100));
            Assert.Equal(new Point(180, 100), Data(line).DataPoints[1]);
            board.ModifiersChanged(ModifierKeys.Shift);
            Assert.Equal(new Point(180, 60), Data(line).DataPoints[1]);
            board.ModifiersChanged(ModifierKeys.None);
            Assert.Equal(new Point(180, 100), Data(line).DataPoints[1]);
            board.ModifiersChanged(ModifierKeys.Shift);
            board.Release(new Point(200, 110));

            Assert.Equal(new Point(200, 110), Data(line).DataPoints[1]);
            board.ModifiersChanged(ModifierKeys.Shift);
            Assert.Equal(new Point(200, 110), Data(line).DataPoints[1]);
        });
    }

    [Theory]
    [MemberData(nameof(LineTypesAndAxes))]
    public void FixedMode_ConstrainsOppositePointerDirectionAndPersistsAcrossSketches(Type type, bool horizontal)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            board.LineDirectionMode = horizontal ? LineDirectionMode.Horizontal : LineDirectionMode.Vertical;
            for (var i = 0; i < 2; i++)
            {
                manager.SetGeometryType(type);
                board.Press(new Point(60, 60));
                var line = manager.CurrentGeometryInEdit!;
                var pointer = horizontal ? new Point(100, 200) : new Point(200, 100);
                board.Move(pointer, ModifierKeys.Shift);
                board.Release(pointer, ModifierKeys.Shift);
                Assert.Equal(horizontal ? new Point(100, 60) : new Point(60, 100), Data(line).DataPoints[1]);
            }
        });
    }

    [Fact]
    public void ChangingFixedMode_UpdatesStationaryPreviewAndFreeRestoresRawPointer()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(typeof(Line));
            board.Press(new Point(60, 60));
            var line = manager.CurrentGeometryInEdit!;
            board.Move(new Point(180, 100));
            board.LineDirectionMode = LineDirectionMode.Horizontal;
            Assert.Equal(new Point(180, 60), Data(line).DataPoints[1]);
            board.LineDirectionMode = LineDirectionMode.Vertical;
            Assert.Equal(new Point(60, 100), Data(line).DataPoints[1]);
            board.LineDirectionMode = LineDirectionMode.Free;
            Assert.Equal(new Point(180, 100), Data(line).DataPoints[1]);
        });
    }

    [Theory]
    [MemberData(nameof(LineTypesAndAxes))]
    public void ResizingEitherEndpoint_ShiftUsesOppositeEndpointAsOrigin(Type type, bool moveStart)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var line = LoadLine(manager, type, new Point(60, 100), new Point(140, 160));
            var origin = Data(line).DataPoints[moveStart ? 1 : 0];
            board.Press(Data(line).DataPoints[moveStart ? 0 : 1]);
            board.Move(origin + new Vector(80, 30), ModifierKeys.Shift);
            Assert.Equal(origin + new Vector(80, 0), Data(line).DataPoints[moveStart ? 0 : 1]);
            board.Release(origin + new Vector(90, 40), ModifierKeys.Shift);

            Assert.Equal(origin + new Vector(90, 0), Data(line).DataPoints[moveStart ? 0 : 1]);
            Assert.Equal(origin, Data(line).DataPoints[moveStart ? 1 : 0]);
        });
    }

    [Theory]
    [MemberData(nameof(LineTypesAndAxes))]
    public void ResizingEitherEndpoint_FixedVerticalModeOverridesShiftAndPreservesOrigin(Type type, bool moveStart)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            board.LineDirectionMode = LineDirectionMode.Vertical;
            var line = LoadLine(manager, type, new Point(60, 100), new Point(140, 160));
            var origin = Data(line).DataPoints[moveStart ? 1 : 0];
            board.Press(Data(line).DataPoints[moveStart ? 0 : 1]);
            board.Move(origin + new Vector(80, 30), ModifierKeys.Shift);
            board.Release(origin + new Vector(90, 40), ModifierKeys.Shift);

            Assert.Equal(origin + new Vector(0, 40), Data(line).DataPoints[moveStart ? 0 : 1]);
            Assert.Equal(origin, Data(line).DataPoints[moveStart ? 1 : 0]);
        });
    }

    [Theory]
    [InlineData(false, 0.5)]
    [InlineData(false, 1)]
    [InlineData(false, 4)]
    [InlineData(true, 0.5)]
    [InlineData(true, 1)]
    [InlineData(true, 4)]
    public void AxisSnapping_IgnoresCloserOffAxisAnchorsAndKeepsScreenTolerance(bool vertical, double scale)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var anchor = vertical ? new Point(40, 120) : new Point(100, 60);
            var target = LoadLine(manager, typeof(Line), anchor,
                anchor + (vertical ? new Vector(2, 3) : new Vector(3, 2)));
            target.Lock();
            manager.OnImageViewerPropertyChanged(scale);
            board.LineDirectionMode = vertical ? LineDirectionMode.Vertical : LineDirectionMode.Horizontal;
            manager.SetGeometryType(typeof(Line));
            board.Press(new Point(40, 60));
            var line = manager.CurrentGeometryInEdit!;
            var outside = anchor + (vertical ? new Vector(2 / scale, 8.1 / scale) : new Vector(8.1 / scale, 2 / scale));
            board.Move(outside);
            Assert.True(board.MarkerBounds.IsEmpty);

            var inside = anchor + (vertical ? new Vector(2 / scale, 7.9 / scale) : new Vector(7.9 / scale, 2 / scale));
            board.Move(inside);
            Assert.Equal(anchor, Data(line).DataPoints[1]);
            Assert.Equal(13.5, board.MarkerBounds.Width * scale, precision: 8);
            board.Release(inside);
            Assert.Equal(anchor, Data(line).DataPoints[1]);
            Assert.True(target.IsLocked);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Fact]
    public void OnlyOffAxisAnchors_KeepConstrainedEndpointWithoutShowingFalseMarker()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadLine(manager, typeof(Line), new Point(100, 62), new Point(120, 90));
            manager.SetGeometryType(typeof(Line));
            board.Press(new Point(40, 60));
            var line = manager.CurrentGeometryInEdit!;
            board.Move(new Point(103, 62), ModifierKeys.Shift);
            Assert.Equal(new Point(103, 60), Data(line).DataPoints[1]);
            Assert.True(board.MarkerBounds.IsEmpty);
            board.Release(new Point(103, 62), ModifierKeys.Shift);
            Assert.Equal(new Point(103, 60), Data(line).DataPoints[1]);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResizingEndpoint_SnapsToCompatibleAnchorWithoutTiltingLine(bool moveStart)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            board.LineDirectionMode = LineDirectionMode.Horizontal;
            var origin = moveStart ? new Point(200, 180) : new Point(40, 60);
            var anchor = origin + new Vector(60, 0);
            LoadLine(manager, typeof(Line), anchor, anchor + new Vector(3, 2));
            var line = LoadLine(manager, typeof(Line), new Point(40, 60), new Point(200, 180));
            board.Press(Data(line).DataPoints[moveStart ? 0 : 1]);
            board.Move(anchor + new Vector(3, 2));
            Assert.Equal(anchor, Data(line).DataPoints[moveStart ? 0 : 1]);
            Assert.False(board.MarkerBounds.IsEmpty);
            board.Release(anchor + new Vector(4, 3));
            Assert.Equal(anchor, Data(line).DataPoints[moveStart ? 0 : 1]);
            Assert.Equal(origin, Data(line).DataPoints[moveStart ? 1 : 0]);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Fact]
    public void ReleasedPointer_StopsModifierKeysFromChangingUnfinishedSketch()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.SetGeometryType(typeof(Line));
            board.Press(new Point(60, 60));
            var line = manager.CurrentGeometryInEdit!;
            board.Move(new Point(180, 100));
            board.PointerReleased(new Point(180, 100));
            board.ModifiersChanged(ModifierKeys.Shift);
            Assert.Equal(new Point(180, 100), Data(line).DataPoints[1]);
        });
    }

    [Fact]
    public void DisablingAnchorSnapping_StillAllowsDirectionConstraints()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadLine(manager, typeof(Line), new Point(100, 60), new Point(150, 60));
            board.IsSnappingEnabled = false;
            manager.SetGeometryType(typeof(Line));
            board.Press(new Point(40, 60));
            var line = manager.CurrentGeometryInEdit!;
            board.Move(new Point(103, 62), ModifierKeys.Shift);
            board.Release(new Point(103, 62), ModifierKeys.Shift);
            Assert.Equal(new Point(103, 60), Data(line).DataPoints[1]);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Fact]
    public void MovingWholeLine_KeepsRawTranslationWithShiftAndFixedMode()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            board.LineDirectionMode = LineDirectionMode.Vertical;
            var line = LoadLine(manager, typeof(Line), new Point(40, 60), new Point(160, 120));
            board.Press(new Point(100, 90));
            board.Move(new Point(107, 95), ModifierKeys.Shift);
            board.Release(new Point(107, 95), ModifierKeys.Shift);
            Assert.Equal(new Point(47, 65), Data(line).DataPoints[0]);
            Assert.Equal(new Point(167, 125), Data(line).DataPoints[1]);
        });
    }

    [Fact]
    public void DrawingCircle_RemainsFreeWithShiftAndFixedLineMode()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            board.LineDirectionMode = LineDirectionMode.Horizontal;
            manager.SetGeometryType(typeof(Circle));
            board.Press(new Point(40, 60));
            var circle = Assert.IsType<Circle>(manager.CurrentGeometryInEdit);
            board.Move(new Point(100, 140), ModifierKeys.Shift);
            board.Release(new Point(100, 140), ModifierKeys.Shift);
            Assert.Equal(100, circle.Radius, precision: 8);
            Assert.Equal(new Point(40, 60), circle.Center);
        });
    }

    private static PointsData Data(ShapeVisualBase line) => ((IDataExport<PointsData>)line).GetMetaData();

    private static ShapeVisualBase LoadLine(SketchBoardDataManager manager, Type type, Point start, Point end)
    {
        var line = (ShapeVisualBase)Activator.CreateInstance(type, manager.CurrentShapeLayer!)!;
        ((IDataExport<PointsData>)line).FromData(new PointsData(15, new List<Point> { start, end }));
        manager.AddShape(line);
        return line;
    }

    private static TestBoard CreateBoard(out SketchBoardDataManager manager)
    {
        manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        var board = new TestBoard { Width = 320, Height = 240, Background = Brushes.Transparent, SketchBoardDataManager = manager };
        board.Measure(new Size(320, 240));
        board.Arrange(new Rect(0, 0, 320, 240));
        return board;
    }

    private sealed class TestBoard : SketchBoard
    {
        public Rect MarkerBounds => ((DrawingVisual)GetVisualChild(VisualChildrenCount - 1)).ContentBounds;
        public void Press(Point point) => HandleLeftButtonDown(point, 1);
        public void Move(Point point, ModifierKeys modifiers = ModifierKeys.None)
            => HandleMouseMove(point, MouseButtonState.Pressed, modifiers);
        public void Release(Point point, ModifierKeys modifiers = ModifierKeys.None) => HandleLeftButtonUp(point, modifiers);
        public void ModifiersChanged(ModifierKeys modifiers) => HandleModifierKeysChanged(modifiers);
        public void PointerReleased(Point point) => HandleMouseMove(point, MouseButtonState.Released);
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
