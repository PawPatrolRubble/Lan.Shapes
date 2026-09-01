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
            };

        #endregion

        #region fields

        protected readonly GeometryGroup RenderGeometryGroup = new GeometryGroup();

        private bool _canMoveWithHand;
        private bool _isBeingDraggedOrPanMoving;
        private bool _isGeometryRendered;
        private bool _isLocked;

        private ShapeVisualState _state;

        protected readonly List<DragHandle> Handles = new List<DragHandle>();

        protected Point? MouseDownPoint;

        protected Point? OldPointForTranslate;

        protected readonly CombinedGeometry PanSensitiveArea = new CombinedGeometry();

        private readonly List<(Point Location, string Content)> _textGeometries = new List<(Point Location, string Content)>();

        #endregion

        #region Properties

        public abstract Rect BoundsRect { get; }

        protected double DragHandleSize { get; set; }

        /// <summary>
        /// Current canvas scale used by adornments whose distance from the model
        /// geometry must remain constant in screen space.
        /// </summary>
        protected double ViewportScale { get; private set; } = 1.0;

        public Guid Id { get; }

        public bool IsBeingDraggedOrPanMoving
        {
            get => _isBeingDraggedOrPanMoving;
            protected set => SetField(ref _isBeingDraggedOrPanMoving, value);
        }

        public bool IsGeometryRendered
        {
            get => _isGeometryRendered;
            protected set => SetField(ref _isGeometryRendered, value);
        }

        public bool IsLocked
        {
            get => _isLocked;
            protected set
            {
                State = value ? ShapeVisualState.Locked : ShapeVisualState.Normal;
            }
        }

        public virtual Geometry RenderGeometry
        {
            get => RenderGeometryGroup;
        }

        protected DragHandle? SelectedDragHandle { get; set; }

        private ShapeLayer _shapeLayer;

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
                if (!IsGeometryRendered)
                {
                    UpdateVisual();
                }
            }
        }

        public IShapeStyler? ShapeStyler
        {
            get => ShapeLayer?.GetStyler(State);
        }

        public ShapeVisualState State
        {
            get => _state;
            set
            {
                if (_state == value)
                {
                    return;
                }

                var wasLocked = _isLocked;
                _state = value;
                _isLocked = value == ShapeVisualState.Locked;

                DragHandleSize = ShapeStyler?.DragHandleSize ?? DefaultDragHandleSize;
                OnDragHandleSizeChanges(DragHandleSize);
                UpdateVisualOnStateChanged();

                OnPropertyChanged();
                OnPropertyChanged(nameof(ShapeStyler));
                if (wasLocked != _isLocked)
                {
                    OnPropertyChanged(nameof(IsLocked));
                }
            }
        }

        private string? _tag;

        public string? Tag
        {
            get => _tag;
            set
            {
                if (SetField(ref _tag, value))
                {
                    UpdateVisual();
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
                    UpdateVisual();
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
            UpdateVisual();
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
            !IsLocked && (!IsGeometryRendered || State == ShapeVisualState.Selected);

        protected virtual Brush? GetDragHandleFill() => ShapeStyler?.FillColor;

        protected virtual Pen? GetDragHandlePen() => ShapeStyler?.SketchPen;

        /// <summary>
        /// Draws every registered handle exactly once, independently of model geometry.
        /// </summary>
        protected void DrawDragHandles(DrawingContext renderContext)
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
            ViewportScale = viewportScale > 0 &&
                            !double.IsNaN(viewportScale) &&
                            !double.IsInfinity(viewportScale)
                ? viewportScale
                : 1.0;

            DragHandleSize = ShapeStyler?.DragHandleSize ?? DefaultDragHandleSize;
            OnDragHandleSizeChanges(DragHandleSize);
            OnViewportScaleChanged(ViewportScale);

            if (IsGeometryRendered)
            {
                UpdateVisual();
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
        }

        public virtual void OnMouseLeftButtonDown(Point mousePoint)
        {
            FindSelectedHandle(mousePoint);
            _canMoveWithHand = !IsLocked && PanSensitiveArea.FillContains(mousePoint);

            OldPointForTranslate = mousePoint;
            MouseDownPoint = mousePoint;
        }

        public virtual void OnMouseLeftButtonUp(Point newPoint)
        {
            if (!IsGeometryRendered && RenderGeometryGroup.Children.Count > 0)
            {
                IsGeometryRendered = true;
            }

            SelectedDragHandle = null;
            IsBeingDraggedOrPanMoving = false;
            _canMoveWithHand = false;
        }

        public virtual void OnMouseMove(Point point, MouseButtonState buttonState)
        {
            if (buttonState == MouseButtonState.Released)
            {
                HandleMouseMoveReleased(point);
            }
            else
            {
                HandleMouseMovePressed(point);
            }

            OldPointForTranslate = point;
        }

        private void HandleMouseMoveReleased(Point point)
        {
            if (State != ShapeVisualState.MouseOver)
            {
                State = ShapeVisualState.MouseOver;
            }

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
            UpdateVisual();
        }

        public virtual void OnMouseRightButtonUp(Point mousePosition)
        {
            IsGeometryRendered = true;
            State = ShapeVisualState.Normal;
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

        public virtual void UpdateVisual()
        {
            if (ShapeStyler == null)
            {
                return;
            }

            var renderContext = RenderOpen();
            renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, RenderGeometry);
            DrawDragHandles(renderContext);
            DrawText(renderContext);
            renderContext.Close();
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
                new Typeface(DefaultFontFamily),
                ShapeLayer.TagFontSize,
                foreground,
                pixelsPerDip);
        }

        protected void AddTagText(DrawingContext renderContext, Point location)
        {
            if (!string.IsNullOrEmpty(Tag))
            {
                var brush = ShapeStyler?.TagColor ?? Brushes.Red;
                var formattedText = CreateFormattedText(Tag, brush);
                renderContext.DrawText(formattedText, location);
            }
        }

        protected void AddTagText(DrawingContext renderContext, Point location, double angle)
        {
            if (!string.IsNullOrEmpty(Tag))
            {
                var rt = new RotateTransform(angle, location.X, location.Y);
                renderContext.PushTransform(rt);

                var brush = ShapeStyler?.TagColor ?? Brushes.Red;
                var formattedText = CreateFormattedText(Tag, brush);
                renderContext.DrawText(formattedText, location);
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
            UpdateVisual();
        }

        /// <summary>
        /// Removes all text entries previously added with <see cref="AddText"/>.
        /// Call this before re-adding updated labels to prevent stale text accumulating.
        /// </summary>
        public virtual void ClearText()
        {
            _textGeometries.Clear();
            UpdateVisual();
        }

        protected void DrawText(DrawingContext renderContext)
        {
            foreach (var textGeometry in _textGeometries)
            {
                var formattedText = CreateFormattedText(textGeometry.Content, Brushes.Red);
                renderContext.DrawText(formattedText, textGeometry.Location);
            }
        }

        #endregion // text rendering helpers

        #endregion // methods
    }
}
