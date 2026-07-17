using System;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;

namespace Lan.ImageViewer
{
    /// <summary>
    /// View-model surface for the image viewer control.
    /// Shape list/selection/commands use <see cref="IShapeRepository"/> members;
    /// <see cref="SketchBoardDataManager"/> is retained only so the WPF control can
    /// attach the visual host (<c>VisualCollection</c>, scale feedback).
    /// </summary>
    public interface IImageViewerViewModel
    {
        /// <summary>
        /// Fat board manager for the control dependency property / visual host only.
        /// Prefer <see cref="ShapeRepository"/>, <see cref="Shapes"/>, and
        /// <see cref="SelectedShape"/> for shape state.
        /// </summary>
        ISketchBoardDataManager SketchBoardDataManager { get; }

        /// <summary>
        /// Shape-data surface (collections, selection, CRUD, events) without visual-host members.
        /// Same instance as <see cref="SketchBoardDataManager"/> in the default implementation.
        /// </summary>
        IShapeRepository ShapeRepository { get; }

        /// <summary>Shapes on the board (bindable list).</summary>
        ObservableCollection<ShapeVisualBase> Shapes { get; }

        /// <summary>
        /// Shape currently selected for edit/delete (maps to repository
        /// <c>SelectedGeometry</c>, not the in-progress sketch
        /// <c>CurrentGeometryInEdit</c>).
        /// </summary>
        ShapeVisualBase? SelectedShape { get; set; }

        /// <summary>Geometry type palette for the toolbar.</summary>
        ObservableCollection<GeometryType> GeometryTypeList { get; }

        GeometryType? SelectedGeometryType { get; }

        /// <summary>Image displayed under the sketch board.</summary>
        ImageSource Image { get; set; }

        double Scale { get; set; }

        ObservableCollection<ShapeLayer> Layers { get; set; }

        /// <summary>Active layer for new shapes.</summary>
        ShapeLayer SelectedShapeLayer { get; set; }

        /// <summary>Last double-click position in image coordinates.</summary>
        Point MouseDoubleClickPosition { get; set; }

        #region commands

        ICommand ZoomOutCommand { get; }
        ICommand ZoomInCommand { get; }
        ICommand ScaleToOriginalSizeCommand { get; }
        ICommand ScaleToFitCommand { get; }
        ICommand DeleteShapeCommand { get; }

        /// <summary>When true, the shape list pane is shown; when false, canvas only.</summary>
        bool ShowSimpleCanvas { get; set; }

        /// <summary>Controls visibility of geometry-type tools.</summary>
        bool ShowShapeTypes { get; set; }

        /// <summary>Filters the geometry-type palette by the given predicate.</summary>
        void FilterGeometryTypes(Expression<Func<GeometryType, bool>> predicate);

        #endregion
    }
}
