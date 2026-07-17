#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using Lan.Shapes;

namespace Lan.SketchBoard
{
    /// <summary>
    /// Builds and caches the constructor delegates used to create registered shapes.
    /// This keeps reflection and shape-constructor validation out of the data manager.
    /// </summary>
    internal sealed class ShapeFactory
    {
        private readonly ConcurrentDictionary<Type, Func<ShapeLayer, ShapeVisualBase>> _factories =
            new ConcurrentDictionary<Type, Func<ShapeLayer, ShapeVisualBase>>();

        public void Validate(Type shapeType)
        {
            if (shapeType == null)
            {
                throw new ArgumentNullException(nameof(shapeType));
            }

            _factories.GetOrAdd(shapeType, BuildFactory);
        }

        public ShapeVisualBase Create(Type shapeType, ShapeLayer layer)
        {
            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            Validate(shapeType);
            return _factories[shapeType](layer);
        }

        private static Func<ShapeLayer, ShapeVisualBase> BuildFactory(Type shapeType)
        {
            if (!typeof(ShapeVisualBase).IsAssignableFrom(shapeType) ||
                shapeType.IsAbstract ||
                shapeType.ContainsGenericParameters)
            {
                throw new ArgumentException(
                    $"Type '{shapeType.FullName}' must be a concrete {nameof(ShapeVisualBase)}.",
                    nameof(shapeType));
            }

            var constructor = shapeType.GetConstructor(new[] { typeof(ShapeLayer) });
            if (constructor == null)
            {
                throw new ArgumentException(
                    $"Shape type '{shapeType.FullName}' must expose a public constructor accepting {nameof(ShapeLayer)}.",
                    nameof(shapeType));
            }

            var layer = Expression.Parameter(typeof(ShapeLayer), "layer");
            var create = Expression.New(constructor, layer);
            var cast = Expression.Convert(create, typeof(ShapeVisualBase));
            return Expression.Lambda<Func<ShapeLayer, ShapeVisualBase>>(cast, layer).Compile();
        }
    }
}
