using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class LayerManagementFeatureTests
{
    [Fact]
    public void CreateEditAndSave_RoundTripAllStateStylesAndKeepLayerIdentity()
    {
        var path = Path.GetTempFileName();
        try
        {
            var manager = Manager();
            var definition = manager.Layers[0].ToShapeLayerParameter();
            definition.Name = "  New layer  ";
            definition.Description = "Description";
            definition.StyleSchema[ShapeVisualState.Normal].StrokeThickness = 3;
            definition.StyleSchema[ShapeVisualState.Normal].FillOpacity = 0.4;
            definition.StyleSchema[ShapeVisualState.Normal].DashStyle = "Dash";
            var created = manager.CreateLayer(definition);
            Assert.Equal(2, created.LayerId);
            Assert.Equal("New layer", created.Name);
            Assert.True(manager.HasUnsavedChanges);
            Assert.Equal("  New layer  ", definition.Name);
            manager.SaveConfiguration(path);
            Assert.False(manager.HasUnsavedChanges);
            var renamed = created.ToShapeLayerParameter();
            renamed.Name = "Renamed";
            manager.UpdateLayer(renamed);
            Assert.Same(created, manager.Layers[1]);
            Assert.False(manager.HasUnsavedChanges);
            var loaded = new ShapeLayerManager();
            loaded.ReadConfiguration(path);
            var result = loaded.Layers.Single(x => x.LayerId == 2).ToShapeLayerParameter();
            Assert.Equal("Renamed", result.Name);
            Assert.Equal("Description", result.Description);
            Assert.Equal(3, result.StyleSchema[ShapeVisualState.Normal].StrokeThickness);
            Assert.Equal(0.4, result.StyleSchema[ShapeVisualState.Normal].FillOpacity);
            Assert.Equal("Dash", result.StyleSchema[ShapeVisualState.Normal].DashStyle);
            Assert.Equal(1, result.StyleSchema[ShapeVisualState.Selected].StrokeThickness);
            Assert.Equal(new[] { "Line" }, loaded.Configuration.AvailableGeometryTypes);
            Assert.All(loaded.Layers, layer => Assert.Same(loaded.Configuration.Measurement, layer.Measurement));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(" first ")]
    public void InvalidOrDuplicateName_DoesNotCreateLayer(string name)
    {
        var manager = Manager();
        var definition = manager.Layers[0].ToShapeLayerParameter();
        definition.Name = name;
        Assert.Throws<ArgumentException>(() => manager.CreateLayer(definition));
        Assert.Single(manager.Layers);
    }

    [Fact]
    public void DuplicateRename_PreservesBothDefinitions()
    {
        var manager = Manager();
        var definition = manager.Layers[0].ToShapeLayerParameter();
        definition.Name = "Second";
        var second = manager.CreateLayer(definition);
        var edited = second.ToShapeLayerParameter();
        edited.Name = "FIRST";
        Assert.Throws<ArgumentException>(() => manager.UpdateLayer(edited));
        Assert.Equal("Second", second.Name);
        Assert.Equal(new[] { 1, 2 }, manager.Layers.Select(x => x.LayerId));
    }

    [Fact]
    public void FailedSaveAs_PreservesOriginalPathAndBothFiles()
    {
        var original = Path.GetTempFileName();
        var destination = Path.GetTempFileName();
        try
        {
            var manager = Manager();
            manager.SaveConfiguration(original);
            var originalContents = File.ReadAllText(original);
            File.WriteAllText(destination, "Original destination contents");
            using (var locked = new FileStream(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.ThrowsAny<IOException>(() => manager.SaveConfiguration(destination));
            Assert.Equal(original, manager.ConfigurationFilePath);
            Assert.Equal(originalContents, File.ReadAllText(original));
            Assert.Equal("Original destination contents", File.ReadAllText(destination));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(destination)!, Path.GetFileName(destination) + ".*.tmp"));
        }
        finally { File.Delete(original); File.Delete(destination); }
    }

    [Fact]
    public void FailedAutomaticSave_PreservesExistingDefinitionAndLayerCollection()
    {
        var path = Path.GetTempFileName();
        try
        {
            var manager = Manager();
            manager.SaveConfiguration(path);
            var edited = manager.Layers[0].ToShapeLayerParameter();
            edited.Name = "Renamed";
            var added = manager.Layers[0].ToShapeLayerParameter();
            added.Name = "Second";
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.ThrowsAny<IOException>(() => manager.UpdateLayer(edited));
                Assert.ThrowsAny<IOException>(() => manager.CreateLayer(added));
            }
            Assert.Equal("First", Assert.Single(manager.Layers).Name);
            Assert.False(manager.HasUnsavedChanges);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Configuration_RejectsDuplicateLayerIds()
    {
        var definition = BulkLayerFeatureTests.Layer(1, "First").ToShapeLayerParameter();
        var configuration = new LanShapesConfiguration { ShapeLayers = new List<ShapeLayerParameter> { definition, definition } };
        Assert.Throws<InvalidOperationException>(() => configuration.Validate());
    }

    [Fact]
    public void ViewerCreatesLayerAndMakesItCurrentWithoutChangingExistingShapeLayer()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var layers = Manager();
            var board = new SketchBoardDataManager();
            var types = new GeometryTypeManager();
            types.RegisterGeometryType<Lan.Shapes.Shapes.Line>();
            var vm = new ImageViewerControlViewModel(layers, board, types);
            var original = new Lan.Shapes.Shapes.Line(board.CurrentShapeLayer!);
            board.AddShape(original);
            var definition = layers.Layers[0].ToShapeLayerParameter();
            definition.Name = "Created";
            var created = vm.CreateLayer(definition);
            Assert.Same(created, vm.SelectedShapeLayer);
            Assert.Equal(created.LayerId, board.CurrentShapeLayer!.LayerId);
            Assert.Equal(1, original.ShapeLayer.LayerId);
            Assert.Equal(2, vm.LayerGroups.Count);
            Assert.Contains("未保存", vm.LayerConfigurationStatus);
        });
    }

    private static ShapeLayerManager Manager()
    {
        var manager = new ShapeLayerManager();
        manager.Layers.Add(BulkLayerFeatureTests.Layer(1, "First"));
        manager.Configuration.AvailableGeometryTypes = new List<string> { "Line" };
        return manager;
    }
}
