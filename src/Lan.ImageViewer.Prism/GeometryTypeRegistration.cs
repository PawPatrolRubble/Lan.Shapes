using System;
using System.Collections.Generic;
using System.Linq;
using Lan.ImageViewer;
using Lan.Shapes.Custom;
using Lan.Shapes.DialogGeometry;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;
using Microsoft.Extensions.Configuration;

#nullable enable

namespace Lan.ImageViewer.Prism
{
    /// <summary>
    /// Catalog of geometry types the Prism host can register.
    /// Hosts choose a subset via configuration; the view-model palette
    /// mirrors whatever is registered on <see cref="IGeometryTypeManager"/>.
    /// </summary>
    public static class GeometryTypeRegistration
    {
        private static readonly (string Name, Type Type)[] DefaultEntries =
        {
            (nameof(GridGeometry), typeof(GridGeometry)),
            (nameof(GriddedRectangle), typeof(GriddedRectangle)),
            (nameof(ThickenedCircle), typeof(ThickenedCircle)),
            (nameof(ThickenedCross), typeof(ThickenedCross)),
            (nameof(ThickenedRectangle), typeof(ThickenedRectangle)),
            (nameof(ThickenedLine), typeof(ThickenedLine)),
            (nameof(ArrowedLine), typeof(ArrowedLine)),
            (nameof(Circle), typeof(Circle)),
            (nameof(FixedCenterCircle), typeof(FixedCenterCircle)),
            (nameof(Cross), typeof(Cross)),
            (nameof(Line), typeof(Line)),
            (nameof(Rectangle), typeof(Rectangle)),
            (nameof(Rectangle2), typeof(Rectangle2)),
            (nameof(Fiber), typeof(Fiber)),
            (nameof(DxfGeometry), typeof(DxfGeometry)),
        };

        private static readonly Dictionary<string, Type> CatalogByName =
            DefaultEntries.ToDictionary(x => x.Name, x => x.Type, StringComparer.OrdinalIgnoreCase);

        /// <summary>Canonical type-name → CLR type map (ordinal, insertion order).</summary>
        public static IReadOnlyDictionary<string, Type> Catalog { get; } =
            new Dictionary<string, Type>(
                DefaultEntries.Select(x => new KeyValuePair<string, Type>(x.Name, x.Type)),
                StringComparer.Ordinal);

        /// <summary>
        /// Registers every type in <see cref="Catalog"/>.
        /// Used when configuration does not list a subset.
        /// </summary>
        public static void RegisterDefaultGeometryTypes(IGeometryTypeManager geometryTypeManager)
        {
            RegisterGeometryTypes(
                geometryTypeManager,
                DefaultEntries.Select(x => x.Name));
        }

        /// <summary>
        /// Registers the named catalog types. Empty / null
        /// <paramref name="geometryTypeNames"/> falls back to the full catalog.
        /// Unknown names throw.
        /// </summary>
        public static void RegisterGeometryTypes(
            IGeometryTypeManager geometryTypeManager,
            IEnumerable<string>? geometryTypeNames)
        {
            if (geometryTypeManager == null)
            {
                throw new ArgumentNullException(nameof(geometryTypeManager));
            }

            var names = NormalizeNames(geometryTypeNames);
            if (names.Count == 0)
            {
                names = DefaultEntries.Select(x => x.Name).ToList();
            }

            foreach (var name in names)
            {
                if (!CatalogByName.TryGetValue(name, out var geometryType))
                {
                    throw new InvalidOperationException(
                        $"Unknown geometry type '{name}'. Known types: {string.Join(", ", Catalog.Keys)}.");
                }

                geometryTypeManager.RegisterGeometryType(geometryType.Name, geometryType);
            }
        }

        /// <summary>
        /// Registers types listed under configuration key <c>geometryTypes</c>.
        /// Missing or empty section → full catalog.
        /// </summary>
        public static void RegisterFromConfiguration(
            IGeometryTypeManager geometryTypeManager,
            IConfiguration? configuration)
        {
            RegisterGeometryTypes(
                geometryTypeManager,
                ReadConfiguredGeometryTypeNames(configuration));
        }

        /// <summary>
        /// Reads <c>geometryTypes</c> as a string array from
        /// <paramref name="configuration"/>. Missing section → empty list.
        /// </summary>
        public static IReadOnlyList<string> ReadConfiguredGeometryTypeNames(IConfiguration? configuration)
        {
            if (configuration == null)
            {
                return Array.Empty<string>();
            }

            return configuration.GetSection("geometryTypes")
                .GetChildren()
                .Select(child => child.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToArray();
        }

        private static List<string> NormalizeNames(IEnumerable<string>? geometryTypeNames)
        {
            if (geometryTypeNames == null)
            {
                return new List<string>();
            }

            return geometryTypeNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .ToList();
        }
    }
}
