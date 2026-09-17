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

public class SnappingInteractionTests
{
    [Theory]
    [InlineData(typeof(Line), false)]
    [InlineData(typeof(Line), true)]
    [InlineData(typeof(ThickenedLine), false)]
    [InlineData(typeof(ThickenedLine), true)]
    [InlineData(typeof(ArrowedLine), false)]
    [InlineData(typeof(ArrowedLine), true)]
    public void DrawingLine_SnapsToLineMidpointWithoutSelectingTarget(Type type, bool locked)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var target = (ShapeVisualBase)Activator.CreateInstance(type, manager.CurrentShapeLayer!)!;
            var targetData = (IDataExport<PointsData>)target;
            var points = new List<Point> { new(40, 40), new(160, 120) };
            targetData.FromData(new PointsData(1, points));
            manager.AddShape(target);
            if (locked) target.Lock();
            var selections = 0;
            manager.ShapeSelected += (_, shape) => { if (ReferenceEquals(shape, target)) selections++; };
            manager.SetGeometryType(typeof(Line));
            board.Move(new Point(103, 82), MouseButtonState.Released);

            Assert.Equal(new Point(100, 80), board.MarkerBounds.Location + new Vector(
                board.MarkerBounds.Width / 2, board.MarkerBounds.Height / 2));
            board.Press(new Point(103, 82));
            var line = Assert.IsType<Line>(manager.CurrentGeometryInEdit);
            Assert.Equal(new Point(100, 80), line.Start);
            board.Move(new Point(220, 180), MouseButtonState.Pressed);
            board.Release(new Point(157, 118));

