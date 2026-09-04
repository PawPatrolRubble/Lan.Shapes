using System.Collections.ObjectModel;

namespace Lan.Shapes.Interfaces
{
    public interface IShapeLayerManager
    {
        /// <summary>
        /// Global Lan.Shapes configuration currently used by the layer collection.
        /// </summary>
        LanShapesConfiguration Configuration { get; }

        /// <summary>
        /// Reads global measurement settings and shape layers from one config file.
        /// </summary>
        void ReadConfiguration(string configurationFilePath);

        /// <summary>
        /// Persists global measurement settings and current shape layers.
        /// </summary>
        void SaveConfiguration(string filePath = "");

        /// <summary>
        /// Compatibility alias for <see cref="ReadConfiguration"/>.
        /// </summary>
        [System.Obsolete("Use ReadConfiguration.")]
        void ReadShapeLayers(string configurationFilePath = "");
        
        /// <summary>
        /// get all shape layers
        /// </summary>
        ObservableCollection<ShapeLayer> Layers { get; }

        /// <summary>
        /// Compatibility alias for <see cref="SaveConfiguration"/>.
        /// </summary>
        [System.Obsolete("Use SaveConfiguration.")]
        void SaveLayerConfigurations(string filePath = "");

    }
}
