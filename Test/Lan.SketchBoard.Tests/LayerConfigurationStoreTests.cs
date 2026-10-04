using System;
using System.IO;
using System.Linq;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Xunit;

namespace Lan.SketchBoard.Tests;

/// <summary>
/// The runtime layer configuration must live outside the build output: a build restores the
/// shipped file (<c>CopyToOutputDirectory</c>), which silently dropped saved layer edits.
/// </summary>
public class LayerConfigurationStoreTests
{
    [Fact]
    public void ResolveRuntimeFile_SeedsTheRuntimeCopyOnceAndKeepsLaterSaves()
    {
        using var workspace = new TempWorkspace();
        var shipped = workspace.WriteShipped("{\"layer\":\"shipped\"}");
        var runtimeDirectory = workspace.RuntimeDirectory;

        var runtime = LayerConfigurationStore.ResolveRuntimeFile(shipped, runtimeDirectory);

        Assert.Equal(Path.Combine(runtimeDirectory, "LanShapesConfig.json"), runtime);
        Assert.Equal("{\"layer\":\"shipped\"}", File.ReadAllText(runtime));

        // The running application saves a layer edit; the next start must keep it.
        File.WriteAllText(runtime, "{\"layer\":\"edited\"}");
        var afterRestart = LayerConfigurationStore.ResolveRuntimeFile(shipped, runtimeDirectory);

        Assert.Equal(runtime, afterRestart);
        Assert.Equal("{\"layer\":\"edited\"}", File.ReadAllText(afterRestart));
    }

    [Fact]
    public void ResolveRuntimeFile_KeepsTheFileNameOfTheShippedConfiguration()
    {
        using var workspace = new TempWorkspace();
        var shipped = workspace.WriteShipped("{}", "LanShapesConfig.line.json");

        var runtime = LayerConfigurationStore.ResolveRuntimeFile(shipped, workspace.RuntimeDirectory);

        Assert.Equal("LanShapesConfig.line.json", Path.GetFileName(runtime));
    }

    [Fact]
    public void ResolveRuntimeFile_ReturnsTheShippedPathWhenItIsAlreadyWritable()
    {
        using var workspace = new TempWorkspace();
        var shipped = workspace.WriteShipped("{}");

        var runtime = LayerConfigurationStore.ResolveRuntimeFile(
            shipped, Path.GetDirectoryName(shipped));

        Assert.Equal(shipped, runtime);
    }

    [Fact]
    public void ResolveRuntimeFile_ReportsAMissingShippedDefault()
    {
        using var workspace = new TempWorkspace();
        var missing = Path.Combine(workspace.ShippedDirectory, "LanShapesConfig.json");

        var error = Assert.Throws<InvalidOperationException>(
            () => LayerConfigurationStore.ResolveRuntimeFile(missing, workspace.RuntimeDirectory));

        Assert.Contains("LanShapesConfig.json", error.Message);
        Assert.False(Directory.Exists(workspace.RuntimeDirectory));
    }

    [Fact]
    public void ResolveRuntimeFile_RequiresAShippedPath()
        => Assert.Throws<ArgumentException>(() => LayerConfigurationStore.ResolveRuntimeFile(" "));
    [Fact]
    public void RuntimeDirectory_DefaultsToTheUserLocalApplicationDataFolder()
    {
        var directory = LayerConfigurationStore.RuntimeDirectory;

        Assert.True(Path.IsPathRooted(directory));
        Assert.Equal(LayerConfigurationStore.ApplicationFolderName, Path.GetFileName(directory));
    }

    [Fact]
    public void SavedLayerDefinitionsInTheRuntimeFileSurviveARestartWhileTheShippedDefaultStaysIntact()
    {
        using var workspace = new TempWorkspace();
        var shipped = workspace.WriteShipped("{}");
        var seed = new ShapeLayerManager();
        seed.Layers.Add(TestShapeLayer.CreateWithThickness(stroke: 3, handle: 8));
        seed.SaveConfiguration(shipped);
        var shippedContent = File.ReadAllText(shipped);

        var runtime = LayerConfigurationStore.ResolveRuntimeFile(shipped, workspace.RuntimeDirectory);
        var manager = new ShapeLayerManager();
        manager.ReadConfiguration(runtime);
        Assert.Equal(runtime, manager.ConfigurationFilePath);

        var edited = manager.Layers[0].ToShapeLayerParameter();
        edited.Name = "重命名图层";
        edited.StyleSchema[ShapeVisualState.Normal].StrokeThickness = 7;
        manager.UpdateLayer(edited);

        // Restart: the app resolves the same writable file and sees the saved definition.
        var restarted = new ShapeLayerManager();
        restarted.ReadConfiguration(
            LayerConfigurationStore.ResolveRuntimeFile(shipped, workspace.RuntimeDirectory));

        var restored = restarted.Layers.Single(x => x.LayerId == edited.LayerId);
        Assert.Equal("重命名图层", restored.Name);
        Assert.Equal(7, restored.ToShapeLayerParameter().StyleSchema[ShapeVisualState.Normal].StrokeThickness);
        Assert.Equal(shippedContent, File.ReadAllText(shipped));
    }

    private sealed class TempWorkspace : IDisposable
    {
        private readonly string _root;

        public TempWorkspace()
        {
            _root = Path.Combine(Path.GetTempPath(), "LanShapesLayerConfigurationStore",
                Guid.NewGuid().ToString("N"));
            ShippedDirectory = Path.Combine(_root, "output");
            RuntimeDirectory = Path.Combine(_root, "user");
            Directory.CreateDirectory(ShippedDirectory);
        }

        public string ShippedDirectory { get; }

        public string RuntimeDirectory { get; }

        public string WriteShipped(string content, string fileName = "LanShapesConfig.json")
        {
            var path = Path.Combine(ShippedDirectory, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
