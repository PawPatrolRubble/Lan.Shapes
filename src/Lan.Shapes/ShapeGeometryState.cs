#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Lan.Shapes
{
    /// <summary>An opaque, exact model snapshot for a completed shape geometry edit.</summary>
    public sealed class ShapeGeometryState
    {
        internal ShapeGeometryState(ShapeVisualBase owner, object model, IReadOnlyList<(Point Location, string Content)> texts)
            => (Owner, Model, Texts) = (owner, model, texts);

        internal ShapeVisualBase Owner { get; }
        internal object Model { get; }
        internal IReadOnlyList<(Point Location, string Content)> Texts { get; }

        /// <summary>Compares model values, excluding selection and pointer interaction state.</summary>
        public bool HasSameContent(ShapeGeometryState other)
            => other != null && ReferenceEquals(Owner, other.Owner)
                && Texts.SequenceEqual(other.Texts) && ShapeTranslationState.ModelEquals(Model, other.Model);
    }
}
