#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Lan.Shapes
{
    /// <summary>
    /// A logical selection and movement unit. Members remain independent shapes
    /// in their original visual order; groups never contain other groups.
    /// </summary>
    public sealed class ShapeGroup
    {
        public ShapeGroup(IEnumerable<ShapeVisualBase> members, string? name = null)
        {
            if (members == null) throw new ArgumentNullException(nameof(members));
            var shapes = members.Distinct().ToList();
            if (shapes.Count < 2 || shapes.Any(x => x == null))
                throw new ArgumentException("A group must contain at least two distinct shapes.", nameof(members));
            Id = Guid.NewGuid();
            Name = string.IsNullOrWhiteSpace(name) ? "组合" : name;
            Members = new ReadOnlyCollection<ShapeVisualBase>(shapes);
        }

        public Guid Id { get; }
        public string Name { get; }
        public IReadOnlyList<ShapeVisualBase> Members { get; }
    }
}
