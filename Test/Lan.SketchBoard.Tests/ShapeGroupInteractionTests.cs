using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeGroupInteractionTests
{
    [Fact]
    public void GroupShortcut_PersistsWholeGroupSelectionAfterDeselecting()
    {
        Sta(() =>
        {
            var board = Board(out var manager);
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 120);
            manager.SetSelection(new[] { first, second });
            board.Key(Key.G, ModifierKeys.Control);
            manager.UnselectGeometry();
            board.Click(new Point(80, 50));
            Assert.Equal(new[] { first, second }, manager.SelectedGeometries);
            board.Key(Key.G, ModifierKeys.Control | ModifierKeys.Shift);
            manager.UnselectGeometry();
            board.Click(new Point(80, 50));
            Assert.Same(first, Assert.Single(manager.SelectedGeometries));
        });
    }

    [Fact]
    public void DraggingMember_MovesEveryMemberAndAppliesReleasePosition()
    {
        Sta(() =>
        {
            var board = Board(out var manager);
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 120);
            Group(manager, first, second);
            board.Press(new Point(80, 50));
            board.Move(new Point(100, 70));
            board.Release(new Point(110, 90));
            Assert.Equal(new Point(70, 90), first.Start);
            Assert.Equal(new Point(70, 160), second.Start);
            Assert.Equal(new Point(170, 90), first.End);
            Assert.Equal(new Point(170, 160), second.End);
            Assert.Equal(new[] { first, second }, manager.SelectedGeometries);
        });
    }

    [Fact]
    public void ControlClick_TogglesTheEntireGroupWithoutMovingIt()
    {
        Sta(() =>
        {
            var board = Board(out var manager);
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 120);
            Group(manager, first, second);
            manager.UnselectGeometry();
            board.Click(new Point(80, 50), ModifierKeys.Control);
            Assert.Equal(2, manager.SelectedGeometries.Count);
            board.Press(new Point(80, 120), ModifierKeys.Control);
            board.Move(new Point(100, 140));
            board.Release(new Point(100, 140));
            Assert.Empty(manager.SelectedGeometries);
            Assert.Equal(new Point(40, 50), first.Start);
            Assert.Equal(new Point(40, 120), second.Start);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Marquee_SelectsGroupsAsWholeUnits(bool crossing, bool fullyInside)
    {
        Sta(() =>
        {
            var board = Board(out var manager);
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 120);
            Group(manager, first, second);
            manager.UnselectGeometry();
            var far = new Point(160, fullyInside ? 140 : 80);
            var start = crossing ? far : new Point(20, 20);
            var end = crossing ? new Point(20, 20) : far;
            board.Press(start);
            board.Release(end);
            Assert.Equal(crossing || fullyInside ? 2 : 0, manager.SelectedGeometries.Count);
        });
    }

    [Fact]
    public void LockingGroupByDoubleClick_PreventsPartialMovementAndUnlocksTogether()
    {
        Sta(() =>
        {
            var board = Board(out var manager);
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 120);
            Group(manager, first, second);
            board.Click(new Point(80, 50), clickCount: 2);
            Assert.True(first.IsLocked);
            Assert.True(second.IsLocked);
            board.Press(new Point(80, 120));
            board.Move(new Point(100, 140));
            board.Release(new Point(100, 140));
            Assert.Equal(new Point(40, 50), first.Start);
            Assert.Equal(new Point(40, 120), second.Start);
            board.Click(new Point(80, 120), clickCount: 2);
            Assert.False(first.IsLocked);
            Assert.False(second.IsLocked);
        });
    }

    [Fact]
    public void CancellingDrag_StopsResidualMoveAndRelease()
    {
        Sta(() =>
        {
            var board = Board(out var manager);
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 120);
            Group(manager, first, second);
            board.Press(new Point(80, 50));
            board.Move(new Point(90, 60));
            board.Key(Key.Escape);
            board.Move(new Point(160, 130));
            board.Release(new Point(170, 140));
            Assert.Equal(new Point(50, 60), first.Start);
            Assert.Equal(new Point(50, 130), second.Start);
            Assert.Empty(manager.SelectedGeometries);
        });
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2.0)]
    public void DraggingTransformedMember_AppliesSameBoardDeltaToEveryMember(double viewportScale)
    {
        Sta(() =>
        {
            var board = Board(out var manager);
            manager.OnImageViewerPropertyChanged(viewportScale);
            var first = AddLine(manager, 50);
            first.Transform = new ScaleTransform(2, 2);
            var second = AddLine(manager, 120);
            Group(manager, first, second);
            board.Press(new Point(160, 100));
            board.Release(new Point(190, 140));
            Assert.Equal(new Point(55, 70), first.Start);
            Assert.Equal(new Point(70, 160), second.Start);
        });
    }

    [Fact]
    public void SelectionOutline_IsOneGroupRectangleAndTracksProgrammaticTranslation()
    {
        Sta(() =>
        {
            var board = Board(out var manager);
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 120);
            Group(manager, first, second);
            manager.SelectedGeometry = first;
            Assert.Equal(new Rect(40, 50, 100, 70), board.SelectionOutline);
            manager.TranslateShapes(new[] { first, second }, new Vector(20, 30));
            Assert.Equal(new Rect(60, 80, 100, 70), board.SelectionOutline);
        });
    }

    [Theory]
    [InlineData("hide")]
    [InlineData("ungroup")]
    [InlineData("remove")]
    [InlineData("lock")]
    [InlineData("deselect")]
    [InlineData("capture")]
    [InlineData("rightclick")]
    public void InterruptedGroupDrag_DoesNotApplyResidualPointerUpdates(string interruption)
    {
        Sta(() =>
        {
            var board = Board(out var manager);
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 120);
            Group(manager, first, second);
            board.Press(new Point(80, 50));
            board.Move(new Point(90, 60));
            switch (interruption)
            {
                case "hide": manager.SetLayerVisibility(first.ShapeLayer.LayerId, false); break;
                case "ungroup":
                    manager.UngroupShapes(new[] { first });
                    break;
                case "remove": manager.Shapes.Remove(first); break;
                case "lock": first.Lock(); break;
                case "deselect": manager.UnselectGeometry(); break;
                case "capture": board.LoseCapture(); break;
                case "rightclick": board.RightClick(new Point(90, 60)); break;
            }
            board.Move(new Point(160, 130));
            board.Release(new Point(170, 140));
            Assert.Equal(new Point(50, 60), first.Start);
            Assert.Equal(new Point(50, 130), second.Start);
        });
    }

    [Fact]
    public void AltClick_CyclesDistinctGroupAndIndependentShape()
    {
        Sta(() =>
        {
            var board = Board(out var manager);
            var independent = AddLine(manager, 50);
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 50);
            Group(manager, first, second);
            manager.UnselectGeometry();
            board.Click(new Point(80, 50), ModifierKeys.Alt);
            Assert.Equal(2, manager.SelectedGeometries.Count);
            board.Click(new Point(80, 50), ModifierKeys.Alt);
            Assert.Same(independent, Assert.Single(manager.SelectedGeometries));
            board.Click(new Point(80, 50), ModifierKeys.Alt);
            Assert.Equal(2, manager.SelectedGeometries.Count);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangingDocumentOrDrawingToolDuringGroupDrag_DoesNotCommitAnUnrelatedSketch(bool rebind)
    {
        Sta(() =>
        {
            var board = Board(out var manager);
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 120);
            Group(manager, first, second);
            board.Press(new Point(80, 50));
            board.Move(new Point(90, 60));
            var destination = rebind ? new SketchBoardDataManager() : manager;
            if (rebind) destination.SetShapeLayer(TestShapeLayer.Create());
            destination.SetGeometryType(typeof(Line));
            var sketch = Assert.IsType<Line>(destination.CreateNewGeometry(new Point(220, 180)));
            sketch.OnMouseLeftButtonDown(new Point(220, 180));
            sketch.OnMouseMove(new Point(280, 210), MouseButtonState.Pressed);
            var committed = 0;
            destination.NewShapeSketched += (_, _) => committed++;
            if (rebind) board.SketchBoardDataManager = destination;
            board.Move(new Point(300, 240));
            board.Release(new Point(320, 250));
            Assert.False(sketch.IsGeometryRendered);
            Assert.Equal(0, committed);
            Assert.Equal(new Point(280, 210), sketch.End);
            Assert.Equal(new Point(50, 60), first.Start);
            Assert.Equal(new Point(50, 130), second.Start);
        });
    }

    [Fact]
    public void InvalidatingAMembersTransformDuringDrag_RejectsMovementWithoutThrowing()
    {
        Sta(() =>
        {
            var board = Board(out var manager);
            var first = AddLine(manager, 50);
            var second = AddLine(manager, 120);
            Group(manager, first, second);
            board.Press(new Point(80, 50));
            second.Transform = new MatrixTransform(new Matrix(1, 0, 0, 1, double.NaN, 0));
            board.Move(new Point(100, 70));
            board.Release(new Point(110, 80));
            Assert.Equal(new Point(40, 50), first.Start);
            Assert.Equal(new Point(40, 120), second.Start);
        });
    }

    private static void Group(SketchBoardDataManager manager, params ShapeVisualBase[] members)
        => manager.GroupShapes(members, "测试组");

    private static ProbeBoard Board(out SketchBoardDataManager manager)
    {
        manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        var board = new ProbeBoard
        {
            Width = 400, Height = 300, Background = Brushes.Transparent,
            SketchBoardDataManager = manager, IsSnappingEnabled = false
        };
        board.Measure(new Size(400, 300));
        board.Arrange(new Rect(0, 0, 400, 300));
        return board;
    }

    private static Line AddLine(SketchBoardDataManager manager, double y)
    {
        var line = new Line(manager.CurrentShapeLayer!);
        line.FromData(new PointsData(1, new List<Point> { new(40, y), new(140, y) }));
        manager.AddShape(line);
        return line;
    }

    private sealed class ProbeBoard : SketchBoard
    {
        public Rect SelectionOutline
        {
            get
            {
                var visual = (DrawingVisual)GetVisualChild(SketchBoardDataManager!.VisualCollection.Count);
                return Assert.IsType<GeometryDrawing>(Assert.Single(visual.Drawing!.Children)).Geometry.Bounds;
            }
        }
        public void Press(Point point, ModifierKeys modifiers = ModifierKeys.None, int clickCount = 1)
            => HandleLeftButtonDown(point, clickCount, modifiers);
        public void Move(Point point) => HandleMouseMove(point, MouseButtonState.Pressed);
        public void Release(Point point, ModifierKeys modifiers = ModifierKeys.None)
            => HandleLeftButtonUp(point, modifiers);
        public void Key(Key key, ModifierKeys modifiers = ModifierKeys.None) => HandleSelectionKey(key, modifiers);
        public void RightClick(Point point) => HandleRightButtonUp(point);
        public void LoseCapture() => OnLostMouseCapture(new MouseEventArgs(Mouse.PrimaryDevice, 0)
        { RoutedEvent = Mouse.LostMouseCaptureEvent, Source = this });
        public void Click(Point point, ModifierKeys modifiers = ModifierKeys.None, int clickCount = 1)
        { Press(point, modifiers, clickCount); Release(point, modifiers); }
    }

    private static void Sta(Action action)
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
