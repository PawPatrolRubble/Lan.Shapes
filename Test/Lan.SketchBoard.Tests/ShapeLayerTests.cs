using System;
using System.Collections.Generic;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeLayerTests
{
    [Fact]
    public void Constructor_RequiresNormalAndSelectedStylers()
    {
        var parameter = CreateParameter(includeSelected: false);

        var ex = Assert.Throws<InvalidOperationException>(() => new ShapeLayer(parameter));
        Assert.Contains(nameof(ShapeVisualState.Selected), ex.Message);
        Assert.Contains("Required", ex.Message);
    }

    [Fact]
    public void Constructor_AcceptsRequiredStatesWithoutRecommended()
    {
        var parameter = CreateParameter(includeSelected: true, includeMouseOver: false, includeLocked: false);

        var layer = new ShapeLayer(parameter);

        Assert.Equal("Tests", layer.Name);
        Assert.NotNull(layer.GetStyler(ShapeVisualState.Normal));
        Assert.NotNull(layer.GetStyler(ShapeVisualState.Selected));
        // Missing MouseOver falls back to Normal.
        Assert.Same(
            layer.GetStyler(ShapeVisualState.Normal),
            layer.GetStyler(ShapeVisualState.MouseOver));
    }

    [Fact]
    public void EnsureRequiredStylerStates_NullSchema_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => ShapeLayer.EnsureRequiredStylerStates(null!));
    }

    [Fact]
    public void ToShapeLayerParameter_RoundTripsRequiredStylers()
    {
        var layer = new ShapeLayer(CreateParameter(includeSelected: true, includeMouseOver: true, includeLocked: true));
        var parameter = layer.ToShapeLayerParameter();

        Assert.Contains(ShapeVisualState.Normal, parameter.StyleSchema.Keys);
        Assert.Contains(ShapeVisualState.Selected, parameter.StyleSchema.Keys);
        Assert.Equal(layer.LayerId, parameter.LayerId);
        Assert.Equal(layer.Name, parameter.Name);

        // Reconstruct without throw.
        _ = new ShapeLayer(parameter);
    }

    private static ShapeLayerParameter CreateParameter(
        bool includeSelected,
        bool includeMouseOver = false,
        bool includeLocked = false)
    {
        var schema = new Dictionary<ShapeVisualState, ShapeStylerParameter>
        {
            [ShapeVisualState.Normal] = MakeStyler(Colors.Red)
        };

        if (includeSelected)
        {
            schema[ShapeVisualState.Selected] = MakeStyler(Colors.Blue);
        }

        if (includeMouseOver)
        {
            schema[ShapeVisualState.MouseOver] = MakeStyler(Colors.Orange);
        }

        if (includeLocked)
        {
            schema[ShapeVisualState.Locked] = MakeStyler(Colors.Gray);
        }

        return new ShapeLayerParameter
        {
            LayerId = 1,
            Name = "Tests",
            Description = "Test layer",
            MaximumThickenedShapeWidth = 100,
            TagFontSize = 12,
            UnitsPerMillimeter = 1,
            PixelPerUnit = 1,
            UnitName = "px",
            TextForeground = new SolidColorBrush(Colors.Black),
            BorderBackground = new SolidColorBrush(Colors.LightBlue),
            StyleSchema = schema
        };
    }

    private static ShapeStylerParameter MakeStyler(Color stroke)
    {
        return new ShapeStylerParameter
        {
            FillColor = new SolidColorBrush(Colors.Transparent),
            StrokeColor = new SolidColorBrush(stroke),
            StrokeThickness = 1,
            DashStyle = "Solid",
            DragHandleSize = 10,
            FillOpacity = 0
        };
    }
}
