#nullable enable

#region

using System;
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

        #endregion


        public SketchBoard()
        {
            // Stroke/handle sizing is driven solely by ImageViewer LocalScale →
            // SketchBoardDataManager.OnImageViewerPropertyChanged. Window resize
            // alone must not fight zoom-driven thickness (Phase 2 scale policy).
        }

        /// <summary>Invoked when an unhandled <see cref="E:System.Windows.Input.Keyboard.KeyDown" /> attached event reaches an element in its route that is derived from this class. Implement this method to add class handling for this event.</summary>
        /// <param name="e">The <see cref="T:System.Windows.Input.KeyEventArgs" /> that contains the event data.</param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Delete && SketchBoardDataManager?.SelectedGeometry != null)
            {
                SketchBoardDataManager?.RemoveShape(SketchBoardDataManager.SelectedGeometry);
            }
        }

        #region others

        private static void OnSketchBoardDataManagerChangedCallBack(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is SketchBoard sketchBoard && e.NewValue is ISketchBoardDataManager dataManager)
            {
                dataManager.InitializeVisualCollection(sketchBoard);
            }
        }

        #endregion


        #region overrides

        protected override int VisualChildrenCount
        {
            get => SketchBoardDataManager?.VisualCollection.Count ?? 0;
        }

        protected override Visual GetVisualChild(int index)
        {
            return SketchBoardDataManager?.VisualCollection[index] ?? throw new InvalidOperationException();
        }

        #endregion


        #region events handling

        /// <summary>
        /// right click the mouse means ending the drawing of current shape
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            var hitShape = GetHitTestShape(e.GetPosition(this));
            if (hitShape != null)
            {
                hitShape.OnMouseRightButtonUp(e.GetPosition(this));
            }
            else
            {
                SketchBoardDataManager?.CurrentGeometryInEdit?.OnMouseRightButtonUp(e.GetPosition(this));
            }

            SketchBoardDataManager?.UnselectGeometry();

            base.OnMouseRightButtonUp(e);
        }



        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Focus();
            if (SketchBoardDataManager == null) return;

            var position = e.GetPosition(this);
            var hitShape = GetHitTestShape(position);
            _mouseDownHitExistingShape = hitShape != null;

            SketchBoardDataManager.SelectedGeometry =
                hitShape
                ?? SketchBoardDataManager.CurrentGeometryInEdit
                ?? SketchBoardDataManager.CreateNewGeometry(position);

            if (e.ClickCount == 2)
            {
                SketchBoardDataManager.SelectedGeometry?.OnMouseLeftButtonDoubleClick(position);
            }
            else
            {
                SketchBoardDataManager.SelectedGeometry?.OnMouseLeftButtonDown(position);
            }

            // Keep forwarding the drag after the pointer leaves the board or
            // crosses a child visual. Without capture, WPF stops raising move/up
            // events and the shape remains in a half-dragged state.
            _leftDragMouseCaptured = CaptureMouse();
        }


        private bool _mouseDownHitExistingShape;
        private bool _leftDragMouseCaptured;
        private ShapeVisualBase? _hoveredShape;

        private ShapeVisualBase? GetHitTestShape(Point mousePosition)
        {
            if (SketchBoardDataManager == null) return null;

            if ((SketchBoardDataManager.SelectedGeometry?.IsBeingDraggedOrPanMoving ?? false)
                && !SketchBoardDataManager.SelectedGeometry.IsLocked)
            {
                return SketchBoardDataManager.SelectedGeometry;
            }

            var hitTestResult = VisualTreeHelper.HitTest(this, mousePosition);
            var shape = hitTestResult?.VisualHit as ShapeVisualBase;

            if (shape != null)
            {
                return shape.IsLocked ? null : shape;
            }

            // WPF's visual hit test only sees rendered geometry. Probe the shared
            // logical handle regions as a fallback so DetectionRange remains useful
            // just outside a visible handle.
            for (var i = SketchBoardDataManager.Shapes.Count - 1; i >= 0; i--)
            {
                var candidate = SketchBoardDataManager.Shapes[i];
                if (!candidate.IsLocked && candidate.HasDragHandleAt(mousePosition))
                {
                    return candidate;
                }
            }

            return null;
        }


        protected override void OnMouseMove(MouseEventArgs e)
        {
            var position = e.GetPosition(this);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (SketchBoardDataManager?.CurrentGeometryInEdit != null)
                {
                    SketchBoardDataManager.CurrentGeometryInEdit.OnMouseMove(position, e.LeftButton);
                }
                else
                {
                    SketchBoardDataManager?.SelectedGeometry?.OnMouseMove(position, e.LeftButton);
                }
            }
            else
            {
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
                if (SketchBoardDataManager == null) return;

                var geometry = SketchBoardDataManager.SelectedGeometry;
                if (geometry == null) return;

                var position = e.GetPosition(this);
                if (!geometry.IsGeometryRendered)
                {
                    SketchBoardDataManager.RaiseNewShapeSketched(geometry);
                }

                geometry.OnMouseLeftButtonUp(position);

                if (geometry.IsGeometryRendered)
                {
                    if (!_mouseDownHitExistingShape)
                    {
                        SketchBoardDataManager.UnselectGeometry();
                    }
                    SketchBoardDataManager.UnselectGeometryType();
                }
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

        #endregion
    }
}
