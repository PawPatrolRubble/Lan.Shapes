using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Custom;
using Lan.Shapes.Enums;
using Lan.Shapes.Styler;

using Newtonsoft.Json;

string JsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LanShapesConfig.json");

int passed = 0, failed = 0;

void AssertEqual<T>(T expected, T actual, string label)
{
    if (EqualityComparer<T>.Default.Equals(expected, actual))
    {
        Console.WriteLine($"  PASS: {label}");
        passed++;
    }
    else
    {
        Console.WriteLine($"  FAIL: {label} — expected {expected}, got {actual}");
        failed++;
    }
}

void AssertTrue(bool condition, string label)
{
    if (condition)
    {
        Console.WriteLine($"  PASS: {label}");
        passed++;
    }
    else
    {
        Console.WriteLine($"  FAIL: {label}");
        failed++;
    }
}

void AssertNotNull(object? obj, string label)
{
    AssertTrue(obj != null, label);
}

void RunTest(string name, Action test)
{
    Console.WriteLine($"\n{name}");
    try { test(); }
    catch (Exception ex) { Console.WriteLine($"  ERROR: {ex.Message}"); failed++; }
}

ShapeLayer CreateDefaultShapeLayer(
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
        MaximumThickenedShapeWidth = 80,
        TextForeground = Brushes.Black,
        BorderBackground = Brushes.Transparent,
        StyleSchema = new Dictionary<ShapeVisualState, ShapeStylerParameter>
        {
            [ShapeVisualState.Normal] = new ShapeStylerParameter
            {
                FillColor = new SolidColorBrush(Color.FromRgb(0, 191, 255)),
                StrokeColor = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                StrokeThickness = 2.0,
                DashStyle = "Solid",
                DragHandleSize = 8,
                FillOpacity = 0.2
            },
            [ShapeVisualState.Selected] = new ShapeStylerParameter
            {
                FillColor = new SolidColorBrush(Color.FromRgb(0, 191, 255)),
                StrokeColor = new SolidColorBrush(Colors.Blue),
                StrokeThickness = 2.0,
                DashStyle = "Solid",
                DragHandleSize = 8,
                FillOpacity = 0.2
            }
        }
    };
    return new ShapeLayer(
        param,
        new ShapeMeasurementSettings
        {
            UnitsPerMillimeter = unitsPerMillimeter,
            PixelPerUnit = pixelPerUnit,
            UnitName = unitName
        },
        new ShapeStylerFactory());
}

// ===== Test 1: ShapeLayer construction preserves all properties =====
RunTest("ShapeLayer construction from parameter", () =>
{
    var layer = CreateDefaultShapeLayer();
    AssertEqual(50, layer.TagFontSize, "TagFontSize = 50");
    AssertEqual(1000, layer.Measurement.UnitsPerMillimeter, "UnitsPerMillimeter = 1000");
    AssertEqual(3410d, layer.Measurement.PixelPerUnit, "PixelPerUnit = 3410");
    AssertEqual("um", layer.Measurement.UnitName, "UnitName = 'um'");
    AssertEqual(80, layer.MaximumThickenedShapeWidth, "MaximumThickenedShapeWidth = 80");
    AssertNotNull(layer.TextForeground, "TextForeground is not null");
    AssertNotNull(layer.BorderBackground, "BorderBackground is not null");
});

// ===== Test 2: Round-trip through ToShapeLayerParameter =====
RunTest("ToShapeLayerParameter round-trip", () =>
{
    var original = CreateDefaultShapeLayer(tagFontSize: 72, unitName: "mm");
    var param = original.ToShapeLayerParameter();
    var restored = new ShapeLayer(param, original.Measurement, new ShapeStylerFactory());

    AssertEqual(original.TagFontSize, restored.TagFontSize, "TagFontSize round-trips");
    AssertTrue(ReferenceEquals(original.Measurement, restored.Measurement), "Measurement profile is shared");
    AssertEqual(original.MaximumThickenedShapeWidth, restored.MaximumThickenedShapeWidth, "MaxThickenedShapeWidth round-trips");
});

// ===== Test 3: JSON deserialization =====
RunTest("LanShapesConfig.json deserialization", () =>
{
    var json = File.ReadAllText(JsonPath);
    var configuration = JsonConvert.DeserializeObject<LanShapesConfiguration>(json);
    AssertNotNull(configuration, "configuration is not null");
    AssertEqual(2, configuration!.ShapeLayers.Count, "2 layers in JSON");
    AssertEqual(1000, configuration.Measurement.UnitsPerMillimeter, "global UnitsPerMillimeter = 1000");
    AssertEqual(3410d, configuration.Measurement.PixelPerUnit, "global PixelPerUnit = 3410");
    AssertEqual("um", configuration.Measurement.UnitName, "global UnitName = 'um'");

    foreach (var layer in configuration.ShapeLayers)
    {
        AssertTrue(layer.TagFontSize != 0, $"TagFontSize is non-zero ({layer.TagFontSize})");
        AssertNotNull(layer.StyleSchema, "StyleSchema is not null");
    }
});

