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

string JsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ShapeLayers.json");

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
                FillColor = new SolidColorBrush(Color.FromRgb(0, 191, 255)),
                StrokeColor = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                StrokeThickness = 2.0,
                DashStyle = "Solid",
                DragHandleSize = 8,
                FillOpacity = 0.2
            }
        }
    };
    return new ShapeLayer(param);
}

// ===== Test 1: ShapeLayer construction preserves all properties =====
RunTest("ShapeLayer construction from parameter", () =>
{
    var layer = CreateDefaultShapeLayer();
    AssertEqual(50, layer.TagFontSize, "TagFontSize = 50");
    AssertEqual(1000, layer.UnitsPerMillimeter, "UnitsPerMillimeter = 1000");
    AssertEqual(3410, layer.PixelPerUnit, "PixelPerUnit = 3410");
    AssertEqual("um", layer.UnitName, "UnitName = 'um'");
    AssertEqual(80, layer.MaximumThickenedShapeWidth, "MaximumThickenedShapeWidth = 80");
    AssertNotNull(layer.TextForeground, "TextForeground is not null");
    AssertNotNull(layer.BorderBackground, "BorderBackground is not null");
});

// ===== Test 2: Round-trip through ToShapeLayerParameter =====
RunTest("ToShapeLayerParameter round-trip", () =>
{
    var original = CreateDefaultShapeLayer(tagFontSize: 72, unitName: "mm");
    var param = original.ToShapeLayerParameter();
    var restored = new ShapeLayer(param);

    AssertEqual(original.TagFontSize, restored.TagFontSize, "TagFontSize round-trips");
    AssertEqual(original.UnitsPerMillimeter, restored.UnitsPerMillimeter, "UnitsPerMillimeter round-trips");
    AssertEqual(original.PixelPerUnit, restored.PixelPerUnit, "PixelPerUnit round-trips");
    AssertEqual(original.UnitName, restored.UnitName, "UnitName round-trips");
    AssertEqual(original.MaximumThickenedShapeWidth, restored.MaximumThickenedShapeWidth, "MaxThickenedShapeWidth round-trips");
});

// ===== Test 3: JSON deserialization =====
RunTest("ShapeLayers.json deserialization", () =>
{
    var json = File.ReadAllText(JsonPath);
    var layers = JsonConvert.DeserializeObject<List<ShapeLayerParameter>>(json);
    AssertNotNull(layers, "layers is not null");
    AssertEqual(2, layers!.Count, "2 layers in JSON");

    foreach (var layer in layers)
    {
        AssertTrue(layer.TagFontSize != 0, $"TagFontSize is non-zero ({layer.TagFontSize})");
        AssertTrue(layer.UnitsPerMillimeter != 0, $"UnitsPerMillimeter is non-zero ({layer.UnitsPerMillimeter})");
        AssertTrue(layer.PixelPerUnit != 0, $"PixelPerUnit is non-zero ({layer.PixelPerUnit})");
        AssertTrue(!string.IsNullOrEmpty(layer.UnitName), $"UnitName is set ('{layer.UnitName}')");
        AssertNotNull(layer.TextForeground, "TextForeground is not null");
    }
});

// ===== Test 4: ShapeLayer from JSON has correct values =====
RunTest("ShapeLayer from JSON values", () =>
{
    var json = File.ReadAllText(JsonPath);
    var parameters = JsonConvert.DeserializeObject<List<ShapeLayerParameter>>(json)!;
    var layer = new ShapeLayer(parameters[0]);

    AssertEqual(50, layer.TagFontSize, "TagFontSize = 50 from JSON");
    AssertEqual(1000, layer.UnitsPerMillimeter, "UnitsPerMillimeter = 1000 from JSON");
    AssertEqual(3410, layer.PixelPerUnit, "PixelPerUnit = 3410 from JSON");
    AssertEqual("um", layer.UnitName, "UnitName = 'um' from JSON");
    AssertNotNull(layer.TextForeground, "TextForeground loaded from JSON");
    AssertEqual(Colors.Black, ((SolidColorBrush)layer.TextForeground).Color, "TextForeground is black");
});

// ===== Test 5: Fiber has access to ShapeLayer properties =====
RunTest("Fiber accesses ShapeLayer properties", () =>
{
    var layer = CreateDefaultShapeLayer(tagFontSize: 64, unitsPerMillimeter: 500, pixelPerUnit: 2000, unitName: "mm");
    var fiber = new Fiber(layer);

    AssertEqual(64, fiber.ShapeLayer.TagFontSize, "Fiber sees TagFontSize = 64");
    AssertEqual(500, fiber.ShapeLayer.UnitsPerMillimeter, "Fiber sees UnitsPerMillimeter = 500");
    AssertEqual(2000, fiber.ShapeLayer.PixelPerUnit, "Fiber sees PixelPerUnit = 2000");
    AssertEqual("mm", fiber.ShapeLayer.UnitName, "Fiber sees UnitName = 'mm'");
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
    AssertTrue(data.Width > 50000, $"Width in um > 50000 (got {data.Width:F0})");
    AssertTrue(data.Height > 20000, $"Height in um > 20000 (got {data.Height:F0})");

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
        Width = 58651,   // ~200 px
        Height = 29325,  // ~100 px
        FilletCenter = new Point(200, 100),
        FilletRadius = 30,
        FiberAngleInDeg = 0,
        EnableTranslation = true
    };

    fiber.FromData(data);

    double w = Math.Sqrt(Math.Pow(fiber.RectTopRight.X - fiber.RectTopLeft.X, 2) +
                        Math.Pow(fiber.RectTopRight.Y - fiber.RectTopLeft.Y, 2));

    AssertTrue(Math.Abs(w - 200) < 1.0, $"Width from 58651 um = ~200 px (got {w:F2})");
});

// ===== Test 8: Different unit scales produce different pixel geometry =====
RunTest("Different unit scales affect pixel geometry", () =>
{
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

    AssertTrue(wB < wA, $"Higher PixelPerUnit → smaller pixels (wB={wB:F1} < wA={wA:F1})");
});

// ===== Report =====
Console.WriteLine($"\n=== Results: {passed} passed, {failed} failed ===");
return failed == 0 ? 0 : 1;
