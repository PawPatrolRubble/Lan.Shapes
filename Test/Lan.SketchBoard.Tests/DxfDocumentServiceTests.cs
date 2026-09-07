using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Input;
using Point = System.Windows.Point;
using Vector = System.Windows.Vector;
using Lan.Shapes.Enums;
using Lan.Shapes.DialogGeometry;
using Lan.Shapes.Handle;
using netDxf;
using netDxf.Entities;
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

    [Fact]
    public void Import_CentersRenderedBoundsOnImage()
    {
        var document = new DxfDocument();
        document.Entities.Add(new Line(new Vector2(10, 20), new Vector2(30, 40)));
        var service = new StubDxfDocumentService(document);
        var shape = new DxfGeometry(TestShapeLayer.Create(), service);
        shape.OnBoardContextAvailable(200, 100);

        var import = typeof(DxfGeometry).GetMethod("ReadDxfFile", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(import);
        import.Invoke(shape, new object[] { "drawing.dxf", new Point(5, 7), 1.0 });

        Assert.Equal(100, shape.BoundsRect.Left + shape.BoundsRect.Width / 2, 8);
        Assert.Equal(50, shape.BoundsRect.Top + shape.BoundsRect.Height / 2, 8);
        Assert.Equal(1, service.LoadCount);
    }

    [Fact]
    public void CenterHandle_DragTranslatesGeometryWithoutResizing()
    {
        RunOnSta(() =>
        {
            var document = new DxfDocument();
            document.Entities.Add(new Line(new Vector2(10, 20), new Vector2(30, 40)));
            var shape = new DxfGeometry(TestShapeLayer.Create(), new StubDxfDocumentService(document));
            shape.OnBoardContextAvailable(200, 100);

            var import = typeof(DxfGeometry).GetMethod("ReadDxfFile", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(import);
            import.Invoke(shape, new object[] { "drawing.dxf", new Point(), 1.0 });
            typeof(DxfGeometry).GetProperty(nameof(DxfGeometry.IsGeometryRendered))!
                .SetValue(shape, true);
            shape.State = ShapeVisualState.Selected;

            var originalBounds = shape.BoundsRect;
            var center = new Point(
                originalBounds.Left + originalBounds.Width / 2,
                originalBounds.Top + originalBounds.Height / 2);
            var target = center + new Vector(25, 15);

            shape.OnMouseLeftButtonDown(center);
            shape.OnMouseMove(target, MouseButtonState.Pressed);
            shape.OnMouseLeftButtonUp(target);

            Assert.Equal(originalBounds.Width, shape.BoundsRect.Width, 8);
            Assert.Equal(originalBounds.Height, shape.BoundsRect.Height, 8);
            Assert.Equal(originalBounds.Left + 25, shape.BoundsRect.Left, 8);
            Assert.Equal(originalBounds.Top + 15, shape.BoundsRect.Top, 8);
        });
    }

    [Fact]
    public void HandleAvailability_ShowsTranslationAlwaysAndRotationOnlyWhenSelected()
    {
        RunOnSta(() =>
        {
            var document = new DxfDocument();
            document.Entities.Add(new Line(new Vector2(10, 20), new Vector2(30, 40)));
            var shape = new DxfGeometry(TestShapeLayer.Create(), new StubDxfDocumentService(document));
            shape.OnBoardContextAvailable(200, 100);

            var import = typeof(DxfGeometry).GetMethod("ReadDxfFile", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(import);
            import.Invoke(shape, new object[] { "drawing.dxf", new Point(), 1.0 });
            typeof(DxfGeometry).GetProperty(nameof(DxfGeometry.IsGeometryRendered))!
                .SetValue(shape, true);

            var bounds = shape.BoundsRect;
            var center = new Point(
                bounds.Left + bounds.Width / 2,
                bounds.Top + bounds.Height / 2);
            var rotationCenter = new Point(center.X, bounds.Top - 50);

            var translationHandle = shape.FindDragHandleMouseOver(center);
            Assert.NotNull(translationHandle);
            Assert.Equal(DragLocation.Move, translationHandle.CursorLocation);
            Assert.Equal(shape.ShapeStyler!.DragHandleSize * 1.5, translationHandle.HandleSize.Width, 8);
            Assert.Equal(translationHandle.HandleSize.Width, translationHandle.HandleSize.Height, 8);
            Assert.Null(shape.FindDragHandleMouseOver(rotationCenter));

            shape.State = ShapeVisualState.Selected;
            Assert.NotNull(shape.FindDragHandleMouseOver(rotationCenter));

            shape.State = ShapeVisualState.Normal;
            Assert.NotNull(shape.FindDragHandleMouseOver(center));
            Assert.Null(shape.FindDragHandleMouseOver(rotationCenter));

            shape.Lock();
            Assert.Null(shape.FindDragHandleMouseOver(center));
            Assert.Null(shape.FindDragHandleMouseOver(rotationCenter));
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private sealed class StubDxfDocumentService : IDxfDocumentService
    {
        private readonly DxfDocument _document;
        public int LoadCount { get; private set; }

        public StubDxfDocumentService(DxfDocument document)
        {
            _document = document;
        }

        public DxfDocument Load(string filePath)
        {
            LoadCount++;
            return _document;
        }

        public void Save(DxfDocument document, string filePath)
        {
        }
    }
}
