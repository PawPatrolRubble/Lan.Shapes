#nullable enable

namespace Lan.Shapes.Interfaces
{
    /// <summary>
    /// Marker for the WPF sketch-board host control.
    /// Implemented by <c>Lan.SketchBoard.SketchBoard</c>.
    /// <para>
    /// Mouse routing lives on the control itself (WPF <c>UIElement</c> events),
    /// not on a separate aspirational interface.
    /// </para>
    /// </summary>
    public interface ISketchBoard
    {
    }
}
