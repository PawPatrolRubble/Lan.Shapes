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

        /// <summary>Clears the current selection without removing the shape.</summary>
        void UnselectGeometry();

        /// <summary>Clears the active geometry type selection.</summary>
        void UnselectGeometryType();

        // ── Layer & type management ──────────────────────────────────────────────

        ShapeLayer? CurrentShapeLayer { get; }

        /// <summary>Sets the active layer that new shapes will be assigned to.</summary>
        void SetShapeLayer(ShapeLayer layer);

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
