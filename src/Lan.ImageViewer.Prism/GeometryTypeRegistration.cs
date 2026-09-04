#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Lan.Shapes.Custom;
using Lan.Shapes.DialogGeometry;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;

namespace Lan.ImageViewer.Prism
{
    /// <summary>
    /// Registers the geometry set shared by the Prism host and the sample WPF hosts.
    /// Host-specific extensions can register additional types after this call.
    /// </summary>
    public static class GeometryTypeRegistration
    {
        /// <summary>
        /// Known geometry types that a host may enable. Order is the default palette order.
        /// </summary>
        public static IReadOnlyDictionary<string, Type> Catalog { get; } = CreateCatalog();

        public static void RegisterDefaultGeometryTypes(IGeometryTypeManager geometryTypeManager)
        {
            RegisterGeometryTypes(geometryTypeManager, names: null);
        }

        /// <summary>
        /// Registers geometry types from <paramref name="names"/>.
        /// <c>null</c> registers the full <see cref="Catalog"/>;
        /// an empty sequence registers nothing.
        /// </summary>
        public static void RegisterGeometryTypes(
            IGeometryTypeManager geometryTypeManager,
            IEnumerable<string>? names)
        {
            if (geometryTypeManager == null)
            {
                throw new ArgumentNullException(nameof(geometryTypeManager));
            }

            var requested = names == null
                ? Catalog.Keys.ToArray()
                : NormalizeNames(names);

            if (names != null)
            {
                var unknown = requested.Where(name => !Catalog.ContainsKey(name)).ToArray();
                if (unknown.Length > 0)
                {
                    throw new ArgumentException(
                        $"Unknown geometry type(s): {string.Join(", ", unknown)}.",
                        nameof(names));
                }
            }

            foreach (var name in requested)
            {
                geometryTypeManager.RegisterGeometryType(name, Catalog[name]);
            }
        }

        private static string[] NormalizeNames(IEnumerable<string> names)
        {
            return names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .ToArray();
        }

        private static IReadOnlyDictionary<string, Type> CreateCatalog()
        {
            Type[] types =
            {
                typeof(GridGeometry),
                typeof(GriddedRectangle),
                typeof(ThickenedCircle),
                typeof(ThickenedCross),
                typeof(ThickenedRectangle),
                typeof(ThickenedLine),
                typeof(ArrowedLine),
                typeof(Circle),
                typeof(FixedCenterCircle),
                typeof(Cross),
                typeof(Line),
                typeof(Rectangle),
                typeof(Fiber),
                typeof(DxfGeometry),
            };

            return types.ToDictionary(type => type.Name, type => type, StringComparer.Ordinal);
        }
    }
}
