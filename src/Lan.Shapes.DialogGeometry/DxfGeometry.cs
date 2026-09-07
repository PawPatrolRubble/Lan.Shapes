#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Lan.Shapes.DialogGeometry.Dialog;
using Lan.Shapes.Enums;
using Lan.Shapes.Handle;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;

using Microsoft.Win32;

using netDxf;
using netDxf.Entities;

using Circle = netDxf.Entities.Circle;
using Line = netDxf.Entities.Line;
using Point = System.Windows.Point;

namespace Lan.Shapes.DialogGeometry
{
    public class DxfGeometry : ShapeVisualBase, IBoardContextAware
    {
        private const int RotationHandleId = 100;
        private const int TranslateHandleId = 101;
        private const double RotationBarScreenLength = 50.0;
        private const double TranslateHandleSizeMultiplier = 1.5;

        #region constructor

        private readonly IDxfDocumentService _dxfDocumentService;

        public DxfGeometry(ShapeLayer layer) : this(layer, DxfDocumentService.Default)
        {
        }

        public DxfGeometry(ShapeLayer layer, IDxfDocumentService dxfDocumentService) : base(layer)
        {
            _dxfDocumentService = dxfDocumentService ?? throw new ArgumentNullException(nameof(dxfDocumentService));
        }

        #endregion

        #region private fields

        private GeometryGroup? _dxfGeometryWrapper;

        private Geometry? _geometry;

        private DxfDocument? _originalDxfDoc;
        private Point _initialOffset;
        private Matrix _accumulatedWpfTransform = Matrix.Identity;
        private double _boardWidth;
        private double _boardHeight;

        #endregion

        #region Overrides of ShapeVisualBase

        public override Rect BoundsRect
        {
            get { return _dxfGeometryWrapper?.Bounds ?? new Rect(); }
        }

        protected override void CreateHandles()
        {
            // handles are creating during ReadDxfFile
        }

        protected override bool AreDragHandlesActive => !IsLocked;

        private bool AreSelectionHandlesVisible =>
            !IsGeometryRendered || State == ShapeVisualState.Selected;

        protected override void OnDragHandleSizeChanges(double dragHandleSize)
        {
            base.OnDragHandleSizeChanges(dragHandleSize);

            var translateHandle = Handles.FirstOrDefault(h => h.Id == TranslateHandleId);
            if (translateHandle != null)
            {
                var translateHandleSize = dragHandleSize * TranslateHandleSizeMultiplier;
                translateHandle.HandleSize = new Size(translateHandleSize, translateHandleSize);
            }
        }

        protected override void OnViewportScaleChanged(double viewportScale)
        {
            UpdateHandleLocation();
        }

        private Point GetRotationHandleCenter(Rect bounds)
        {
            return new Point(
                (bounds.Left + bounds.Right) / 2,
                bounds.Top - RotationBarScreenLength / ViewportScale);
        }

        private void UpdateHandleLocation()
        {
            var bounds = BoundsRect;
            if (bounds.IsEmpty)
            {
                return;
            }

            foreach (var h in Handles)
            {
                if (h.Id == (int)DragLocation.TopLeft)
                {
                    h.GeometryCenter = bounds.TopLeft;
                }
                else if (h.Id == (int)DragLocation.TopRight)
                {
                    h.GeometryCenter = bounds.TopRight;
                }
                else if (h.Id == (int)DragLocation.BottomRight)
                {
                    h.GeometryCenter = bounds.BottomRight;
                }
                else if (h.Id == (int)DragLocation.BottomLeft)
                {
                    h.GeometryCenter = bounds.BottomLeft;
                }
                else if (h.Id == TranslateHandleId)
                {
                    h.GeometryCenter = new Point(
                        (bounds.Left + bounds.Right) / 2,
                        (bounds.Top + bounds.Bottom) / 2);
                }
                else if (h.Id == RotationHandleId)
                {
                    h.GeometryCenter = GetRotationHandleCenter(bounds);
                }
            }
        }

        public override DragHandle? FindDragHandleMouseOver(Point p)
        {
            if (IsLocked)
            {
                return null;
            }

            var translateHandle = Handles.FirstOrDefault(h => h.Id == TranslateHandleId);
            if (translateHandle?.FillContains(p) == true)
            {
                return translateHandle;
            }

            return AreSelectionHandlesVisible
                ? base.FindDragHandleMouseOver(p)
                : null;
        }

