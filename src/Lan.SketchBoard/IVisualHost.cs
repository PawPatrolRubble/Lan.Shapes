#nullable enable

using System;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes.Interfaces;

namespace Lan.SketchBoard
{
    /// <summary>
    /// Visual-host contract that bridges an <see cref="IShapeRepository"/> to the
    /// WPF visual tree used by <see cref="SketchBoard"/>.
    /// Separates visual-collection attachment and scale feedback from the
    /// shape-data API so hosts and ViewModels can depend on the narrower surface.
    /// This project is WPF-only; there is no non-WPF host target.
    /// </summary>
    public interface IVisualHost
    {
        /// <summary>
        /// The WPF <see cref="System.Windows.Media.VisualCollection"/> that backs the
        /// sketch board's visual children. Populated by <see cref="InitializeVisualCollection"/>.
        /// </summary>
        VisualCollection VisualCollection { get; }

        /// <summary>Reference to the <see cref="ISketchBoard"/> WPF control.</summary>
        ISketchBoard? SketchBoard { get; }

        /// <summary>
        /// Attaches this host to a WPF <see cref="Visual"/> (typically the <see cref="SketchBoard"/>
        /// canvas) and creates the backing <see cref="VisualCollection"/>.
        /// Must be called before any shapes can be rendered.
        /// </summary>
        void InitializeVisualCollection(Visual visual);

        /// <summary>
        /// Notifies the host that the image viewer's zoom scale has changed so that
        /// stroke thickness and drag handle sizes can be recalculated.
        /// </summary>
        void OnImageViewerPropertyChanged(double scale);

        /// <summary>
        /// Raised once after <see cref="InitializeVisualCollection"/> completes and the
        /// board is ready to receive shapes.
        /// Prefer this when the subscriber only depends on <see cref="IVisualHost"/> /
        /// <see cref="IShapeRepository"/>. Same fire site as
        /// <c>ISketchBoardDataManager.SketchBoardManagerInitialized</c> — subscribe to one, not both.
        /// </summary>
        event EventHandler<IShapeRepository> HostInitialized;
    }
}