            Assert.Equal(new Point(160, 120), line.End);
            Assert.Equal(points, targetData.GetMetaData().DataPoints);
            Assert.Equal(locked, target.IsLocked);
            Assert.Equal(0, selections);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Theory]
    [InlineData(typeof(Line))]
    [InlineData(typeof(ThickenedLine))]
    [InlineData(typeof(ArrowedLine))]
    public void ResizingLine_SnapsEndpointToLineMidpoint(Type type)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var target = (ShapeVisualBase)Activator.CreateInstance(type, manager.CurrentShapeLayer!)!;
            ((IDataExport<PointsData>)target).FromData(new PointsData(1,
                new List<Point> { new(40, 40), new(160, 120) }));
            manager.AddShape(target);
            var line = LoadLine(manager, new Point(200, 180), new Point(250, 200));
            board.Press(line.End);
            board.Move(new Point(103, 82), MouseButtonState.Pressed);

            Assert.Equal(new Point(100, 80), line.End);
            Assert.False(board.MarkerBounds.IsEmpty);
            board.Release(new Point(104, 83));

            Assert.Equal(new Point(100, 80), line.End);
            Assert.Equal(new Point(200, 180), line.Start);
            Assert.Same(line, manager.SelectedGeometry);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Theory]
    [InlineData(typeof(Circle), 100, 100)]
    [InlineData(typeof(Circle), 140, 100)]
    [InlineData(typeof(Circle), 100, 60)]
    [InlineData(typeof(Circle), 60, 100)]
    [InlineData(typeof(Circle), 100, 140)]
    [InlineData(typeof(FixedCenterCircle), 100, 100)]
    [InlineData(typeof(FixedCenterCircle), 140, 100)]
    [InlineData(typeof(FixedCenterCircle), 100, 60)]
    [InlineData(typeof(FixedCenterCircle), 60, 100)]
    [InlineData(typeof(FixedCenterCircle), 100, 140)]
    [InlineData(typeof(ThickenedCircle), 100, 100)]
    [InlineData(typeof(ThickenedCircle), 140, 100)]
    [InlineData(typeof(ThickenedCircle), 100, 60)]
    [InlineData(typeof(ThickenedCircle), 60, 100)]
    [InlineData(typeof(ThickenedCircle), 100, 140)]
    public void DrawingLine_SnapsToCircleCenterAndQuadrants(Type type, double x, double y)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadCircle(manager, type, new Point(100, 100), 40);
            manager.SetGeometryType(typeof(Line));
            var anchor = new Point(x, y);

            board.Press(anchor + new Vector(3, 2));

            Assert.Equal(anchor, Assert.IsType<Line>(manager.CurrentGeometryInEdit).Start);
        });
    }

    [Theory]
    [InlineData(typeof(Circle), 0.5)]
    [InlineData(typeof(FixedCenterCircle), 2)]
    [InlineData(typeof(ThickenedCircle), 4)]
    public void LockedTransformedCircle_SnapsWithScreenToleranceWithoutSelectingTarget(Type type, double scale)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var circle = LoadCircle(manager, type, new Point(100, 100), 40);
            circle.Transform = new TranslateTransform(50, 30);
            circle.Lock();
            manager.OnImageViewerPropertyChanged(scale);
            manager.SetGeometryType(typeof(Line));
            var anchor = new Point(190, 130);
            board.Move(anchor + new Vector(8.1 / scale, 0), MouseButtonState.Released);
            Assert.True(board.MarkerBounds.IsEmpty);

            var near = anchor + new Vector(7.9 / scale, 0);
            board.Move(near, MouseButtonState.Released);
            Assert.Equal(13.5, board.MarkerBounds.Width * scale, precision: 8);
            board.Press(near);
            var line = Assert.IsType<Line>(manager.CurrentGeometryInEdit);
            Assert.Equal(anchor, line.Start);
            board.Move(new Point(260, 200), MouseButtonState.Pressed);
            board.Release(new Point(150, 130) + new Vector(3 / scale, 2 / scale));

            Assert.Equal(new Point(150, 130), line.End);
            Assert.True(circle.IsLocked);
            Assert.NotSame(circle, manager.SelectedGeometry);
            Assert.Equal(new Point(100, 100), ((IDataExport<EllipseData>)circle).GetMetaData().Center);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Theory]
    [InlineData(typeof(Circle))]
    [InlineData(typeof(ThickenedCircle))]
    public void DrawingCircle_SnapsCenterAndFinalRadiusToRectangleCorners(Type type)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadRectangle(manager);
            manager.SetGeometryType(type);
            board.Press(new Point(22, 23));
            var circle = manager.CurrentGeometryInEdit!;
            board.Move(new Point(160, 150), MouseButtonState.Pressed);
            board.Release(new Point(103, 77));

            var data = ((IDataExport<EllipseData>)circle).GetMetaData();
            Assert.Equal(new Point(20, 20), data.Center);
            Assert.Equal(100, data.RadiusX, precision: 8);
            Assert.True(circle.IsGeometryRendered);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Theory]
    [InlineData(typeof(Circle))]
    [InlineData(typeof(FixedCenterCircle))]
    [InlineData(typeof(ThickenedCircle))]
    public void ResizingCircle_SnapsRadiusWithoutMovingCenter(Type type)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadLine(manager, new Point(240, 120), new Point(280, 120));
            var circle = LoadCircle(manager, type, new Point(160, 120), 40);
            board.Press(new Point(200, 120));
            board.Move(new Point(237, 122), MouseButtonState.Pressed);
            var dataExport = (IDataExport<EllipseData>)circle;
            Assert.Equal(80, dataExport.GetMetaData().RadiusX, precision: 8);
            Assert.False(board.MarkerBounds.IsEmpty);
            board.Release(new Point(277, 122));

            Assert.Equal(120, dataExport.GetMetaData().RadiusX, precision: 8);
            Assert.Equal(new Point(160, 120), dataExport.GetMetaData().Center);
            Assert.Same(circle, manager.SelectedGeometry);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Theory]
    [InlineData(typeof(Circle))]
    [InlineData(typeof(FixedCenterCircle))]
    [InlineData(typeof(ThickenedCircle))]
    public void UpdatingCircleTarget_ClearsMarkerAndUsesNewAnchors(Type type)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var circle = LoadCircle(manager, type, new Point(100, 100), 40);
            manager.SetGeometryType(typeof(Line));
            board.Move(new Point(143, 102), MouseButtonState.Released);
            Assert.False(board.MarkerBounds.IsEmpty);

            ((IDataExport<EllipseData>)circle).FromData(new EllipseData
            {
                Center = new Point(160, 100), RadiusX = 40, RadiusY = 40
            });

            Assert.True(board.MarkerBounds.IsEmpty);
            board.Move(new Point(143, 102), MouseButtonState.Released);
            Assert.True(board.MarkerBounds.IsEmpty);
            board.Press(new Point(203, 102));
            Assert.Equal(new Point(200, 100), Assert.IsType<Line>(manager.CurrentGeometryInEdit).Start);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DrawingLine_SnapsBothEndsToRectangleCornersWithoutSelectingTarget(bool locked)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var rectangle = LoadRectangle(manager);
            if (locked) rectangle.Lock();
            var selections = 0;
            manager.ShapeSelected += (_, shape) => { if (ReferenceEquals(shape, rectangle)) selections++; };
            Point? committedEnd = null;
            manager.NewShapeSketched += (_, shape) => committedEnd = ((Line)shape).End;
            manager.SetGeometryType(typeof(Line));

            board.Move(new Point(103, 22), MouseButtonState.Released);
            Assert.Equal(new Point(100, 20), board.MarkerBounds.Location + new Vector(
                board.MarkerBounds.Width / 2, board.MarkerBounds.Height / 2));
            Assert.Same(rectangle, VisualTreeHelper.HitTest(board, new Point(100, 20))!.VisualHit);
            Assert.Equal(locked ? ShapeVisualState.Locked : ShapeVisualState.Normal, rectangle.State);

            board.Press(new Point(103, 22));
            var line = Assert.IsType<Line>(manager.CurrentGeometryInEdit);
            Assert.Equal(new Point(100, 20), line.Start);
            board.Move(new Point(160, 150), MouseButtonState.Pressed);
            // No intervening move at this corner: the release must apply the snap.
            board.Release(new Point(22, 77));

            Assert.Equal(new Point(20, 80), line.End);
            Assert.Equal(line.End, committedEnd);
            Assert.True(line.IsGeometryRendered);
            Assert.Equal(0, selections);
            Assert.Equal(locked, rectangle.IsLocked);
            Assert.Equal(new Point(20, 20), rectangle.TopLeft);
            Assert.Equal(new Point(100, 80), rectangle.BottomRight);
            Assert.Equal(2, manager.ShapeCount);
            Assert.Equal(2, manager.VisualCollection.Count);
            Assert.True(board.MarkerBounds.IsEmpty);
            Assert.Null(manager.CurrentGeometryType);
        });
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void SnapDistanceAndMarkerSize_RemainConstantOnScreen(double scale)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadRectangle(manager);
            manager.OnImageViewerPropertyChanged(scale);
            manager.SetGeometryType(typeof(Line));

            board.Move(new Point(20 - 8.1 / scale, 20), MouseButtonState.Released);
            Assert.True(board.MarkerBounds.IsEmpty);
            var near = new Point(20 - 7.9 / scale, 20);
            board.Move(near, MouseButtonState.Released);
            Assert.Equal(13.5, board.MarkerBounds.Width * scale, precision: 8);
            Assert.Equal(13.5, board.MarkerBounds.Height * scale, precision: 8);
            board.Press(near);
            Assert.Equal(new Point(20, 20), Assert.IsType<Line>(manager.CurrentGeometryInEdit).Start);
        });
    }

    [Fact]
    public void SnapChoosesNearestAnchorAcrossShapesRatherThanFirstMatch()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadRectangle(manager);
            LoadLine(manager, new Point(25, 23), new Point(150, 100));
            LoadLine(manager, new Point(28, 23), new Point(160, 100));
            manager.SetGeometryType(typeof(Line));

            board.Press(new Point(22, 23));

            Assert.Equal(new Point(25, 23), Assert.IsType<Line>(manager.CurrentGeometryInEdit).Start);
        });
    }

    [Fact]
    public void OutsideCircularTolerance_KeepsRawPointAndClearsMarker()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadRectangle(manager);
            manager.SetGeometryType(typeof(Line));
            board.Move(new Point(23, 23), MouseButtonState.Released);
            Assert.False(board.MarkerBounds.IsEmpty);

            var point = new Point(26, 26);
            board.Press(point);

            Assert.Equal(point, Assert.IsType<Line>(manager.CurrentGeometryInEdit).Start);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Fact]
    public void UnfinishedGeometriesAndCurrentSketch_AreNotSnapTargets()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.AddShape(new Line(manager.CurrentShapeLayer!) { Start = new Point(100, 100), End = new Point(200, 200) });
            manager.SetGeometryType(typeof(Line));
            var start = new Point(102, 101);
            board.Press(start);
            var line = Assert.IsType<Line>(manager.CurrentGeometryInEdit);
            Assert.Equal(start, line.Start);

            board.Move(new Point(150, 160), MouseButtonState.Pressed);
            var end = new Point(151, 162);
            board.Move(end, MouseButtonState.Pressed);
            board.Release(end);

            Assert.Equal(end, line.End);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Fact]
    public void MultiClickPolygon_ContinuesSnappingAfterToolIsUnselected()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadRectangle(manager);
            manager.SetGeometryType(typeof(Polygon));
            board.Press(new Point(22, 23));
            board.Release(new Point(22, 23));
            var polygon = Assert.IsType<Polygon>(manager.CurrentGeometryInEdit);
            manager.UnselectGeometryType();
            board.Press(new Point(97, 22));
            board.Release(new Point(97, 22));

            Assert.Equal(new[] { new Point(20, 20), new Point(100, 20) }, polygon.GetMetaData().DataPoints);
            board.Finish(new Point(250, 180));
            Assert.True(polygon.IsGeometryRendered);
            Assert.True(board.MarkerBounds.IsEmpty);
            Assert.Null(manager.CurrentGeometryInEdit);
        });
    }

    [Fact]
    public void RotatedRectangle_SnapsToActualCornerRatherThanBoundingBox()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            manager.LoadShape<Rectangle2, Rectangle2Data>(new Rectangle2Data
            {
                Row = 100, Column = 100, Phi = Math.PI / 4, Length1 = 40, Length2 = 20
            });
            manager.SetGeometryType(typeof(Line));
            var corner = new Point(100 - 20 / Math.Sqrt(2), 100 - 60 / Math.Sqrt(2));

            board.Press(corner + new Vector(2, 1));

            var start = Assert.IsType<Line>(manager.CurrentGeometryInEdit).Start;
            Assert.Equal(corner.X, start.X, precision: 8);
            Assert.Equal(corner.Y, start.Y, precision: 8);
        });
    }

    [Theory]
    [InlineData(typeof(ThickenedRectangle))]
    [InlineData(typeof(ThickenedLine))]
    [InlineData(typeof(ArrowedLine))]
    public void CustomRectangleAndLineVariants_ProvideSnapAnchors(Type type)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var target = (ShapeVisualBase)Activator.CreateInstance(type, manager.CurrentShapeLayer!)!;
            ((IDataExport<PointsData>)target).FromData(new PointsData(5, new List<Point> { new(40, 40), new(110, 90) }));
            manager.AddShape(target);
            target.Lock();
            manager.SetGeometryType(typeof(Line));

            board.Press(new Point(113, 92));

            Assert.Equal(new Point(110, 90), Assert.IsType<Line>(manager.CurrentGeometryInEdit).Start);
            Assert.True(target.IsLocked);
        });
    }

    [Fact]
    public void TargetVisualTransform_IsAppliedToAnchors()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var target = LoadLine(manager, new Point(30, 30), new Point(100, 100));
            target.Transform = new TranslateTransform(50, 30);
            manager.SetGeometryType(typeof(Line));

            board.Press(new Point(154, 130));

            Assert.Equal(new Point(150, 130), Assert.IsType<Line>(manager.CurrentGeometryInEdit).Start);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ResizingLine_SnapsEitherEndpointWithoutMovingOrSelectingTarget(bool moveStart, bool lockedTarget)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var rectangle = LoadRectangle(manager);
            if (lockedTarget) rectangle.Lock();
            var line = LoadLine(manager, new Point(150, 140), new Point(200, 180));
            var fixedPoint = moveStart ? line.End : line.Start;
            var selections = 0;
            manager.ShapeSelected += (_, shape) => { if (ReferenceEquals(shape, rectangle)) selections++; };
            board.Press(moveStart ? line.Start : line.End);
            board.Move(new Point(23, 22), MouseButtonState.Pressed);

            Assert.Equal(new Point(20, 20), moveStart ? line.Start : line.End);
            Assert.False(board.MarkerBounds.IsEmpty);
            // Snap the final release even when no move event reached this corner.
            board.Release(new Point(103, 77));

            Assert.Equal(new Point(100, 80), moveStart ? line.Start : line.End);
            Assert.Equal(fixedPoint, moveStart ? line.End : line.Start);
            Assert.Same(line, manager.SelectedGeometry);
            Assert.Equal(0, selections);
            Assert.Equal(lockedTarget, rectangle.IsLocked);
            Assert.Equal(new Point(20, 20), rectangle.TopLeft);
            Assert.Equal(new Point(100, 80), rectangle.BottomRight);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Theory]
    [InlineData(typeof(Rectangle))]
    [InlineData(typeof(ThickenedRectangle))]
    [InlineData(typeof(ThickenedLine))]
    [InlineData(typeof(ArrowedLine))]
    public void GeometryResizeHandles_SnapToExistingAnchors(Type type)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadRectangle(manager);
            var shape = (ShapeVisualBase)Activator.CreateInstance(type, manager.CurrentShapeLayer!)!;
            var dataExport = (IDataExport<PointsData>)shape;
            dataExport.FromData(new PointsData(5, new List<Point> { new(10, 10), new(180, 150) }));
            manager.AddShape(shape);
            board.Press(new Point(180, 150));
            board.Move(new Point(103, 82), MouseButtonState.Pressed);

            Assert.Equal(new Point(100, 80), dataExport.GetMetaData().DataPoints[1]);
            Assert.Equal(new Point(10, 10), dataExport.GetMetaData().DataPoints[0]);
            Assert.False(board.MarkerBounds.IsEmpty);
            board.Release(new Point(103, 82));
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Resizing_UsesScreenToleranceAtEveryZoom(double scale)
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadRectangle(manager);
            var line = LoadLine(manager, new Point(150, 140), new Point(200, 180));
            manager.OnImageViewerPropertyChanged(scale);
            board.Press(line.End);
            var outside = new Point(20 - 8.1 / scale, 20);
            board.Move(outside, MouseButtonState.Pressed);
            Assert.Equal(outside, line.End);
            Assert.True(board.MarkerBounds.IsEmpty);

            var inside = new Point(20 - 7.9 / scale, 20);
            board.Move(inside, MouseButtonState.Pressed);
            Assert.Equal(new Point(20, 20), line.End);
            Assert.Equal(13.5, board.MarkerBounds.Width * scale, precision: 8);
            board.Release(inside);
            Assert.Equal(new Point(20, 20), line.End);
        });
    }

    [Fact]
    public void Resizing_DoesNotSnapBackToItsOwnEndpoint()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var line = LoadLine(manager, new Point(150, 140), new Point(200, 180));
            board.Press(line.End);
            board.Move(new Point(203, 181), MouseButtonState.Pressed);
            Assert.Equal(new Point(203, 181), line.End);
            Assert.True(board.MarkerBounds.IsEmpty);
            board.Release(new Point(204, 182));
            Assert.Equal(new Point(204, 182), line.End);
        });
    }

    [Fact]
    public void ClickingEndpointNearTarget_DoesNotResizeUntilPointerMoves()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadRectangle(manager);
            var endpoint = new Point(23, 22);
            var line = LoadLine(manager, new Point(150, 140), endpoint);
            board.Press(endpoint);
            board.Move(endpoint, MouseButtonState.Pressed);
            board.Release(endpoint);
            Assert.Equal(endpoint, line.End);
            Assert.True(board.MarkerBounds.IsEmpty);

            board.Press(endpoint);
            board.Move(new Point(24, 22), MouseButtonState.Pressed);
            Assert.Equal(new Point(20, 20), line.End);
            Assert.False(board.MarkerBounds.IsEmpty);
        });
    }

    [Fact]
    public void ResizingRotatedRectangle_SnapsCornerAndPreservesOrientation()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadLine(manager, new Point(180, 200), new Point(280, 200));
            var rectangle = (Rectangle2)manager.LoadShape<Rectangle2, Rectangle2Data>(new Rectangle2Data
            {
                Row = 100, Column = 100, Phi = Math.PI / 4, Length1 = 40, Length2 = 40
            });
            var originalCorners = new List<Point>(rectangle.GetSnapPoints());
            board.Press(originalCorners[2]);
            board.Move(new Point(183, 201), MouseButtonState.Pressed);
            board.Release(new Point(183, 201));

            var corners = new List<Point>(rectangle.GetSnapPoints());
            Assert.Equal(180, corners[2].X, precision: 8);
            Assert.Equal(200, corners[2].Y, precision: 8);
            Assert.Equal(originalCorners[0].X, corners[0].X, precision: 8);
            Assert.Equal(originalCorners[0].Y, corners[0].Y, precision: 8);
            Assert.Equal(Math.PI / 4, rectangle.Phi);
        });
    }

    [Fact]
    public void MovingRulerOrigin_DoesNotSnapItsMoveHandle()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadRectangle(manager);
            var ruler = (RulerCross)manager.LoadShape<RulerCross, RulerCrossData>(new RulerCrossData
            {
                Center = new Point(160, 120), Width = 320, Height = 240
            });
            board.Press(ruler.Center);
            var point = new Point(103, 82);
            board.Move(point, MouseButtonState.Pressed);
            board.Release(point);

            Assert.Equal(point, ruler.Center);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Fact]
    public void MovingWholeLine_KeepsTheRawTranslation()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadRectangle(manager);
            var start = new Point(150, 140);
            var end = new Point(200, 180);
            var line = LoadLine(manager, start, end);
            var midpoint = new Point(175, 160);
            var pointer = new Point(23, 22);
            board.Press(midpoint);
            board.Move(pointer, MouseButtonState.Pressed);
            board.Release(pointer);

            Assert.Equal(start + (pointer - midpoint), line.Start);
            Assert.Equal(end + (pointer - midpoint), line.End);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Fact]
    public void RotatingRectangle_KeepsTheRawAngle()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadRectangle(manager);
            var rectangle = (Rectangle2)manager.LoadShape<Rectangle2, Rectangle2Data>(new Rectangle2Data
            {
                Row = 150, Column = 220, Phi = 0, Length1 = 40, Length2 = 20
            });
            manager.SelectedGeometry = rectangle;
            board.Press(new Point(220, 106));
            var pointer = new Point(23, 22);
            board.Move(pointer, MouseButtonState.Pressed);
            board.Release(pointer);

            Assert.Equal(Math.Atan2(pointer.Y - 150, pointer.X - 220) + Math.PI / 2, rectangle.Phi, precision: 8);
            Assert.Equal(new Point(220, 150), rectangle.Center);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Fact]
    public void DisablingSnapping_AlsoAppliesToResizing()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadRectangle(manager);
            var line = LoadLine(manager, new Point(150, 140), new Point(200, 180));
            board.IsSnappingEnabled = false;
            board.Press(line.End);
            var point = new Point(23, 22);
            board.Move(point, MouseButtonState.Pressed);
            board.Release(point);

            Assert.Equal(point, line.End);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Fact]
    public void MarkerClearsWhenToolTargetOrViewportChanges()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            var target = LoadRectangle(manager);
            manager.SetGeometryType(typeof(Line));
            var near = new Point(22, 21);
            board.Move(near, MouseButtonState.Released);
            Assert.False(board.MarkerBounds.IsEmpty);
            board.Leave();
            Assert.True(board.MarkerBounds.IsEmpty);
            board.Move(near, MouseButtonState.Released);
            manager.OnImageViewerPropertyChanged(2);
            Assert.True(board.MarkerBounds.IsEmpty);
            board.Move(near, MouseButtonState.Released);
            manager.UnselectGeometryType();
            Assert.True(board.MarkerBounds.IsEmpty);
            manager.SetGeometryType(typeof(Line));
            board.Move(near, MouseButtonState.Released);
            manager.RemoveShape(target);
            Assert.True(board.MarkerBounds.IsEmpty);
        });
    }

    [Fact]
    public void ChangingManager_ClearsMarkerAndUsesOnlyTheNewTargets()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var oldManager);
            LoadRectangle(oldManager);
            oldManager.SetGeometryType(typeof(Line));
            board.Move(new Point(22, 21), MouseButtonState.Released);
            Assert.False(board.MarkerBounds.IsEmpty);
            var manager = new SketchBoardDataManager();
            manager.SetShapeLayer(TestShapeLayer.Create());
            board.SketchBoardDataManager = manager;
            Assert.True(board.MarkerBounds.IsEmpty);
            Assert.Empty(oldManager.VisualCollection);
            LoadLine(manager, new Point(30, 30), new Point(100, 100));
            manager.SetGeometryType(typeof(Line));
            board.Move(new Point(32, 31), MouseButtonState.Released);
            Assert.False(board.MarkerBounds.IsEmpty);
            oldManager.UnselectGeometryType();
            Assert.False(board.MarkerBounds.IsEmpty);

            board.Press(new Point(32, 31));
            Assert.Equal(new Point(30, 30), Assert.IsType<Line>(manager.CurrentGeometryInEdit).Start);
            board.SketchBoardDataManager = null;
            Assert.True(board.MarkerBounds.IsEmpty);
            Assert.Empty(manager.VisualCollection);
            Assert.Equal(1, VisualTreeHelper.GetChildrenCount(board));
        });
    }

    [Fact]
    public void SnappingCanBeDisabledOrGivenACustomTolerance()
    {
        RunOnSta(() =>
        {
            var board = CreateBoard(out var manager);
            LoadRectangle(manager);
            manager.SetGeometryType(typeof(Line));
            var near = new Point(23, 20);
            board.Move(near, MouseButtonState.Released);
            Assert.False(board.MarkerBounds.IsEmpty);
            board.SnapTolerance = 2;
            Assert.True(board.MarkerBounds.IsEmpty);
            board.Move(near, MouseButtonState.Released);
            Assert.True(board.MarkerBounds.IsEmpty);
            board.SnapTolerance = 4;
            board.Move(near, MouseButtonState.Released);
            Assert.False(board.MarkerBounds.IsEmpty);
            board.IsSnappingEnabled = false;
            Assert.True(board.MarkerBounds.IsEmpty);

            board.Press(near);
            Assert.Equal(near, Assert.IsType<Line>(manager.CurrentGeometryInEdit).Start);
        });
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

    private static Rectangle LoadRectangle(SketchBoardDataManager manager)
        => (Rectangle)manager.LoadShape<Rectangle, PointsData>(new PointsData(1, new List<Point> { new(20, 20), new(100, 80) }));

    private static ShapeVisualBase LoadCircle(SketchBoardDataManager manager, Type type, Point center, double radius)
    {
        var circle = (ShapeVisualBase)Activator.CreateInstance(type, manager.CurrentShapeLayer!)!;
        ((IDataExport<EllipseData>)circle).FromData(new EllipseData
        {
            Center = center, RadiusX = radius, RadiusY = radius, StrokeThickness = 15
        });
        manager.AddShape(circle);
        return circle;
    }

    private static Line LoadLine(SketchBoardDataManager manager, Point start, Point end)
        => (Line)manager.LoadShape<Line, PointsData>(new PointsData(1, new List<Point> { start, end }));

    private sealed class TestBoard : SketchBoard
    {
        public Rect MarkerBounds => ((DrawingVisual)GetVisualChild(VisualChildrenCount - 1)).ContentBounds;
        public void Press(Point point) => HandleLeftButtonDown(point, 1);
        public void Move(Point point, MouseButtonState state) => HandleMouseMove(point, state);
        public void Release(Point point) => HandleLeftButtonUp(point);
        public void Finish(Point point) => HandleRightButtonUp(point);
        public void Leave() => OnMouseLeave(new MouseEventArgs(Mouse.PrimaryDevice, 0));
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
