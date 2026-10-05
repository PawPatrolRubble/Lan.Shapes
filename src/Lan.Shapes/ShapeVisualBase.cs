#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes.Enums;
using Lan.Shapes.Handle;

using Lan.Shapes.Styler;

namespace Lan.Shapes
{
    public abstract class ShapeVisualBase : DrawingVisual, INotifyPropertyChanged
    {
        #region constants

        private const double DefaultDragHandleSize = 10;
        private const string DefaultFontFamily = "Verdana";
        private const string DefaultCulture = "en-us";
        private const int MaximumTextLayouts = 32;
        private static readonly Typeface AnnotationTypeface = new Typeface(DefaultFontFamily);

        private static readonly IReadOnlyDictionary<DragLocation, Cursor> DragCursorMap =
            new Dictionary<DragLocation, Cursor>
            {
                { DragLocation.TopLeft, Cursors.SizeNWSE },
                { DragLocation.TopMiddle, Cursors.SizeNS },
                { DragLocation.TopRight, Cursors.SizeNESW },
                { DragLocation.RightMiddle, Cursors.SizeWE },
                { DragLocation.BottomRight, Cursors.SizeNWSE },
                { DragLocation.BottomMiddle, Cursors.SizeNS },
                { DragLocation.BottomLeft, Cursors.SizeNESW },
                { DragLocation.LeftMiddle, Cursors.SizeWE },
                { DragLocation.Rotate, Cursors.Hand },
                { DragLocation.Move, Cursors.SizeAll },
            };

        #endregion

        #region fields

        protected readonly GeometryGroup RenderGeometryGroup = new GeometryGroup();

        private bool _canMoveWithHand;
        private bool _isBeingDraggedOrPanMoving;
        private bool _isGeometryRendered;
        private bool _isLocked;
        private bool _isHovered;

        private ShapeVisualState _state;
        private int _visualUpdateDepth;
        private bool _visualUpdatePending;
        private readonly Dictionary<TextLayoutKey, DrawingGroup> _textDrawings = new();
        private readonly record struct TextLayoutKey(string Text, double FontSize, double PixelsPerDip, Brush Foreground);

        protected readonly List<DragHandle> Handles = new List<DragHandle>();

        protected Point? MouseDownPoint;

        protected Point? OldPointForTranslate;

        /// <summary>Whether a mouse-down gesture is still active. Cleared by CancelInteraction.</summary>
        protected bool HasPointerInteraction => MouseDownPoint.HasValue || OldPointForTranslate.HasValue;

        protected readonly CombinedGeometry PanSensitiveArea = new CombinedGeometry();

        private readonly List<(Point Location, string Content)> _textGeometries = new List<(Point Location, string Content)>();

        #endregion

        #region Properties

        /// <summary>Model-space bounds, excluding interaction handles and annotations.</summary>
        public virtual Rect BoundsRect => RenderGeometry.Bounds;

        /// <summary>
        /// Whether this shape supports rigid translation of its model coordinates.
        /// Custom shapes opt in by overriding this property and TranslateCore.
        /// </summary>
        public virtual bool CanTranslate => false;

        /// <summary>
        /// Moves a completed, unlocked shape by a finite vector in its local model space.
        /// Model geometry, handles and attached text move together; the visual Transform is preserved.
        /// </summary>
        public virtual void Translate(Vector delta)
        {
            if (!double.IsFinite(delta.X) || !double.IsFinite(delta.Y))
                throw new ArgumentOutOfRangeException(nameof(delta), "Translation must be finite.");
            if (!CanTranslate)
                throw new NotSupportedException($"{GeometryType} does not support model translation.");
            if (!IsGeometryRendered || IsLocked)
                throw new InvalidOperationException("Only completed, unlocked shapes can be translated.");
            if (delta.X == 0 && delta.Y == 0) return;

            using (DeferVisualUpdates())
            {
                TranslateCore(delta);
                for (var i = 0; i < _textGeometries.Count; i++)
                {
                    var text = _textGeometries[i];
                    _textGeometries[i] = (text.Location + delta, text.Content);
                }
                CreateHandles();
                UpdateGeometryGroup();
                RequestVisualUpdate();
                OnPropertyChanged(nameof(BoundsRect));
                OnPropertyChanged(nameof(SelectionBounds));
            }
        }

