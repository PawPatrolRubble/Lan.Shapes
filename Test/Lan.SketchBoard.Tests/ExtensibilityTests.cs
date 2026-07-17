using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;
using Lan.Shapes.Styler;
using Lan.SketchBoard;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ExtensibilityTests
{
    [Fact]
    public void GeometryIconProvider_UsesDictionaryAndAliases()
    {
        var resources = new ResourceDictionary
        {
            ["Circle"] = Geometry.Parse("M0,0 L1,0"),
            ["Grid"] = Geometry.Parse("M0,0 L2,0")
        };
        IGeometryIconProvider provider = new ResourceDictionaryGeometryIconProvider(resources);

        Assert.Same(resources["Circle"], provider.GetIcon("Circle"));
        Assert.Same(resources["Grid"], provider.GetIcon("GriddedRectangle"));
        Assert.Null(provider.GetIcon("MissingType"));
        Assert.Null(provider.GetIcon(null!));
    }

    [Fact]
    public void ViewModel_PaletteUsesInjectedIconProvider()
    {
        var icon = Geometry.Parse("M0,0 L3,0");
        var provider = new StubIconProvider(new Dictionary<string, Geometry?>
        {
            ["Line"] = icon
        });

        var layer = TestShapeLayer.Create();
        var layerManager = new ShapeLayerManager();
        layerManager.Layers.Add(layer);

        var geometryTypeManager = new GeometryTypeManager();
        geometryTypeManager.RegisterGeometryType<Line>();

        var manager = new SketchBoardDataManager();
        var vm = new ImageViewerControlViewModel(
            layerManager,
            manager,
            geometryTypeManager,
            provider);

        var entry = Assert.Single(vm.GeometryTypeList);
        Assert.Equal("Line", entry.Name);
        Assert.Same(icon, entry.IconGeometry);
    }

    [Fact]
    public void ShapeLayer_UsesInjectedStylerFactory()
    {
        var factory = new CountingStylerFactory();
        var layer = new ShapeLayer(CreateParameter(), factory);

        Assert.Equal(2, factory.CreateCount); // Normal + Selected
        Assert.NotNull(layer.GetStyler(ShapeVisualState.Normal));
        Assert.NotNull(layer.GetStyler(ShapeVisualState.Selected));
    }

    [Fact]
    public void ShapeCreationCancelled_RemovesShapeFromRepository()
    {
        var manager = new SketchBoardDataManager();
        manager.SetShapeLayer(TestShapeLayer.Create());
        manager.InitializeVisualCollection(new ContainerVisual());

        var keep = new Line(manager.CurrentShapeLayer!);
        manager.AddShape(keep);

        var cancellable = new CancellableShape(manager.CurrentShapeLayer!);
        manager.AddShape(cancellable);
        Assert.Equal(2, manager.Shapes.Count);

        cancellable.Cancel();

        Assert.Single(manager.Shapes);
        Assert.DoesNotContain(cancellable, manager.Shapes);
        Assert.Contains(keep, manager.Shapes);
    }

    [Fact]
    public void BoardContextAware_ContractReceivesBoardSize()
    {
        var layer = TestShapeLayer.Create();
        var shape = new ContextAwareShape(layer);

        shape.OnBoardContextAvailable(320, 240);

        Assert.True(shape.ContextReceived);
        Assert.Equal(320, shape.BoardWidth);
        Assert.Equal(240, shape.BoardHeight);
    }

    private static ShapeLayerParameter CreateParameter()
    {
        return new ShapeLayerParameter
        {
            LayerId = 9,
            Name = "Factory",
            Description = "test",
            MaximumThickenedShapeWidth = 10,
            TagFontSize = 12,
            UnitsPerMillimeter = 1,
            PixelPerUnit = 1,
            UnitName = "px",
            TextForeground = new SolidColorBrush(Colors.Black),
            BorderBackground = new SolidColorBrush(Colors.White),
            StyleSchema = new Dictionary<ShapeVisualState, ShapeStylerParameter>
            {
                [ShapeVisualState.Normal] = new ShapeStylerParameter
                {
                    FillColor = new SolidColorBrush(Colors.Transparent),
                    StrokeColor = new SolidColorBrush(Colors.Red),
                    StrokeThickness = 1,
                    DashStyle = "Solid",
                    DragHandleSize = 8,
                    FillOpacity = 0
                },
                [ShapeVisualState.Selected] = new ShapeStylerParameter
                {
                    FillColor = new SolidColorBrush(Colors.Transparent),
                    StrokeColor = new SolidColorBrush(Colors.Blue),
                    StrokeThickness = 1,
                    DashStyle = "Solid",
                    DragHandleSize = 8,
                    FillOpacity = 0
                }
            }
        };
    }

    private sealed class StubIconProvider : IGeometryIconProvider
    {
        private readonly IReadOnlyDictionary<string, Geometry?> _icons;

        public StubIconProvider(IReadOnlyDictionary<string, Geometry?> icons)
        {
            _icons = icons;
        }

        public Geometry? GetIcon(string geometryTypeName) =>
            _icons.TryGetValue(geometryTypeName, out var g) ? g : null;
    }

    private sealed class CountingStylerFactory : IShapeStylerFactory
    {
        public int CreateCount { get; private set; }

        public IShapeStyler CreateStyler(ShapeStylerParameter parameter)
        {
            CreateCount++;
            return new ShapeStyler(parameter);
        }

        public IShapeStyler ShapeUnselectedVisualState() => throw new NotSupportedException();
        public IShapeStyler ShapeSelectedVisualState() => throw new NotSupportedException();
        public IShapeStyler DottedLineStyler() => throw new NotSupportedException();
        public IShapeStyler CustomShapeStyler(Brush fillColor, Brush strokeColor, double strokeThickness) =>
            throw new NotSupportedException();
        public IShapeStyler CustomShapeStyler(Brush fillColor, Brush strokeColor, double strokeThickness, double dragHandleSize) =>
            throw new NotSupportedException();
    }

    /// <summary>Minimal shape that can raise ShapeCreationCancelled.</summary>
    private sealed class CancellableShape : ShapeVisualBase
    {
        public CancellableShape(ShapeLayer layer) : base(layer)
        {
        }

        public void Cancel() => OnShapeCreationCancelled();

        public override Rect BoundsRect => new(0, 0, 1, 1);

        protected override void CreateHandles()
        {
        }

        protected override void HandleResizing(Point point)
        {
        }

        protected override void HandleTranslate(Point newPoint)
        {
        }
    }

    private sealed class ContextAwareShape : ShapeVisualBase, IBoardContextAware
    {
        public bool ContextReceived { get; private set; }
        public double BoardWidth { get; private set; }
        public double BoardHeight { get; private set; }

        public ContextAwareShape(ShapeLayer layer) : base(layer)
        {
        }

        public void OnBoardContextAvailable(double boardWidth, double boardHeight)
        {
            ContextReceived = true;
            BoardWidth = boardWidth;
            BoardHeight = boardHeight;
        }

        public override Rect BoundsRect => new(0, 0, 1, 1);

        protected override void CreateHandles()
        {
        }

        protected override void HandleResizing(Point point)
        {
        }

        protected override void HandleTranslate(Point newPoint)
        {
        }
    }
}
