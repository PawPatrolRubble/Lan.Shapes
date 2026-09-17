#nullable enable

namespace Lan.Shapes.Interfaces
{
    /// <summary>
    /// Implemented by shapes that require image/canvas dimensions after creation.
    /// <see cref="ISketchBoardDataManager"/> detects this interface and calls
    /// <see cref="OnBoardContextAvailable"/> instead of hard-coding shape-specific logic.
    /// </summary>
    public interface IBoardContextAware
    {
        /// <summary>
        /// Called by the sketch board manager immediately after the shape is added to the board.
        /// </summary>
        /// <param name="boardWidth">Image width in pixels, or board width when no bitmap is available.</param>
        /// <param name="boardHeight">Image height in pixels, or board height when no bitmap is available.</param>
        void OnBoardContextAvailable(double boardWidth, double boardHeight);
    }
}
