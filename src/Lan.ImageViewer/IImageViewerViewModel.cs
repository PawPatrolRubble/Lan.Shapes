using System;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;

#nullable enable

namespace Lan.ImageViewer
{
    /// <summary>
    /// View-model surface for the image viewer control.
    /// Shape list/selection/commands use <see cref="IShapeRepository"/> members;
    /// <see cref="SketchBoardDataManager"/> is retained only so the WPF control can
    /// attach the visual host (<c>VisualCollection</c>, scale feedback).
    /// </summary>
    public interface IImageViewerViewModel : IDisposable
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
        ReadOnlyObservableCollection<ShapeVisualBase> SelectedShapes { get; }
        ShapeVisualBase? PropertyShape { get; }
        int SelectedShapeCount { get; }
        string SelectedLayerSummary { get; }
        string SelectionPrompt { get; }
        ShapeGroup? SelectedGroup { get; }
        string SelectedGroupSummary { get; }
        string GroupingPrompt { get; }
        bool CanGroupSelectedShapes { get; }
        bool CanUngroupSelectedShapes { get; }
        ShapeLayer? TargetShapeLayer { get; set; }
        bool CanAssignSelectedLayer { get; }
        int AssignSelectedShapesToLayer();
        ICommand SelectionModeCommand { get; }

        /// <summary>Geometry type palette for the toolbar.</summary>
        ObservableCollection<GeometryType> GeometryTypeList { get; }

        GeometryType? SelectedGeometryType { get; }

        /// <summary>Image displayed under the sketch board.</summary>
        ImageSource Image { get; set; }

        double Scale { get; set; }

        /// <summary>
        /// The layer manager's directory. The setter accepts that same collection
        /// for compatibility; replacing the directory is a manager operation.
        /// </summary>
        ObservableCollection<ShapeLayer> Layers { get; set; }

        /// <summary>Layers and their board shapes for the tree panel.</summary>
        ObservableCollection<ShapeLayerTreeNode> LayerGroups { get; }

        /// <summary>Applies and persists edits to a layer definition.</summary>
        void UpdateLayerConfiguration(ShapeLayerParameter parameter);
        ShapeLayer CreateLayer(ShapeLayerParameter parameter);
        void SaveLayerConfiguration(string filePath = "");
        string LayerConfigurationPath { get; }
        string LayerConfigurationStatus { get; }

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
        ICommand GroupShapesCommand { get; }
        ICommand UngroupShapesCommand { get; }

        /// <summary>When true, show only the canvas; when false, show the layer and property pane.</summary>
        bool ShowSimpleCanvas { get; set; }

        /// <summary>Controls visibility of geometry-type tools.</summary>
        bool ShowShapeTypes { get; set; }

        /// <summary>When true, the image viewer draws the center crosshair overlay.</summary>
        bool ShowCrossLine { get; set; }

        /// <summary>When true, geometries on the sketch board are visible; when false, hidden.</summary>
        bool ShowGeometries { get; set; }

        /// <summary>Filters the geometry-type palette by the given predicate.</summary>
        void FilterGeometryTypes(Expression<Func<GeometryType, bool>> predicate);

        #endregion
    }
}
