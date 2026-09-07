using System;
using System.IO;
using Lan.ImageViewer.Prism;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ImageViewerModuleConfigPathTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "LanShapesLatestJson_" + Guid.NewGuid().ToString("N"));

    public ImageViewerModuleConfigPathTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void ResolveLatestJsonFile_Directory_ReturnsNewestJsonByWriteTime()
    {
        var older = WriteJson("older.json", "{\"ShapeLayers\":[]}");
        var newer = WriteJson("newer.json", "{\"ShapeLayers\":[]}");
        File.SetLastWriteTimeUtc(older, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newer, new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc));

        var resolved = ImageViewerModule.ResolveLatestJsonFile(_directory, ".");

        Assert.Equal(newer, resolved);
    }

    [Fact]
    public void ResolveLatestJsonFile_NamedFile_ReturnsNewestMatchingPrefix()
    {
        var older = WriteJson("LanShapesConfig.json", "{\"ShapeLayers\":[]}");
        var newer = WriteJson("LanShapesConfig.20260907.json", "{\"ShapeLayers\":[]}");
        File.SetLastWriteTimeUtc(older, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newer, new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc));

        var resolved = ImageViewerModule.ResolveLatestJsonFile(_directory, "LanShapesConfig.json");

        Assert.Equal(newer, resolved);
    }

    [Fact]
    public void ResolveLatestJsonFile_SingleFile_ReturnsThatFile()
    {
        var only = WriteJson("LanShapesConfig.json", "{\"ShapeLayers\":[]}");

        var resolved = ImageViewerModule.ResolveLatestJsonFile(_directory, "LanShapesConfig.json");

        Assert.Equal(only, resolved);
    }

    [Fact]
    public void ResolveLatestJsonFile_MissingFile_ReturnsCombinedPath()
    {
        var resolved = ImageViewerModule.ResolveLatestJsonFile(_directory, "missing.json");

        Assert.Equal(Path.Combine(_directory, "missing.json"), resolved);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string WriteJson(string fileName, string contents)
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, contents);
        return path;
    }
}
