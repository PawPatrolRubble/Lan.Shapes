using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Shapes;
using Lan.SketchBoard;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ImageViewerCanvasExportTests
{
    private const int ImageWidth = 200;
    private const int ImageHeight = 120;
    // Half-pixel y keeps the 1 px stroke inside one pixel row, so it is fully covered.
    private const double ShapeY = 60.5;

    [Fact]
    public void RenderCanvasContent_ComposesImageAndGeometriesAtImageResolution()
    {
        RunOnSta(() =>
        {
            var (_, viewer, manager, layer) = CreateControl();
            AddHorizontalLine(manager, layer);

            var bitmap = viewer.RenderCanvasContent();

            Assert.NotNull(bitmap);
            Assert.Equal(ImageWidth, bitmap!.PixelWidth);
            Assert.Equal(ImageHeight, bitmap.PixelHeight);

            var pixels = ReadBgra32(bitmap);
            // The geometry keeps its image-space coordinates in the export.
            Assert.True(IsShapeStroke(ReadPixel(pixels, bitmap.PixelWidth, 100, (int)ShapeY)),
                $"the geometry stroke is missing from the exported canvas: {ReadPixel(pixels, bitmap.PixelWidth, 100, (int)ShapeY)}");
            Assert.Equal(Colors.White, ReadPixel(pixels, bitmap.PixelWidth, 100, (int)ShapeY - 1));
            Assert.Equal(Colors.White, ReadPixel(pixels, bitmap.PixelWidth, 100, (int)ShapeY + 1));
            // ... drawn over the image.
            Assert.Equal(Colors.White, ReadPixel(pixels, bitmap.PixelWidth, 100, 10));
        });
    }

    [Fact]
    public void RenderCanvasContent_DrawsShapeStrokeAtImageSpaceThicknessWhenZoomed()
    {
        RunOnSta(() =>
        {
            var (_, viewer, manager, layer) = CreateControl();
            AddHorizontalLine(manager, layer);

            // At this zoom the on-screen policy thins strokes to base / scale; the export
            // must not inherit that zoom-dependent thickness.
            viewer.LocalScale = 2.5;
            Assert.Equal(2.5, manager.ViewportScale, 3);

            var bitmap = viewer.RenderCanvasContent();

            Assert.NotNull(bitmap);
            var pixels = ReadBgra32(bitmap!);
            Assert.True(IsShapeStroke(ReadPixel(pixels, bitmap!.PixelWidth, 100, (int)ShapeY)),
                $"the geometry stroke was not rendered at its base thickness: {ReadPixel(pixels, bitmap.PixelWidth, 100, (int)ShapeY)}");
            // The viewer keeps its live zoom for the on-screen canvas.
            Assert.Equal(2.5, manager.ViewportScale, 3);
        });
    }

    [Fact]
    public void RenderCanvasContent_ReturnsNullWithoutImage()
    {
        RunOnSta(() =>
        {
            var (_, viewer, _, _) = CreateControl(withImage: false);

            Assert.Null(viewer.RenderCanvasContent());
        });
    }

    [Fact]
    public void SaveCanvasContent_WritesCanvasResolutionPng()
    {
        RunOnSta(() =>
        {
            var (_, viewer, manager, layer) = CreateControl();
            AddHorizontalLine(manager, layer);
            var directory = Path.Combine(Path.GetTempPath(), "LanShapesCanvasExportTests");
            Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".png");

            try
            {
                viewer.SaveCanvasContent(filePath);

                Assert.True(File.Exists(filePath));
                var saved = BitmapFrame.Create(new Uri(filePath), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                Assert.Equal(ImageWidth, saved.PixelWidth);
                Assert.Equal(ImageHeight, saved.PixelHeight);
                var pixels = ReadBgra32(saved);
                Assert.True(IsShapeStroke(ReadPixel(pixels, saved.PixelWidth, 100, (int)ShapeY)),
                    "the saved file does not contain the geometry stroke");
            }
            finally
            {
                File.Delete(filePath);
            }
        });
    }

    [Fact]
    public void SaveCanvasContent_RejectsUnsupportedExtension()
    {
        RunOnSta(() =>
        {
            var (_, viewer, _, _) = CreateControl();

            Assert.Throws<NotSupportedException>(() => viewer.SaveCanvasContent("canvas.tiff"));
        });
    }

    private static void AddHorizontalLine(SketchBoardDataManager manager, ShapeLayer layer)
        => manager.AddShape(new Line(layer) { Start = new Point(20, ShapeY), End = new Point(180, ShapeY) });

    private static (ImageViewerControl Control, Lan.ImageViewer.ImageViewer Viewer, SketchBoardDataManager Manager,
        ShapeLayer Layer) CreateControl(bool withImage = true)
    {
        var layer = TestShapeLayer.Create();
        var layerManager = new ShapeLayerManager();
        layerManager.Layers.Add(layer);

        var geometryTypeManager = new GeometryTypeManager();
        geometryTypeManager.RegisterGeometryType<Line>();

        var manager = new SketchBoardDataManager();
        var viewModel = new ImageViewerControlViewModel(layerManager, manager, geometryTypeManager);
        viewModel.Image = withImage ? CreateWhiteImage() : null!;

        var control = new ImageViewerControl { DataContext = viewModel };
        Layout(control, new Size(400, 300));
        var viewer = Assert.IsType<Lan.ImageViewer.ImageViewer>(control.FindName("ImageViewer"));
        return (control, viewer, manager, layer);
    }

    private static void Layout(FrameworkElement element, Size size)
    {
        element.Measure(size);
        element.Arrange(new Rect(size));
        element.UpdateLayout();
    }

    private static BitmapSource CreateWhiteImage()
    {
        var stride = ImageWidth * 4;
        var pixels = new byte[stride * ImageHeight];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = 0xFF;
        }

        var bitmap = BitmapSource.Create(ImageWidth, ImageHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] ReadBgra32(BitmapSource bitmap)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var buffer = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(buffer, stride, 0);
        return buffer;
    }

    private static Color ReadPixel(byte[] pixels, int width, int x, int y)
    {
        var index = (y * width + x) * 4;
        return Color.FromArgb(pixels[index + 3], pixels[index + 2], pixels[index + 1], pixels[index]);
    }

    /// <summary>The test layer draws its normal stroke in red; the shape covers the pixel fully.</summary>
    private static bool IsShapeStroke(Color color)
        => color.A == 0xFF && color.R == 0xFF && color.G == 0 && color.B == 0;

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    [Fact]
    public void SaveCanvasButton_IsPresentOnTheToolbar()
    {
        RunOnSta(() =>
        {
            var (control, _, _, _) = CreateControl();

            Assert.NotNull(control.FindName("BtnSaveCanvas"));
        });
    }
}
