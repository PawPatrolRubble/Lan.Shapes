#nullable enable

using System;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes.Interfaces;

namespace Lan.SketchBoard
{
    /// <summary>
    /// WPF-specific contract for the component that bridges a <see cref="IShapeRepository"/>
    /// to a WPF visual tree.
    /// Keeps <see cref="System.Windows.Media.VisualCollection"/> and related WPF types
    /// out of the core <c>Lan.Shapes</c> library.
    /// </summary>
    public interface IVisualHost
    {
        /// <summary>
        /// The WPF <see cref="System.Windows.Media.VisualCollection"/> that backs the
        /// sketch board's visual children. Populated by <see cref="InitializeVisualCollection"/>.
        /// </summary>
        VisualCollection VisualCollection { get; }

        /// <summary>Reference to the <see cref="ISketchBoard"/> WPF control.</summary>
        ISketchBoard SketchBoard { get; }

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
        /// </summary>
        event EventHandler<IShapeRepository> HostInitialized;
    }
}
