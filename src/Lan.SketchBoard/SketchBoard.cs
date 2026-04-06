#nullable enable

#region

using System;
using System.Linq;
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

        #region Propeties


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
            SizeChanged += SketchBoard_SizeChanged;
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

        private void SketchBoard_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (SketchBoardDataManager == null) return;

            var scaleFactor = Lan.Shapes.Scaling.ViewportScalingService.CalculateStrokeThicknessFromViewportSize(ActualWidth, ActualHeight);
            var stylers = SketchBoardDataManager.CurrentShapeLayer.Stylers;

            foreach (var shapeStyler in stylers)
            {
                shapeStyler.Value.SetStrokeThickness(2 * scaleFactor);
                shapeStyler.Value.DragHandleSize = 10 * scaleFactor;
            }
        }

        #region others

        private static void OnSketchBoardDataManagerChangedCallBack(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is SketchBoard sketchBoard && e.NewValue is ISketchBoardDataManager dataManager)
            {
                // Take a snapshot BEFORE InitializeVisualCollection clears Shapes.
                var existingShapes = dataManager.Shapes?.ToList();

                dataManager.InitializeVisualCollection(sketchBoard);

                if (existingShapes != null)
                {
                    foreach (var shape in existingShapes)
                    {
                        dataManager.AddShape(shape);
                    }
                }
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
        }


        private bool _mouseDownHitExistingShape;
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

            return (shape?.IsLocked ?? true) ? null : shape;
        }


        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (SketchBoardDataManager?.CurrentGeometryInEdit != null)
                {
                    SketchBoardDataManager.CurrentGeometryInEdit.OnMouseMove(e.GetPosition(this), e.LeftButton);
                }
                else
                {
                    SketchBoardDataManager?.SelectedGeometry?.OnMouseMove(e.GetPosition(this), e.LeftButton);
                }
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
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

        #endregion
    }
}