        /// <summary>Updates model coordinates and their geometry without changing pointer state.</summary>
        protected virtual void TranslateCore(Vector delta)
            => throw new NotSupportedException($"{GeometryType} does not support model translation.");

        /// <summary>
        /// Model-space anchors that other shapes can snap to while being drawn, resized, or moved.
        /// These do not depend on selection, locking, or visible drag handles.
        /// Override to opt a custom geometry into snapping.
        /// </summary>
        public virtual IEnumerable<Point> GetSnapPoints() => Array.Empty<Point>();

        /// <summary>
        /// Whether the active drag handle accepts a snapped resize position.
        /// Override for handles that translate, rotate, or adjust other properties.
        /// </summary>
        public virtual bool CanSnapDuringResize => IsGeometryRendered && !IsLocked
            && SelectedDragHandle != null
            && SelectedDragHandle.CursorLocation is not (DragLocation.Move or DragLocation.Rotate);

        /// <summary>
        /// Model-space anchor to snap while translating, or null when the active gesture
        /// does not support move snapping. The pointer's offset from this anchor is preserved.
        /// </summary>
        public virtual Point? MoveSnapPoint => null;

        protected double DragHandleSize { get; set; }

        /// <summary>
        /// Current canvas scale used by adornments whose distance from the model
        /// geometry must remain constant in screen space.
        /// </summary>
        protected double ViewportScale { get; private set; } = 1.0;

        public Guid Id { get; }

        /// <summary>
        /// Gets the geometry type name (e.g. "Circle", "Rectangle", "Line").
        /// </summary>
        public virtual string GeometryType => GetType().Name;

        public bool IsBeingDraggedOrPanMoving
        {
            get => _isBeingDraggedOrPanMoving;
            protected set => SetField(ref _isBeingDraggedOrPanMoving, value);
        }

        public bool IsGeometryRendered
        {
            get => _isGeometryRendered;
            protected set
            {
                if (SetField(ref _isGeometryRendered, value))
                {
                    RequestVisualUpdate();
                }
            }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                if (!SetField(ref _isLocked, value)) return;
                if (value) CancelInteraction();
                UpdateAppearanceState();
            }
        }

        public bool IsHovered => _isHovered;

        public virtual Geometry RenderGeometry
        {
            get => RenderGeometryGroup;
        }

        protected DragHandle? SelectedDragHandle { get; set; }

        private ShapeLayer _shapeLayer;

        private bool _isSelected;
        private bool _showSelectionHandles = true;
        public bool IsSelected => _isSelected;
        public Rect SelectionBounds
        {
            get
            {
                var geometry = GetSelectionGeometry();
                var clip = GetSelectionClip();
                return clip == null ? ToSelectionCoordinates(geometry).Bounds
                    : GetClippedSelectionBounds(GetClippedSelectionGeometry(geometry, clip), clip);
            }
        }
        protected bool ShowSelectionHandles => _showSelectionHandles;

        /// <summary>Selection feedback driven by the owning repository.</summary>
        public void SetSelectionAppearance(bool selected, bool showHandles)
        {
            using (DeferVisualUpdates())
            {
                var selectionChanged = SetField(ref _isSelected, selected, nameof(IsSelected));
                var handlesChanged = _showSelectionHandles != showHandles;
                _showSelectionHandles = showHandles;
                if (!selectionChanged && !handlesChanged) return;
                if (!selected) CancelInteraction();
                UpdateAppearanceState();
                RequestVisualUpdate();
            }
        }

