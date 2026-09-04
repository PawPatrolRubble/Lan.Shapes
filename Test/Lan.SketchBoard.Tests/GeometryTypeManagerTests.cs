using System;
using System.Linq;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes.DialogGeometry;
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
    }

    [Fact]
    public void RegisterGeometryTypes_RegistersOnlyRequestedNames()
    {
        var manager = new GeometryTypeManager();

        GeometryTypeRegistration.RegisterGeometryTypes(manager, new[] { nameof(Line), nameof(DxfGeometry) });

        var names = manager.GetRegisteredGeometryTypes().ToArray();
        Assert.Equal(new[] { nameof(Line), nameof(DxfGeometry) }, names);
        Assert.Equal(typeof(Line), manager.GetGeometryTypeByName(nameof(Line)));
        Assert.Equal(typeof(DxfGeometry), manager.GetGeometryTypeByName(nameof(DxfGeometry)));
    }

    [Fact]
    public void RegisterGeometryTypes_NullNames_RegistersCatalog()
    {
        var manager = new GeometryTypeManager();

        GeometryTypeRegistration.RegisterGeometryTypes(manager, null);

        var names = manager.GetRegisteredGeometryTypes().ToArray();
        Assert.Contains(nameof(Line), names);
        Assert.Contains(nameof(Rectangle), names);
        Assert.Contains(nameof(DxfGeometry), names);
        Assert.Equal(GeometryTypeRegistration.Catalog.Count, names.Length);
    }

    [Fact]
    public void RegisterGeometryTypes_EmptyNames_RegistersNothing()
    {
        var manager = new GeometryTypeManager();

        GeometryTypeRegistration.RegisterGeometryTypes(manager, Array.Empty<string>());

        Assert.Empty(manager.GetRegisteredGeometryTypes());
    }

    [Fact]
    public void RegisterGeometryTypes_UnknownName_Throws()
    {
        var manager = new GeometryTypeManager();

        var exception = Assert.Throws<ArgumentException>(() =>
            GeometryTypeRegistration.RegisterGeometryTypes(manager, new[] { "NotAShape" }));

        Assert.Contains("NotAShape", exception.Message);
        Assert.Empty(manager.GetRegisteredGeometryTypes());
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
