#region

using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Lan.Shapes;
using Lan.Shapes.Enums;

#endregion

namespace Lan.ImageViewer
{
    /// <summary>
    /// Interaction logic for ImageViewerControl.xaml
    /// </summary>
    public partial class ImageViewerControl : UserControl
    {
        private ShapeVisualBase _selectionAnchor;
        private bool _handlingTreeSelection;
        private int _propertyPanelAnimationVersion;
        public static readonly DependencyProperty LineDirectionModeProperty = DependencyProperty.Register(
            nameof(LineDirectionMode), typeof(LineDirectionMode), typeof(ImageViewerControl),
            new FrameworkPropertyMetadata(LineDirectionMode.Free, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault),
            value => value is LineDirectionMode mode && Enum.IsDefined(mode));

        public LineDirectionMode LineDirectionMode
        {
            get => (LineDirectionMode)GetValue(LineDirectionModeProperty);
            set => SetValue(LineDirectionModeProperty, value);
        }

        public static readonly DependencyProperty ShowCrossLineProperty = DependencyProperty.Register(
            nameof(ShowCrossLine), typeof(bool), typeof(ImageViewerControl),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnShowCrossLineChanged));

        public bool ShowCrossLine
        {
            get => (bool)GetValue(ShowCrossLineProperty);
            set => SetValue(ShowCrossLineProperty, value);
        }

