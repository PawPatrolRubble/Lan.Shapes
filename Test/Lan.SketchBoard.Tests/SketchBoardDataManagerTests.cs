using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Shapes;
using Lan.SketchBoard;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class SketchBoardDataManagerTests
{
    [Fact]
    public void InitializeVisualCollection_PreservesShapesAddedBeforeHostAttachment()
    {
        var manager = CreateManager();
        var shape = new Line(manager.CurrentShapeLayer!);

        manager.AddShape(shape);
        manager.InitializeVisualCollection(new ContainerVisual());

        Assert.Single(manager.Shapes);
        Assert.Single(manager.VisualCollection.Cast<Visual>());
        Assert.Same(shape, manager.VisualCollection[0]);
    }

    [Fact]
    public void InitializeVisualCollection_ReattachesExistingShapesToNewHost()
    {
        var manager = CreateManager();
        var shape = new Line(manager.CurrentShapeLayer!);
        manager.AddShape(shape);
        manager.InitializeVisualCollection(new ContainerVisual());
        var previousVisuals = manager.VisualCollection;

        manager.InitializeVisualCollection(new ContainerVisual());

        Assert.Empty(previousVisuals.Cast<Visual>());
        Assert.Single(manager.Shapes);
        Assert.Same(shape, manager.VisualCollection[0]);
    }

    [Fact]
    public void CreateNewGeometry_AddsOneShapeAndRaisesOneCreatedEvent()
    {
        var manager = CreateManager();
        manager.InitializeVisualCollection(new ContainerVisual());
        manager.SetGeometryType(typeof(Line));
        var createdEvents = 0;
        manager.ShapeCreated += (_, _) => createdEvents++;

        var shape = manager.CreateNewGeometry(new Point(10, 20));

        Assert.IsType<Line>(shape);
        Assert.Same(shape, manager.CurrentGeometryInEdit);
        Assert.Single(manager.Shapes);
        Assert.Single(manager.VisualCollection.Cast<Visual>());
        Assert.Equal(1, createdEvents);
    }

    [Fact]
    public void RemoveShape_ClearsSelectionAndKeepsCollectionsSynchronized()
    {
        var manager = CreateManager();
        manager.InitializeVisualCollection(new ContainerVisual());
        var shape = new Line(manager.CurrentShapeLayer!);
        manager.AddShape(shape);
        manager.CurrentGeometryInEdit = shape;
        manager.SelectedGeometry = shape;
        var removedEvents = 0;
        var unselectedEvents = 0;
        manager.ShapeRemoved += (_, _) => removedEvents++;
        manager.ShapeUnselected += (_, _) => unselectedEvents++;

        manager.RemoveShape(shape);

        Assert.Null(manager.SelectedGeometry);
        Assert.Null(manager.CurrentGeometryInEdit);
        Assert.Empty(manager.Shapes);
        Assert.Empty(manager.VisualCollection.Cast<Visual>());
        Assert.Equal(1, removedEvents);
        Assert.Equal(1, unselectedEvents);
    }

    [Fact]
    public void RemoveAtRange_UsesTheSameRemovalLifecycleForEveryShape()
    {
        var manager = CreateManager();
        manager.InitializeVisualCollection(new ContainerVisual());
        var shapes = Enumerable.Range(0, 3)
            .Select(_ => new Line(manager.CurrentShapeLayer!))
            .ToArray();
        foreach (var shape in shapes)
        {
            manager.AddShape(shape);
        }

        var removedEvents = 0;
        manager.ShapeRemoved += (_, _) => removedEvents++;

        manager.RemoveAt(1, 2);

        Assert.Single(manager.Shapes);
        Assert.Same(shapes[0], manager.Shapes[0]);
        Assert.Single(manager.VisualCollection.Cast<Visual>());
        Assert.Equal(2, removedEvents);
    }

    [Fact]
    public void SetGeometryType_RejectsTypesThatAreNotConcreteShapes()
    {
        var manager = CreateManager();

        var exception = Assert.Throws<ArgumentException>(() => manager.SetGeometryType(typeof(string)));

        Assert.Contains(nameof(ShapeVisualBase), exception.Message);
    }

    private static SketchBoardDataManager CreateManager()
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        return manager;
    }
}

internal static class TestShapeLayer
{
    public static ShapeLayer Create()
    {
        return CreateWithThickness(stroke: 1, handle: 10);
    }

    public static ShapeLayer CreateWithThickness(double stroke, double handle)
    {
        return new ShapeLayer(new ShapeLayerParameter
        {
            LayerId = 1,
            Name = "Tests",
            Description = "Test layer",
            MaximumThickenedShapeWidth = 100,
            TagFontSize = 12,
            TextForeground = new SolidColorBrush(Colors.Black),
            BorderBackground = new SolidColorBrush(Colors.LightBlue),
            StyleSchema = new Dictionary<ShapeVisualState, ShapeStylerParameter>
            {
                [ShapeVisualState.Normal] = new ShapeStylerParameter
                {
                    FillColor = new SolidColorBrush(Colors.Transparent),
                    StrokeColor = new SolidColorBrush(Colors.Red),
                    StrokeThickness = stroke,
                    DashStyle = "Solid",
                    DragHandleSize = handle,
                    FillOpacity = 0
                },
                [ShapeVisualState.Selected] = new ShapeStylerParameter
                {
                    FillColor = new SolidColorBrush(Colors.Transparent),
                    StrokeColor = new SolidColorBrush(Colors.Blue),
                    StrokeThickness = stroke,
                    DashStyle = "Solid",
                    DragHandleSize = handle,
                    FillOpacity = 0
                }
            }
        });
    }
}
