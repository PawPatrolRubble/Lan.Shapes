#region

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Lan.Shapes.Enums;

#endregion

namespace Lan.ImageViewer
{
    /// <summary>
    /// Interaction logic for ImageViewerControl.xaml
    /// </summary>
    public partial class ImageViewerControl : UserControl
    {
        public static readonly DependencyProperty LineDirectionModeProperty = DependencyProperty.Register(
            nameof(LineDirectionMode), typeof(LineDirectionMode), typeof(ImageViewerControl),
            new FrameworkPropertyMetadata(LineDirectionMode.Free, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault),
            value => value is LineDirectionMode mode && Enum.IsDefined(mode));

        public LineDirectionMode LineDirectionMode
        {
            get => (LineDirectionMode)GetValue(LineDirectionModeProperty);
            set => SetValue(LineDirectionModeProperty, value);
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

            this.DataContextChanged += (s, e) =>
            {
                if (e.OldValue is INotifyPropertyChanged oldNpc)
                {
                    oldNpc.PropertyChanged -= OnViewModelPropertyChanged;
                }
                if (e.NewValue is IImageViewerViewModel vm)
                {
                    this.ShowGeometries = vm.ShowGeometries;
                    if (this.ImageViewer != null)
                    {
                        this.ImageViewer.ShowGeometries = vm.ShowGeometries;
                        this.ImageViewer.UpdateSketchBoardVisibility();
                    }
                    if (e.NewValue is INotifyPropertyChanged newNpc)
                    {
                        newNpc.PropertyChanged += OnViewModelPropertyChanged;
                    }
                }
            };
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IImageViewerViewModel.ShowGeometries) &&
                sender is IImageViewerViewModel vm &&
                this.ShowGeometries != vm.ShowGeometries)
            {
                this.ShowGeometries = vm.ShowGeometries;
                if (this.ImageViewer != null)
                {
                    this.ImageViewer.ShowGeometries = vm.ShowGeometries;
                    this.ImageViewer.UpdateSketchBoardVisibility();
                }
            }
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
