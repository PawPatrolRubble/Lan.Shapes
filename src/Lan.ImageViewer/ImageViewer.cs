#region

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Enums;
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

        public static readonly DependencyProperty LineDirectionModeProperty = DependencyProperty.Register(
            nameof(LineDirectionMode), typeof(LineDirectionMode), typeof(ImageViewer),
            new FrameworkPropertyMetadata(LineDirectionMode.Free, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault),
            value => value is LineDirectionMode mode && Enum.IsDefined(mode));

        /// <summary>Direction constraint used when drawing or resizing line endpoints.</summary>
        public LineDirectionMode LineDirectionMode {
            get => (LineDirectionMode)GetValue(LineDirectionModeProperty);
            set => SetValue(LineDirectionModeProperty, value);
        }

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

        public static readonly DependencyProperty ShowGeometriesProperty = DependencyProperty.Register(
            nameof(ShowGeometries), typeof(bool), typeof(ImageViewer),
            new FrameworkPropertyMetadata(true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault |
                FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.AffectsMeasure,
                OnShowGeometriesChanged));

        private static void OnShowGeometriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ImageViewer imageViewer) {
                imageViewer.UpdateSketchBoardVisibility();
            }
        }

        public bool ShowGeometries {
            get => (bool)GetValue(ShowGeometriesProperty);
            set => SetValue(ShowGeometriesProperty, value);
        }

        private FrameworkElement? _sketchBoard;

        internal void UpdateSketchBoardVisibility() {
            _sketchBoard ??= GetTemplateChild("SketchBoard") as FrameworkElement;
            if (_sketchBoard != null) {
                _sketchBoard.Visibility = ShowGeometries ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            _sketchBoard = GetTemplateChild("SketchBoard") as FrameworkElement;
            UpdateSketchBoardVisibility();
        }

        #endregion

        #nullable enable

        #region canvas export

        /// <summary>
        /// Composes everything currently on the canvas — the loaded image plus every
        /// rendered geometry — into one bitmap at the image's native pixel resolution.
        /// Shapes are drawn at their image-space stroke thickness, so the result does not
        /// depend on the current zoom. Returns <c>null</c> when there is nothing to render.
        /// </summary>
        public BitmapSource? RenderCanvasContent()
        {
            var image = ImageSource;
            if (image == null)
            {
                return null;
            }

            var size = GetCanvasPixelSize();
            if (size == null)
            {
                return null;
            }

            var width = size.Value.Width;
            var height = size.Value.Height;
            var board = GetSketchBoard();
            var isBoardRendered = board is { Visibility: Visibility.Visible }
                && board.ActualWidth > 0 && board.ActualHeight > 0;
            var dataManager = SketchBoardDataManager;
            var viewportScale = dataManager?.ViewportScale ?? 1.0;
            // Stroke and handle sizes are kept constant on screen as base / viewportScale.
            // Native-resolution output wants the base sizes instead of the current zoom's.
            var normalizeScale = isBoardRendered && dataManager != null && Math.Abs(viewportScale - 1.0) > 0.0001;

            try
            {
                if (normalizeScale)
                {
                    dataManager!.OnImageViewerPropertyChanged(1.0);
                }

                var canvas = new Rect(0, 0, width, height);
                var content = new DrawingVisual();
                using (var context = content.RenderOpen())
                {
                    context.DrawImage(image, canvas);
                    if (isBoardRendered)
                    {
                        var boardLayer = new RenderTargetBitmap(
                            (int)Math.Round(board!.ActualWidth),
                            (int)Math.Round(board.ActualHeight),
                            96, 96, PixelFormats.Pbgra32);
                        boardLayer.Render(board);
                        // Render() bakes in the board's layout offset (its template margin).
                        // Board coordinates are image pixels, so undo it to keep them aligned.
                        var boardOffset = VisualTreeHelper.GetOffset(board);
                        context.PushTransform(new TranslateTransform(-boardOffset.X, -boardOffset.Y));
                        context.DrawImage(boardLayer, canvas);
                        context.Pop();
                    }
                }

                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                bitmap.Freeze();
                return bitmap;
            }
            finally
            {
                if (normalizeScale)
                {
                    dataManager!.OnImageViewerPropertyChanged(viewportScale);
                }
            }
        }

        /// <summary>
        /// Saves <see cref="RenderCanvasContent"/> to <paramref name="filePath"/>, choosing
        /// the encoder from the file extension (<c>.png</c>, <c>.jpg</c>/<c>.jpeg</c>, <c>.bmp</c>).
        /// </summary>
        /// <exception cref="InvalidOperationException">There is no canvas content to save.</exception>
        public void SaveCanvasContent(string filePath)
            => ImageViewerBasic.SaveImage(
                RenderCanvasContent() ?? throw new InvalidOperationException("The canvas has no content to save."),
                filePath);

        /// <summary>Image pixel size of the canvas, falling back to the sketch board layout size.</summary>
        private (int Width, int Height)? GetCanvasPixelSize()
        {
            var width = PixelWidth;
            var height = PixelHeight;
            if (width <= 0 || height <= 0)
            {
                var board = GetSketchBoard();
                width = board?.ActualWidth ?? 0;
                height = board?.ActualHeight ?? 0;
            }

            return width > 0 && height > 0
                ? ((int)Math.Round(width), (int)Math.Round(height))
                : ((int, int)?)null;
        }

        private FrameworkElement? GetSketchBoard()
            => _sketchBoard ??= GetTemplateChild("SketchBoard") as FrameworkElement;

        #endregion

        #nullable restore
        #region events handlers

        #endregion
    }
}
