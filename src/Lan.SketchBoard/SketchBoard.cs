#nullable enable

#region

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Interfaces;


#endregion

namespace Lan.SketchBoard
{
    public class SketchBoard : Canvas, ISketchBoard
    {
        #region fields

        public static readonly DependencyProperty SketchBoardDataManagerProperty = DependencyProperty.Register(
            "SketchBoardDataManager", typeof(ISketchBoardDataManager), typeof(SketchBoard),
            new PropertyMetadata(default(ISketchBoardDataManager), OnSketchBoardDataManagerChangedCallBack));


        public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(
            "Image", typeof(ImageSource), typeof(SketchBoard), new PropertyMetadata(default(ImageSource)));

        public static readonly DependencyProperty IsSnappingEnabledProperty = DependencyProperty.Register(
            nameof(IsSnappingEnabled), typeof(bool), typeof(SketchBoard),
            new PropertyMetadata(true, OnSnappingSettingsChanged));

        public static readonly DependencyProperty SnapToleranceProperty = DependencyProperty.Register(
            nameof(SnapTolerance), typeof(double), typeof(SketchBoard),
            new PropertyMetadata(8.0, OnSnappingSettingsChanged),
            value => value is double distance && distance >= 0 && double.IsFinite(distance));

        public static readonly DependencyProperty LineDirectionModeProperty = DependencyProperty.Register(
            nameof(LineDirectionMode), typeof(LineDirectionMode), typeof(SketchBoard),
            new PropertyMetadata(LineDirectionMode.Free, OnLineDirectionModeChanged),
            value => value is LineDirectionMode mode && Enum.IsDefined(mode));

        private readonly SnapMarkerVisual _snapMarker = new SnapMarkerVisual();
        // During a manager rebind, WPF still enumerates the old mirror while
        // its children are detached, even though the dependency property changed.
        private ISketchBoardDataManager? _visualDataManager;
        private ShapeVisualBase? _snapTargetShape;
        private Point? _lastDragPoint;
        private Point? _resizeStartPoint;
        private Point? _lastRawDragPoint;
        private ModifierKeys _dragModifiers;


        #endregion

        #region Properties


        public ImageSource Image
        {
            get => (ImageSource)GetValue(ImageProperty);
            set => SetValue(ImageProperty, value);
        }

        public ISketchBoardDataManager? SketchBoardDataManager
        {
            get => (ISketchBoardDataManager)GetValue(SketchBoardDataManagerProperty);
            set => SetValue(SketchBoardDataManagerProperty, value);
        }

        /// <summary>Enables snapping to completed shapes while drawing or resizing geometry.</summary>
        public bool IsSnappingEnabled
        {
            get => (bool)GetValue(IsSnappingEnabledProperty);
            set => SetValue(IsSnappingEnabledProperty, value);
        }

        /// <summary>Maximum snapping distance in screen device-independent pixels.</summary>
        public double SnapTolerance
        {
            get => (double)GetValue(SnapToleranceProperty);
            set => SetValue(SnapToleranceProperty, value);
        }

        /// <summary>Persistent axis constraint. In Free mode, Shift chooses the nearest axis.</summary>
        public LineDirectionMode LineDirectionMode
        {
            get => (LineDirectionMode)GetValue(LineDirectionModeProperty);
            set => SetValue(LineDirectionModeProperty, value);
        }

        #endregion


        public SketchBoard()
        {
            AddVisualChild(_snapMarker);
            // Stroke/handle sizing is driven solely by ImageViewer LocalScale →
            // SketchBoardDataManager.OnImageViewerPropertyChanged. Window resize
            // alone must not fight zoom-driven thickness (Phase 2 scale policy).
        }

        /// <summary>Invoked when an unhandled <see cref="E:System.Windows.Input.Keyboard.KeyDown" /> attached event reaches an element in its route that is derived from this class. Implement this method to add class handling for this event.</summary>
        /// <param name="e">The <see cref="T:System.Windows.Input.KeyEventArgs" /> that contains the event data.</param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key is Key.LeftShift or Key.RightShift)
                HandleModifierKeysChanged(Keyboard.Modifiers);
            if (e.Key == Key.Delete && SketchBoardDataManager?.SelectedGeometry != null)
            {
                SketchBoardDataManager?.RemoveShape(SketchBoardDataManager.SelectedGeometry);
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.Key is Key.LeftShift or Key.RightShift)
                HandleModifierKeysChanged(Keyboard.Modifiers);
        }

