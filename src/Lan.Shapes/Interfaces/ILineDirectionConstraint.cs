using System.Windows;

namespace Lan.Shapes.Interfaces
{
    /// <summary>Opt-in for shapes whose active endpoint can be constrained to an axis.</summary>
    public interface ILineDirectionConstraint
    {
        /// <summary>The stationary endpoint in model coordinates, or null when translating or adjusting another property.</summary>
        Point? ConstraintOrigin { get; }
    }
}
