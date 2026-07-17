#nullable enable
using System;
using System.Windows;
using System.Windows.Media;

namespace Lan.ImageViewer
{
    /// <summary>
    /// Reads geometry toolbar icons from a WPF <see cref="ResourceDictionary"/>
    /// (typically <c>Geometries.xaml</c>). Safe to construct under headless tests:
    /// pack URI load failures yield an empty dictionary and null icons.
    /// </summary>
    public sealed class ResourceDictionaryGeometryIconProvider : IGeometryIconProvider
    {
        private readonly ResourceDictionary _resources;

        /// <summary>
        /// Loads icons from the default <c>Lan.ImageViewer</c> geometries resource.
        /// </summary>
        public ResourceDictionaryGeometryIconProvider()
            : this(TryLoadDefaultResources())
        {
        }

        /// <summary>
        /// Uses an already-loaded dictionary (tests / custom themes).
        /// </summary>
        public ResourceDictionaryGeometryIconProvider(ResourceDictionary resources)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        /// <inheritdoc />
        public Geometry? GetIcon(string geometryTypeName)
        {
            if (string.IsNullOrWhiteSpace(geometryTypeName))
            {
                return null;
            }

            if (TryGet(geometryTypeName, out var geometry))
            {
                return geometry;
            }

            // Type name → resource key aliases used by Geometries.xaml.
            return geometryTypeName switch
            {
                "GriddedRectangle" or "GridGeometry" when TryGet("Grid", out geometry) => geometry,
                "ArrowedLine" when TryGet("Line", out geometry) => geometry,
                "Fiber" when TryGet("Line", out geometry) => geometry,
                "Cross" when TryGet("ThickenedCross", out geometry) => geometry,
                "DxfGeometry" when TryGet("Save", out geometry) => geometry,
                _ => null
            };
        }

        private bool TryGet(string key, out Geometry? geometry)
        {
            if (_resources.Contains(key) && _resources[key] is Geometry g)
            {
                geometry = g;
                return true;
            }

            geometry = null;
            return false;
        }

        private static ResourceDictionary TryLoadDefaultResources()
        {
            try
            {
                return new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/Lan.ImageViewer;component/Geometries.xaml",
                        UriKind.Absolute)
                };
            }
            catch (Exception)
            {
                return new ResourceDictionary();
            }
        }
    }
}