        protected void HandleModifierKeysChanged(ModifierKeys modifiers)
        {
            var geometry = IsDrawing ? SketchBoardDataManager?.CurrentGeometryInEdit
                : SketchBoardDataManager?.SelectedGeometry;
            if (geometry is ILineDirectionConstraint { ConstraintOrigin: not null }
                && _lastRawDragPoint is Point point && _lastDragPoint.HasValue
                && (IsDrawing || ShouldSnapResize(geometry, point)))
                HandleMouseMove(point, MouseButtonState.Pressed, modifiers);
        }

        #region others

        private static void OnSketchBoardDataManagerChangedCallBack(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is not SketchBoard sketchBoard) return;

            sketchBoard.ClearSnapMarker();
            sketchBoard.UpdateHoveredShape(null);
            sketchBoard._lastDragPoint = null;
            sketchBoard._resizeStartPoint = null;
            sketchBoard._lastRawDragPoint = null;
            if (e.OldValue is ISketchBoardDataManager previous)
            {
                previous.GeometryTypeSelected -= sketchBoard.OnDrawingToolChanged;
                previous.GeometryTypeUnselected -= sketchBoard.OnDrawingToolChanged;
                previous.Shapes.CollectionChanged -= sketchBoard.OnShapesChanged;
                if (previous is INotifyPropertyChanged observable)
                    observable.PropertyChanged -= sketchBoard.OnManagerPropertyChanged;
                if (ReferenceEquals(previous.SketchBoard, sketchBoard))
                    previous.VisualCollection.Clear();
            }

