#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace Lan.Shapes.Interfaces
{
    /// <summary>
    /// Shape-data contract for a WPF sketch board: collections, selection, layers,
    /// geometry-type selection, CRUD, and lifecycle events.
    /// Prefer this over <see cref="ISketchBoardDataManager"/> when the consumer only
    /// needs shape state (ViewModels, services, tests) and does not touch the
    /// <see cref="System.Windows.Media.VisualCollection"/> or board host wiring.
    /// This library targets WPF only; the split is for interface segregation inside
    /// the WPF stack, not for non-WPF platforms.
    /// </summary>
    public interface IShapeRepository
    {
        // ── Collections ─────────────────────────────────────────────────────────

        /// <summary>Observable, bindable list of all shapes on the board.</summary>
        ObservableCollection<ShapeVisualBase> Shapes { get; }

        /// <summary>Total number of shapes currently on the board.</summary>
        int ShapeCount { get; }

        // ── Selection state ──────────────────────────────────────────────────────

        /// <summary>The shape currently being drawn (not yet committed).</summary>
        ShapeVisualBase? CurrentGeometryInEdit { get; set; }

        /// <summary>The shape currently selected by the user.</summary>
        ShapeVisualBase? SelectedGeometry { get; set; }

        ReadOnlyObservableCollection<ShapeVisualBase> SelectedGeometries { get; }

        /// <summary>Logical groups whose members remain in <see cref="Shapes"/>.</summary>
        ReadOnlyObservableCollection<ShapeGroup> Groups { get; }

        /// <summary>Returns the group containing the shape, or null for an independent shape.</summary>
        ShapeGroup? GetGroup(ShapeVisualBase shape);

        /// <summary>Checks whether two or more independent, movable shapes with finite, invertible transforms on one visible layer can be grouped.</summary>
        bool CanGroupShapes(IEnumerable<ShapeVisualBase> shapes);

        /// <summary>Groups completed, unlocked, movable shapes from one visible layer without changing their visual order.</summary>
        ShapeGroup GroupShapes(IEnumerable<ShapeVisualBase> shapes, string? name = null);

        /// <summary>Removes the groups containing the supplied shapes, preserving geometry, selection and lock state.</summary>
        int UngroupShapes(IEnumerable<ShapeVisualBase> shapes);

        /// <summary>
        /// Moves completed, visible, unlocked shapes by a board-coordinate displacement.
        /// Group members are expanded and all movement constraints are checked before any shape is moved.
        /// </summary>
        int TranslateShapes(IEnumerable<ShapeVisualBase> shapes, Vector delta);

        event EventHandler GroupsChanged;

        /// <summary>Replaces selection with completed, visible shapes owned by this board, expanding group members.</summary>
        void SetSelection(IEnumerable<ShapeVisualBase> shapes);

        /// <summary>Moves completed, unlocked shapes to an independent board copy of the target layer.</summary>
        int AssignShapesToLayer(IEnumerable<ShapeVisualBase> shapes, ShapeLayer targetLayer);

        event EventHandler SelectionChanged;
        event EventHandler LayerAssignmentsChanged;

        /// <summary>Clears the current selection without removing the shape.</summary>
        void UnselectGeometry();

        /// <summary>Clears the active geometry type selection.</summary>
        void UnselectGeometryType();

        /// <summary>Cancels the unfinished sketch and exits drawing mode, preserving completed selection.</summary>
        void CancelCurrentSketch();

        // ── Layer & type management ──────────────────────────────────────────────

        /// <summary>The active drawing tool, or null when no tool is selected.</summary>
        Type? CurrentGeometryType { get; }

        ShapeLayer? CurrentShapeLayer { get; }

        /// <summary>Sets the active layer that new shapes will be assigned to.</summary>
        void SetShapeLayer(ShapeLayer layer);

        /// <summary>Shows or hides all shapes assigned to a layer on this board.</summary>
        void SetLayerVisibility(int layerId, bool isVisible);

        /// <summary>Gets the board-local visibility of a layer.</summary>
        bool IsLayerVisible(int layerId);

        /// <summary>Updates all board-owned copies of an edited layer and redraws its shapes.</summary>
        void UpdateLayerConfiguration(ShapeLayerParameter parameter);

        /// <summary>
        /// Replaces the board's configured layer definitions, including measurement calibration.
        /// Shapes whose definition was removed retain their independent, visible board-local layer.
        /// </summary>
        void ApplyLayerDefinitions(IEnumerable<ShapeLayer> definitions);

        /// <summary>Sets the active geometry type by <see cref="Type"/> directly.</summary>
        void SetGeometryType(Type type);

        /// <summary>
        /// Registers a named drawing tool so it can be selected by name
        /// via the string overload of <c>SetGeometryType</c>.
        /// </summary>
        void RegisterDrawingTool(string name, Type type);

        // ── CRUD ─────────────────────────────────────────────────────────────────

        void AddShape(ShapeVisualBase shape);
        void AddShape(ShapeVisualBase shape, int index);
        void RemoveShape(ShapeVisualBase shape);

        /// <summary>Removes all shapes matching <paramref name="predicate"/>.</summary>
        void RemoveShapes(Func<ShapeVisualBase, bool> predicate);

        void RemoveAt(int index);
        void RemoveAt(int index, int count);
        void ClearAllShapes();
        ShapeVisualBase? GetShapeVisual(int index);

        // ── Factory methods ───────────────────────────────────────────────────────

        /// <summary>
        /// Loads a shape from serialised data and adds it to the board.
        /// </summary>
        ShapeVisualBase LoadShape<T, TP>(TP parameter)
            where T : ShapeVisualBase, IDataExport<TP>
            where TP : IGeometryMetaData;

        /// <summary>
        /// Creates a shape from serialised data without adding it to the board.
        /// </summary>
        ShapeVisualBase CreateShape<T, TP>(TP parameter)
            where T : ShapeVisualBase, IDataExport<TP>
            where TP : IGeometryMetaData;

        /// <summary>
        /// Instantiates a new shape of the currently selected geometry type at
        /// <paramref name="mousePosition"/> and adds it to the board.
        /// Returns <c>null</c> when no geometry type is selected.
        /// </summary>
        ShapeVisualBase? CreateNewGeometry(Point mousePosition);

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Raised when a shape is added to the board.</summary>
        event EventHandler<ShapeVisualBase> ShapeCreated;

        /// <summary>Raised when a shape is removed from the board.</summary>
        event EventHandler<ShapeVisualBase> ShapeRemoved;

        /// <summary>Raised when a shape transitions to the Selected state.</summary>
        event EventHandler<ShapeVisualBase> ShapeSelected;

        /// <summary>Raised when a shape transitions away from the Selected state.</summary>
        event EventHandler<ShapeVisualBase> ShapeUnselected;

        /// <summary>Raised when the user picks a geometry type to draw.</summary>
        event EventHandler<Type> GeometryTypeSelected;

        /// <summary>Raised when the active geometry type is cleared.</summary>
        event EventHandler<Type> GeometryTypeUnselected;

        /// <summary>
        /// Invoked immediately after a new shape is first committed
        /// (on <c>MouseLeftButtonUp</c> while <c>IsGeometryRendered</c> is false).
        /// </summary>
        event EventHandler<ShapeVisualBase> NewShapeSketched;

        /// <summary>
        /// Raises <see cref="NewShapeSketched"/> after a new shape is first committed.
        /// </summary>
        void RaiseNewShapeSketched(ShapeVisualBase shape);
    }
}
