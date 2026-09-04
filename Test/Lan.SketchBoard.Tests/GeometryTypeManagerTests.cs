using System;
using System.Linq;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class GeometryTypeManagerTests
{
    [Fact]
    public void RegisterDefaultGeometryTypes_IsIdempotent()
    {
        var manager = new GeometryTypeManager();

        GeometryTypeRegistration.RegisterDefaultGeometryTypes(manager);
        GeometryTypeRegistration.RegisterDefaultGeometryTypes(manager);

        var names = manager.GetRegisteredGeometryTypes().ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(nameof(Line), names);
        Assert.Contains(nameof(Rectangle), names);
        Assert.Contains(nameof(Rectangle2), names);
    }

    [Fact]
    public void RegisterGeometryTypes_RegistersOnlySpecifiedCatalogTypes()
    {
        var manager = new GeometryTypeManager();

        GeometryTypeRegistration.RegisterGeometryTypes(manager, new[] { "Line", "Circle" });

        Assert.Equal(new[] { nameof(Line), nameof(Circle) }, manager.GetRegisteredGeometryTypes());
    }

    [Fact]
    public void RegisterGeometryTypes_UnknownNameThrows()
    {
        var manager = new GeometryTypeManager();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GeometryTypeRegistration.RegisterGeometryTypes(manager, new[] { "NotAShape" }));

        Assert.Contains("NotAShape", exception.Message);
    }

    [Fact]
    public void RegisterGeometryTypes_EmptyFallsBackToFullCatalog()
    {
        var manager = new GeometryTypeManager();

        GeometryTypeRegistration.RegisterGeometryTypes(manager, Array.Empty<string>());

        Assert.Equal(
            GeometryTypeRegistration.Catalog.Count,
            manager.GetRegisteredGeometryTypes().Count());
    }

    [Fact]
    public void RegisterDefaultGeometryTypes_MatchesCatalog()
    {
        var manager = new GeometryTypeManager();

        GeometryTypeRegistration.RegisterDefaultGeometryTypes(manager);

        Assert.Equal(
            GeometryTypeRegistration.Catalog.Keys.OrderBy(x => x, StringComparer.Ordinal),
            manager.GetRegisteredGeometryTypes().OrderBy(x => x, StringComparer.Ordinal));
    }


    [Fact]
    public void RegisterGeometryType_RejectsConflictingNames()
    {
        var manager = new GeometryTypeManager();
        manager.RegisterGeometryType(nameof(Line), typeof(Line));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            manager.RegisterGeometryType(nameof(Line), typeof(Circle)));

        Assert.Contains(nameof(Line), exception.Message);
    }

    [Fact]
    public void RegisterGeometryType_RejectsNonShapeTypes()
    {
        var manager = new GeometryTypeManager();

        Assert.Throws<ArgumentException>(() =>
            manager.RegisterGeometryType("Invalid", typeof(string)));
    }
}
