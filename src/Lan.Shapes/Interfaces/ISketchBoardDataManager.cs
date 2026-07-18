#nullable enable

using System;
using System.Windows;
using System.Windows.Media;

namespace Lan.Shapes.Interfaces
{
    /// <summary>
    /// Full sketch-board manager for WPF hosts.
    /// Extends <see cref="IShapeRepository"/> with visual-host members required by
    /// <c>Lan.SketchBoard.SketchBoard</c> at runtime (<see cref="VisualCollection"/>,
    /// board attachment, scale notifications).
    ///
    /// <para>
    /// <b>Dependency guidance (WPF-only stack):</b><br/>
    /// — Depend on <see cref="IShapeRepository"/> when the consumer only needs shape
    ///   state and events (ViewModels, services, unit tests).<br/>
    /// — Depend on <c>ISketchBoardDataManager</c> in control/code-behind code that
    ///   must attach the visual tree or drive host-level scale updates.
    /// </para>
    /// </summary>
    public interface ISketchBoardDataManager : IShapeRepository
    {
        // ── WPF visual-host members (not on IShapeRepository) ───────────────────

        /// <summary>Reference to the host <see cref="ISketchBoard"/> WPF control.</summary>
        ISketchBoard? SketchBoard { get; }

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
        /// Prefer this over <c>IVisualHost.HostInitialized</c> when the subscriber already
        /// depends on <see cref="ISketchBoardDataManager"/> (same fire site; do not subscribe to both).
        /// </summary>
        event EventHandler<ISketchBoardDataManager> SketchBoardManagerInitialized;
    }
}
