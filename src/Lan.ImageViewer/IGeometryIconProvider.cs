#nullable enable
using System.Windows.Media;

namespace Lan.ImageViewer
{
    /// <summary>
    /// Resolves toolbar icons for registered geometry type names.
    /// Composition roots supply a resource-backed or test stub implementation so
    /// ViewModels do not hard-code icon dictionaries.
    /// </summary>
    public interface IGeometryIconProvider
    {
        /// <summary>
        /// Returns the icon geometry for <paramref name="geometryTypeName"/>,
        /// or <c>null</c> when no icon is registered.
        /// </summary>
        Geometry? GetIcon(string geometryTypeName);
    }
}