        protected override void HandleResizing(Point point)
        {
            if (SelectedDragHandle == null || _dxfGeometryWrapper == null)
            {
                return;
            }

            var oldBounds = BoundsRect;
            if (oldBounds.IsEmpty || oldBounds.Width == 0 || oldBounds.Height == 0)
            {
                return;
            }

            if (SelectedDragHandle.Id == RotationHandleId)
            {
                var center = new Point((oldBounds.Left + oldBounds.Right) / 2, (oldBounds.Top + oldBounds.Bottom) / 2);
                if (OldPointForTranslate.HasValue)
                {
                    var vOld = OldPointForTranslate.Value - center;
                    var vNew = point - center;

                    var angleOld = Math.Atan2(vOld.Y, vOld.X) * 180 / Math.PI;
                    var angleNew = Math.Atan2(vNew.Y, vNew.X) * 180 / Math.PI;
                    var angleDelta = angleNew - angleOld;

                    var rotateTransform = new RotateTransform(angleDelta, center.X, center.Y);

                    if (_dxfGeometryWrapper.Transform is TransformGroup rotGroup)
                    {
                        rotGroup.Children.Add(rotateTransform);
                    }
                    else
                    {
                        var group = new TransformGroup();
                        if (_dxfGeometryWrapper.Transform != null)
                        {
                            group.Children.Add(_dxfGeometryWrapper.Transform);
                        }

                        group.Children.Add(rotateTransform);
                        _dxfGeometryWrapper.Transform = group;
                    }
                }

                OldPointForTranslate = point;
                UpdateHandleLocation();
                return;
            }

            var centerX = oldBounds.Left;
            var centerY = oldBounds.Top;
            var handleX = oldBounds.Right;
            var handleY = oldBounds.Bottom;

            if (SelectedDragHandle.Id == (int)DragLocation.TopLeft)
            {
                centerX = oldBounds.Right;
                centerY = oldBounds.Bottom;
                handleX = oldBounds.Left;
                handleY = oldBounds.Top;
            }
            else if (SelectedDragHandle.Id == (int)DragLocation.TopRight)
            {
                centerX = oldBounds.Left;
                centerY = oldBounds.Bottom;
                handleX = oldBounds.Right;
                handleY = oldBounds.Top;
            }
            else if (SelectedDragHandle.Id == (int)DragLocation.BottomRight)
            {
                centerX = oldBounds.Left;
                centerY = oldBounds.Top;
                handleX = oldBounds.Right;
                handleY = oldBounds.Bottom;
            }
            else if (SelectedDragHandle.Id == (int)DragLocation.BottomLeft)
            {
                centerX = oldBounds.Right;
                centerY = oldBounds.Top;
                handleX = oldBounds.Left;
                handleY = oldBounds.Bottom;
            }

            var diagonal = new Vector(handleX - centerX, handleY - centerY);
            var mouseVec = new Vector(point.X - centerX, point.Y - centerY);

            // Calculate uniform scale by projecting the mouse movement along the diagonal
            var scale = mouseVec * diagonal / (diagonal * diagonal);

            // Prevent the scale from becoming too small, inverting, or effectively flattening
            if (scale < 0.05)
            {
                scale = 0.05;
            }

            var scaleTransform = new ScaleTransform(scale, scale, centerX, centerY);

            if (_dxfGeometryWrapper.Transform is TransformGroup tg)
            {
                tg.Children.Add(scaleTransform);
            }
            else
            {
                var group = new TransformGroup();
                if (_dxfGeometryWrapper.Transform != null)
                {
                    group.Children.Add(_dxfGeometryWrapper.Transform);
                }

                group.Children.Add(scaleTransform);
                _dxfGeometryWrapper.Transform = group;
            }

            UpdateHandleLocation();
        }