        private static void OnShowCrossLineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageViewerControl control)
            {
                var isVisible = (bool)e.NewValue;
                if (control.ImageViewer != null && control.ImageViewer.ShowCrossLine != isVisible)
                {
                    control.ImageViewer.ShowCrossLine = isVisible;
                }
                if (control.DataContext is IImageViewerViewModel vm && vm.ShowCrossLine != isVisible)
                {
                    vm.ShowCrossLine = isVisible;
                }
            }
        }

        public static readonly DependencyProperty ShowGeometriesProperty = DependencyProperty.Register(
            nameof(ShowGeometries), typeof(bool), typeof(ImageViewerControl),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnShowGeometriesChanged));

        public bool ShowGeometries
        {
            get => (bool)GetValue(ShowGeometriesProperty);
            set => SetValue(ShowGeometriesProperty, value);
        }

        private static void OnShowGeometriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageViewerControl control)
            {
                var isVisible = (bool)e.NewValue;
                if (control.ImageViewer != null && control.ImageViewer.ShowGeometries != isVisible)
                {
                    control.ImageViewer.ShowGeometries = isVisible;
                    control.ImageViewer.UpdateSketchBoardVisibility();
                }
                if (control.DataContext is IImageViewerViewModel vm && vm.ShowGeometries != isVisible)
                {
                    vm.ShowGeometries = isVisible;
                }
            }
        }

        #region Constructors

        public ImageViewerControl()
        {
            InitializeComponent();
            UpdatePropertyPanelVisibility(animate: false);
            Loaded += (_, _) => UpdatePropertyPanelVisibility(animate: false);
            Unloaded += (_, _) => UpdatePropertyPanelVisibility(animate: false);

            this.DataContextChanged += (s, e) =>
            {
                if (e.OldValue is INotifyPropertyChanged oldNpc)
                {
                    PropertyChangedEventManager.RemoveHandler(oldNpc, OnViewModelPropertyChanged, string.Empty);
                }
                _selectionAnchor = null;
                if (e.NewValue is IImageViewerViewModel vm)
                {
                    var isShowCrossLineSet = ReadLocalValue(ShowCrossLineProperty) != DependencyProperty.UnsetValue;
                    if (isShowCrossLineSet)
                    {
                        vm.ShowCrossLine = this.ShowCrossLine;
                    }
                    else
                    {
                        this.ShowCrossLine = vm.ShowCrossLine;
                    }
                    if (this.ImageViewer != null)
                    {
                        this.ImageViewer.ShowCrossLine = this.ShowCrossLine;
                    }

                    this.ShowGeometries = vm.ShowGeometries;
                    if (this.ImageViewer != null)
                    {
                        this.ImageViewer.ShowGeometries = vm.ShowGeometries;
                        this.ImageViewer.UpdateSketchBoardVisibility();
                    }
                    if (e.NewValue is INotifyPropertyChanged newNpc)
                    {
                        PropertyChangedEventManager.AddHandler(newNpc, OnViewModelPropertyChanged, string.Empty);
                    }
                    ScheduleSelectedLayersExpansion(vm);
                }
            };
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is IImageViewerViewModel vm)
            {
                if (e.PropertyName is null or nameof(IImageViewerViewModel.SelectedShapeCount))
                    ScheduleSelectedLayersExpansion(vm);

                if (e.PropertyName == nameof(IImageViewerViewModel.ShowGeometries) &&
                    this.ShowGeometries != vm.ShowGeometries)
                {
                    this.ShowGeometries = vm.ShowGeometries;
                    if (this.ImageViewer != null)
                    {
                        this.ImageViewer.ShowGeometries = vm.ShowGeometries;
                        this.ImageViewer.UpdateSketchBoardVisibility();
                    }
                }
                else if (e.PropertyName == nameof(IImageViewerViewModel.ShowCrossLine) &&
                    this.ShowCrossLine != vm.ShowCrossLine)
                {
                    this.ShowCrossLine = vm.ShowCrossLine;
                    if (this.ImageViewer != null)
                    {
                        this.ImageViewer.ShowCrossLine = vm.ShowCrossLine;
                    }
                }
            }
        }

        private void ScheduleSelectedLayersExpansion(IImageViewerViewModel vm)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!ReferenceEquals(DataContext, vm) || LayerTree == null) return;

                var selectedLayerIds = new HashSet<int>(vm.SelectedShapes.Select(shape => shape.ShapeLayer.LayerId));
                foreach (var layer in vm.LayerGroups.Where(layer => selectedLayerIds.Contains(layer.LayerId)))
                    if (LayerTree.ItemContainerGenerator.ContainerFromItem(layer) is TreeViewItem container)
                        container.IsExpanded = true;
            }), DispatcherPriority.Loaded);
        }

        private void BtnToggleGeometries_Click(object sender, RoutedEventArgs e)
        {
            var isVisible = BtnToggleGeometries.IsChecked ?? false;
            ShowGeometries = isVisible;
            if (ImageViewer != null)
            {
                ImageViewer.ShowGeometries = isVisible;
                ImageViewer.UpdateSketchBoardVisibility();
            }
            if (DataContext is IImageViewerViewModel vm && vm.ShowGeometries != isVisible)
            {
                vm.ShowGeometries = isVisible;
            }
        }

        private void PropertyPanelToggle_Changed(object sender, RoutedEventArgs e)
            => UpdatePropertyPanelVisibility(animate: IsLoaded);

        private void UpdatePropertyPanelVisibility(bool animate)
        {
            if (LayerPanel == null || LayerPanelSlideTransform == null) return;

            var isOpen = BtnToggleProperties.IsChecked == true;
            // Include the toolbar's right inset and the panel shadow in the slide distance.
            var hiddenOffset = Math.Max(LayerPanel.ActualWidth, LayerPanel.Width) + 24;
            var from = LayerPanel.Visibility == Visibility.Visible ? LayerPanelSlideTransform.X : hiddenOffset;
            var to = isOpen ? 0 : hiddenOffset;
            var animationVersion = ++_propertyPanelAnimationVersion;

            // Capture the current animated position before replacing its clock, so a toggle can reverse smoothly.
            LayerPanelSlideTransform.BeginAnimation(TranslateTransform.XProperty, null);
            LayerPanelSlideTransform.X = from;
            LayerPanel.IsHitTestVisible = isOpen;
            if (!isOpen && LayerPanel.IsKeyboardFocusWithin) BtnToggleProperties.Focus();

            if (!animate || !SystemParameters.ClientAreaAnimation || Math.Abs(from - to) < 0.5)
            {
                LayerPanelSlideTransform.X = to;
                LayerPanel.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            LayerPanel.Visibility = Visibility.Visible;
            var duration = (isOpen ? 220 : 180) * Math.Min(1, Math.Abs(from - to) / hiddenOffset);
            var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(duration))
            {
                EasingFunction = new CubicEase { EasingMode = isOpen ? EasingMode.EaseOut : EasingMode.EaseIn }
            };
            animation.Completed += (_, _) =>
            {
                if (animationVersion != _propertyPanelAnimationVersion) return;
                LayerPanelSlideTransform.X = to;
                LayerPanelSlideTransform.BeginAnimation(TranslateTransform.XProperty, null);
                LayerPanel.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
            };
            LayerPanelSlideTransform.BeginAnimation(TranslateTransform.XProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private void LayerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (!_handlingTreeSelection && e.NewValue is ShapeVisualBase shape && DataContext is IImageViewerViewModel vm)
                SelectTreeShape(shape, Keyboard.Modifiers);
        }

        private void LayerTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            var item = FindAncestor<TreeViewItem>(source);
            if (item?.DataContext is not ShapeVisualBase shape || FindAncestor<Button>(source) != null
                || FindAncestor<CheckBox>(source) != null || FindAncestor<System.Windows.Controls.Primitives.ToggleButton>(source) != null)
                return;
            SelectTreeShape(shape, Keyboard.Modifiers);
            _handlingTreeSelection = true;
            try { item.Focus(); }
            finally { _handlingTreeSelection = false; }
            e.Handled = true;
        }

        protected void SelectTreeShape(ShapeVisualBase shape, ModifierKeys modifiers)
        {
            if (DataContext is not IImageViewerViewModel vm) return;
            PrepareSelection(vm);
            if (!vm.Shapes.Contains(shape)) return;
            if ((modifiers & ModifierKeys.Shift) != 0)
            {
                var visible = VisibleTreeShapes(vm).Where(x => Eligible(vm, x)).ToList();
                var start = visible.IndexOf(_selectionAnchor);
                var end = visible.IndexOf(shape);
                if (end < 0) return;
                if (start < 0) start = end;
                var range = visible.Skip(Math.Min(start, end)).Take(Math.Abs(start - end) + 1);
                vm.ShapeRepository.SetSelection((modifiers & ModifierKeys.Control) != 0
                    ? vm.SelectedShapes.Where(x => Eligible(vm, x)).Concat(range) : range);
            }
            else if ((modifiers & ModifierKeys.Control) != 0)
            {
                if (!Eligible(vm, shape)) return;
                var selection = vm.SelectedShapes.Where(x => Eligible(vm, x)).ToList();
                var group = vm.ShapeRepository.GetGroup(shape);
                var toggled = group != null ? group.Members.ToList() : new List<ShapeVisualBase> { shape };
                if (toggled.Any(selection.Contains)) selection.RemoveAll(toggled.Contains);
                else selection.AddRange(toggled);
                vm.ShapeRepository.SetSelection(selection);
                _selectionAnchor = shape;
            }
            else
            {
                vm.SelectedShape = shape;
                _selectionAnchor = shape;
            }
        }

        private IEnumerable<ShapeVisualBase> VisibleTreeShapes(IImageViewerViewModel vm)
        {
            foreach (var layer in vm.LayerGroups)
                if (LayerTree.ItemContainerGenerator.ContainerFromItem(layer) is TreeViewItem { IsExpanded: true })
                    foreach (var shape in layer.Shapes) yield return shape;
        }

        private static bool Eligible(IImageViewerViewModel vm, ShapeVisualBase shape)
        {
            if (!EligibleMember(vm, shape)) return false;
            var group = vm.ShapeRepository.GetGroup(shape);
            return group == null || group.Members.All(member => EligibleMember(vm, member));
        }

        private static bool EligibleMember(IImageViewerViewModel vm, ShapeVisualBase shape)
            => shape.IsGeometryRendered && !shape.IsLocked && vm.ShapeRepository.IsLayerVisible(shape.ShapeLayer.LayerId);

        private static void PrepareSelection(IImageViewerViewModel vm)
        {
            vm.ShapeRepository.CancelCurrentSketch();
        }

        private static T FindAncestor<T>(DependencyObject source) where T : DependencyObject
        {
            while (source != null)
            {
                if (source is T result) return result;
                source = source is Visual || source is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }
            return null;
        }

        private void ShapeSelection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton { Tag: ShapeVisualBase shape } marker)
            {
                SelectTreeShape(shape, ModifierKeys.Control);
                marker.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
            }
            e.Handled = true;
        }

        private void SelectLayerShapes_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is IImageViewerViewModel vm && sender is Button { DataContext: ShapeLayerTreeNode layer })
            {
                PrepareSelection(vm);
                vm.ShapeRepository.SetSelection(layer.Shapes.Where(x => Eligible(vm, x)).ToList());
            }
            e.Handled = true;
        }

        private void LayerTree_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (HandleTreeSelectionKey(e.Key, Keyboard.Modifiers)) e.Handled = true;
        }

        protected bool HandleTreeSelectionKey(Key key, ModifierKeys modifiers)
        {
            if (DataContext is not IImageViewerViewModel vm) return false;
            if (key == Key.G && (modifiers & ModifierKeys.Control) != 0 && (modifiers & ModifierKeys.Alt) == 0)
            {
                var command = (modifiers & ModifierKeys.Shift) != 0 ? vm.UngroupShapesCommand : vm.GroupShapesCommand;
                if (command.CanExecute(null)) command.Execute(null);
                return true;
            }
            if (key == Key.Delete && vm.DeleteShapeCommand.CanExecute(null))
            {
                vm.DeleteShapeCommand.Execute(null);
                return true;
            }
            if (key == Key.A && (modifiers & ModifierKeys.Control) != 0)
            {
                PrepareSelection(vm);
                vm.ShapeRepository.SetSelection(vm.Shapes.Where(x => Eligible(vm, x)));
                return true;
            }
            if (key == Key.Escape)
            {
                vm.ShapeRepository.SetSelection(Array.Empty<ShapeVisualBase>());
                return true;
            }
            return false;
        }

        private void EditLayer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: ShapeLayerTreeNode node } ||
                DataContext is not IImageViewerViewModel vm || !node.CanEdit) return;

            var editor = new LayerEditorWindow(node.Layer, false, vm.UpdateLayerConfiguration)
            {
                Owner = Window.GetWindow(this)
            };
            editor.ShowDialog();
        }

        private void NewLayer_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not IImageViewerViewModel vm) return;
            var editor = new LayerEditorWindow(vm.SelectedShapeLayer, true, parameter => vm.CreateLayer(parameter))
                { Owner = Window.GetWindow(this) };
            editor.ShowDialog();
        }

        private void SaveLayers_Click(object sender, RoutedEventArgs e) => SaveLayers(false);
        private void SaveLayersAs_Click(object sender, RoutedEventArgs e) => SaveLayers(true);
        private void SaveLayers(bool saveAs)
        {
            if (DataContext is not IImageViewerViewModel vm) return;
            try
            {
                var path = vm.LayerConfigurationPath;
                if (saveAs || string.IsNullOrWhiteSpace(path))
                {
                    var dialog = new SaveFileDialog
                    {
                        Title = "保存图层配置", Filter = "JSON 配置 (*.json)|*.json", DefaultExt = ".json",
                        FileName = string.IsNullOrWhiteSpace(path) ? "LanShapesConfig.json" : System.IO.Path.GetFileName(path)
                    };
                    if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
                    path = dialog.FileName;
                }
                vm.SaveLayerConfiguration(path);
            }
            catch (Exception error) { ShowLayerError(error); }
        }

        private void SaveCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (ImageViewer == null) return;
            if (ImageViewer.ImageSource == null)
            {
                MessageBox.Show(Window.GetWindow(this), "没有可保存的画布内容。", "保存画布",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new SaveFileDialog
            {
                Title = "保存画布内容",
                Filter = "PNG 图像 (*.png)|*.png|JPEG 图像 (*.jpg)|*.jpg|BMP 图像 (*.bmp)|*.bmp",
                DefaultExt = ".png",
                FileName = "Canvas"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try { ImageViewer.SaveCanvasContent(dialog.FileName); }
            catch (Exception error)
            {
                MessageBox.Show(Window.GetWindow(this), error.Message, "保存画布失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AssignLayer_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not IImageViewerViewModel vm || !vm.CanAssignSelectedLayer) return;
            try { vm.AssignSelectedShapesToLayer(); }
            catch (Exception error) { ShowLayerError(error); }
        }
        private void ShowLayerError(Exception error) => MessageBox.Show(Window.GetWindow(this), error.Message,
            "图层操作失败", MessageBoxButton.OK, MessageBoxImage.Error);

        #endregion

        #region local methods

        //private void AnimateGrids(EventHandler postAnimation, double commGridWidth, double mainGridWidth)

        //{
        //    /* 

        //     *  If commGrid is right 

        //     *  commGrid.Margin.Left should move from 0 to -mainGridWidth 

        //     *  commGrid.Margin.Right should move from 0 to +mainGridWidth 

        //     *   

        //     *  If commGrid is left 
        //     *  commGrid.Margin.Left should move from 0 to +mainGridWidth 
        //     *  commGrid.Margin.Right should move from 0 to -mainGridWidth 

        //     *  

        //     */


        //    //var sb = new Storyboard();
        //    //var commGridAnimation
        //    //    = new ThicknessAnimation
        //    //    {
        //    //        From = new Thickness(0),

        //    //        To = new Thickness((isRightHanded ? -1 : 1) * mainGridWidth, 0,
        //    //                (isRightHanded ? 1 : -1) * mainGridWidth, 0),

        //    //        AccelerationRatio = 0.2,
        //    //        FillBehavior = FillBehavior.Stop,
        //    //        DecelerationRatio = 0.8,
        //    //        Duration = DURATION
        //    //    };

        //    //var mainGridAnimation
        //    //    = new ThicknessAnimation

        //    //    {
        //    //        From = new Thickness(0),
        //    //        To = new Thickness((isRightHanded ? 1 : -1) * commGridWidth, 0,
        //    //                (isRightHanded ? -1 : 1) * commGridWidth, 0),

        //    //        FillBehavior = FillBehavior.Stop,
        //    //        AccelerationRatio = 0.2,
        //    //        DecelerationRatio = 0.8,
        //    //        Duration = DURATION
        //    //    };


        //    //Storyboard.SetTarget(commGridAnimation, commGrid);
        //    //Storyboard.SetTargetProperty(commGridAnimation,
        //    //    new PropertyPath(MarginProperty));
        //    //Storyboard.SetTarget(mainGridAnimation, mainGrid);
        //    //Storyboard.SetTargetProperty(mainGridAnimation,
        //    //    new PropertyPath(MarginProperty));

        //    //sb.Children.Add(commGridAnimation);
        //    //sb.Children.Add(mainGridAnimation);
        //    //sb.Completed += postAnimation;
        //    //sb.Begin();
        //}

        //private void ToolBarSwitch_OnClick(object sender, RoutedEventArgs e)
        //{
        //    var t = ToolsBorder.Width;
        //}

        #endregion

        //private void ToolsBorder_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        //{
        //    ;
        //}
    }
}