        /// <summary>Updates pointer feedback without changing selection or locking.</summary>
        public void SetHoverAppearance(bool hovered)
        {
            if (SetField(ref _isHovered, hovered, nameof(IsHovered))) UpdateAppearanceState();
        }

        /// <summary>Tests model geometry only, excluding handles and labels. Custom shapes may override.</summary>
        public virtual bool MatchesSelectionRectangle(Rect rectangle, bool crossing)
        {
            if (!IsGeometryRendered) return false;
            var geometry = GetSelectionGeometry();
            if (geometry.IsEmpty()) return false;
            var region = new RectangleGeometry(rectangle);
            var clip = GetSelectionClip();
            if (clip != null)
            {
                var visibleGeometry = GetClippedSelectionGeometry(geometry, clip);
                if (visibleGeometry.IsEmpty()) return false;
                return crossing
                    ? visibleGeometry.FillContainsWithDetail(region) is not (IntersectionDetail.Empty or IntersectionDetail.NotCalculated)
                    : rectangle.Contains(GetClippedSelectionBounds(visibleGeometry, clip));
            }
            if (!crossing) return rectangle.Contains(ToSelectionCoordinates(geometry).Bounds);
            // Selection highlighting must not change which part of a shape can be selected.
            var styler = ShapeLayer.GetStyler(ShapeVisualState.Normal);
            return (HasVisibleBrush(styler.FillColor)
                    && ToSelectionCoordinates(geometry).FillContainsWithDetail(region) is not (IntersectionDetail.Empty or IntersectionDetail.NotCalculated))
                || (GetSelectionPen() is Pen pen && HasVisibleBrush(pen.Brush)
                    && ToSelectionCoordinates(geometry.GetWidenedPathGeometry(pen)).FillContainsWithDetail(region)
                        is not (IntersectionDetail.Empty or IntersectionDetail.NotCalculated));
        }

        /// <summary>The model geometry used for selection; custom drawing must expose the same body here.</summary>
        protected virtual Geometry GetSelectionGeometry() => RenderGeometry;

        /// <summary>Stable model stroke used for selection, independent of hover/selection highlighting.</summary>
        protected virtual Pen? GetSelectionPen() => ShapeLayer.GetStyler(ShapeVisualState.Normal).SketchPen;

        private Geometry? GetSelectionClip()
        {
            var drawingClip = DrawingClip;
            var visualClip = Clip;
            if (drawingClip == null) return visualClip == null ? null : ToSelectionCoordinates(visualClip);
            if (visualClip == null) return ToSelectionCoordinates(drawingClip);
            return Geometry.Combine(ToSelectionCoordinates(drawingClip), ToSelectionCoordinates(visualClip),
                GeometryCombineMode.Intersect, null);
        }

        private Geometry GetClippedSelectionGeometry(Geometry geometry, Geometry clip)
        {
            Geometry body = Geometry.Empty;
            if (HasVisibleBrush(ShapeLayer.GetStyler(ShapeVisualState.Normal).FillColor))
                body = ToSelectionCoordinates(geometry);
            if (GetSelectionPen() is Pen pen && HasVisibleBrush(pen.Brush))
                body = Geometry.Combine(body, ToSelectionCoordinates(geometry.GetWidenedPathGeometry(pen)),
                    GeometryCombineMode.Union, null);
            // Open paths must be widened before clipping; their fill alone can be empty.
            return Geometry.Combine(body, clip, GeometryCombineMode.Intersect, null);
        }

        private static Rect GetClippedSelectionBounds(Geometry visibleGeometry, Geometry clip)
        {
            var bounds = visibleGeometry.Bounds;
            // WPF boolean geometry can round vertices slightly beyond a clip edge.
            bounds.Intersect(clip.Bounds);
            return bounds;
        }

