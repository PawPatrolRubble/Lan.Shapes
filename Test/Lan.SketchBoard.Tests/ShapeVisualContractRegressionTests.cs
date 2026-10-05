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
using Lan.Shapes.Handle;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeVisualContractRegressionTests
{
    [Fact]
    public void ReleasedPointerAndRightButton_DoNotUnlockGeometry()
    {
        Sta(() =>
        {
            var shape = new ProbeShape();
            shape.Complete();
            shape.Lock();
            shape.OnMouseMove(new Point(50, 0), MouseButtonState.Released);
            Assert.True(shape.IsLocked);
            shape.OnMouseRightButtonUp(new Point(50, 0));
            Assert.True(shape.IsLocked);
        });
    }

    [Fact]
    public void Hover_PreservesSelectionAndItsHandles()
    {
        Sta(() =>
        {
            var shape = new ProbeShape();
            shape.Complete();
            shape.State = ShapeVisualState.Selected;
            shape.SetSelectionAppearance(true, true);
            shape.OnMouseMove(new Point(), MouseButtonState.Released);
            Assert.Equal(ShapeVisualState.Selected, shape.State);
            Assert.True(shape.IsSelected);
            Assert.True(shape.HasDragHandleAt(new Point()));
        });
    }

    [Fact]
    public void Unlock_RestoresExistingSelectionAppearance()
    {
        var shape = new ProbeShape();
        shape.Complete();
        shape.State = ShapeVisualState.Selected;
        shape.SetSelectionAppearance(true, true);
        shape.Lock();
        shape.UnLock();
        Assert.Equal(ShapeVisualState.Selected, shape.State);
        Assert.True(shape.IsSelected);
        Assert.True(shape.HasDragHandleAt(new Point()));
    }

    [Fact]
    public void MouseUp_CompletesGeometryProvidedOutsideDefaultGroup()
    {
        var shape = new ProbeShape();
        shape.OnMouseLeftButtonUp(new Point());
        Assert.True(shape.IsGeometryRendered);
    }

    [Fact]
    public void CompletingCreation_RemovesRetainedCreationHandles()
    {
        var shape = new ProbeShape();
        shape.UpdateVisual();
        Assert.Equal(2, GeometryCount(shape.Drawing));
        shape.Complete();
        Assert.Equal(1, GeometryCount(shape.Drawing));
        Assert.False(shape.HasDragHandleAt(new Point()));
    }

    [Fact]
    public void WindowSelection_UsesBoundsOfRotatedModelGeometry()
    {
        var shape = new ProbeShape(new LineGeometry(new Point(), new Point(100, 100)));
        shape.Complete();
        shape.Transform = new RotateTransform(-45);
        Assert.True(shape.MatchesSelectionRectangle(new Rect(-2, -2, 146, 4), crossing: false));
        Assert.InRange(shape.SelectionBounds.Height, 0, 0.00001);
    }

    [Fact]
    public void WindowSelection_TransformsOpenFiguresInsideGeometryGroups()
    {
        var figure = new PathFigure { StartPoint = new Point(), IsFilled = false };
        figure.Segments.Add(new LineSegment(new Point(100, 100), true));
        var path = new PathGeometry();
        path.Figures.Add(figure);
        var group = new GeometryGroup();
        group.Children.Add(path);
        var shape = new ProbeShape(group) { Transform = new RotateTransform(-45) };
        shape.Complete();
        Assert.True(shape.MatchesSelectionRectangle(new Rect(-2, -2, 146, 4), crossing: false));
        Assert.InRange(shape.SelectionBounds.Width, 141, 142);
        Assert.InRange(shape.SelectionBounds.Height, 0, 0.00001);
    }

    [Theory]
    [InlineData(10, 8, true)]
    [InlineData(0.1, 0.5, false)]
    public void CrossingSelection_TransformsStrokeAlongWithTheVisual(double scale, double y, bool expected)
    {
        var shape = new ProbeShape(layer: TestShapeLayer.CreateWithThickness(2, 10));
        shape.Complete();
        shape.Transform = new ScaleTransform(scale, scale);
        var region = new Rect(50 * scale, y, scale, 0.01);
        Assert.Equal(expected, shape.MatchesSelectionRectangle(region, crossing: true));
    }

    [Fact]
    public void ScaleRefresh_CoalescesHookAndFinalVisualRequest()
    {
        var shape = new ProbeShape();
        shape.Complete();
        shape.RequestDuringScaleChange = true;
        shape.RenderCount = 0;
        shape.RefreshScaleDependentVisuals(2);
        Assert.Equal(1, shape.RenderCount);
    }

    [Fact]
    public void CancelledGesture_CannotRestartAfterHoverWithoutMouseDown()
    {
        Sta(() =>
        {
            var shape = new ProbeShape();
            shape.Complete();
            shape.State = ShapeVisualState.Selected;
            shape.OnMouseLeftButtonDown(new Point(50, 0));
            shape.OnMouseMove(new Point(55, 0), MouseButtonState.Pressed);
            Assert.True(shape.IsBeingDraggedOrPanMoving);
            shape.CancelInteraction();
            shape.OnMouseMove(new Point(55, 0), MouseButtonState.Released);
            shape.OnMouseMove(new Point(60, 0), MouseButtonState.Pressed);
            Assert.False(shape.IsBeingDraggedOrPanMoving);
            Assert.False(shape.HasPointerTracking);
        });
    }

    [Fact]
    public void Selection_ExcludesModelContentOutsideTheDrawingClip()
    {
        var shape = new ProbeShape(new LineGeometry(new Point(-10, 0), new Point(110, 0)))
        {
            ModelClip = new RectangleGeometry(new Rect(0, -5, 100, 10))
        };
        shape.Complete();
        Assert.True(shape.MatchesSelectionRectangle(new Rect(0, -5, 100, 10), crossing: false),
            $"Clipped bounds: {shape.SelectionBounds}");
        Assert.False(shape.MatchesSelectionRectangle(new Rect(105, -0.5, 2, 1), crossing: true));
    }

    [Fact]
    public void RulerAtImageEdge_CanBeWindowSelectedInsideItsClip()
    {
        Sta(() =>
        {
            var ruler = new Lan.Shapes.Shapes.RulerCross(TestShapeLayer.Create());
            ruler.FromData(new Lan.Shapes.Models.RulerCrossData { Center = new Point(), Width = 100, Height = 100 });
            Assert.True(ruler.MatchesSelectionRectangle(new Rect(0, 0, 100, 100), crossing: false));
        });
    }

    [Fact]
    public void FullyClippedGeometry_CannotBeSelected()
    {
        var shape = new ProbeShape(new LineGeometry(new Point(105, 0), new Point(110, 0)))
        {
            ModelClip = new RectangleGeometry(new Rect(0, -5, 100, 10))
        };
        shape.Complete();
        Assert.True(shape.SelectionBounds.IsEmpty);
        Assert.False(shape.MatchesSelectionRectangle(new Rect(0, -5, 120, 10), crossing: false));
        Assert.False(shape.MatchesSelectionRectangle(new Rect(0, -5, 120, 10), crossing: true));
    }

    [Fact]
    public void DrawingAndVisualClips_BothConstrainSelection()
    {
        var shape = new ProbeShape
        {
            ModelClip = new RectangleGeometry(new Rect(0, -5, 100, 10)),
            Clip = new RectangleGeometry(new Rect(20, -5, 60, 10))
        };
        shape.Complete();
        Assert.True(shape.MatchesSelectionRectangle(new Rect(20, -5, 60, 10), crossing: false));
        Assert.False(shape.MatchesSelectionRectangle(new Rect(5, -0.5, 2, 1), crossing: true));
    }

    [Fact]
    public void Selection_TransformsTheDrawingClipWithTheBody()
    {
        var shape = new ProbeShape(new LineGeometry(new Point(-10, 0), new Point(110, 0)))
        {
            ModelClip = new RectangleGeometry(new Rect(0, -5, 100, 10)),
            Transform = new RotateTransform(90)
        };
        shape.Complete();
        Assert.True(shape.MatchesSelectionRectangle(new Rect(-5, -1, 10, 102), crossing: false));
        Assert.False(shape.MatchesSelectionRectangle(new Rect(-0.5, 105, 1, 2), crossing: true));
    }

    [Fact]
    public void LostCapture_EndsDragAndRequiresAnotherMouseDown()
    {
        Sta(() =>
        {
            var manager = new SketchBoardDataManager();
            manager.SetShapeLayer(TestShapeLayer.Create());
            var board = new ProbeBoard { SketchBoardDataManager = manager };
            var cross = new Lan.Shapes.Shapes.Cross(manager.CurrentShapeLayer!);
            cross.FromData(new Lan.Shapes.Models.CrossData { Center = new Point(100, 100), Width = 80, Height = 60 });
            manager.AddShape(cross);
            board.Press(new Point(140, 100));
            board.Move(new Point(160, 100), MouseButtonState.Pressed);
            Assert.True(cross.IsBeingDraggedOrPanMoving);
            board.LoseCapture();
            var bounds = cross.BoundsRect;
            Assert.False(cross.IsBeingDraggedOrPanMoving);
            board.Move(new Point(160, 100), MouseButtonState.Released);
            board.Move(new Point(180, 100), MouseButtonState.Pressed);
            board.Move(new Point(190, 100), MouseButtonState.Pressed);
            Assert.Equal(bounds, cross.BoundsRect);
            Assert.False(cross.IsBeingDraggedOrPanMoving);
            board.Press(new Point(160, 100));
            board.Move(new Point(180, 100), MouseButtonState.Pressed);
            Assert.NotEqual(bounds, cross.BoundsRect);
        });
    }

    [Fact]
    public void LostCapture_DoesNotCompleteTheCancelledCreation()
    {
        Sta(() =>
        {
            var manager = new SketchBoardDataManager();
            manager.SetShapeLayer(TestShapeLayer.Create());
            var board = new ProbeBoard { SketchBoardDataManager = manager };
            manager.SetGeometryType(typeof(Lan.Shapes.Shapes.Line));
            var completed = 0;
            manager.NewShapeSketched += (_, _) => completed++;
            board.Press(new Point(40, 30));
            var line = Assert.IsType<Lan.Shapes.Shapes.Line>(manager.CurrentGeometryInEdit);
            board.Move(new Point(100, 80), MouseButtonState.Pressed);
            board.LoseCapture();
            var bounds = line.BoundsRect;
            board.Move(new Point(120, 100), MouseButtonState.Pressed);
            board.Release(new Point(120, 100));
            Assert.Equal(bounds, line.BoundsRect);
            Assert.False(line.IsGeometryRendered);
            Assert.Same(line, manager.CurrentGeometryInEdit);
            Assert.Equal(0, completed);

            board.Press(new Point(40, 30));
            board.Move(new Point(140, 130), MouseButtonState.Pressed);
            board.Release(new Point(140, 130));
            Assert.True(line.IsGeometryRendered);
            Assert.Equal(1, completed);
            Assert.Contains(line, manager.Shapes);
        });
    }

    private static int GeometryCount(Drawing? drawing) => drawing switch
    {
        DrawingGroup group => group.Children.Sum(GeometryCount),
        GeometryDrawing => 1,
        _ => 0
    };

    private sealed class ProbeShape : ShapeVisualBase
    {
        private readonly Geometry _geometry;
        public ProbeShape(Geometry? geometry = null, ShapeLayer? layer = null)
            : base(layer ?? TestShapeLayer.Create())
        {
            _geometry = geometry ?? new LineGeometry(new Point(), new Point(100, 0));
            RegisterHandle(new RectDragHandle(DragHandleSize, new Point(), 1));
            PanSensitiveArea.Geometry1 = new RectangleGeometry(new Rect(0, -5, 100, 10));
        }

        public override Rect BoundsRect => _geometry.Bounds;
        public override Geometry RenderGeometry => _geometry;
        public int RenderCount { get; set; }
        public bool RequestDuringScaleChange { get; set; }
        public bool HasPointerTracking => MouseDownPoint.HasValue || OldPointForTranslate.HasValue;
        public Geometry? ModelClip { get; set; }
        protected override Geometry? DrawingClip => ModelClip;
        public void Complete() => IsGeometryRendered = true;
        public override void UpdateVisual() { RenderCount++; base.UpdateVisual(); }
        protected override void OnViewportScaleChanged(double viewportScale)
        {
            if (RequestDuringScaleChange) RequestVisualUpdate();
        }
        protected override void CreateHandles() { }
        protected override void HandleResizing(Point point) { }
        protected override void HandleTranslate(Point point) { }
    }

    private sealed class ProbeBoard : SketchBoard
    {
        public void Press(Point point) => HandleLeftButtonDown(point, 1);
        public void Move(Point point, MouseButtonState state) => HandleMouseMove(point, state);
        public void Release(Point point) => HandleLeftButtonUp(point);
        public void LoseCapture() => OnLostMouseCapture(new MouseEventArgs(Mouse.PrimaryDevice, 0)
        {
            RoutedEvent = Mouse.LostMouseCaptureEvent,
            Source = this
        });
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
