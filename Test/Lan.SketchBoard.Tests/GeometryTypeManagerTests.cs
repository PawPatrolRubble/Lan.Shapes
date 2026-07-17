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
