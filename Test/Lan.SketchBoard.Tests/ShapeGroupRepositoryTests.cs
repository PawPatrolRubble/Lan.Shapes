using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeGroupRepositoryTests
{
    [Fact]
    public void Grouping_PreservesShapesAndVisualOrderAndSelectingMemberSelectsWholeGroup()
    {
        var manager = Manager();
        manager.InitializeVisualCollection(new ContainerVisual());
        var first = AddLine(manager, 10);
        var other = AddLine(manager, 20);
        var second = AddLine(manager, 30);
        var group = manager.GroupShapes(new[] { first, second, first }, "Pair");
        Assert.Equal("Pair", group.Name);
        Assert.NotEqual(Guid.Empty, group.Id);
        Assert.Equal(new[] { first, second }, group.Members);
        Assert.Same(group, Assert.Single(manager.Groups));
        Assert.Same(group, manager.GetGroup(second));
        Assert.Null(manager.GetGroup(other));
        Assert.Equal(new[] { first, other, second }, manager.Shapes);
        Assert.Equal(new Visual[] { first, other, second }, manager.VisualCollection.Cast<Visual>());
        manager.SelectedGeometry = first;
        Assert.Equal(new[] { first, second }, manager.SelectedGeometries);
        manager.SetSelection(new[] { other, second, first });
        Assert.Equal(new[] { other, first, second }, manager.SelectedGeometries);
        manager.SelectedGeometry = other;
        Assert.Same(other, Assert.Single(manager.SelectedGeometries));
        manager.UnselectGeometry();
        Assert.Same(group, manager.GetGroup(first));
    }

    [Fact]
    public void Grouping_ASelectedMemberExpandsSelectionAndCopiesTheMembershipInput()
    {
        var manager = Manager();
        var first = AddLine(manager, 10);
        var second = AddLine(manager, 20);
        manager.SelectedGeometry = first;
        var input = new List<ShapeVisualBase> { first, second };
        var group = manager.GroupShapes(input);
        input.Clear();
        Assert.Equal(new[] { first, second }, manager.SelectedGeometries);
        Assert.Equal(new[] { first, second }, group.Members);
        Assert.Throws<NotSupportedException>(() => ((IList<ShapeVisualBase>)group.Members).Remove(first));
    }

    [Theory]
    [InlineData("one")]
    [InlineData("duplicates")]
    [InlineData("mixed-layer")]
    [InlineData("locked")]
    [InlineData("hidden")]
    [InlineData("unfinished")]
    [InlineData("foreign")]
    [InlineData("unsupported")]
    [InlineData("grouped")]
    [InlineData("null")]
    [InlineData("singular-transform")]
    [InlineData("non-finite-transform")]
    public void InvalidGrouping_PreservesGeometrySelectionAndExistingGroups(string reason)
    {
        var manager = Manager();
        var first = AddLine(manager, 10);
        var second = AddLine(manager, 20);
        manager.SetSelection(new[] { first, second });
        var candidates = new List<ShapeVisualBase> { first, second };
        switch (reason)
        {
            case "one": candidates.Remove(second); break;
            case "duplicates": candidates[1] = first; break;
            case "mixed-layer": second.ShapeLayer = BulkLayerFeatureTests.Layer(2, "Second"); break;
            case "locked": second.Lock(); break;
            case "hidden": manager.SetLayerVisibility(first.ShapeLayer.LayerId, false); break;
            case "unfinished": candidates[1] = new Line(manager.CurrentShapeLayer!); manager.AddShape(candidates[1]); break;
            case "foreign": candidates[1] = CompletedLine(manager.CurrentShapeLayer!, 20); break;
            case "unsupported": candidates[1] = CompletedLine(manager.CurrentShapeLayer!, 20, movable: false); manager.AddShape(candidates[1]); break;
            case "grouped": manager.GroupShapes(candidates); break;
            case "null": candidates[1] = null!; break;
            case "singular-transform": second.Transform = new ScaleTransform(0, 1); break;
            case "non-finite-transform": second.Transform = new MatrixTransform(new Matrix(1, 0, 0, 1, double.NaN, 0)); break;
        }
        var selection = manager.SelectedGeometries.ToArray();
        var existingGroups = manager.Groups.ToArray();
        Assert.False(manager.CanGroupShapes(candidates));
        Assert.Throws<ArgumentException>(() => manager.GroupShapes(candidates));
        Assert.Equal(existingGroups, manager.Groups);
        Assert.Equal(selection, manager.SelectedGeometries);
        Assert.Equal(new Point(10, 10), first.Start);
        Assert.Equal(new Point(10, 20), second.Start);
        Assert.Equal(reason == "locked", second.IsLocked);
    }

    [Fact]
    public void Ungrouping_LockedMembersPreservesGeometrySelectionAndLockState()
    {
        var manager = Manager();
        var first = AddLine(manager, 10);
        var second = AddLine(manager, 20);
        manager.GroupShapes(new[] { first, second });
        var firstId = first.Id;
        var secondId = second.Id;
        var layer = first.ShapeLayer;
        second.Lock();
        manager.SelectedGeometry = first;
        Assert.True(second.IsLocked);
        Assert.Equal(1, manager.UngroupShapes(new[] { first, second, first }));
        Assert.Empty(manager.Groups);
        Assert.Null(manager.GetGroup(first));
        Assert.Equal(new[] { first, second }, manager.SelectedGeometries);
        Assert.True(second.IsLocked);
        Assert.Equal(firstId, first.Id);
        Assert.Equal(secondId, second.Id);
        Assert.Same(layer, first.ShapeLayer);
        Assert.Same(layer, second.ShapeLayer);
        Assert.Equal(new Point(10, 10), first.Start);
        Assert.Equal(new Point(10, 20), second.Start);
        manager.SelectedGeometry = first;
        Assert.Same(first, Assert.Single(manager.SelectedGeometries));
        Assert.False(second.IsSelected);
        Assert.True(second.IsLocked);
    }

    [Fact]
    public void Ungrouping_SeveralGroupsCountsGroupsAndPreservesTheShapeCollection()
    {
        var manager = Manager();
        var a = AddLine(manager, 10);
        var b = AddLine(manager, 20);
        var c = AddLine(manager, 30);
        var d = AddLine(manager, 40);
        manager.GroupShapes(new[] { a, b });
        manager.GroupShapes(new[] { c, d });
        Assert.Equal(2, manager.UngroupShapes(new[] { b, c, a }));
        Assert.Equal(new[] { a, b, c, d }, manager.Shapes);
        Assert.Empty(manager.Groups);
        Assert.Equal(0, manager.UngroupShapes(new[] { a }));
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("replace")]
    [InlineData("clear")]
    public void DirectCollectionMutations_DissolveGroupsAndCleanSelectionAndVisuals(string operation)
    {
        var manager = Manager();
        manager.InitializeVisualCollection(new ContainerVisual());
        var first = AddLine(manager, 10);
        var second = AddLine(manager, 20);
        manager.GroupShapes(new[] { first, second });
        manager.SelectedGeometry = first;
        switch (operation)
        {
            case "remove": manager.Shapes.Remove(first); break;
            case "replace": manager.Shapes[0] = CompletedLine(manager.CurrentShapeLayer!, 30); break;
            case "clear": manager.Shapes.Clear(); break;
        }
        Assert.Empty(manager.Groups);
        Assert.Null(manager.GetGroup(first));
        Assert.Null(manager.GetGroup(second));
        Assert.DoesNotContain(first, manager.SelectedGeometries);
        Assert.False(first.IsSelected);
        Assert.Equal(manager.Shapes.Cast<Visual>(), manager.VisualCollection.Cast<Visual>());
        if (operation == "clear") Assert.Empty(manager.SelectedGeometries);
        else Assert.Same(second, Assert.Single(manager.SelectedGeometries));
    }

    [Fact]
    public void AssigningOneGroupMemberToLayer_MovesWholeGroupAndPreservesRelationship()
    {
        var manager = Manager();
        var first = AddLine(manager, 10);
        var second = AddLine(manager, 20);
        var group = manager.GroupShapes(new[] { first, second });
        manager.SelectedGeometry = first;
        Assert.Equal(2, manager.AssignShapesToLayer(new[] { second }, BulkLayerFeatureTests.Layer(2, "Second")));
        Assert.All(group.Members, shape => Assert.Equal(2, shape.ShapeLayer.LayerId));
        Assert.Same(group, manager.GetGroup(first));
        Assert.Equal(new[] { first, second }, manager.SelectedGeometries);
        manager.SetLayerVisibility(3, false);
        Assert.Equal(2, manager.AssignShapesToLayer(new[] { first }, BulkLayerFeatureTests.Layer(3, "Hidden")));
        Assert.Empty(manager.SelectedGeometries);
        Assert.Same(group, manager.GetGroup(first));
    }

    [Fact]
    public void ChangingAMembersLayerDirectly_DissolvesTheGroup()
    {
        var manager = Manager();
        var first = AddLine(manager, 10);
        var second = AddLine(manager, 20);
        manager.GroupShapes(new[] { first, second });
        second.ShapeLayer = BulkLayerFeatureTests.Layer(2, "Second");
        Assert.Empty(manager.Groups);
        Assert.Null(manager.GetGroup(first));
        Assert.Equal(1, first.ShapeLayer.LayerId);
        Assert.Equal(2, second.ShapeLayer.LayerId);
        manager.SelectedGeometry = first;
        Assert.Same(first, Assert.Single(manager.SelectedGeometries));
    }

    [Fact]
    public void AssigningAGroup_RenderFailureRestoresAllLayersSelectionAndMembership()
    {
        var manager = Manager();
        manager.InitializeVisualCollection(new ContainerVisual());
        var first = AddLine(manager, 10);
        var second = new FailingLayerLine(manager.CurrentShapeLayer!);
        second.FromData(new PointsData(1, new List<Point> { new(10, 20), new(30, 20) }));
        manager.AddShape(second);
        var group = manager.GroupShapes(new[] { first, second });
        manager.SelectedGeometry = first;
        second.FailNextLayerChange = true;
        Assert.Throws<InvalidOperationException>(() => manager.AssignShapesToLayer(new[] { first }, BulkLayerFeatureTests.Layer(2, "Second")));
        Assert.All(group.Members, shape => Assert.Equal(1, shape.ShapeLayer.LayerId));
        Assert.Same(group, manager.GetGroup(first));
        Assert.Same(group, manager.GetGroup(second));
        Assert.Equal(new[] { first, second }, manager.SelectedGeometries);
        Assert.Equal(new Visual[] { first, second }, manager.VisualCollection.Cast<Visual>());
    }

    [Fact]
    public void HidingAGroup_ClearsSelectionAndPreventsMovementWithoutLosingRelationship()
    {
        var manager = Manager();
        manager.InitializeVisualCollection(new ContainerVisual());
        var first = AddLine(manager, 10);
        var second = AddLine(manager, 20);
        var group = manager.GroupShapes(new[] { first, second });
        manager.SelectedGeometry = first;
        manager.SetLayerVisibility(first.ShapeLayer.LayerId, false);
        Assert.Empty(manager.SelectedGeometries);
        Assert.Empty(manager.VisualCollection.Cast<Visual>());
        Assert.Throws<ArgumentException>(() => manager.TranslateShapes(new[] { first }, new Vector(15, 20)));
        Assert.Same(group, manager.GetGroup(first));
        Assert.Equal(new Point(10, 10), first.Start);
        Assert.Equal(new Point(10, 20), second.Start);
        manager.SetLayerVisibility(first.ShapeLayer.LayerId, true);
        manager.SelectedGeometry = second;
        Assert.Equal(new[] { first, second }, manager.SelectedGeometries);
    }

    [Fact]
    public void TranslatingALockedGroup_RejectsTheWholeMoveAndPreservesLockDuringSelection()
    {
        var manager = Manager();
        var first = AddLine(manager, 10);
        var second = AddLine(manager, 20);
        manager.GroupShapes(new[] { first, second });
        second.Lock();
        manager.SetSelection(new[] { first });
        Assert.Equal(new[] { first, second }, manager.SelectedGeometries);
        Assert.True(second.IsLocked);
        Assert.Throws<ArgumentException>(() => manager.TranslateShapes(new[] { first }, new Vector(15, 20)));
        Assert.Equal(new Point(10, 10), first.Start);
        Assert.Equal(new Point(10, 20), second.Start);
        second.UnLock();
        Assert.Equal(2, manager.TranslateShapes(new[] { first, second, first }, new Vector(15, 20)));
        Assert.Equal(new Point(25, 30), first.Start);
        Assert.Equal(new Point(25, 40), second.Start);
    }

    [Fact]
    public void Translation_ConvertsBoardDisplacementThroughEachMembersVisualTransform()
    {
        var manager = Manager();
        var first = AddLine(manager, 10);
        var second = AddLine(manager, 20);
        first.Transform = new ScaleTransform(2, 4);
        second.Transform = new RotateTransform(90);
        var group = manager.GroupShapes(new[] { first, second });
        var firstId = first.Id;
        var secondId = second.Id;
        Assert.Equal(2, manager.TranslateShapes(new[] { second }, new Vector(20, 12)));
        Assert.Equal(new Point(20, 13), first.Start);
        Assert.Equal(new Point(40, 13), first.End);
        Assert.Equal(22, second.Start.X, 8);
        Assert.Equal(0, second.Start.Y, 8);
        Assert.Equal(42, second.End.X, 8);
        Assert.Equal(0, second.End.Y, 8);
        Assert.Equal(firstId, first.Id);
        Assert.Equal(secondId, second.Id);
        Assert.Same(group, manager.GetGroup(second));
        Assert.Equal(0, manager.TranslateShapes(new[] { first }, new Vector()));
    }

    [Theory]
    [InlineData("singular-transform")]
    [InlineData("non-finite-transform")]
    [InlineData("non-finite-delta")]
    [InlineData("foreign")]
    [InlineData("unsupported")]
    [InlineData("unfinished")]
    public void InvalidTranslation_PrechecksAllMembersBeforeMovingAnyShape(string reason)
    {
        var manager = Manager();
        var first = AddLine(manager, 10);
        var second = AddLine(manager, 20);
        ShapeVisualBase other = second;
        var delta = new Vector(15, 20);
        switch (reason)
        {
            case "singular-transform": second.Transform = new ScaleTransform(0, 1); break;
            case "non-finite-transform": second.Transform = new MatrixTransform(new Matrix(1, 0, 0, 1, 0, double.PositiveInfinity)); break;
            case "non-finite-delta": delta = new Vector(double.NaN, 20); break;
            case "foreign": other = CompletedLine(manager.CurrentShapeLayer!, 30); break;
            case "unsupported": other = CompletedLine(manager.CurrentShapeLayer!, 30, movable: false); manager.AddShape(other); break;
            case "unfinished": other = new Line(manager.CurrentShapeLayer!); manager.AddShape(other); break;
        }
        Assert.Throws<ArgumentException>(() => manager.TranslateShapes(new[] { first, other }, delta));
        Assert.Equal(new Point(10, 10), first.Start);
        Assert.Equal(new Point(10, 20), second.Start);
    }

    private static SketchBoardDataManager Manager()
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        return manager;
    }

    private static Line AddLine(SketchBoardDataManager manager, double y)
    {
        var line = CompletedLine(manager.CurrentShapeLayer!, y);
        manager.AddShape(line);
        return line;
    }

    private static Line CompletedLine(ShapeLayer layer, double y, bool movable = true)
    {
        var line = movable ? new Line(layer) : new NonMovableLine(layer);
        line.FromData(new PointsData(layer.LayerId, new List<Point> { new(10, y), new(30, y) }));
        return line;
    }

    private sealed class NonMovableLine : Line
    {
        public NonMovableLine(ShapeLayer layer) : base(layer) { }
        public override bool CanTranslate => false;
    }

    private sealed class FailingLayerLine : Line
    {
        public FailingLayerLine(ShapeLayer layer) : base(layer) { }
        public bool FailNextLayerChange { get; set; }
        protected override void OnViewportScaleChanged(double scale)
        {
            if (FailNextLayerChange && ShapeLayer.LayerId == 2)
            {
                FailNextLayerChange = false;
                throw new InvalidOperationException("Injected group layer render failure.");
            }
            base.OnViewportScaleChanged(scale);
        }
    }
}
