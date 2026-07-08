using System.Windows;
using System.Windows.Media;
using Lan.Shapes.Custom;
using Lan.Shapes.Enums;
using Lan.Shapes.Styler;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Lan.Shapes.Tests;

[TestClass]
public class FiberDataPipelineTests
{
    private static ShapeLayer CreateDefaultShapeLayer(
        int tagFontSize = 50,
        int unitsPerMillimeter = 1000,
        int pixelPerUnit = 3410,
        string unitName = "um")
    {
        var param = new ShapeLayerParameter
        {
            LayerId = 1,
            Name = "test",
            Description = "test layer",
            TagFontSize = tagFontSize,
            UnitsPerMillimeter = unitsPerMillimeter,
            PixelPerUnit = pixelPerUnit,
            UnitName = unitName,
            MaximumThickenedShapeWidth = 80,
            TextForeground = Brushes.Black,
            BorderBackground = Brushes.Transparent,
            StyleSchema = new Dictionary<ShapeVisualState, ShapeStylerParameter>
            {
                [ShapeVisualState.Normal] = new ShapeStylerParameter
                {
                    FillColor = "#00BFFF",
                    StrokeColor = "#007ACC",
                    StrokeThickness = 2.0,
                    DashStyle = "Solid",
                    DragHandleSize = 8,
                    FillOpacity = 0.2
                }
            }
        };
        return new ShapeLayer(param);
    }

    [TestMethod]
    public void ShapeLayer_Constructed_From_Parameter_Preserves_All_Properties()
    {
        var layer = CreateDefaultShapeLayer();

        Assert.AreEqual(50, layer.TagFontSize);
        Assert.AreEqual(1000, layer.UnitsPerMillimeter);
        Assert.AreEqual(3410, layer.PixelPerUnit);
        Assert.AreEqual("um", layer.UnitName);
        Assert.AreEqual(80, layer.MaximumThickenedShapeWidth);
        Assert.IsNotNull(layer.TextForeground);
        Assert.IsNotNull(layer.BorderBackground);
    }

    [TestMethod]
    public void ShapeLayer_ToShapeLayerParameter_RoundTrips_All_Properties()
    {
        var original = CreateDefaultShapeLayer(tagFontSize: 72, unitName: "mm");
        var param = original.ToShapeLayerParameter();
        var restored = new ShapeLayer(param);

        Assert.AreEqual(original.TagFontSize, restored.TagFontSize);
        Assert.AreEqual(original.UnitsPerMillimeter, restored.UnitsPerMillimeter);
        Assert.AreEqual(original.PixelPerUnit, restored.PixelPerUnit);
        Assert.AreEqual(original.UnitName, restored.UnitName);
        Assert.AreEqual(original.MaximumThickenedShapeWidth, restored.MaximumThickenedShapeWidth);
    }

    [TestMethod]
    public void ShapeLayers_Json_Deserializes_With_All_Properties()
    {
        var json = File.ReadAllText("TestData/ShapeLayers.json");
        var layers = JsonConvert.DeserializeObject<List<ShapeLayerParameter>>(json);
        Assert.IsNotNull(layers);
        Assert.AreEqual(2, layers!.Count);

        foreach (var layer in layers)
        {
            Assert.AreNotEqual(0, layer.TagFontSize);
            Assert.AreNotEqual(0, layer.UnitsPerMillimeter);
            Assert.AreNotEqual(0, layer.PixelPerUnit);
            Assert.IsNotNull(layer.UnitName);
            Assert.IsFalse(string.IsNullOrEmpty(layer.UnitName));
            Assert.IsNotNull(layer.TextForeground);
        }
    }

    [TestMethod]
    public void ShapeLayer_From_Json_Has_Correct_Values()
    {
        var json = File.ReadAllText("TestData/ShapeLayers.json");
        var parameters = JsonConvert.DeserializeObject<List<ShapeLayerParameter>>(json)!;
        var layer = new ShapeLayer(parameters[0]);

        Assert.AreEqual(50, layer.TagFontSize);
        Assert.AreEqual(1000, layer.UnitsPerMillimeter);
        Assert.AreEqual(3410, layer.PixelPerUnit);
        Assert.AreEqual("um", layer.UnitName);
        Assert.IsNotNull(layer.TextForeground);
        Assert.AreEqual(Colors.Black, ((SolidColorBrush)layer.TextForeground).Color);
    }

    [TestMethod]
    public void Fiber_Has_Access_To_ShapeLayer_Font_Size()
    {
        var layer = CreateDefaultShapeLayer(tagFontSize: 64);
        var fiber = new Fiber(layer);

        Assert.AreEqual(64, fiber.ShapeLayer.TagFontSize);
    }

