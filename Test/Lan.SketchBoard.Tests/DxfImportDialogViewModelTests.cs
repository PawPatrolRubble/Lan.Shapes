using System;
using Lan.Shapes.DialogGeometry.Dialog;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class DxfImportDialogViewModelTests
{
    [Fact]
    public void Constructor_UsesConfiguredPixelToMmFactor()
    {
        var viewModel = new DxfImportDialogViewModel(3410);

        Assert.Equal(3410, viewModel.PixelToMmFactor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositivePixelToMmFactor(double factor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DxfImportDialogViewModel(factor));
    }
}
