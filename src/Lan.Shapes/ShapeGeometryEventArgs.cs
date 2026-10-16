#nullable enable
using System;

namespace Lan.Shapes
{
    /// <summary>Model snapshots surrounding a completed geometry edit.</summary>
    public sealed class ShapeGeometryEventArgs : EventArgs
    {
        public ShapeGeometryEventArgs(ShapeGeometryState? before, ShapeGeometryState? after)
            => (Before, After) = (before, after);
        public ShapeGeometryState? Before { get; }
        public ShapeGeometryState? After { get; }
    }
}
