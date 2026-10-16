using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeHistoryTests
{
    [Fact]
    public void Deletion_UndoRestoresIdentityOrderSelectionAndGroup_RedoRemovesAgain()
    {
        var manager = Manager();
        var a = Add(manager, 10);
        var b = Add(manager, 30);
        var c = Add(manager, 50);
        var group = manager.GroupShapes(new[] { a, c }, "original");
        manager.SetSelection(new[] { a });
        manager.InitializeVisualCollection(new ContainerVisual());
        var history = History(manager);
        using (history.BeginHistoryTransaction())
        {
            manager.RemoveShape(a);
            manager.RemoveShape(c);
        }
        Assert.Same(b, Assert.Single(manager.Shapes));
        history.Undo();
        Assert.Equal(new[] { a, b, c }, manager.Shapes);
        Assert.Same(group, Assert.Single(manager.Groups));
        Assert.Equal(new[] { a, c }, manager.SelectedGeometries);
        Assert.Equal(new[] { a, b, c }, manager.VisualCollection.Cast<ShapeVisualBase>());
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        history.Redo();
        Assert.Same(b, Assert.Single(manager.Shapes));
        Assert.Empty(manager.Groups);
    }

    [Fact]
    public void RepeatedTranslationsInNestedTransaction_AreOneStepAndReplayIsNotRecorded()
    {
        var manager = Manager();
        var a = Add(manager, 10);
        var b = Add(manager, 30);
        manager.GroupShapes(new[] { a, b });
        var history = History(manager);
        using (history.BeginHistoryTransaction())
        {
            manager.TranslateShapes(new[] { a }, new Vector(5, 6));
            using (history.BeginHistoryTransaction())
                manager.TranslateShapes(new[] { a }, new Vector(10, 4));
        }
        history.Undo();
        Assert.Equal(new Point(10, 10), a.Start);
        Assert.Equal(new Point(10, 30), b.Start);
        Assert.False(history.CanUndo);
        history.Redo();
        Assert.Equal(new Point(25, 20), a.Start);
        Assert.Equal(new Point(25, 40), b.Start);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void DirectShapeTranslation_IsRecordedAndRestoresExactCoordinatesEvenWhenLockedOrHidden()
    {
        var manager = Manager();
        var a = Add(manager, 10);
        a.SetEndpoints(new Point(0.1, 0.2), new Point(30.3, 40.4));
        var history = History(manager);
        a.Translate(new Vector(9999, 8888));
        a.IsLocked = true;
        manager.SetLayerVisibility(a.ShapeLayer.LayerId, false);
        history.Undo();
        Assert.Equal(new Point(0.1, 0.2), a.Start);
        Assert.Equal(new Point(30.3, 40.4), a.End);
        Assert.True(a.IsLocked);
        history.Redo();
        Assert.Equal(new Point(9999.1, 8888.2), a.Start);
    }

    [Fact]
    public void NewEditAfterUndo_ClearsRedoAndHistoriesAreIndependent()
    {
        var manager = Manager();
        var a = Add(manager, 10);
        var b = Add(manager, 30);
        var history = History(manager);
        manager.RemoveShape(a);
        history.Undo();
        manager.RemoveShape(b);
        Assert.False(history.CanRedo);
        Assert.False(History(Manager()).CanUndo);
    }

    [Fact]
    public void ZeroMovementAndAbsentDeletion_DoNotCreateSteps()
    {
        var manager = Manager();
        var a = Add(manager, 10);
        var history = History(manager);
        using (history.BeginHistoryTransaction())
        {
            a.Translate(new Vector(5, 6));
            a.Translate(new Vector(-5, -6));
        }
        manager.TranslateShapes(new[] { a }, default);
        manager.RemoveShape(new Line(manager.CurrentShapeLayer!));
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void UntrackedGeometryEdit_InvalidatesPreviousHistory()
    {
        var manager = Manager();
        var a = Add(manager, 10);
        var history = History(manager);
        a.Translate(new Vector(5, 6));
        a.SetEndpoints(new Point(1, 2), new Point(3, 4));
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void ClearAllAndPredicateRemoval_EachUndoAsOneStep()
    {
        var manager = Manager();
        var a = Add(manager, 10);
        var b = Add(manager, 30);
        var history = History(manager);
        manager.ClearAllShapes();
        history.Undo();
        Assert.Equal(new[] { a, b }, manager.Shapes);
        Assert.False(history.CanUndo);
        manager.RemoveShapes(_ => true);
        history.Undo();
        Assert.Equal(new[] { a, b }, manager.Shapes);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void HistoryRetainsAtMostOneHundredSteps()
    {
        var manager = Manager();
        var a = Add(manager, 10);
        var history = History(manager);
        for (var i = 0; i < 110; i++) a.Translate(new Vector(1, 0));
        var count = 0;
        while (history.CanUndo) { history.Undo(); count++; }
        Assert.Equal(100, count);
        Assert.Equal(new Point(20, 10), a.Start);
    }

    [Fact]
    public void SelectionHoverLockVisibilityAndZoom_PreserveHistory()
    {
        var manager = Manager();
        var a = Add(manager, 10);
        var history = History(manager);
        a.Translate(new Vector(5, 6));
        manager.SetSelection(new[] { a });
        a.SetHoverAppearance(true);
        a.SetHoverAppearance(false);
        manager.UnselectGeometry();
        a.IsLocked = true;
        manager.SetLayerVisibility(a.ShapeLayer.LayerId, false);
        manager.OnImageViewerPropertyChanged(2);
        Assert.True(history.CanUndo);
        history.Undo();
        Assert.Equal(new Point(10, 10), a.Start);
    }

    [Fact]
    public void NewShapeAndRegrouping_InvalidateRedo()
    {
        var manager = Manager();
        var a = Add(manager, 10);
        var b = Add(manager, 20);
        var history = History(manager);
        a.Translate(new Vector(5, 6));
        history.Undo();
        Add(manager, 40);
        Assert.False(history.CanRedo);
        a.Translate(new Vector(5, 6));
        history.Undo();
        manager.GroupShapes(new[] { a, b });
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void FailureDuringTranslation_DiscardsStaleHistory()
    {
        var manager = Manager();
        var a = new ThrowingLine(manager.CurrentShapeLayer!);
        a.FromData(new PointsData(1, new List<Point> { new(10, 10), new(30, 10) }));
        manager.AddShape(a);
        var history = History(manager);
        a.Translate(new Vector(1, 0));
        a.ThrowAfterTranslate = true;
        Assert.Throws<InvalidOperationException>(() => a.Translate(new Vector(5, 0)));
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    private sealed class ThrowingLine : Line
    {
        public ThrowingLine(ShapeLayer layer) : base(layer) { }
        public bool ThrowAfterTranslate { get; set; }
        protected override void TranslateCore(Vector delta)
        {
            base.TranslateCore(delta);
            if (ThrowAfterTranslate) throw new InvalidOperationException("translation failure");
        }
    }

    private static IShapeHistory History(SketchBoardDataManager manager) => Assert.IsAssignableFrom<IShapeHistory>(manager);
    private static SketchBoardDataManager Manager()
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        return manager;
    }
    private static Line Add(SketchBoardDataManager manager, double y)
    {
        var line = new Line(manager.CurrentShapeLayer!);
        line.FromData(new PointsData(1, new List<Point> { new(10, y), new(30, y) }));
        manager.AddShape(line);
        return line;
    }
}
