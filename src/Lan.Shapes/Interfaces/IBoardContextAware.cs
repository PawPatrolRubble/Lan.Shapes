#nullable enable

namespace Lan.Shapes.Interfaces
{
    /// <summary>
    /// Implemented by shapes that require board dimensions after creation.
    /// <see cref="ISketchBoardDataManager"/> detects this interface and calls
    /// <see cref="OnBoardContextAvailable"/> instead of hard-coding shape-specific logic.
    /// </summary>
    public interface IBoardContextAware
    {
        /// <summary>
        /// Called by the sketch board manager immediately after the shape is added to the board.
        /// </summary>
        /// <param name="boardWidth">Current width of the sketch board.</param>
        /// <param name="boardHeight">Current height of the sketch board.</param>
        void OnBoardContextAvailable(double boardWidth, double boardHeight);
    }
}
