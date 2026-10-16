using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using Lan.Shapes.DialogGeometry;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class HistoryBoundaryTests
{
    [Fact]
    public void ViewportZoom_PreservesAngleTranslationHistory()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var board = new SketchBoardDataManager();
            board.SetShapeLayer(TestShapeLayer.Create());
            var angle = new Angle(board.CurrentShapeLayer!);
            angle.FromData(new PointsData(1, new List<Point> { new(10, 10), new(30, 30), new(60, 10) }));
            board.AddShape(angle);
            angle.Translate(new Vector(5, 5));
            Assert.True(board.CanUndo);
            board.OnImageViewerPropertyChanged(2);
            Assert.True(board.CanUndo);
            board.Undo();
            Assert.Equal(new Point(10, 10), angle.FirstPoint);
        });
    }

    [Fact]
    public void AnnotationEdit_InvalidatesTranslationHistoryBeforeItCanOverwriteTheNewLabel()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var board = new SketchBoardDataManager();
            board.SetShapeLayer(TestShapeLayer.Create());
            var line = new Line(board.CurrentShapeLayer!);
            line.FromData(new PointsData(1, new List<Point> { new(10, 10), new(60, 10) }));
            board.AddShape(line);
            line.Translate(new Vector(5, 5));
            Assert.True(board.CanUndo);
            line.AddText("new annotation", new Point(20, 20));
            Assert.False(board.CanUndo);
        });
    }

    [Fact]
    public void PolygonVertexResize_InvalidatesPreviousTranslationHistory()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var board = new SketchBoardDataManager();
            board.SetShapeLayer(TestShapeLayer.Create());
            var polygon = new Polygon(board.CurrentShapeLayer!);
            polygon.FromData(new PointsData(1, new List<Point> { new(0, 0), new(100, 0), new(100, 100) }));
            board.AddShape(polygon);
            polygon.Translate(new Vector(5, 5));
            board.SetSelection(new[] { polygon });
            Assert.True(board.CanUndo);
            polygon.OnMouseLeftButtonDown(new Point(105, 5));
            polygon.OnMouseMove(new Point(125, 5), System.Windows.Input.MouseButtonState.Pressed);
            polygon.OnMouseLeftButtonUp(new Point(125, 5));
            Assert.Equal(new Point(125, 5), polygon.GetMetaData().DataPoints[1]);
            Assert.False(board.CanUndo);
        });
    }

    [Fact]
    public void DxfResize_InvalidatesPreviousTranslationHistory()
    {
        BulkLayerFeatureTests.Sta(() =>
        {
            var board = new SketchBoardDataManager();
            board.SetShapeLayer(TestShapeLayer.Create());
            var document = new netDxf.DxfDocument();
            document.Entities.Add(new netDxf.Entities.Line(new netDxf.Vector2(10, 20), new netDxf.Vector2(140, 150)));
            var shape = new DxfGeometry(board.CurrentShapeLayer!, new StubDxfService(document));
            typeof(DxfGeometry).GetMethod("ReadDxfFile", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(shape, new object[] { "drawing.dxf", new Point(100, 100), 1.0 });
            shape.OnMouseLeftButtonUp(new Point(100, 100));
            board.AddShape(shape);
            shape.Translate(new Vector(5, 5));
            shape.OnMouseLeftButtonUp(new Point(105, 105));
            board.SetSelection(new[] { shape });
            Assert.True(board.CanUndo);
            var bounds = shape.BoundsRect;
            shape.OnMouseLeftButtonDown(bounds.BottomRight);
            shape.OnMouseMove(bounds.BottomRight + new Vector(20, 20), System.Windows.Input.MouseButtonState.Pressed);
            shape.OnMouseLeftButtonUp(bounds.BottomRight + new Vector(20, 20));
            Assert.True(shape.BoundsRect.Width > bounds.Width);
            Assert.False(board.CanUndo);
        });
    }

    private sealed class StubDxfService : IDxfDocumentService
    {
        private readonly netDxf.DxfDocument _document;
        public StubDxfService(netDxf.DxfDocument document) => _document = document;
        public netDxf.DxfDocument Load(string path) => _document;
        public void Save(netDxf.DxfDocument document, string path) { }
    }
}

