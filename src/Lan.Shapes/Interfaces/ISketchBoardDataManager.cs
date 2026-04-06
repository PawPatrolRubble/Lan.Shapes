#nullable enable

using System;
using System.Windows;
using System.Windows.Media;

namespace Lan.Shapes.Interfaces
{
    /// <summary>
    /// Combined interface for the sketch board data manager.
    /// Extends <see cref="IShapeRepository"/> with the WPF visual-host members
    /// that are required by <c>Lan.SketchBoard.SketchBoard</c> at runtime.
    ///
    /// <para>
    /// <b>Dependency guidance:</b><br/>
    /// — Depend on <see cref="IShapeRepository"/> when your consumer does not need
    ///   access to <see cref="VisualCollection"/> or the WPF visual tree
    ///   (ViewModels, services, unit tests).<br/>
    /// — Depend on <c>ISketchBoardDataManager</c> only in WPF-layer code
    ///   (controls, code-behind) that must manage the visual collection.
    /// </para>
    /// </summary>
    public interface ISketchBoardDataManager : IShapeRepository
    {
        // ── WPF visual-host members (not on IShapeRepository) ───────────────────

        /// <summary>Reference to the host <see cref="ISketchBoard"/> WPF control.</summary>
        ISketchBoard SketchBoard { get; }

        /// <summary>
        /// The WPF <see cref="System.Windows.Media.VisualCollection"/> backing the board's
        /// visual children. Populated by <see cref="InitializeVisualCollection"/>.
        /// </summary>
        VisualCollection VisualCollection { get; }

        /// <summary>
        /// Attaches this manager to a WPF <see cref="Visual"/> (the <c>SketchBoard</c> canvas)
        /// and initialises the <see cref="VisualCollection"/>.
        /// Must be called before shapes can be rendered.
        /// </summary>
        void InitializeVisualCollection(Visual visual);

        /// <summary>
        /// Notifies the manager that the image viewer's zoom scale changed so that
        /// stroke thickness and drag-handle sizes can be recalculated.
        /// </summary>
        void OnImageViewerPropertyChanged(double scale);

        /// <summary>
        /// Raised after <see cref="InitializeVisualCollection"/> completes and the board
        /// is ready to accept shapes.
        /// </summary>
        event EventHandler<ISketchBoardDataManager> SketchBoardManagerInitialized;
    }
}