        private Geometry ToSelectionCoordinates(Geometry geometry)
        {
            if (Transform == null || Transform.Value.IsIdentity) return geometry;
            // Primitive Geometry.Bounds may transform an already axis-aligned bounding box.
            // A path computes bounds from the transformed model, including rotated line endpoints.
            var transformed = PathGeometry.CreateFromGeometry(geometry).CloneCurrentValue();
            var transforms = new TransformGroup();
            if (transformed.Transform != null) transforms.Children.Add(transformed.Transform);
            transforms.Children.Add(Transform);
            transformed.Transform = transforms;
            return transformed;
        }

        private static bool HasVisibleBrush(Brush? brush)
            => brush != null && brush.Opacity > 0
                && (brush is not SolidColorBrush solid || solid.Color.A > 0);

        public ShapeLayer ShapeLayer
        {
            get => _shapeLayer;
            set
            {
                if (ReferenceEquals(_shapeLayer, value))
                {
                    return;
                }

                _shapeLayer = value ?? throw new ArgumentNullException(nameof(value));
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShapeStyler));

                RefreshScaleDependentVisuals(ViewportScale);
            }
        }

        public IShapeStyler? ShapeStyler
        {
            get => ShapeLayer?.GetStyler(State);
        }

        /// <summary>
        /// Effective style state. Prefer SetSelectionAppearance, SetHoverAppearance and IsLocked.
        /// Legacy Selected assignments explicitly select and unlock; Normal clears selection and hover.
        /// Hover and Normal assignments never unlock a shape.
        /// </summary>
        public ShapeVisualState State
        {
            get => _state;
            set
            {
                if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
                using (DeferVisualUpdates())
                {
                    switch (value)
                    {
                        case ShapeVisualState.Locked:
                            IsLocked = true;
                            break;
                        case ShapeVisualState.Selected:
                            IsLocked = false;
                            SetHoverAppearance(false);
                            SetSelectionAppearance(true, _showSelectionHandles);
                            break;
                        case ShapeVisualState.MouseOver:
                            SetHoverAppearance(true);
                            break;
                        case ShapeVisualState.Normal:
                            SetHoverAppearance(false);
                            SetSelectionAppearance(false, _showSelectionHandles);
                            break;
                    }
                }
            }
        }

        private void UpdateAppearanceState()
        {
            var state = IsLocked ? ShapeVisualState.Locked
                : IsSelected ? ShapeVisualState.Selected
                : IsHovered ? ShapeVisualState.MouseOver
                : ShapeVisualState.Normal;
            if (_state == state) return;
            _state = state;
            DragHandleSize = ShapeStyler?.DragHandleSize ?? DefaultDragHandleSize;
            OnDragHandleSizeChanges(DragHandleSize);
            UpdateVisualOnStateChanged();
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(ShapeStyler));
        }

        private string? _tag;

        public string? Tag
        {
            get => _tag;
            set
            {
                if (SetField(ref _tag, value))
                {
                    RequestVisualUpdate();
                }
            }
        }

        protected IReadOnlyList<(Point Location, string Content)> TextGeometries => _textGeometries;

        #endregion

        #region Constructors

        protected ShapeVisualBase(ShapeLayer layer)
        {
            _shapeLayer = layer ?? throw new ArgumentNullException(nameof(layer));
            Id = Guid.NewGuid();
            _state = ShapeVisualState.Normal;
            DragHandleSize = ShapeStyler?.DragHandleSize ?? DefaultDragHandleSize;
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raised when shape creation is cancelled (e.g., user cancels an import dialog).
        /// The shape should be removed from the canvas.
        /// </summary>
        public event EventHandler? ShapeCreationCancelled;

        /// <summary>
        /// Raises the ShapeCreationCancelled event to signal that this shape should be removed.
        /// </summary>
        protected void OnShapeCreationCancelled()
        {
            ShapeCreationCancelled?.Invoke(this, EventArgs.Empty);
        }

        #region methods

        protected virtual void UpdateVisualOnStateChanged()
        {
            switch (State)
            {
                case ShapeVisualState.Selected:
                case ShapeVisualState.MouseOver:
                case ShapeVisualState.Normal:
                    RequestVisualUpdate();
                    break;
                case ShapeVisualState.Locked:
                    UpdateVisualOnLocked();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected virtual void UpdateVisualOnLocked()
        {
            RequestVisualUpdate();
        }

        public virtual void Lock()
        {
            IsLocked = true;
        }

        public virtual void UnLock()
        {
            IsLocked = false;
        }

        protected virtual void OnDragHandleSizeChanges(double dragHandleSize)
        {
            foreach (var handle in Handles)
            {
                handle.HandleSize = new Size(dragHandleSize, dragHandleSize);
            }
        }

        /// <summary>
        /// Gives derived shapes a chance to reposition scale-dependent adornments.
        /// </summary>
        protected virtual void OnViewportScaleChanged(double viewportScale)
        {
        }

        /// <summary>
        /// Registers a handle as an interaction adornment without adding it to model geometry.
        /// The typed return value lets shape implementations retain specialized handle references.
        /// </summary>
        protected T RegisterHandle<T>(T handle) where T : DragHandle
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (!Handles.Contains(handle))
            {
                Handles.Add(handle);
            }

            return handle;
        }

        /// <summary>
        /// Drag handles are visible and interactive while a shape is being created or selected.
        /// </summary>
        protected virtual bool AreDragHandlesActive =>
            !IsLocked && (!IsGeometryRendered || (IsSelected && ShowSelectionHandles));

        protected virtual Brush? GetDragHandleFill() => ShapeStyler?.FillColor;

        protected virtual Pen? GetDragHandlePen() => ShapeStyler?.SketchPen;

        /// <summary>
        /// Draws every registered handle exactly once, independently of model geometry.
        /// </summary>
        protected virtual void DrawDragHandles(DrawingContext renderContext)
        {
            if (!AreDragHandlesActive)
            {
                return;
            }

            var fill = GetDragHandleFill();
            var pen = GetDragHandlePen();
            foreach (var handle in Handles)
            {
                if (handle.HandleGeometry != null)
                {
                    renderContext.DrawGeometry(fill, pen, handle.HandleGeometry);
                }
            }
        }

        /// <summary>
        /// Re-reads stroke/handle sizes from the current layer styler and redraws.
        /// Called by the board when zoom scale changes after stylers are updated.
        /// </summary>
        public void RefreshScaleDependentVisuals()
        {
            RefreshScaleDependentVisuals(ViewportScale);
        }

        public void RefreshScaleDependentVisuals(double viewportScale)
        {
            using (DeferVisualUpdates())
            {
                ViewportScale = double.IsFinite(viewportScale) && viewportScale > 0 ? viewportScale : 1.0;
                DragHandleSize = ShapeStyler?.DragHandleSize ?? DefaultDragHandleSize;
                OnDragHandleSizeChanges(DragHandleSize);
                OnViewportScaleChanged(ViewportScale);
                RequestVisualUpdate();
            }
        }


        protected abstract void CreateHandles();

        /// <inheritdoc cref="RectDragHandle.CreateRectDragHandleFromStyler(IShapeStyler, Point, int)"/>
        [Obsolete("Use RectDragHandle.CreateRectDragHandleFromStyler(ShapeStyler, location, id) directly.")]
        protected DragHandle CreateRectDragHandle(Point location, int id)
        {
            if (ShapeStyler == null)
            {
                throw new InvalidOperationException("ShapeStyler must be set before creating drag handles.");
            }

            return RectDragHandle.CreateRectDragHandleFromStyler(ShapeStyler, location, id);
        }

        protected virtual void DrawGeometryInMouseMove(Point oldPoint, Point newPoint)
        {
        }

        public virtual DragHandle? FindDragHandleMouseOver(Point p)
        {
            if (!AreDragHandlesActive)
            {
                return null;
            }

            foreach (var handle in Handles)
            {
                if (handle.FillContains(p))
                {
                    return handle;
                }
            }

            return null;
        }

        public virtual bool HasDragHandleAt(Point p)
        {
            return FindDragHandleMouseOver(p) != null;
        }

        public virtual void FindSelectedHandle(Point p)
        {
            SelectedDragHandle = FindDragHandleMouseOver(p);
        }

        protected double GetDistanceBetweenTwoPoint(Point p1, Point p2)
        {
            return (p2 - p1).Length;
        }

        protected abstract void HandleResizing(Point point);

        protected abstract void HandleTranslate(Point newPoint);

        public virtual void OnDeselected()
        {
            CancelInteraction();
        }

        /// <summary>Ends pointer tracking without completing or reverting the current geometry.</summary>
        public virtual void CancelInteraction()
        {
            SelectedDragHandle = null;
            IsBeingDraggedOrPanMoving = false;
            _canMoveWithHand = false;
            MouseDownPoint = null;
            OldPointForTranslate = null;
        }

        /// <summary>Staged sketches can override the condition for committing their model geometry.</summary>
        protected virtual bool CanCompleteCreation => !RenderGeometry.IsEmpty();

        protected void CompleteCreation()
        {
            if (!IsGeometryRendered && CanCompleteCreation) IsGeometryRendered = true;
        }

        public virtual void OnMouseLeftButtonDown(Point mousePoint)
        {
            if (IsLocked) return;
            FindSelectedHandle(mousePoint);
            _canMoveWithHand = !IsLocked && PanSensitiveArea.FillContains(mousePoint);

            OldPointForTranslate = mousePoint;
            MouseDownPoint = mousePoint;
        }

        public virtual void OnMouseLeftButtonUp(Point newPoint)
        {
            using (DeferVisualUpdates())
            {
                if (!IsLocked) CompleteCreation();
                CancelInteraction();
            }
        }

        public virtual void OnMouseMove(Point point, MouseButtonState buttonState)
        {
            if (IsLocked || (buttonState == MouseButtonState.Pressed && !HasPointerInteraction)) return;
            if (buttonState == MouseButtonState.Released)
            {
                HandleMouseMoveReleased(point);
            }
            else
            {
                HandleMouseMovePressed(point);
            }

            if (buttonState == MouseButtonState.Pressed) OldPointForTranslate = point;
        }

        private void HandleMouseMoveReleased(Point point)
        {
            CancelInteraction();
            SetHoverAppearance(true);

            var handle = FindDragHandleMouseOver(point);
            if (handle != null)
            {
                TryUpdateMouseCursor(handle);
            }

            _canMoveWithHand = PanSensitiveArea.FillContains(point);
            if (_canMoveWithHand)
            {
                Mouse.SetCursor(Cursors.Hand);
            }
        }

        private void HandleMouseMovePressed(Point point)
        {
            if (IsLocked)
            {
                return;
            }

            if (IsGeometryRendered)
            {
                if (SelectedDragHandle != null)
                {
                    IsBeingDraggedOrPanMoving = true;
                    TryUpdateMouseCursor(SelectedDragHandle);
                    HandleResizing(point);
                }
                else if (_canMoveWithHand)
                {
                    IsBeingDraggedOrPanMoving = true;
                    HandleTranslate(point);
                }
                else
                {
                    return;
                }
            }
            else
            {
                if (MouseDownPoint != null)
                {
                    DrawGeometryInMouseMove(MouseDownPoint.Value, point);
                }
            }

            CreateHandles();
            UpdateGeometryGroup();
            RequestVisualUpdate();
        }

        public virtual void OnMouseRightButtonUp(Point mousePosition)
        {
            if (IsLocked) return;
            using (DeferVisualUpdates())
            {
                CompleteCreation();
                CancelInteraction();
                State = ShapeVisualState.Normal;
            }
        }

        public virtual void OnMouseLeftButtonDoubleClick(Point mouseDoubleClickPoint)
        {
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public virtual void OnSelected()
        {
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void SetMouseCursorToHand()
        {
            Mouse.SetCursor(Cursors.Hand);
        }

        protected virtual void UpdateGeometryGroup()
        {
        }

        public void UpdateMouseCursor(DragLocation dragLocation)
        {
            if (DragCursorMap.TryGetValue(dragLocation, out var cursor))
            {
                Mouse.SetCursor(cursor);
            }
            // Unassigned or unknown roles are handled by the point-based hover path.
        }

        public void UpdateMouseCursorForPoint(Point point)
        {
            if (IsLocked)
            {
                Mouse.SetCursor(Cursors.Arrow);
                return;
            }
            if (!AreDragHandlesActive)
            {
                Mouse.SetCursor(PanSensitiveArea.FillContains(point) ? Cursors.Hand : Cursors.Arrow);
                return;
            }

            var handle = FindDragHandleMouseOver(point);
            if (handle != null)
            {
                if (!TryUpdateMouseCursor(handle))
                {
                    Mouse.SetCursor(Cursors.Hand);
                }

                return;
            }

            if (HasDragHandleAt(point))
            {
                Mouse.SetCursor(Cursors.Hand);
                return;
            }

            Mouse.SetCursor(PanSensitiveArea.FillContains(point) ? Cursors.Hand : Cursors.Arrow);
        }

        protected virtual bool TryUpdateMouseCursor(DragHandle handle)
        {
            if (handle.CursorLocation.HasValue &&
                DragCursorMap.TryGetValue(handle.CursorLocation.Value, out var cursor))
            {
                Mouse.SetCursor(cursor);
                return true;
            }

            return false;
        }

        /// <summary>Coalesces visual requests while a single geometry operation changes several properties.</summary>
        public IDisposable DeferVisualUpdates()
        {
            _visualUpdateDepth++;
            return new VisualUpdateScope(this);
        }

        /// <summary>Use from geometry setters so an active update scope renders only the final geometry.</summary>
        protected void RequestVisualUpdate()
        {
            if (_visualUpdateDepth > 0) _visualUpdatePending = true;
            else UpdateVisual();
        }

        private void EndVisualUpdate()
        {
            if (--_visualUpdateDepth != 0 || !_visualUpdatePending) return;
            _visualUpdatePending = false;
            UpdateVisual();
        }

        private sealed class VisualUpdateScope : IDisposable
        {
            private ShapeVisualBase? _shape;
            public VisualUpdateScope(ShapeVisualBase shape) => _shape = shape;
            public void Dispose()
            {
                var shape = _shape;
                _shape = null;
                shape?.EndVisualUpdate();
            }
        }

        /// <summary>
        /// Replaces retained visual content. Override DrawShape to customize the body while retaining
        /// the common handle and annotation stages. This entry point remains virtual for compatibility.
        /// </summary>
        public virtual void UpdateVisual()
        {
            if (ShapeStyler == null)
            {
                return;
            }

            using var renderContext = RenderOpen();
            var clip = DrawingClip;
            if (clip != null) renderContext.PushClip(clip);
            DrawShape(renderContext);
            DrawDragHandles(renderContext);
            DrawText(renderContext);
            if (clip != null) renderContext.Pop();
        }

        /// <summary>Local-space clip shared by the drawing pipeline and model selection.</summary>
        protected virtual Geometry? DrawingClip => null;

        /// <summary>Draws the model body and shape-specific labels. The base draws handles and added text.</summary>
        protected virtual void DrawShape(DrawingContext renderContext)
        {
            var styler = ShapeStyler;
            if (styler != null) renderContext.DrawGeometry(styler.FillColor, styler.SketchPen, RenderGeometry);
        }

        protected static double EnsureNumberWithinRange(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(value, max));
        }

        protected static Point ForcePointInRange(Point point, double minX, double maxX, double minY, double maxY)
        {
            var x = EnsureNumberWithinRange(point.X, minX, maxX);
            var y = EnsureNumberWithinRange(point.Y, minY, maxY);
            return new Point(x, y);
        }

        #region text rendering helpers

        /// <summary>Uses a muted foreground for locked geometry without changing shared styles.</summary>
        protected Brush GetTextForeground(Brush foreground) => IsLocked ? Brushes.Gray : foreground;

        /// <summary>Uses the normal handle size so selection and locking do not resize labels.</summary>
        protected double AnnotationFontSize
        {
            get
            {
                var handleSize = ShapeLayer.GetStyler(ShapeVisualState.Normal).DragHandleSize;
                if (!double.IsFinite(handleSize) || handleSize <= 0)
                {
                    handleSize = DefaultDragHandleSize / ViewportScale;
                }

                return handleSize * ShapeLayer.AnnotationFontToHandleRatio;
            }
        }

        protected FormattedText CreateFormattedText(string text, Brush foreground)
        {
            return CreateFormattedText(
                text,
                foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        protected FormattedText CreateFormattedText(string text, Brush foreground, double pixelsPerDip)
        {
            if (pixelsPerDip <= 0 ||
                double.IsNaN(pixelsPerDip) ||
                double.IsInfinity(pixelsPerDip))
            {
                throw new ArgumentOutOfRangeException(nameof(pixelsPerDip));
            }

            return new FormattedText(
                text,
                CultureInfo.GetCultureInfo(DefaultCulture),
                FlowDirection.LeftToRight,
                AnnotationTypeface,
                AnnotationFontSize,
                GetTextForeground(foreground),
                pixelsPerDip);
        }

        /// <summary>Reuses glyph drawings when only a label's position changes.</summary>
        protected void DrawCachedText(DrawingContext context, string text, Brush foreground, Point location)
        {
            var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var brush = GetTextForeground(foreground);
            var key = new TextLayoutKey(text, AnnotationFontSize, pixelsPerDip, brush);
            if (!_textDrawings.TryGetValue(key, out var drawing))
            {
                drawing = new DrawingGroup();
                using (var textContext = drawing.Open())
                    textContext.DrawText(CreateFormattedText(text, foreground, pixelsPerDip), new Point());
                // Never freeze a mutable brush owned by a layer; its changes must still propagate.
                if (brush.IsFrozen && drawing.CanFreeze) drawing.Freeze();
                if (_textDrawings.Count >= MaximumTextLayouts) _textDrawings.Clear();
                _textDrawings.Add(key, drawing);
            }
            context.PushTransform(new TranslateTransform(location.X, location.Y));
            context.DrawDrawing(drawing);
            context.Pop();
        }

        protected void AddTagText(DrawingContext renderContext, Point location)
        {
            if (!string.IsNullOrEmpty(Tag))
            {
                var brush = ShapeStyler?.TagColor ?? Brushes.Red;
                DrawCachedText(renderContext, Tag, brush, location);
            }
        }

        protected void AddTagText(DrawingContext renderContext, Point location, double angle)
        {
            if (!string.IsNullOrEmpty(Tag))
            {
                var rt = new RotateTransform(angle, location.X, location.Y);
                renderContext.PushTransform(rt);

                var brush = ShapeStyler?.TagColor ?? Brushes.Red;
                DrawCachedText(renderContext, Tag, brush, location);
                renderContext.Pop();
            }
        }

        public virtual void AddText(string content, Point? location = null)
        {
            if (string.IsNullOrEmpty(content) || location == null)
            {
                return;
            }

            _textGeometries.Add((location.Value, content));
            RequestVisualUpdate();
        }

        /// <summary>
        /// Removes all text entries previously added with <see cref="AddText"/>.
        /// Call this before re-adding updated labels to prevent stale text accumulating.
        /// </summary>
        public virtual void ClearText()
        {
            _textGeometries.Clear();
            RequestVisualUpdate();
        }

        protected void DrawText(DrawingContext renderContext)
        {
            foreach (var textGeometry in _textGeometries)
            {
                DrawCachedText(renderContext, textGeometry.Content, Brushes.Red, textGeometry.Location);
            }
        }

        #endregion // text rendering helpers

        #endregion // methods
    }
}