        protected override void HandleTranslate(Point newPoint)
        {
            if (OldPointForTranslate.HasValue && IsGeometryRendered)
            {
                SetMouseCursorToHand();
                var delta = newPoint - OldPointForTranslate.Value;

                // 1. Translate the main DXF wrapper geometry
                if (_dxfGeometryWrapper != null)
                {
                    if (_dxfGeometryWrapper.Transform is TranslateTransform tt)
                    {
                        tt.X += delta.X;
                        tt.Y += delta.Y;
                    }
                    else if (_dxfGeometryWrapper.Transform == null ||
                             _dxfGeometryWrapper.Transform == Transform.Identity)
                    {
                        _dxfGeometryWrapper.Transform = new TranslateTransform(delta.X, delta.Y);
                    }
                    else if (_dxfGeometryWrapper.Transform is TransformGroup tg)
                    {
                        tg.Children.Add(new TranslateTransform(delta.X, delta.Y));
                    }
                    else
                    {
                        var group = new TransformGroup();
                        group.Children.Add(_dxfGeometryWrapper.Transform);
                        group.Children.Add(new TranslateTransform(delta.X, delta.Y));
                        _dxfGeometryWrapper.Transform = group;
                    }
                }

                // 2. Translate any handles attached
                foreach (var h in Handles)
                {
                    h.GeometryCenter += delta;
                }

                OldPointForTranslate = newPoint;
            }
        }

        public override void OnDeselected()
        {
        }

        public override void OnSelected()
        {
        }

        public override void OnMouseRightButtonUp(Point mousePosition)
        {
            base.OnMouseRightButtonUp(mousePosition);

            if (IsGeometryRendered)
            {
                var contextMenu = new ContextMenu();

                var exportItem = new MenuItem { Header = "Export to DXF..." };
                exportItem.Click += (s, e) =>
                {
                    var dialog = new DialogService();
                    dialog.ShowDialog<DxfExportDialog, DxfExportDialogViewModel>(() => new DxfExportDialogViewModel(),
                        x =>
                        {
                            if (x.Result == DialogResult.Ok)
                            {
                                var doc = ExportToDxf(new Point(x.TopLeftX, x.TopLeftY), x.ReverseYAxis);

                                var saveFileDialog = new SaveFileDialog();
                                saveFileDialog.Filter = "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*";
                                saveFileDialog.DefaultExt = "dxf";
                                saveFileDialog.AddExtension = true;
                                saveFileDialog.FileName = "exported_sketch.dxf";

                                if (saveFileDialog.ShowDialog() == true)
                                {
                                    _dxfDocumentService.Save(doc, saveFileDialog.FileName);
                                    MessageBox.Show("Export to DXF completed!");
                                }
                            }
                        });
                };
                contextMenu.Items.Add(exportItem);

                var anotherItem = new MenuItem { Header = "Delete Geometry" };
                anotherItem.Click += (s, e) =>
                {
                    // Handle deletion
                };
                contextMenu.Items.Add(anotherItem);

                contextMenu.IsOpen = true;
            }
        }


        public override void UpdateVisual()
        {
            if (ShapeStyler == null)
            {
                return;
            }

            var renderContext = RenderOpen();
            var bounds = BoundsRect;

            renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, RenderGeometry);

            if (AreSelectionHandlesVisible &&
                !bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0 && ShapeStyler.SketchPen != null)
            {
                var topCenter = new Point((bounds.Left + bounds.Right) / 2, bounds.Top);
                var handleCenter = GetRotationHandleCenter(bounds);
                renderContext.DrawLine(ShapeStyler.SketchPen, topCenter, handleCenter);
            }

            DrawVisibleHandles(renderContext);

            renderContext.Close();
        }

        private void DrawVisibleHandles(DrawingContext renderContext)
        {
            if (!AreDragHandlesActive)
            {
                return;
            }

            var fill = GetDragHandleFill();
            var pen = GetDragHandlePen();
            foreach (var handle in Handles)
            {
                if ((AreSelectionHandlesVisible || handle.Id == TranslateHandleId) &&
                    handle.HandleGeometry != null)
                {
                    renderContext.DrawGeometry(fill, pen, handle.HandleGeometry);
                }
            }
        }

        public override void OnMouseMove(Point point, MouseButtonState buttonState)
        {
            if (IsGeometryRendered && buttonState == MouseButtonState.Pressed)
            {
                if (SelectedDragHandle?.Id == TranslateHandleId)
                {
                    IsBeingDraggedOrPanMoving = true;
                    UpdateMouseCursor(DragLocation.Move);
                    HandleTranslate(point);
                    UpdateGeometryGroup();
                    UpdateVisual();
                }
                else if (SelectedDragHandle != null)
                {
                    IsBeingDraggedOrPanMoving = true;
                    HandleResizing(point);
                    UpdateGeometryGroup();
                    UpdateVisual();
                }
                else
                {
                    IsBeingDraggedOrPanMoving = true;
                    HandleTranslate(point);
                    UpdateGeometryGroup();
                    UpdateVisual();
                }
            }
            else
            {
                base.OnMouseMove(point, buttonState);
            }
        }

