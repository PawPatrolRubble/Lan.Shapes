#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lan.Shapes;
using Lan.Shapes.Interfaces;

namespace Lan.ImageViewer
{
    /// <summary>
    /// Explicit registry for the geometry types exposed by an application.
    /// </summary>
    public class GeometryTypeManager : IGeometryTypeManager
    {
        private readonly Dictionary<string, Type> _registeredShapeTypes =
            new Dictionary<string, Type>(StringComparer.Ordinal);

        public void RegisterGeometryType(string geometryName, Type geometryType)
        {
            if (string.IsNullOrWhiteSpace(geometryName))
            {
                throw new ArgumentException("A geometry name is required.", nameof(geometryName));
            }

            ValidateGeometryType(geometryType);

            if (_registeredShapeTypes.TryGetValue(geometryName, out var registeredType))
            {
                if (registeredType == geometryType)
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Geometry name '{geometryName}' is already registered for '{registeredType.FullName}'.");
            }

            _registeredShapeTypes.Add(geometryName, geometryType);
        }

        public void RegisterGeometryType<T>() where T : ShapeVisualBase
        {
            RegisterGeometryType(typeof(T).Name, typeof(T));
        }

        public IEnumerable<string> GetRegisteredGeometryTypes()
        {
            return _registeredShapeTypes.Keys.ToArray();
        }

        public Type GetGeometryTypeByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A geometry name is required.", nameof(name));
            }

            if (_registeredShapeTypes.TryGetValue(name, out var geometryType))
            {
                return geometryType;
            }

            throw new KeyNotFoundException($"Geometry type '{name}' is not registered.");
        }

        /// <summary>
        /// Discovers concrete shape types from currently loaded assemblies.
        /// Explicit registration remains the preferred path for application startup.
        /// </summary>
        public void ReadGeometryTypesFromAssembly()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var geometryType in GetLoadableTypes(assembly).Where(IsConcreteShapeType))
                {
                    RegisterGeometryType(geometryType.Name, geometryType);
                }
            }
        }

        private static bool IsConcreteShapeType(Type type)
        {
            return type.IsClass &&
                   !type.IsAbstract &&
                   !type.ContainsGenericParameters &&
                   typeof(ShapeVisualBase).IsAssignableFrom(type);
        }

        private static void ValidateGeometryType(Type geometryType)
        {
            if (geometryType == null)
            {
                throw new ArgumentNullException(nameof(geometryType));
            }

            if (!IsConcreteShapeType(geometryType))
            {
                throw new ArgumentException(
                    $"Type '{geometryType.FullName}' must be a concrete {nameof(ShapeVisualBase)}.",
                    nameof(geometryType));
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.OfType<Type>();
            }
        }
    }
}
