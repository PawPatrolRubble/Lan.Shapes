using System.Collections.Generic;
using System.IO;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeLayerManagerTests
{
    [Fact]
    public void ReadConfiguration_AppliesOneMeasurementProfileToAllLayers()
    {
        var path = Path.GetTempFileName();
        try
        {
            var layer = TestShapeLayer.Create().ToShapeLayerParameter();
            var configuration = new LanShapesConfiguration
            {
                AvailableGeometryTypes = new List<string> { "Line", "DxfGeometry" },
                Measurement = new ShapeMeasurementSettings
                {
                    PixelPerUnit = 3410,
                    UnitsPerMillimeter = 1000,
                    UnitName = "um"
                },
                ShapeLayers = new List<ShapeLayerParameter> { layer, layer }
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(configuration));
            var manager = new ShapeLayerManager();

            manager.ReadConfiguration(path);

            Assert.Equal(2, manager.Layers.Count);
            Assert.All(
                manager.Layers,
                configuredLayer =>
                    Assert.Same(manager.Configuration.Measurement, configuredLayer.Measurement));
            Assert.Equal(3410, manager.Configuration.Measurement.PixelPerUnit);
            Assert.Equal(
                new[] { "Line", "DxfGeometry" },
                manager.Configuration.AvailableGeometryTypes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SaveConfiguration_WritesMeasurementOutsideShapeLayers()
    {
        var sourcePath = Path.GetTempFileName();
        var destinationPath = Path.GetTempFileName();
        try
        {
            var configuration = new LanShapesConfiguration
            {
                AvailableGeometryTypes = new List<string> { "Line", "DxfGeometry" },
                Measurement = new ShapeMeasurementSettings
                {
                    PixelPerUnit = 3410,
                    UnitsPerMillimeter = 1000,
                    UnitName = "um"
                },
                ShapeLayers = new List<ShapeLayerParameter>
                {
                    TestShapeLayer.Create().ToShapeLayerParameter()
                }
            };
            File.WriteAllText(sourcePath, JsonConvert.SerializeObject(configuration));
            var manager = new ShapeLayerManager();
            manager.ReadConfiguration(sourcePath);

            manager.SaveConfiguration(destinationPath);

            var saved = JObject.Parse(File.ReadAllText(destinationPath));
            Assert.Equal(3410, saved["Measurement"]?["PixelPerUnit"]?.Value<double>());
            Assert.Equal(
                new[] { "Line", "DxfGeometry" },
                saved["AvailableGeometryTypes"]?.Values<string>());
            Assert.Null(saved["ShapeLayers"]?[0]?["PixelPerUnit"]);
            Assert.Null(saved["ShapeLayers"]?[0]?["UnitsPerMillimeter"]);
            Assert.Null(saved["ShapeLayers"]?[0]?["UnitName"]);
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(destinationPath);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ReadConfiguration_PreservesMissingAndEmptyGeometryTypeLists(
        bool includeEmptyList,
        bool expectedNull)
    {
        var path = Path.GetTempFileName();
        try
        {
            var configuration = JObject.FromObject(new LanShapesConfiguration
            {
                Measurement = new ShapeMeasurementSettings(),
                ShapeLayers = new List<ShapeLayerParameter>
                {
                    TestShapeLayer.Create().ToShapeLayerParameter()
                }
            });

            configuration.Remove(nameof(LanShapesConfiguration.AvailableGeometryTypes));
            if (includeEmptyList)
            {
                configuration[nameof(LanShapesConfiguration.AvailableGeometryTypes)] =
                    new JArray();
            }

            File.WriteAllText(path, configuration.ToString());
            var manager = new ShapeLayerManager();

            manager.ReadConfiguration(path);

            if (expectedNull)
            {
                Assert.Null(manager.Configuration.AvailableGeometryTypes);
            }
            else
            {
                Assert.Empty(manager.Configuration.AvailableGeometryTypes!);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadConfiguration_LegacyLayerArrayLeavesGeometryTypeListMissing()
    {
        var path = Path.GetTempFileName();
        try
        {
            var legacyLayer = JObject.FromObject(
                TestShapeLayer.Create().ToShapeLayerParameter());
            legacyLayer["PixelPerUnit"] = 3410;
            legacyLayer["UnitsPerMillimeter"] = 1000;
            legacyLayer["UnitName"] = "um";
            File.WriteAllText(path, new JArray(legacyLayer).ToString());
            var manager = new ShapeLayerManager();

            manager.ReadConfiguration(path);

            Assert.Null(manager.Configuration.AvailableGeometryTypes);
            Assert.Single(manager.Layers);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