// ===== Test 4: ShapeLayer from JSON has correct values =====
RunTest("ShapeLayer from JSON values", () =>
{
    var json = File.ReadAllText(JsonPath);
    var configuration = JsonConvert.DeserializeObject<LanShapesConfiguration>(json)!;
    var layer = new ShapeLayer(
        configuration.ShapeLayers[0],
        configuration.Measurement,
        new ShapeStylerFactory());

    AssertEqual(50, layer.TagFontSize, "TagFontSize = 50 from JSON");
    AssertEqual(1000, layer.Measurement.UnitsPerMillimeter, "UnitsPerMillimeter = 1000 from JSON");
    AssertEqual(3410d, layer.Measurement.PixelPerUnit, "PixelPerUnit = 3410 from JSON");
    AssertEqual("um", layer.Measurement.UnitName, "UnitName = 'um' from JSON");
});

// ===== Test 5: Fiber has access to ShapeLayer properties =====
RunTest("Fiber accesses ShapeLayer properties", () =>
{
    var layer = CreateDefaultShapeLayer(tagFontSize: 64, unitsPerMillimeter: 500, pixelPerUnit: 2000, unitName: "mm");
    var fiber = new Fiber(layer);

    AssertEqual(64, fiber.ShapeLayer.TagFontSize, "Fiber sees TagFontSize = 64");
    AssertEqual(500, fiber.ShapeLayer.Measurement.UnitsPerMillimeter, "Fiber sees UnitsPerMillimeter = 500");
    AssertEqual(2000d, fiber.ShapeLayer.Measurement.PixelPerUnit, "Fiber sees PixelPerUnit = 2000");
    AssertEqual("mm", fiber.ShapeLayer.Measurement.UnitName, "Fiber sees UnitName = 'mm'");
});

// ===== Test 6: FiberData round-trip with micrometer conversion =====
RunTest("FiberData round-trip preserves geometry", () =>
{
    var layer = CreateDefaultShapeLayer(unitsPerMillimeter: 1000, pixelPerUnit: 3410);
    var fiber = new Fiber(layer);

    fiber.RectTopLeft = new Point(100, 50);
    fiber.RectTopRight = new Point(300, 50);
    fiber.RectBottomLeft = new Point(100, 150);
    fiber.RectBottomRight = new Point(300, 150);
    fiber.FilletRadius = 30;

    var data = fiber.GetMetaData();

    // Verify data is in micrometers
    AssertTrue(Math.Abs(data.Width - 58.65) < 0.01, $"Width in um is ~58.65 (got {data.Width:F2})");
    AssertTrue(Math.Abs(data.Height - 29.33) < 0.01, $"Height in um is ~29.33 (got {data.Height:F2})");

    // Reconstruct
    var fiber2 = new Fiber(layer);
    fiber2.FromData(data);

    double w = Math.Sqrt(Math.Pow(fiber2.RectTopRight.X - fiber2.RectTopLeft.X, 2) +
                        Math.Pow(fiber2.RectTopRight.Y - fiber2.RectTopLeft.Y, 2));
    double h = Math.Sqrt(Math.Pow(fiber2.RectBottomLeft.X - fiber2.RectTopLeft.X, 2) +
                        Math.Pow(fiber2.RectBottomLeft.Y - fiber2.RectTopLeft.Y, 2));

    AssertTrue(Math.Abs(w - 200) < 1.0, $"Width restored to ~200 px (got {w:F2})");
    AssertTrue(Math.Abs(h - 100) < 1.0, $"Height restored to ~100 px (got {h:F2})");
});

// ===== Test 7: FromData micrometer-to-pixel conversion =====
RunTest("FromData converts micrometers to pixels", () =>
{
    var layer = CreateDefaultShapeLayer(unitsPerMillimeter: 1000, pixelPerUnit: 3410);
    var fiber = new Fiber(layer);

    var data = new FiberData
    {
        Width = 58.651,  // ~200 px
        Height = 29.325, // ~100 px
        FilletCenter = new Point(200, 100),
        FilletRadius = 30,
        FiberAngleInDeg = 0,
        EnableTranslation = true
    };

    fiber.FromData(data);

    double w = Math.Sqrt(Math.Pow(fiber.RectTopRight.X - fiber.RectTopLeft.X, 2) +
                        Math.Pow(fiber.RectTopRight.Y - fiber.RectTopLeft.Y, 2));

    AssertTrue(Math.Abs(w - 200) < 1.0, $"Width from 58.651 um = ~200 px (got {w:F2})");
});

// ===== Test 8: Different unit scales produce different pixel geometry =====
RunTest("Different unit scales affect pixel geometry", () =>
{
    var layerA = CreateDefaultShapeLayer(unitsPerMillimeter: 1000, pixelPerUnit: 3410);
    var layerB = CreateDefaultShapeLayer(unitsPerMillimeter: 1000, pixelPerUnit: 1000);

    var data = new FiberData
    {
        Width = 50,
        Height = 25,
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

    AssertTrue(wB < wA, $"Lower PixelPerUnit produces fewer pixels (wB={wB:F1} < wA={wA:F1})");
});

// ===== Report =====
Console.WriteLine($"\n=== Results: {passed} passed, {failed} failed ===");
return failed == 0 ? 0 : 1;
