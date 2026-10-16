#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Lan.Shapes
{
    /// <summary>An opaque, in-memory model position captured for one shape.</summary>
    public sealed class ShapeTranslationState
    {
        internal ShapeTranslationState(ShapeVisualBase owner, object model, IReadOnlyList<(Point Location, string Content)> texts)
        {
            Owner = owner;
            Model = model;
            Texts = texts;
        }

        internal ShapeVisualBase Owner { get; }
        internal object Model { get; }
        internal IReadOnlyList<(Point Location, string Content)> Texts { get; }

        /// <summary>Compares model positions so a gesture returning to its origin creates no history step.</summary>
        public bool HasSameContent(ShapeTranslationState other)
            => other != null && ReferenceEquals(Owner, other.Owner)
                && Texts.SequenceEqual(other.Texts) && ModelEquals(Model, other.Model);

        internal static bool ModelEquals(object first, object second)
        {
            if (ReferenceEquals(first, second)) return true;
            if (first is Dictionary<int, Point> points && second is Dictionary<int, Point> otherPoints)
                return points.Count == otherPoints.Count
                    && points.All(pair => otherPoints.TryGetValue(pair.Key, out var value) && value == pair.Value);
            if (first is ITuple tuple && second is ITuple otherTuple)
            {
                if (tuple.Length != otherTuple.Length) return false;
                for (var index = 0; index < tuple.Length; index++)
                    if (!ModelEquals(tuple[index]!, otherTuple[index]!)) return false;
                return true;
            }
            if (first is Geometry geometry && second is Geometry otherGeometry)
                return geometry.ToString(CultureInfo.InvariantCulture) == otherGeometry.ToString(CultureInfo.InvariantCulture);
            return Equals(first, second);
        }
    }

    /// <summary>Exact model states surrounding one successful rigid translation; null states mean an unsupported custom snapshot.</summary>
    public sealed class ShapeTranslationEventArgs : EventArgs
    {
        public ShapeTranslationEventArgs(ShapeTranslationState? before, ShapeTranslationState? after)
            => (Before, After) = (before, after);
        public ShapeTranslationState? Before { get; }
        public ShapeTranslationState? After { get; }
    }
}
