using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using Lan.Shapes;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class GeometryHistoryTests
{
    [Fact]
    public void GeometryScope_CapturesResizeAndTranslationAsOneStep() => BulkLayerFeatureTests.Sta(() =>
    {
        var manager = Manager();
        var circle = Circle(manager);
        using (Edit(circle))
        {
            circle.Translate(new Vector(5, 8));
            circle.SetCircle(new Point(40, 50), 30);
            circle.Translate(new Vector(10, 20));
        }
        Assert.True(manager.CanUndo);
        manager.Undo();
        Assert.Equal(new Point(20, 30), circle.Center);
        Assert.Equal(10, circle.Radius);
        Assert.False(manager.CanUndo);
        manager.Redo();
        Assert.Equal(new Point(50, 70), circle.Center);
        Assert.Equal(30, circle.Radius);
    });

    [Fact]
    public void NestedGeometryScopes_AreOneStep_AndReplayWorksWhenLockedOrHidden() => BulkLayerFeatureTests.Sta(() =>
    {
        var manager = Manager();
        var circle = Circle(manager);
        using (Edit(circle))
        {
            circle.SetCircle(circle.Center, 20);
            using (Edit(circle)) circle.SetCircle(new Point(40, 50), 30);
        }
        circle.IsLocked = true;
        manager.SetLayerVisibility(circle.ShapeLayer.LayerId, false);
        manager.Undo();
        Assert.Equal(10, circle.Radius);
        Assert.Equal(new Point(20, 30), circle.Center);
        Assert.True(circle.IsLocked);
        Assert.False(manager.CanUndo);
        manager.Redo();
        Assert.Equal(30, circle.Radius);
    });

    [Fact]
    public void EmptyOrReturnToOriginalEdit_PreservesRedo() => BulkLayerFeatureTests.Sta(() =>
    {
        var manager = Manager();
        var circle = Circle(manager);
        circle.Translate(new Vector(5, 8));
        manager.Undo();
        using (Edit(circle)) { }
        using (Edit(circle))
        {
            circle.SetCircle(circle.Center, 30);
            circle.SetCircle(circle.Center, 10);
        }
        Assert.False(manager.CanUndo);
        Assert.True(manager.CanRedo);
        manager.Redo();
        Assert.Equal(new Point(25, 38), circle.Center);
    });

    [Fact]
    public void MoveResizeAndDelete_AreThreeOrderedSteps() => BulkLayerFeatureTests.Sta(() =>
    {
        var manager = Manager();
        var circle = Circle(manager);
        circle.Translate(new Vector(5, 8));
        using (Edit(circle)) circle.SetCircle(circle.Center, 30);
        manager.RemoveShape(circle);
        manager.Undo();
        Assert.Same(circle, Assert.Single(manager.Shapes));
        Assert.Equal(30, circle.Radius);
        manager.Undo();
        Assert.Equal(10, circle.Radius);
        Assert.Equal(new Point(25, 38), circle.Center);
        manager.Undo();
        Assert.Equal(new Point(20, 30), circle.Center);
        Assert.False(manager.CanUndo);
        manager.Redo(); manager.Redo(); manager.Redo();
        Assert.Empty(manager.Shapes);
    });

    [Fact]
    public void MixedEditsInsideOneTransaction_ReplayInOriginalOrder() => BulkLayerFeatureTests.Sta(() =>
    {
        var manager = Manager();
        var circle = Circle(manager);
        using (manager.BeginHistoryTransaction())
        {
            circle.Translate(new Vector(5, 8));
            using (Edit(circle)) circle.SetCircle(new Point(40, 50), 30);
            circle.Translate(new Vector(10, 20));
        }
        manager.Undo();
        Assert.Equal(new Point(20, 30), circle.Center);
        Assert.Equal(10, circle.Radius);
        Assert.False(manager.CanUndo);
        manager.Redo();
        Assert.Equal(new Point(50, 70), circle.Center);
        Assert.Equal(30, circle.Radius);
    });

    [Fact]
    public void CompletedGeometryScope_DisposeTwiceDoesNotCreateAdditionalSteps() => BulkLayerFeatureTests.Sta(() =>
    {
        var manager = Manager();
        var circle = Circle(manager);
        var scope = Edit(circle);
        circle.SetCircle(circle.Center, 20);
        scope.Dispose();
        scope.Dispose();
        Assert.False(circle.IsEditingGeometry);
        manager.Undo();
        Assert.Equal(10, circle.Radius);
        Assert.False(manager.CanUndo);
    });

    [Fact]
    public void FailedFinalSnapshot_InvalidatesHistoryAndEndsGeometryScope() => BulkLayerFeatureTests.Sta(() =>
    {
        var manager = Manager();
        var circle = new FailingSnapshotCircle(manager.CurrentShapeLayer!);
        circle.FromData(new EllipseData { Center = new Point(20, 30), RadiusX = 10 });
        manager.AddShape(circle);
        circle.Translate(new Vector(5, 8));
        var scope = Edit(circle);
        circle.SetCircle(circle.Center, 20);
        circle.FailSnapshot = true;
        Assert.Throws<InvalidOperationException>(() => scope.Dispose());
        Assert.False(circle.IsEditingGeometry);
        Assert.False(manager.CanUndo);
        Assert.False(manager.CanRedo);
    });

    private sealed class FailingSnapshotCircle : Circle
    {
        public FailingSnapshotCircle(ShapeLayer layer) : base(layer) { }
        public bool FailSnapshot { get; set; }
        protected override object CaptureGeometryStateCore()
            => FailSnapshot ? throw new InvalidOperationException("snapshot failure") : base.CaptureGeometryStateCore();
    }

    private static IDisposable Edit(ShapeVisualBase shape)
    {
        var method = typeof(ShapeVisualBase).GetMethod("BeginGeometryEdit", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<IDisposable>(method!.Invoke(shape, null));
    }
    private static SketchBoardDataManager Manager()
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        return manager;
    }
    private static Circle Circle(SketchBoardDataManager manager)
    {
        var circle = new Circle(manager.CurrentShapeLayer!);
        circle.FromData(new EllipseData { Center = new Point(20, 30), RadiusX = 10 });
        manager.AddShape(circle);
        return circle;
    }
}
