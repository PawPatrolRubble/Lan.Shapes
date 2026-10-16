#nullable enable
using System;

namespace Lan.Shapes.Interfaces
{
    /// <summary>Optional, board-local undo/redo for shape deletion, translation and geometry editing.</summary>
    public interface IShapeHistory
    {
        bool CanUndo { get; }
        bool CanRedo { get; }
        event EventHandler? HistoryChanged;
        void Undo();
        void Redo();
        void ClearHistory();

        /// <summary>Coalesces changes into one step until the outermost scope is disposed.</summary>
        IDisposable BeginHistoryTransaction();
    }
}
