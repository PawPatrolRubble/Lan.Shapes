using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Newtonsoft.Json;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class LayerDefinitionArchitectureTests
{
    [Fact]
    public void DirectDefinitionEditUpdatesSnapshotAndMarksDirtyWithoutWritingTheFile()
    {
        WithSavedManager((manager, path) =>
        {
            var contents = File.ReadAllText(path);
            var notifications = 0;
            manager.LayerDefinitionChanged += (_, _) => notifications++;
            var edited = manager.Layers[0].ToShapeLayerParameter();
            edited.Name = "Direct edit";

            manager.Layers[0].ApplyConfiguration(edited);

            Assert.Equal("Direct edit", manager.Configuration.ShapeLayers[0].Name);
            Assert.True(manager.HasUnsavedChanges);
            Assert.Equal(1, notifications);
            Assert.Equal(contents, File.ReadAllText(path));
        });
    }

    [Theory]
    [InlineData("pen")]
    [InlineData("handle")]
    [InlineData("fill")]
    [InlineData("field")]
    public void DirectStyleAndFieldEditsAreObserved(string edit)
    {
        WithSavedManager((manager, _) =>
        {
            var layer = manager.Layers[0];
            var styler = layer.GetStyler(ShapeVisualState.Normal);
            var notifications = 0;
            manager.LayerDefinitionChanged += (_, _) => notifications++;
            switch (edit)
            {
                case "pen": styler.SketchPen.Thickness = 7; break;
                case "handle": styler.DragHandleSize = 17; break;
                case "fill": styler.FillColor.Opacity = 0.7; break;
                case "field": layer.TagFontSize = 23; break;
            }

            Assert.True(manager.HasUnsavedChanges);
            Assert.Equal(1, notifications);
            var snapshot = manager.Configuration.ShapeLayers[0];
            Assert.Equal(layer.TagFontSize, snapshot.TagFontSize);
            Assert.Equal(styler.SketchPen.Thickness, snapshot.StyleSchema[ShapeVisualState.Normal].StrokeThickness);
            Assert.Equal(styler.DragHandleSize, snapshot.StyleSchema[ShapeVisualState.Normal].DragHandleSize);
            Assert.Equal(styler.FillColor.Opacity, snapshot.StyleSchema[ShapeVisualState.Normal].FillOpacity);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CollectionRejectsDuplicateIdentitiesBeforeChangingState(bool duplicateId)
    {
        var manager = new ShapeLayerManager();
        var original = TestShapeLayer.Create();
        manager.Layers.Add(original);
        var candidate = original.ToShapeLayerParameter();
        candidate.LayerId = duplicateId ? original.LayerId : original.LayerId + 1;
        candidate.Name = duplicateId ? "Other" : "  " + original.Name.ToUpperInvariant() + "  ";

        Assert.ThrowsAny<ArgumentException>(() => manager.Layers.Add(new ShapeLayer(candidate)));
        Assert.Same(original, Assert.Single(manager.Layers));
        Assert.Single(manager.Configuration.ShapeLayers);
    }

    [Fact]
    public void DirectRenameCannotIntroduceDuplicateNames()
    {
        var manager = new ShapeLayerManager();
        manager.Layers.Add(TestShapeLayer.Create());
        var second = manager.Layers[0].ToShapeLayerParameter();
        second.Name = "Second";
        var layer = manager.CreateLayer(second);
        var invalid = layer.ToShapeLayerParameter();
        invalid.Name = manager.Layers[0].Name;

        Assert.ThrowsAny<ArgumentException>(() => layer.ApplyConfiguration(invalid));
        Assert.Equal("Second", layer.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void LayerConstructionRejectsMissingNames(string? name)
    {
        var definition = TestShapeLayer.Create().ToShapeLayerParameter();
        definition.Name = name!;

        Assert.ThrowsAny<ArgumentException>(() => new ShapeLayer(definition));
    }

    [Fact]
    public void RootConfigurationRejectsDuplicateNormalizedNames()
    {
        var first = TestShapeLayer.Create().ToShapeLayerParameter();
        var second = TestShapeLayer.Create().ToShapeLayerParameter();
        second.LayerId++;
        second.Name = "  " + first.Name.ToUpperInvariant() + "  ";
        var configuration = new LanShapesConfiguration
        {
            ShapeLayers = new List<ShapeLayerParameter> { first, second }
        };

        Assert.ThrowsAny<ArgumentException>(() => configuration.Validate());
    }

    [Fact]
    public void CaseInsensitiveDashNamesAreNormalizedAtConstruction()
    {
        var definition = TestShapeLayer.Create().ToShapeLayerParameter();
        definition.StyleSchema[ShapeVisualState.Normal].DashStyle = "dAsHdOt";

        var layer = new ShapeLayer(definition);

        Assert.Equal("DashDot", layer.ToShapeLayerParameter().StyleSchema[ShapeVisualState.Normal].DashStyle);
        Assert.Equal("dAsHdOt", definition.StyleSchema[ShapeVisualState.Normal].DashStyle);
    }

    [Fact]
    public void StylerCollectionCannotBeStructurallyModified()
    {
        var layer = TestShapeLayer.Create();
        var mutable = layer.Stylers as IDictionary<ShapeVisualState, Lan.Shapes.Styler.IShapeStyler>;

        Assert.True(mutable == null || mutable.IsReadOnly);
    }

    [Fact]
    public void ParametersAndCopiesDoNotShareMutableStrokeBrushes()
    {
        var definition = TestShapeLayer.Create().ToShapeLayerParameter();
        definition.StyleSchema[ShapeVisualState.Normal].StrokeColor = new SolidColorBrush(Colors.Blue);
        var original = new ShapeLayer(definition);
        var copy = original.CreateIndependentCopy();
        var exported = original.ToShapeLayerParameter();

        copy.GetStyler(ShapeVisualState.Normal).SketchPen.Brush.Opacity = 0.3;
        exported.StyleSchema[ShapeVisualState.Normal].StrokeColor.Opacity = 0.6;

        Assert.Equal(1, original.GetStyler(ShapeVisualState.Normal).SketchPen.Brush.Opacity);
        Assert.Equal(1, definition.StyleSchema[ShapeVisualState.Normal].StrokeColor.Opacity);
    }

    [Fact]
    public void RemovedDefinitionsStopSendingManagerChanges()
    {
        WithSavedManager((manager, path) =>
        {
            var second = manager.Layers[0].ToShapeLayerParameter();
            second.Name = "Second";
            var detached = manager.CreateLayer(second);
            manager.Layers.Remove(detached);
            manager.SaveConfiguration(path);
            var notifications = 0;
            manager.LayerDefinitionChanged += (_, _) => notifications++;

            detached.TagFontSize++;

            Assert.False(manager.HasUnsavedChanges);
            Assert.Equal(0, notifications);
            Assert.Single(manager.Configuration.ShapeLayers);
        });
    }

    [Fact]
    public void WholeConfigurationIsPublishedOnceAfterCollectionAndSettingsAreReady()
    {
        WithSavedManager((manager, path) =>
        {
            var callbacks = 0;
            EventHandler handler = (_, _) =>
            {
                callbacks++;
                Assert.False(manager.HasUnsavedChanges);
                Assert.Equal(manager.Layers.Select(x => x.LayerId), manager.Configuration.ShapeLayers.Select(x => x.LayerId));
                Assert.All(manager.Layers, layer => Assert.Same(manager.Configuration.Measurement, layer.Measurement));
            };
            manager.ConfigurationChanged += handler;

            manager.ReadConfiguration(path);

            Assert.Equal(1, callbacks);
        });
    }

    [Theory]
    [InlineData("create")]
    [InlineData("reload")]
    public void RejectedReentrantManagerCommands_PreserveCatalogueSnapshotAndFile(string command)
    {
        WithSavedManager((manager, path) =>
        {
            var original = manager.Layers[0];
            var secondDraft = original.ToShapeLayerParameter();
            secondDraft.LayerId++;
            secondDraft.Name = "Second";
            var second = new ShapeLayer(secondDraft);
            var thirdDraft = original.ToShapeLayerParameter();
            thirdDraft.Name = "Third";
            var contents = File.ReadAllText(path);
            manager.Layers.CollectionChanged += (_, _) =>
                Assert.Throws<InvalidOperationException>(() =>
                {
                    if (command == "create") manager.CreateLayer(thirdDraft);
                    else manager.ReadConfiguration(path);
                });
            manager.Layers.CollectionChanged += (_, _) => { };

            manager.Layers.Add(second);

            Assert.Equal(new[] { original, second }, manager.Layers);
            Assert.Equal(manager.Layers.Select(layer => layer.LayerId),
                manager.Configuration.ShapeLayers.Select(layer => layer.LayerId));
            Assert.True(manager.HasUnsavedChanges);
            Assert.Equal(contents, File.ReadAllText(path));
        });
    }

    [Theory]
    [InlineData("field")]
    [InlineData("pen")]
    [InlineData("rename")]
    public void DefinitionEditsInCatalogueCallbacks_UpdateSnapshotAndRemainUnsaved(string edit)
    {
        WithSavedManager((manager, path) =>
        {
            var changes = 0;
            manager.LayerDefinitionChanged += (_, _) => changes++;
            manager.Layers.CollectionChanged += (_, args) =>
            {
                var layer = (ShapeLayer)args.NewItems![0]!;
                if (edit == "field") layer.TagFontSize = 99;
                else if (edit == "pen") layer.GetStyler(ShapeVisualState.Normal).SketchPen.Thickness = 7;
                else
                {
                    var edited = layer.ToShapeLayerParameter();
                    edited.Name = "Callback rename";
                    layer.ApplyConfiguration(edited);
                }
            };
            var draft = manager.Layers[0].ToShapeLayerParameter();
            draft.Name = "Second";

            var created = manager.CreateLayer(draft);

            var snapshot = manager.Configuration.ShapeLayers.Single(layer => layer.LayerId == created.LayerId);
            var disk = JsonConvert.DeserializeObject<LanShapesConfiguration>(File.ReadAllText(path))!;
            var saved = disk.ShapeLayers.Single(layer => layer.LayerId == created.LayerId);
            Assert.Equal(created.Name, snapshot.Name);
            Assert.Equal(created.TagFontSize, snapshot.TagFontSize);
            Assert.Equal(created.GetStyler(ShapeVisualState.Normal).SketchPen.Thickness,
                snapshot.StyleSchema[ShapeVisualState.Normal].StrokeThickness);
            Assert.Equal("Second", saved.Name);
            Assert.Equal(draft.TagFontSize, saved.TagFontSize);
            Assert.Equal(draft.StyleSchema[ShapeVisualState.Normal].StrokeThickness,
                saved.StyleSchema[ShapeVisualState.Normal].StrokeThickness);
            Assert.True(manager.HasUnsavedChanges);
            Assert.Equal(2, changes); // One external edit and the creation command.
        });
    }

    [Fact]
    public void DuplicateRenameInCatalogueCallback_IsRejectedBeforeChangingTheDefinition()
    {
        WithSavedManager((manager, path) =>
        {
            manager.Layers.CollectionChanged += (_, args) =>
            {
                var layer = (ShapeLayer)args.NewItems![0]!;
                var invalid = layer.ToShapeLayerParameter();
                invalid.Name = manager.Layers[0].Name;
                Assert.Throws<ArgumentException>(() => layer.ApplyConfiguration(invalid));
            };
            var draft = manager.Layers[0].ToShapeLayerParameter();
            draft.Name = "Second";

            var created = manager.CreateLayer(draft);

            Assert.Equal("Second", created.Name);
            manager.Configuration.Validate();
            Assert.False(manager.HasUnsavedChanges);
            Assert.Equal("Second", JsonConvert.DeserializeObject<LanShapesConfiguration>(
                File.ReadAllText(path))!.ShapeLayers[1].Name);
        });
    }

    [Fact]
    public void RejectedLayerUpdate_PreservesFileSnapshotAndStylerIdentity()
    {
        WithSavedManager((manager, path) =>
        {
            var existing = manager.Layers[0];
            var styler = existing.GetStyler(ShapeVisualState.Normal);
            var snapshot = manager.Configuration;
            var contents = File.ReadAllText(path);
            var notifications = 0;
            manager.LayerDefinitionChanged += (_, _) => notifications++;
            existing.ConfigurationChanging += (_, _) => throw new InvalidOperationException("Veto");
            var draft = existing.ToShapeLayerParameter();
            draft.Name = "Changed";

            var error = Assert.Throws<InvalidOperationException>(() => manager.UpdateLayer(draft));

            Assert.Equal("Veto", error.Message);
            Assert.Equal(contents, File.ReadAllText(path));
            Assert.Same(snapshot, manager.Configuration);
            Assert.Same(styler, existing.GetStyler(ShapeVisualState.Normal));
            Assert.NotEqual("Changed", existing.Name);
            Assert.Equal(path, manager.ConfigurationFilePath);
            Assert.False(manager.HasUnsavedChanges);
            Assert.Equal(0, notifications);
        });
    }

    [Fact]
    public void DefinitionEditInUpdateNotification_IsTrackedWithoutOverwritingTheSavedUpdate()
    {
        WithSavedManager((manager, path) =>
        {
            var existing = manager.Layers[0];
            existing.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ShapeLayer.Name)) existing.TagFontSize = 99;
            };
            var draft = existing.ToShapeLayerParameter();
            draft.Name = "Changed";

            manager.UpdateLayer(draft);

            Assert.Equal(99, existing.TagFontSize);
            Assert.Equal(99, manager.Configuration.ShapeLayers[0].TagFontSize);
            Assert.True(manager.HasUnsavedChanges);
            var disk = JsonConvert.DeserializeObject<LanShapesConfiguration>(File.ReadAllText(path))!;
            Assert.Equal("Changed", disk.ShapeLayers[0].Name);
            Assert.Equal(draft.TagFontSize, disk.ShapeLayers[0].TagFontSize);
        });
    }

    [Fact]
    public void SavedUpdate_PublishesTheLayerEventAfterFileAndSnapshotAreCommitted()
    {
        WithSavedManager((manager, path) =>
        {
            var layer = manager.Layers[0];
            var notifications = 0;
            layer.DefinitionChanged += (_, _) =>
            {
                notifications++;
                Assert.Equal("Changed", layer.Name);
                Assert.Equal("Changed", manager.Configuration.ShapeLayers[0].Name);
                Assert.Equal("Changed", JsonConvert.DeserializeObject<LanShapesConfiguration>(
                    File.ReadAllText(path))!.ShapeLayers[0].Name);
                Assert.False(manager.HasUnsavedChanges);
            };
            var draft = layer.ToShapeLayerParameter();
            draft.Name = "Changed";

            manager.UpdateLayer(draft);

            Assert.Equal(1, notifications);
        });
    }

    private static void WithSavedManager(Action<ShapeLayerManager, string> test)
    {
        var path = Path.GetTempFileName();
        try
        {
            var manager = new ShapeLayerManager();
            manager.Layers.Add(TestShapeLayer.Create());
            manager.SaveConfiguration(path);
            test(manager, path);
        }
        finally { File.Delete(path); }
    }
}
