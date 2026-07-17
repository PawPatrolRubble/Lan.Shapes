using System;
using Lan.Shapes.DialogGeometry;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class DxfDocumentServiceTests
{
    [Fact]
    public void Load_RejectsEmptyPath()
    {
        Assert.Throws<ArgumentException>(() => DxfDocumentService.Default.Load(" "));
    }

    [Fact]
    public void Save_RejectsNullDocument()
    {
        Assert.Throws<ArgumentNullException>(() => DxfDocumentService.Default.Save(null!, "drawing.dxf"));
    }
}
