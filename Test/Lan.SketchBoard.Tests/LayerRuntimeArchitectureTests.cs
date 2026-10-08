using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Lan.Shapes.Styler;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class LayerRuntimeArchitectureTests
{
    [Fact]
    public void ExternalShapeLayers_AreOwnedAndScaledIndependentlyByEachBoard()
    {
        var definition = Layer(1, 4);
        var first = Manager(definition);
        var second = Manager(definition);
        var a = CompletedLine(definition);
        var b = CompletedLine(definition);
        first.AddShape(a);
        second.AddShape(b);
        first.OnImageViewerPropertyChanged(2);
        second.OnImageViewerPropertyChanged(4);

        Assert.Same(first.CurrentShapeLayer, a.ShapeLayer);
        Assert.Same(second.CurrentShapeLayer, b.ShapeLayer);
        Assert.NotSame(definition, a.ShapeLayer);
        Assert.NotSame(a.ShapeLayer, b.ShapeLayer);
        Assert.Equal(2, a.ShapeStyler!.SketchPen.Thickness);
        Assert.Equal(1, b.ShapeStyler!.SketchPen.Thickness);
        Assert.Equal(4, definition.GetStyler(ShapeVisualState.Normal).SketchPen.Thickness);
    }

    [Fact]
    public void DirectLayerAssignment_UsesAnOwnedCopyWithoutChangingTheDefinition()
    {
        var manager = Manager(Layer(1, 1));
        var shape = CompletedLine(manager.CurrentShapeLayer!);
        manager.AddShape(shape);
        manager.OnImageViewerPropertyChanged(2);
        var target = Layer(2, 6);

        shape.ShapeLayer = target;

        Assert.NotSame(target, shape.ShapeLayer);
        Assert.Equal(3, shape.ShapeStyler!.SketchPen.Thickness);
        Assert.Equal(6, target.GetStyler(ShapeVisualState.Normal).SketchPen.Thickness);
    }

    [Fact]
    public void DirectCollectionAddAndRemove_UseTheFullRepositoryLifecycle()
    {
        var manager = Manager(Layer(1, 4));
        manager.InitializeVisualCollection(new ContainerVisual());
        manager.OnImageViewerPropertyChanged(2);
        var shape = CompletedLine(Layer(1, 4));
        var created = 0;
        var removed = 0;
        manager.ShapeCreated += (_, _) => created++;
        manager.ShapeRemoved += (_, _) => removed++;

        manager.Shapes.Add(shape);
        Assert.Same(manager.CurrentShapeLayer, shape.ShapeLayer);
        Assert.Equal(2, shape.ShapeStyler!.SketchPen.Thickness);
        Assert.Same(shape, Assert.Single(manager.VisualCollection.Cast<Visual>()));
        manager.SetSelection(new[] { shape });
        manager.Shapes.RemoveAt(0);

        Assert.Empty(manager.Shapes);
        Assert.Empty(manager.VisualCollection.Cast<Visual>());
        Assert.Empty(manager.SelectedGeometries);
        Assert.Null(manager.SelectedGeometry);
        Assert.Equal(1, created);
        Assert.Equal(1, removed);
    }

    [Fact]
    public void DirectCollectionReplaceMoveAndClear_KeepFilteredVisualsAndEventsConsistent()
    {
        var manager = Manager(Layer(1, 1));
        manager.InitializeVisualCollection(new ContainerVisual());
        manager.SetLayerVisibility(2, false);
        var first = CompletedLine(manager.CurrentShapeLayer!);
        var hidden = CompletedLine(Layer(2, 1));
        var last = CompletedLine(manager.CurrentShapeLayer!);
        manager.Shapes.Add(first);
        manager.Shapes.Add(hidden);
        manager.Shapes.Add(last);
        manager.SetSelection(new[] { first });
        var removed = 0;
        var created = 0;
        manager.ShapeRemoved += (_, _) => removed++;
        manager.ShapeCreated += (_, _) => created++;
        var replacement = CompletedLine(Layer(1, 1));

        manager.Shapes[0] = replacement;
        Assert.Empty(manager.SelectedGeometries);
        Assert.Equal(new Visual[] { replacement, last }, manager.VisualCollection.Cast<Visual>());
        manager.Shapes.Move(2, 0);
        Assert.Equal(new Visual[] { last, replacement }, manager.VisualCollection.Cast<Visual>());
        manager.SetSelection(new[] { last, replacement });
        manager.Shapes.Clear();

        Assert.Empty(manager.VisualCollection.Cast<Visual>());
        Assert.Empty(manager.SelectedGeometries);
        Assert.Equal(4, removed);
        Assert.Equal(1, created);
    }

    [Fact]
    public void InvalidDirectCollectionReplacement_PreservesTheExistingShape()
    {
        var manager = Manager(Layer(1, 1));
        manager.InitializeVisualCollection(new ContainerVisual());
        var first = CompletedLine(manager.CurrentShapeLayer!);
        var second = CompletedLine(manager.CurrentShapeLayer!);
        manager.AddShape(first);
        manager.AddShape(second);

        Assert.Throws<InvalidOperationException>(() => manager.Shapes[0] = second);
        Assert.Equal(new[] { first, second }, manager.Shapes);
        Assert.Equal(new Visual[] { first, second }, manager.VisualCollection.Cast<Visual>());
    }

    [Fact]
    public void HidingAnUnfinishedSketch_CancelsItAndPreventsDrawingOnTheHiddenLayer()
    {
        var manager = Manager(Layer(1, 1));
        manager.InitializeVisualCollection(new ContainerVisual());
        manager.SetGeometryType(typeof(Polygon));
        var polygon = manager.CreateNewGeometry(new Point(20, 20))!;
        polygon.OnMouseLeftButtonDown(new Point(20, 20));
        polygon.OnMouseLeftButtonUp(new Point(20, 20));
        manager.SelectedGeometry = polygon;

        manager.SetLayerVisibility(1, false);

        Assert.Empty(manager.Shapes);
        Assert.Empty(manager.SelectedGeometries);
        Assert.Null(manager.CurrentGeometryInEdit);
        Assert.Null(manager.CurrentGeometryType);
        manager.SetGeometryType(typeof(Line));
        Assert.Null(manager.CreateNewGeometry(new Point(20, 20)));
        Assert.Empty(manager.Shapes);
    }

    [Fact]
    public void ReplacingAnExistingDefinition_UpdatesMeasurementAndRedrawsItsShapes()
    {
        var manager = Manager(Layer(1, 1, 2));
        var shape = CompletedLine(manager.CurrentShapeLayer!);
        manager.AddShape(shape);
        var owned = shape.ShapeLayer;
        manager.OnImageViewerPropertyChanged(2);
        var replacement = Layer(1, 8, 10);

        manager.SetShapeLayer(replacement);

        Assert.Same(owned, shape.ShapeLayer);
        Assert.Same(replacement.Measurement, shape.ShapeLayer.Measurement);
        Assert.Equal(4, shape.ShapeStyler!.SketchPen.Thickness);
        var stroke = shape.Drawing.Children.OfType<GeometryDrawing>().First();
        Assert.Equal(4, stroke.Pen!.Thickness);
    }

    [Fact]
    public void ApplyingDefinitions_PreservesRemovedLayerShapesAndSelectsAnActiveDefinition()
    {
        var manager = Manager(Layer(1, 1));
        manager.InitializeVisualCollection(new ContainerVisual());
        var orphan = CompletedLine(manager.CurrentShapeLayer!);
        manager.AddShape(orphan);
        manager.SetLayerVisibility(1, false);
        var replacement = Layer(2, 6, 10);

        manager.ApplyLayerDefinitions(new[] { replacement });

        Assert.Equal(2, manager.CurrentShapeLayer!.LayerId);
        Assert.True(manager.IsLayerVisible(1));
        Assert.Same(orphan, Assert.Single(manager.VisualCollection.Cast<Visual>()));
        manager.SetSelection(new[] { orphan });
        Assert.Same(orphan, manager.SelectedGeometry);
        Assert.Equal(1, orphan.ShapeLayer.LayerId);
    }

    [Fact]
    public void ReintroducingAnOrphanDefinition_ReusesItsBoardLayerAndUpdatesCalibration()
    {
        var manager = Manager(Layer(1, 1));
        var shape = CompletedLine(manager.CurrentShapeLayer!);
        manager.AddShape(shape);
        var owned = shape.ShapeLayer;
        manager.ApplyLayerDefinitions(new[] { Layer(2, 2) });

        var restored = Layer(1, 8, 10);
        manager.ApplyLayerDefinitions(new[] { restored });

        Assert.Same(owned, shape.ShapeLayer);
        Assert.Same(restored.Measurement, shape.ShapeLayer.Measurement);
        Assert.Equal(8, shape.ShapeStyler!.SketchPen.Thickness);
        Assert.Same(owned, manager.CurrentShapeLayer);
    }

    [Fact]
    public void RejectedReentrantCollectionWrite_PreservesSelectionAndVisuals()
    {
        var manager = Manager(Layer(1, 1));
        manager.InitializeVisualCollection(new ContainerVisual());
        var selected = CompletedLine(manager.CurrentShapeLayer!);
        manager.AddShape(selected);
        manager.SetSelection(new[] { selected });
        manager.Shapes.CollectionChanged += (_, _) =>
            Assert.Throws<InvalidOperationException>(() => manager.Shapes.RemoveAt(0));
        manager.Shapes.CollectionChanged += (_, _) => { };
        var added = CompletedLine(manager.CurrentShapeLayer!);

        manager.Shapes.Add(added);

        Assert.Equal(new[] { selected, added }, manager.Shapes);
        Assert.Equal(new Visual[] { selected, added }, manager.VisualCollection.Cast<Visual>());
        Assert.Same(selected, manager.SelectedGeometry);
    }

    [Fact]
    public void DetachingTheManager_ClearsItsHostAndSupportsReattachment()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var manager = Manager(Layer(1, 1));
            var shape = CompletedLine(manager.CurrentShapeLayer!);
            manager.AddShape(shape);
            var first = new SketchBoard { SketchBoardDataManager = manager };

            first.SketchBoardDataManager = null;

            Assert.Null(manager.SketchBoard);
            var second = new SketchBoard { SketchBoardDataManager = manager };
            Assert.Same(second, manager.SketchBoard);
            Assert.Same(shape, Assert.Single(manager.VisualCollection.Cast<Visual>()));
        });
    }

    [Fact]
    public void ReplacingWithAParentedVisual_PreservesTheExistingShapeAndItsSubscriptions()
    {
        var manager = Manager(Layer(1, 1));
        var host = new ContainerVisual();
        manager.InitializeVisualCollection(host);
        var original = CompletedLine(manager.CurrentShapeLayer!);
        manager.AddShape(original);
        manager.SetSelection(new[] { original });
        manager.CurrentGeometryInEdit = original;
        var replacement = CompletedLine(Layer(2, 1));
        var originalReplacementLayer = replacement.ShapeLayer;
        var foreignHost = new ContainerVisual();
        foreignHost.Children.Add(replacement);
        var removed = 0;
        var created = 0;
        manager.ShapeRemoved += (_, _) => removed++;
        manager.ShapeCreated += (_, _) => created++;

        Assert.Throws<ArgumentException>(() => manager.Shapes[0] = replacement);

        Assert.Same(original, Assert.Single(manager.Shapes));
        Assert.Same(original, Assert.Single(manager.VisualCollection.Cast<Visual>()));
        Assert.Same(original, Assert.Single(manager.SelectedGeometries));
        Assert.Same(original, manager.CurrentGeometryInEdit);
        Assert.Same(host, VisualTreeHelper.GetParent(original));
        Assert.Same(foreignHost, VisualTreeHelper.GetParent(replacement));
        Assert.Same(originalReplacementLayer, replacement.ShapeLayer);
        Assert.Equal(0, removed);
        Assert.Equal(0, created);
        var assignments = 0;
        manager.LayerAssignmentsChanged += (_, _) => assignments++;
        original.ShapeLayer = Layer(3, 1);
        Assert.Equal(1, assignments);
    }

    [Fact]
    public void ReplacementVisualAttachFailure_PreservesTheExistingShapeAndSelection()
    {
        var manager = Manager(Layer(1, 1));
        var host = new ContainerVisual();
        manager.InitializeVisualCollection(host);
        var original = CompletedLine(manager.CurrentShapeLayer!);
        manager.AddShape(original);
        manager.SetSelection(new[] { original });
        var replacement = new FailingAttachLine(manager.CurrentShapeLayer!);
        replacement.FromData(new PointsData(1, new List<Point> { new(20, 20), new(100, 20) }));

        Assert.Throws<InvalidOperationException>(() => manager.Shapes[0] = replacement);

        Assert.Same(original, Assert.Single(manager.Shapes));
        Assert.Same(original, Assert.Single(manager.VisualCollection.Cast<Visual>()));
        Assert.Same(original, Assert.Single(manager.SelectedGeometries));
        Assert.Null(VisualTreeHelper.GetParent(replacement));
    }

    private sealed class FailingAttachLine : Line
    {
        private bool _failNextAttach = true;
        public FailingAttachLine(ShapeLayer layer) : base(layer) { }
        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);
            if (oldParent == null && _failNextAttach)
            {
                _failNextAttach = false;
                throw new InvalidOperationException("Injected attach failure");
            }
        }
    }

    private static SketchBoardDataManager Manager(ShapeLayer layer)
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(layer);
        return manager;
    }

    private static Line CompletedLine(ShapeLayer layer)
    {
        var line = new Line(layer);
        line.FromData(new PointsData(1, new List<Point> { new(20, 20), new(100, 20) }));
        return line;
    }

    private static ShapeLayer Layer(int id, double width, double pixelsPerUnit = 1)
    {
        var parameter = TestShapeLayer.CreateWithThickness(width, 16).ToShapeLayerParameter();
        parameter.LayerId = id;
        parameter.Name = $"Layer {id}";
        return new ShapeLayer(parameter,
            new ShapeMeasurementSettings { PixelPerUnit = pixelsPerUnit }, new ShapeStylerFactory());
    }
}