    [TestMethod]
    public void Fiber_Has_Access_To_ShapeLayer_Unit_Properties()
    {
        var layer = CreateDefaultShapeLayer(unitsPerMillimeter: 500, pixelPerUnit: 2000, unitName: "mm");
        var fiber = new Fiber(layer);

        Assert.AreEqual(500, fiber.ShapeLayer.UnitsPerMillimeter);
        Assert.AreEqual(2000, fiber.ShapeLayer.PixelPerUnit);
        Assert.AreEqual("mm", fiber.ShapeLayer.UnitName);
    }

    [TestMethod]
    public void FiberData_RoundTrip_Preserves_Geometry_With_Micrometer_Conversion()
    {
        var layer = CreateDefaultShapeLayer(unitsPerMillimeter: 1000, pixelPerUnit: 3410);
        var fiber = new Fiber(layer);

        // Set up known pixel geometry
        fiber.RectTopLeft = new Point(100, 50);
        fiber.RectTopRight = new Point(300, 50);
        fiber.RectBottomLeft = new Point(100, 150);
        fiber.RectBottomRight = new Point(300, 150);
        fiber.FilletRadius = 30;

        // Export to data (pixels → micrometers)
        var data = fiber.GetMetaData();

        // Width in pixels = 200, height in pixels = 100
        // Conversion: micrometers = pixels * 1000 * UnitsPerMillimeter / PixelPerUnit
        // = 200 * 1000 * 1000 / 3410 ≈ 58651
        // = 100 * 1000 * 1000 / 3410 ≈ 29325
        Assert.IsTrue(data.Width > 50000, $"Expected Width > 50000 um, got {data.Width}");
        Assert.IsTrue(data.Height > 20000, $"Expected Height > 20000 um, got {data.Height}");

        // Reconstruct from data (micrometers → pixels)
        var fiber2 = new Fiber(layer);
        fiber2.FromData(data);

        // Verify geometry is approximately restored
        double w = Math.Sqrt(Math.Pow(fiber2.RectTopRight.X - fiber2.RectTopLeft.X, 2) +
                            Math.Pow(fiber2.RectTopRight.Y - fiber2.RectTopLeft.Y, 2));
        double h = Math.Sqrt(Math.Pow(fiber2.RectBottomLeft.X - fiber2.RectTopLeft.X, 2) +
                            Math.Pow(fiber2.RectBottomLeft.Y - fiber2.RectTopLeft.Y, 2));

        Assert.AreEqual(200, w, 1.0);
        Assert.AreEqual(100, h, 1.0);
    }

    [TestMethod]
    public void FromData_Micrometer_Conversion_Uses_ShapeLayer_Units()
    {
        var layer = CreateDefaultShapeLayer(unitsPerMillimeter: 1000, pixelPerUnit: 3410);
        var fiber = new Fiber(layer);

        // Data with known micrometer values
        var data = new FiberData
        {
            Width = 58651,   // ~200 px at 1000 um/mm, 3410 px/unit
            Height = 29325,  // ~100 px
            FilletCenter = new Point(200, 100),
            FilletRadius = 30,
            FiberAngleInDeg = 0,
            EnableTranslation = true
        };

        fiber.FromData(data);

        double w = Math.Sqrt(Math.Pow(fiber.RectTopRight.X - fiber.RectTopLeft.X, 2) +
                            Math.Pow(fiber.RectTopRight.Y - fiber.RectTopLeft.Y, 2));

        Assert.AreEqual(200, w, 1.0);
    }

    [TestMethod]
    public void FromData_With_Different_Unit_Scale_Produces_Different_Pixel_Geometry()
    {
        // Layer with different scale: fewer pixels per micrometer
        var layerA = CreateDefaultShapeLayer(unitsPerMillimeter: 1000, pixelPerUnit: 3410);
        var layerB = CreateDefaultShapeLayer(unitsPerMillimeter: 1000, pixelPerUnit: 1000);

        var data = new FiberData
        {
            Width = 50000,
            Height = 25000,
            FilletCenter = new Point(200, 100),
            FilletRadius = 30,
            FiberAngleInDeg = 0,
            EnableTranslation = true
        };

        var fiberA = new Fiber(layerA);
        fiberA.FromData(data);

        var fiberB = new Fiber(layerB);
        fiberB.FromData(data);

        double wA = Math.Sqrt(Math.Pow(fiberA.RectTopRight.X - fiberA.RectTopLeft.X, 2) +
                             Math.Pow(fiberA.RectTopRight.Y - fiberA.RectTopLeft.Y, 2));
        double wB = Math.Sqrt(Math.Pow(fiberB.RectTopRight.X - fiberB.RectTopLeft.X, 2) +
                             Math.Pow(fiberB.RectTopRight.Y - fiberB.RectTopLeft.Y, 2));

        // With 3410 px/unit, 50000 um → ~170.5 px
        // With 1000 px/unit, 50000 um → ~50 px
        Assert.AreNotEqual(wA, wB, 1.0);
        Assert.IsTrue(wB < wA, $"Expected wB ({wB}) < wA ({wA})");
    }
}