        public override void OnMouseLeftButtonUp(Point newPoint)
        {
            base.OnMouseLeftButtonUp(newPoint);

            BakeTransform();
            OldPointForTranslate = null;
            SelectedDragHandle = null;
            IsBeingDraggedOrPanMoving = false;
        }

        private void BakeTransform()
        {
            if (_dxfGeometryWrapper == null || _dxfGeometryWrapper.Transform == null ||
                _dxfGeometryWrapper.Transform.Value.IsIdentity)
            {
                return;
            }

            if (!(_geometry is PathGeometry originalPathGeo))
            {
                return;
            }

            var matrix = _dxfGeometryWrapper.Transform.Value;
            _accumulatedWpfTransform = Matrix.Multiply(_accumulatedWpfTransform, matrix);

            // Calculate uniform scale and rotation from the standard affine 2D matrix
            var scale = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M21 * matrix.M21);
            var rotation = Math.Atan2(matrix.M21, matrix.M11) * 180 / Math.PI;

            var bakedGeometry = new PathGeometry();
            bakedGeometry.FillRule = originalPathGeo.FillRule;

            foreach (var figure in originalPathGeo.Figures)
            {
                var newFigure = new PathFigure
                {
                    StartPoint = matrix.Transform(figure.StartPoint),
                    IsClosed = figure.IsClosed,
                    IsFilled = figure.IsFilled
                };

                foreach (var segment in figure.Segments)
                {
                    if (segment is LineSegment ls)
                    {
                        newFigure.Segments.Add(new LineSegment(matrix.Transform(ls.Point), ls.IsStroked));
                    }
                    else if (segment is ArcSegment arc)
                    {
                        newFigure.Segments.Add(new ArcSegment(
                            matrix.Transform(arc.Point),
                            new Size(arc.Size.Width * scale, arc.Size.Height * scale),
                            arc.RotationAngle + rotation,
                            arc.IsLargeArc,
                            arc.SweepDirection,
                            arc.IsStroked));
                    }
                    else if (segment is BezierSegment bezier)
                    {
                        newFigure.Segments.Add(new BezierSegment(
                            matrix.Transform(bezier.Point1),
                            matrix.Transform(bezier.Point2),
                            matrix.Transform(bezier.Point3),
                            bezier.IsStroked));
                    }
                    else if (segment is PolyLineSegment pls)
                    {
                        var newPoints = new PointCollection();
                        foreach (var p in pls.Points)
                        {
                            newPoints.Add(matrix.Transform(p));
                        }

                        newFigure.Segments.Add(new PolyLineSegment(newPoints, pls.IsStroked));
                    }
                }

                bakedGeometry.Figures.Add(newFigure);
            }

            bakedGeometry.Freeze();

            _geometry = bakedGeometry;
            _dxfGeometryWrapper.Children.Clear();
            _dxfGeometryWrapper.Children.Add(_geometry);
            _dxfGeometryWrapper.Transform = Transform.Identity;

