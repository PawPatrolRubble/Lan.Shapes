#region

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lan.Shapes.Interfaces;
using Lan.SketchBoard;

#endregion

namespace Lan.ImageViewer {
    [TemplatePart(Type = typeof(Canvas), Name = "containerCanvas")]
    [TemplatePart(Type = typeof(Image), Name = "ImageViewer")]
    [TemplatePart(Type = typeof(Grid), Name = "GridContainer")]
    [TemplatePart(Type = typeof(TextBlock), Name = "TbMousePosition")]
    [TemplatePart(Type = typeof(Button), Name = "BtnFit")]
    public class ImageViewer : ImageViewerBasic {
        #region fields

#nullable enable
        private PropertyChangedEventHandler? _localScaleChangedHandler;
#nullable restore

        #endregion

        #region Constructors

        static ImageViewer() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageViewer),
                new FrameworkPropertyMetadata(typeof(ImageViewer)));
        }



        #endregion

        #region others

        #endregion

        #region binding properties

        #endregion


        #region dependency properties

        // Register a dependency property with the specified property name,
        // property type, owner type, and property metadata.
        // Assign DependencyPropertyKey to a nonpublic field.

        // Declare a public get accessor.


        // Register a dependency property with the specified property name,
        // property type, owner type, and property metadata.
        // Assign DependencyPropertyKey to a nonpublic field.


        // Declare a public get accessor.


        public static readonly DependencyProperty SketchBoardDataManagerProperty = DependencyProperty.Register(
            "SketchBoardDataManager", typeof(ISketchBoardDataManager), typeof(ImageViewer),
            new PropertyMetadata(default(ISketchBoardDataManager), OnSketchBoardChangeCallBack));

        private static void OnSketchBoardChangeCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is not ImageViewer imageViewer) {
                return;
            }

            // Avoid stacking handlers when the DP is reassigned (multi-binding / rebind).
            if (imageViewer._localScaleChangedHandler != null) {
                imageViewer.PropertyChanged -= imageViewer._localScaleChangedHandler;
                imageViewer._localScaleChangedHandler = null;
            }

            if (e.NewValue is not ISketchBoardDataManager sketchBoardDataManager) {
                return;
            }

            imageViewer._localScaleChangedHandler = (_, args) => {
                if (args.PropertyName == nameof(LocalScale)) {
                    sketchBoardDataManager.OnImageViewerPropertyChanged(imageViewer.LocalScale);
                }
            };
            imageViewer.PropertyChanged += imageViewer._localScaleChangedHandler;

            // Seed stylers for the current zoom so first paint matches chrome.
            sketchBoardDataManager.OnImageViewerPropertyChanged(imageViewer.LocalScale);
        }

        public ISketchBoardDataManager SketchBoardDataManager {
            get => (ISketchBoardDataManager)GetValue(SketchBoardDataManagerProperty);
            set => SetValue(SketchBoardDataManagerProperty, value);
        }

        #endregion


        #region events handlers

        #endregion
    }
}