            sketchBoard._visualDataManager = e.NewValue as ISketchBoardDataManager;
            if (e.NewValue is ISketchBoardDataManager dataManager)
            {
                dataManager.InitializeVisualCollection(sketchBoard);
                dataManager.GeometryTypeSelected += sketchBoard.OnDrawingToolChanged;
                dataManager.GeometryTypeUnselected += sketchBoard.OnDrawingToolChanged;
                dataManager.Shapes.CollectionChanged += sketchBoard.OnShapesChanged;
                if (dataManager is INotifyPropertyChanged observable)
                    observable.PropertyChanged += sketchBoard.OnManagerPropertyChanged;
            }
        }

        private static void OnSnappingSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((SketchBoard)d).ClearSnapMarker();

        private static void OnLineDirectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var board = (SketchBoard)d;
            board.ClearSnapMarker();
            board.HandleModifierKeysChanged(board._dragModifiers);
        }

        #endregion


        #region overrides

        protected override int VisualChildrenCount
        {
            get => (_visualDataManager?.VisualCollection.Count ?? 0) + 1;
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index == (_visualDataManager?.VisualCollection.Count ?? 0)) return _snapMarker;
            return _visualDataManager?.VisualCollection[index] ?? throw new InvalidOperationException();
        }

        #endregion


        #region events handling

        /// <summary>
        /// right click the mouse means ending the drawing of current shape
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            HandleRightButtonUp(e.GetPosition(this));
            base.OnMouseRightButtonUp(e);
        }

        protected void HandleRightButtonUp(Point position)
        {
            if (SketchBoardDataManager == null) return;

            var wasDrawing = IsDrawing;
            var shape = wasDrawing
                ? SketchBoardDataManager.CurrentGeometryInEdit
                : GetHitTestShape(position) ?? SketchBoardDataManager.CurrentGeometryInEdit;
            shape?.OnMouseRightButtonUp(position);
            SketchBoardDataManager.UnselectGeometry();
            if (wasDrawing)
            {
                SketchBoardDataManager.UnselectGeometryType();
            }
            _lastDragPoint = null;
            _resizeStartPoint = null;
            ClearSnapMarker();
            _lastRawDragPoint = null;
        }


        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Focus();
            if (SketchBoardDataManager == null) return;

            HandleLeftButtonDown(e.GetPosition(this), e.ClickCount, Keyboard.Modifiers);

            // Keep forwarding the drag after the pointer leaves the board or
            // crosses a child visual. Without capture, WPF stops raising move/up
            // events and the shape remains in a half-dragged state.
            _leftDragMouseCaptured = CaptureMouse();
        }

        protected void HandleLeftButtonDown(Point position, int clickCount)
            => HandleLeftButtonDown(position, clickCount, ModifierKeys.None);

        protected void HandleLeftButtonDown(Point position, int clickCount, ModifierKeys modifiers)
        {
            if (SketchBoardDataManager == null) return;

            _suppressLeftDrag = false;
            _lastDragPoint = null;
            _resizeStartPoint = null;
            _lastRawDragPoint = null;
            _dragModifiers = modifiers;

            if (IsDrawing)
            {
                UpdateHoveredShape(null);
                _mouseDownHitExistingShape = false;
                position = SnapPoint(position, SketchBoardDataManager.CurrentGeometryInEdit);
                _lastDragPoint = position;
                var isNewSketch = SketchBoardDataManager.CurrentGeometryInEdit == null;
                var sketch = SketchBoardDataManager.CurrentGeometryInEdit
                    ?? SketchBoardDataManager.CreateNewGeometry(position);
                SketchBoardDataManager.SelectedGeometry = sketch;
                if (clickCount == 2 && !isNewSketch)
                {
                    sketch?.OnMouseLeftButtonDoubleClick(position);
                }
                else
                {
                    sketch?.OnMouseLeftButtonDown(position);
                }
                return;
            }

            ClearSnapMarker();

            if ((modifiers & ModifierKeys.Alt) != 0)
            {
                var candidates = GetHitTestShapes(position);
                candidates.RemoveAll(shape => shape.IsLocked);
                var selectedIndex = SketchBoardDataManager.SelectedGeometry is { } selected
                    ? candidates.IndexOf(selected)
                    : -1;
                SketchBoardDataManager.SelectedGeometry = candidates.Count == 0
                    ? null
                    : candidates[(selectedIndex + 1) % candidates.Count];
                _mouseDownHitExistingShape = candidates.Count > 0;
                _suppressLeftDrag = true;
                UpdateHoveredShape(null);
                return;
            }

            // The first click must also see locked shapes so a double-click to
            // unlock cannot accidentally create a new shape over them.
            var hitShape = GetHitTestShape(position, includeLocked: true);
            _mouseDownHitExistingShape = hitShape != null;

            if (clickCount == 2 && hitShape?.IsGeometryRendered == true)
            {
                _suppressLeftDrag = true;
                if (hitShape.IsLocked)
                {
                    hitShape.UnLock();
                    SketchBoardDataManager.SelectedGeometry = hitShape;
                    hitShape.State = ShapeVisualState.Selected;
                }
                else
                {
                    SketchBoardDataManager.SelectedGeometry = hitShape;
                    hitShape.Lock();
                }
                return;
            }

            if (hitShape?.IsLocked == true)
            {
                _suppressLeftDrag = true;
                return;
            }

            SketchBoardDataManager.SelectedGeometry = hitShape;

            if (clickCount == 2)
            {
                SketchBoardDataManager.SelectedGeometry?.OnMouseLeftButtonDoubleClick(position);
            }
            else
            {
                SketchBoardDataManager.SelectedGeometry?.OnMouseLeftButtonDown(position);
                if (SketchBoardDataManager.SelectedGeometry?.CanSnapDuringResize == true)
                {
                    _lastDragPoint = position;
                    _resizeStartPoint = position;
                }
            }
        }


        private bool _mouseDownHitExistingShape;
        private bool _leftDragMouseCaptured;
        private bool _suppressLeftDrag;
        private ShapeVisualBase? _hoveredShape;

        private bool IsDrawing => SketchBoardDataManager?.CurrentGeometryType != null
            || SketchBoardDataManager?.CurrentGeometryInEdit is { IsGeometryRendered: false };

        private bool ShouldSnapResize(ShapeVisualBase? shape, Point position) => shape?.CanSnapDuringResize == true
            && _resizeStartPoint is Point start && (position != start || _lastDragPoint != start);

        private ShapeVisualBase? GetHitTestShape(Point mousePosition, bool includeLocked = false)
        {
            if (SketchBoardDataManager == null) return null;

            if ((SketchBoardDataManager.SelectedGeometry?.IsBeingDraggedOrPanMoving ?? false)
                && !SketchBoardDataManager.SelectedGeometry.IsLocked)
            {
                return SketchBoardDataManager.SelectedGeometry;
            }

            var candidates = GetHitTestShapes(mousePosition);
            ShapeVisualBase? shape = null;
            // A selected handle must remain reachable even when its detection
            // region overlaps another shape's transparent interior or outline.
            foreach (var candidate in candidates)
            {
                if (GetShapePoint(candidate, mousePosition) is Point localPoint
                    && candidate.HasDragHandleAt(localPoint))
                {
                    shape = candidate;
                    break;
                }
            }

            if (shape == null)
            {
                foreach (var candidate in candidates)
                {
                    // Use the drawing's actual pens, including custom thickened
                    // strokes, rather than assuming every shape uses its layer pen.
                    if (GetShapePoint(candidate, mousePosition) is Point localPoint
                        && HasVisibleStrokeAt(candidate.Drawing, localPoint))
                    {
                        shape = candidate;
                        break;
                    }
                }
            }

            shape ??= candidates.Count > 0 ? candidates[0] : null;
            return shape?.IsLocked == true && !includeLocked ? null : shape;
        }

        private List<ShapeVisualBase> GetHitTestShapes(Point mousePosition)
        {
            var candidates = new List<ShapeVisualBase>();
            if (SketchBoardDataManager == null) return candidates;

            // Enumerate every hit in visual z-order; the single-result overload
            // stops at transparent filled geometry and hides shapes underneath.
            VisualTreeHelper.HitTest(this, null, result =>
            {
                if (result.VisualHit is ShapeVisualBase shape && !candidates.Contains(shape))
                    candidates.Add(shape);
                return HitTestResultBehavior.Continue;
            }, new PointHitTestParameters(mousePosition));

            // Logical handle regions extend beyond visible handle geometry.
            for (var i = SketchBoardDataManager.Shapes.Count - 1; i >= 0; i--)
            {
                var shape = SketchBoardDataManager.Shapes[i];
                if (!candidates.Contains(shape) && GetShapePoint(shape, mousePosition) is Point localPoint
                    && shape.HasDragHandleAt(localPoint))
                    candidates.Add(shape);
            }

            return candidates;
        }

        private Point? GetShapePoint(ShapeVisualBase shape, Point boardPoint)
            => shape.TransformToAncestor(this).Inverse?.Transform(boardPoint);

        private static bool HasVisibleStrokeAt(Drawing? drawing, Point point)
        {
            if (drawing is DrawingGroup group)
            {
                if (group.Opacity <= 0) return false;
                if (group.Transform != null)
                {
                    var inverse = group.Transform.Inverse;
                    if (inverse == null) return false;
                    point = inverse.Transform(point);
                }
                if (group.ClipGeometry != null && !group.ClipGeometry.FillContains(point)) return false;
                foreach (var child in group.Children)
                {
                    if (HasVisibleStrokeAt(child, point)) return true;
                }
                return false;
            }

            return drawing is GeometryDrawing geometry
                && geometry.Pen is { Brush: { } brush } pen
                && brush.Opacity > 0
                && (brush is not SolidColorBrush solid || solid.Color.A > 0)
                && geometry.Geometry?.StrokeContains(pen, point) == true;
        }


        protected override void OnMouseMove(MouseEventArgs e)
        {
            HandleMouseMove(e.GetPosition(this), e.LeftButton, Keyboard.Modifiers);
        }

        protected void HandleMouseMove(Point position, MouseButtonState buttonState)
            => HandleMouseMove(position, buttonState, ModifierKeys.None);

        protected void HandleMouseMove(Point position, MouseButtonState buttonState, ModifierKeys modifiers)
        {
            if (buttonState == MouseButtonState.Pressed && _suppressLeftDrag) return;

            if (buttonState == MouseButtonState.Pressed)
            {
                _lastRawDragPoint = position;
                _dragModifiers = modifiers;
            }
            else
            {
                _lastRawDragPoint = null;
            }

            var isDrawing = IsDrawing;
            var shapeInEdit = isDrawing
                ? SketchBoardDataManager?.CurrentGeometryInEdit
                : SketchBoardDataManager?.SelectedGeometry;
            var isResizing = !isDrawing && buttonState == MouseButtonState.Pressed
                && ShouldSnapResize(shapeInEdit, position);

            if (isDrawing || isResizing)
                position = ResolveEditPoint(position, shapeInEdit, modifiers);
            else
                ClearSnapMarker();

            if (buttonState == MouseButtonState.Pressed)
            {
                if (shapeInEdit != null && !shapeInEdit.IsLocked)
                {
                    shapeInEdit.OnMouseMove(position, buttonState);
                    if (isDrawing || isResizing) _lastDragPoint = position;
                }
            }
            else
            {
                if (IsDrawing)
                {
                    UpdateHoveredShape(null);
                    Mouse.SetCursor(Cursors.Cross);
                    return;
                }

                var shape = GetHitTestShape(position);
                UpdateHoveredShape(shape);
                shape?.UpdateMouseCursorForPoint(position);
                if (shape == null)
                {
                    Mouse.SetCursor(Cursors.Arrow);
                }
            }
        }

        private void UpdateHoveredShape(ShapeVisualBase? shape)
        {
            if (ReferenceEquals(_hoveredShape, shape))
            {
                if (shape != null &&
                    !shape.IsLocked &&
                    shape.State == ShapeVisualState.Normal)
                {
                    shape.State = ShapeVisualState.MouseOver;
                }

                return;
            }

            if (_hoveredShape != null &&
                !_hoveredShape.IsLocked &&
                _hoveredShape.State == ShapeVisualState.MouseOver)
            {
                _hoveredShape.State = ShapeVisualState.Normal;
            }

            _hoveredShape = shape;
            if (_hoveredShape != null &&
                !_hoveredShape.IsLocked &&
                _hoveredShape.State == ShapeVisualState.Normal)
            {
                _hoveredShape.State = ShapeVisualState.MouseOver;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            try
            {
                HandleLeftButtonUp(e.GetPosition(this), Keyboard.Modifiers);
            }
            finally
            {
                if (_leftDragMouseCaptured)
                {
                    ReleaseMouseCapture();
                    _leftDragMouseCaptured = false;
                }
            }
        }

        protected void HandleLeftButtonUp(Point position)
            => HandleLeftButtonUp(position, ModifierKeys.None);

        protected void HandleLeftButtonUp(Point position, ModifierKeys modifiers)
        {
            _lastRawDragPoint = null;
            if (_suppressLeftDrag)
            {
                _suppressLeftDrag = false;
                return;
            }

            if (SketchBoardDataManager == null) return;

            var wasDrawing = IsDrawing;
            var geometry = wasDrawing
                ? SketchBoardDataManager.CurrentGeometryInEdit
                : SketchBoardDataManager.SelectedGeometry;
            if (geometry == null) return;

            if (wasDrawing || ShouldSnapResize(geometry, position))
            {
                position = ResolveEditPoint(position, geometry, modifiers);
                // Apply the release position as well: a move event may not have
                // reached the last snap target before the button was released.
                if ((!wasDrawing || !geometry.IsGeometryRendered)
                    && _lastDragPoint.HasValue && position != _lastDragPoint.Value)
                    geometry.OnMouseMove(position, MouseButtonState.Pressed);
            }
            _lastDragPoint = null;
            _resizeStartPoint = null;

            if (!geometry.IsGeometryRendered)
                SketchBoardDataManager.RaiseNewShapeSketched(geometry);

            geometry.OnMouseLeftButtonUp(position);

            if (geometry.IsGeometryRendered)
            {
                if (!_mouseDownHitExistingShape)
                    SketchBoardDataManager.UnselectGeometry();
                SketchBoardDataManager.UnselectGeometryType();
                ClearSnapMarker();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            ClearSnapMarker();
            base.OnMouseLeave(e);
        }

        private Point ResolveEditPoint(Point position, ShapeVisualBase? geometryInEdit, ModifierKeys modifiers)
        {
            var mode = LineDirectionMode;
            var origin = (geometryInEdit as ILineDirectionConstraint)?.ConstraintOrigin;
            if (origin is Point fixedPoint)
            {
                if (mode == LineDirectionMode.Free && (modifiers & ModifierKeys.Shift) != 0)
                {
                    var delta = position - fixedPoint;
                    mode = Math.Abs(delta.X) >= Math.Abs(delta.Y)
                        ? LineDirectionMode.Horizontal : LineDirectionMode.Vertical;
                }

                position = mode switch
                {
                    LineDirectionMode.Horizontal => new Point(position.X, fixedPoint.Y),
                    LineDirectionMode.Vertical => new Point(fixedPoint.X, position.Y),
                    _ => position
                };
            }

            return SnapPoint(position, geometryInEdit, origin, mode);
        }

        private Point SnapPoint(Point position, ShapeVisualBase? geometryInEdit,
            Point? constraintOrigin = null, LineDirectionMode mode = LineDirectionMode.Free)
        {
            var manager = SketchBoardDataManager;
            if (!IsSnappingEnabled || manager == null)
            {
                ClearSnapMarker();
                return position;
            }

            var scale = manager.ViewportScale;
            if (!double.IsFinite(scale) || scale <= 0) scale = 1;
            var tolerance = SnapTolerance / scale;
            var nearestDistanceSquared = tolerance * tolerance;
            Point? snapPoint = null;
            ShapeVisualBase? snapShape = null;

            // Scan anchors directly, without hit testing or selecting the target.
            // Reverse order gives the topmost shape priority when distances tie.
            for (var i = manager.Shapes.Count - 1; i >= 0; i--)
            {
                var shape = manager.Shapes[i];
                if (!shape.IsGeometryRendered || ReferenceEquals(shape, geometryInEdit)
                    || ReferenceEquals(shape, manager.CurrentGeometryInEdit)) continue;
                GeneralTransform? transform = null;
                foreach (var anchor in shape.GetSnapPoints())
                {
                    transform ??= shape.TransformToAncestor(this);
                    var point = transform.Transform(anchor);
                    if (constraintOrigin is Point origin)
                    {
                        // Allow only floating-point roundoff; nearby off-axis anchors must not tilt a line.
                        const double axisEpsilon = 1e-7;
                        if (mode == LineDirectionMode.Horizontal)
                        {
                            if (Math.Abs(point.Y - origin.Y) > axisEpsilon) continue;
                            point.Y = origin.Y;
                        }
                        else if (mode == LineDirectionMode.Vertical)
                        {
                            if (Math.Abs(point.X - origin.X) > axisEpsilon) continue;
                            point.X = origin.X;
                        }
                    }
                    var distanceSquared = (point - position).LengthSquared;
                    if (double.IsFinite(distanceSquared) && distanceSquared <= nearestDistanceSquared
                        && (snapPoint == null || distanceSquared < nearestDistanceSquared))
                    {
                        nearestDistanceSquared = distanceSquared;
                        snapPoint = point;
                        snapShape = shape;
                    }
                }
            }

            if (!ReferenceEquals(_snapTargetShape, snapShape))
            {
                if (_snapTargetShape != null) _snapTargetShape.PropertyChanged -= OnSnapTargetChanged;
                _snapTargetShape = snapShape;
                if (_snapTargetShape != null) _snapTargetShape.PropertyChanged += OnSnapTargetChanged;
            }
            _snapMarker.Update(snapPoint, scale);
            return snapPoint ?? position;
        }

        private void ClearSnapMarker()
        {
            if (_snapTargetShape != null) _snapTargetShape.PropertyChanged -= OnSnapTargetChanged;
            _snapTargetShape = null;
            _snapMarker.Update(null, 1);
        }

        private void OnSnapTargetChanged(object? sender, PropertyChangedEventArgs e) => ClearSnapMarker();

        private void OnDrawingToolChanged(object? sender, Type type)
        {
            ClearSnapMarker();
            if (IsDrawing) UpdateHoveredShape(null);
        }

        private void OnShapesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_snapTargetShape != null && SketchBoardDataManager?.Shapes.Contains(_snapTargetShape) != true)
                ClearSnapMarker();
        }

        private void OnManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if ((!IsDrawing && SketchBoardDataManager?.SelectedGeometry?.CanSnapDuringResize != true)
                || e.PropertyName == nameof(ISketchBoardDataManager.ViewportScale)
                || e.PropertyName == nameof(ISketchBoardDataManager.SelectedGeometry))
                ClearSnapMarker();
        }

        private sealed class SnapMarkerVisual : DrawingVisual
        {
            private Point? _point;
            private double _scale;

            public void Update(Point? point, double scale)
            {
                if (_point == point && (point == null || _scale == scale)) return;
                _point = point;
                _scale = scale;
                using var drawing = RenderOpen();
                if (point is not Point center) return;
                var radius = 5 / scale;
                drawing.DrawEllipse(null, new Pen(Brushes.White, 3.5 / scale), center, radius, radius);
                var pen = new Pen(Brushes.DeepSkyBlue, 1.5 / scale);
                drawing.DrawEllipse(null, pen, center, radius, radius);
                drawing.DrawLine(pen, center - new Vector(3 / scale, 0), center + new Vector(3 / scale, 0));
                drawing.DrawLine(pen, center - new Vector(0, 3 / scale), center + new Vector(0, 3 / scale));
            }

            protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) => null;
            protected override GeometryHitTestResult? HitTestCore(GeometryHitTestParameters hitTestParameters) => null;
        }

        #endregion
    }
}