            UpdateHandleLocation();
            UpdateVisual();
        }

        public override void OnMouseLeftButtonDown(Point mousePoint)
        {
            base.OnMouseLeftButtonDown(mousePoint);
            if (!IsGeometryRendered)
            {
                var dialog = new DialogService();
                dialog.ShowDialog<DxfImportDialog, DxfImportDialogViewModel>(
                    () => new DxfImportDialogViewModel(ShapeLayer.Measurement.PixelPerUnit),
                    x =>
                    {
                        if (x.Result == DialogResult.Ok && !string.IsNullOrWhiteSpace(x.FilePath))
                        {
                            ReadDxfFile(x.FilePath, mousePoint, x.PixelToMmFactor);
                            UpdateVisual();
                            IsGeometryRendered = true;
                        }
                        else
                        {
                            // Dialog was cancelled - signal that this shape should be removed
                            OnShapeCreationCancelled();
                        }
                    });
            }
        }

        public void OnBoardContextAvailable(double boardWidth, double boardHeight)
        {
            _boardWidth = boardWidth;
            _boardHeight = boardHeight;
        }

        private double _dxfRenderScale = 1.0;

        private void ReadDxfFile(string filePath, Point offset, double pixelToMmFactor = 1.0)
        {
            var doc = _dxfDocumentService.Load(filePath);
            _originalDxfDoc = doc;
            _initialOffset = default;
            _accumulatedWpfTransform = Matrix.Identity;
            var dpiScale = 1.0;
            if (Application.Current != null && Application.Current.MainWindow != null)
            {
                dpiScale = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleX;
            }

            // The user inputs pixelToMmFactor based on physical image pixels, but WPF Canvas draws in Device-Independent Units.
            // When Windows has Display Scaling (e.g. 125%), 1 DIU naturally renders to 1.25 physical pixels,
            // making the manually calculated physical pixel scale appear too large. Thus, we divide it by the DpiScale.
            double actualScale = pixelToMmFactor / dpiScale;
            _dxfRenderScale = actualScale;

            _geometry = DxfRenderer.BuildGeometry(doc, actualScale);

            _dxfGeometryWrapper = new GeometryGroup();
            if (_geometry != null)
            {
                _dxfGeometryWrapper.Children.Add(_geometry);

                var geometryBounds = _geometry.Bounds;
                var targetCenter = _boardWidth > 0 && _boardHeight > 0
                    ? new Point(_boardWidth / 2, _boardHeight / 2)
                    : offset;
                var translation = geometryBounds.IsEmpty
                    ? (Vector)targetCenter
                    : targetCenter - new Point(
                        geometryBounds.Left + geometryBounds.Width / 2,
                        geometryBounds.Top + geometryBounds.Height / 2);

                _dxfGeometryWrapper.Transform = new TranslateTransform(translation.X, translation.Y);
            }

            var dragHandleSize = ShapeStyler?.DragHandleSize ?? 10;
            var bounds = BoundsRect;

            RegisterHandle(new RectDragHandle(dragHandleSize, bounds.TopLeft, DragLocation.TopLeft));
            RegisterHandle(new RectDragHandle(dragHandleSize, bounds.TopRight, DragLocation.TopRight));
            RegisterHandle(new RectDragHandle(dragHandleSize, bounds.BottomRight, DragLocation.BottomRight));
            RegisterHandle(new RectDragHandle(dragHandleSize, bounds.BottomLeft, DragLocation.BottomLeft));

            var center = new Point(
                (bounds.Left + bounds.Right) / 2,
                (bounds.Top + bounds.Bottom) / 2);
            var translateHandleSize = dragHandleSize * TranslateHandleSizeMultiplier;
            RegisterHandle(new RectDragHandle(
                new Size(translateHandleSize, translateHandleSize),
                center,
                10,
                TranslateHandleId,
                DragLocation.Move));

            // Add a rotation handle above the center
            RegisterHandle(new RectDragHandle(dragHandleSize, GetRotationHandleCenter(bounds), RotationHandleId));

            RenderGeometryGroup.Children.Add(_dxfGeometryWrapper);
        }

        private (double MinX, double MinY, double MaxX, double MaxY) CalcBounds(DxfDocument doc)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var e in doc.Entities.All)
            {
                switch (e)
                {
                    case Line line:
                        Expand(ref minX, ref minY, ref maxX, ref maxY, line.StartPoint);
                        Expand(ref minX, ref minY, ref maxX, ref maxY, line.EndPoint);
                        break;
                    case Arc arc:
                        Expand(ref minX, ref minY, ref maxX, ref maxY,
                            arc.Center.X - arc.Radius, arc.Center.Y - arc.Radius,
                            arc.Center.X + arc.Radius, arc.Center.Y + arc.Radius);
                        break;
                    case Circle circle:
                        Expand(ref minX, ref minY, ref maxX, ref maxY,
                            circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius,
                            circle.Center.X + circle.Radius, circle.Center.Y + circle.Radius);
                        break;
                    case Polyline2D poly:
                        foreach (var v in poly.Vertexes)
                        {
                            Expand(ref minX, ref minY, ref maxX, ref maxY, v.Position.X, v.Position.Y);
                        }

                        break;
                    case Polyline3D poly3d:
                        foreach (var v in poly3d.Vertexes)
                        {
                            Expand(ref minX, ref minY, ref maxX, ref maxY, v);
                        }

                        break;
                    case Spline spline:
                        foreach (var cp in spline.ControlPoints)
                        {
                            Expand(ref minX, ref minY, ref maxX, ref maxY, cp);
                        }

                        break;
                }
            }

            if (minX == double.MaxValue)
            {
                return (0, 0, 100, 100);
            }

            return (minX, minY, maxX, maxY);
        }

        private static void Expand(ref double minX, ref double minY, ref double maxX, ref double maxY, Vector3 pt)
        {
            if (pt.X < minX)
            {
                minX = pt.X;
            }

            if (pt.Y < minY)
            {
                minY = pt.Y;
            }

            if (pt.X > maxX)
            {
                maxX = pt.X;
            }

            if (pt.Y > maxY)
            {
                maxY = pt.Y;
            }
        }

        private static void Expand(ref double minX, ref double minY, ref double maxX, ref double maxY,
            double x1, double y1, double x2, double y2)
        {
            if (x1 < minX)
            {
                minX = x1;
            }

            if (y1 < minY)
            {
                minY = y1;
            }

            if (x2 > maxX)
            {
                maxX = x2;
            }

            if (y2 > maxY)
            {
                maxY = y2;
            }
        }

        private static void Expand(ref double minX, ref double minY, ref double maxX, ref double maxY, double x,
            double y)
        {
            if (x < minX)
            {
                minX = x;
            }

            if (y < minY)
            {
                minY = y;
            }

            if (x > maxX)
            {
                maxX = x;
            }

            if (y > maxY)
            {
                maxY = y;
            }
        }


        public DxfDocument ExportToDxf(Point sketchBoardTopLeftRealWorld, bool reverseY)
        {
            BakeTransform();

            var doc = new DxfDocument();

            if (_originalDxfDoc == null)
            {
                return doc;
            }

            // We calculate the net affine transformation mapping from the ORIGINAL DXF coordinates to the final EXPORT DXF coordinates.
            // Let F(v) map from original DXF point v to export DXF point.
            var v0 = MapOriginalDxfPointToExportDxfPoint(new Vector3(0, 0, 0), sketchBoardTopLeftRealWorld, reverseY);
            var vX = MapOriginalDxfPointToExportDxfPoint(new Vector3(1, 0, 0), sketchBoardTopLeftRealWorld, reverseY);
            var vY = MapOriginalDxfPointToExportDxfPoint(new Vector3(0, 1, 0), sketchBoardTopLeftRealWorld, reverseY);

            var T = v0;

            // The column vectors of the transformation matrix
            var M11 = vX.X - v0.X;
            var M21 = vX.Y - v0.Y;

            var M12 = vY.X - v0.X;
            var M22 = vY.Y - v0.Y;

            var transformMatrix = new netDxf.Matrix3(
                M11, M12, 0,
                M21, M22, 0,
                0, 0, 1
            );

            // Copy layers to retain exact colors/line types
            foreach (var layer in _originalDxfDoc.Layers)
            {
                if (!doc.Layers.Contains(layer.Name))
                {
                    doc.Layers.Add((netDxf.Tables.Layer)layer.Clone());
                }
            }

            foreach (var entity in _originalDxfDoc.Entities.All)
            {
                var copy = (EntityObject)entity.Clone();
                copy.TransformBy(transformMatrix, T);
                doc.Entities.Add(copy);
            }

            return doc;
        }

        private Vector3 MapOriginalDxfPointToExportDxfPoint(Vector3 v, Point sketchBoardTopLeftRealWorld, bool reverseY)
        {
            var p0 = new Point(v.X * _dxfRenderScale + _initialOffset.X, -v.Y * _dxfRenderScale + _initialOffset.Y);
            var p1 = _accumulatedWpfTransform.Transform(p0);

            var dxfX = sketchBoardTopLeftRealWorld.X + p1.X / _dxfRenderScale;
            var dxfY = reverseY ? sketchBoardTopLeftRealWorld.Y + p1.Y / _dxfRenderScale
                                : sketchBoardTopLeftRealWorld.Y - p1.Y / _dxfRenderScale;

            return new Vector3(dxfX, dxfY, 0);
        }

        #endregion
    }
}
