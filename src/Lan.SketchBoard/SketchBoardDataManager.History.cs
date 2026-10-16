#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Lan.Shapes;

namespace Lan.SketchBoard
{
    public partial class SketchBoardDataManager
    {
        private const int HistoryLimit = 100;
        private readonly List<HistoryStep> _undo = new();
        private readonly List<HistoryStep> _redo = new();
        private readonly List<HistoryStep> _pendingHistory = new();
        private int _historyTransactionDepth;
        private bool _replayingHistory;
        private bool _invalidHistoryTransaction;
        private readonly HashSet<ShapeVisualBase> _activeGeometryEdits = new();

        public bool CanUndo => !_replayingHistory && _historyTransactionDepth == 0 && _activeGeometryEdits.Count == 0 && _undo.Count > 0;
        public bool CanRedo => !_replayingHistory && _historyTransactionDepth == 0 && _activeGeometryEdits.Count == 0 && _redo.Count > 0;
        public event EventHandler? HistoryChanged;

        public void Undo() => Replay(_undo, _redo, undo: true);
        public void Redo() => Replay(_redo, _undo, undo: false);

        public void ClearHistory()
        {
            _undo.Clear();
            _redo.Clear();
            _pendingHistory.Clear();
            if (_historyTransactionDepth > 0) _invalidHistoryTransaction = true;
            NotifyHistoryChanged();
        }

        public IDisposable BeginHistoryTransaction()
        {
            if (_historyTransactionDepth++ == 0)
            {
                _invalidHistoryTransaction = false;
                NotifyHistoryChanged();
            }
            return new HistoryScope(this);
        }

        private void EndHistoryTransaction()
        {
            if (--_historyTransactionDepth != 0) return;
            var steps = _pendingHistory.Where(x => !x.IsEmpty).ToArray();
            _pendingHistory.Clear();
            if (!_invalidHistoryTransaction && steps.Length > 0)
                PushHistory(steps.Length == 1 ? steps[0] : new CompositeStep(steps));
            _invalidHistoryTransaction = false;
            NotifyHistoryChanged();
        }

        private void RecordHistory(HistoryStep step)
        {
            if (_replayingHistory || _invalidHistoryTransaction || step.IsEmpty) return;
            if (_historyTransactionDepth == 0)
            {
                PushHistory(step);
                return;
            }
            // Merge samples per shape inside a move gesture, preserving its exact first and last states.
            if (step is TranslationStep translation)
            {
                var existing = _pendingHistory.AsEnumerable().Reverse().TakeWhile(x => x is TranslationStep).OfType<TranslationStep>()
                    .LastOrDefault(x => ReferenceEquals(x.Shape, translation.Shape));
                if (existing != null) { existing.After = translation.After; return; }
            }
            _pendingHistory.Add(step);
        }

        private void PushHistory(HistoryStep step)
        {
            _redo.Clear();
            _undo.Add(step);
            if (_undo.Count > HistoryLimit) _undo.RemoveAt(0);
            NotifyHistoryChanged();
        }

        private void Replay(List<HistoryStep> source, List<HistoryStep> destination, bool undo)
        {
            if (_replayingHistory || _historyTransactionDepth != 0 || _activeGeometryEdits.Count != 0 || source.Count == 0) return;
            var step = source[source.Count - 1];
            _replayingHistory = true;
            try
            {
                if (undo) step.Undo(this); else step.Redo(this);
                source.RemoveAt(source.Count - 1);
                destination.Add(step);
            }
            catch
            {
                // A custom renderer can throw during restoration. Do not retain a stack that
                // assumes a successful replay when the board may have changed partially.
                _undo.Clear();
                _redo.Clear();
                throw;
            }
            finally
            {
                _replayingHistory = false;
                NotifyHistoryChanged();
            }
        }

        private void NotifyHistoryChanged()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        private void InvalidateHistory()
        {
            if (_replayingHistory) return;
            if (_undo.Count == 0 && _redo.Count == 0 && _pendingHistory.Count == 0 && _historyTransactionDepth == 0) return;
            ClearHistory();
        }

        private void InvalidateHistoryForShapeChange(ShapeVisualBase shape, string? property)
        {
            if (_replayingHistory || shape.IsTranslating || shape.IsRefreshingScale || shape.IsEditingGeometry) return;
            if (property is nameof(ShapeVisualBase.State) or nameof(ShapeVisualBase.ShapeStyler)
                or nameof(ShapeVisualBase.IsLocked) or nameof(ShapeVisualBase.IsSelected)
                or nameof(ShapeVisualBase.IsHovered)
                or nameof(ShapeVisualBase.IsBeingDraggedOrPanMoving)) return;
            InvalidateHistory();
        }

        private void OnShapeTranslationCompleted(object? sender, ShapeTranslationEventArgs args)
        {
            if (sender is not ShapeVisualBase shape || _replayingHistory) return;
            if (args.Before == null || args.After == null) { InvalidateHistory(); return; }
            RecordHistory(new TranslationStep(shape, args.Before, args.After));
        }

        private void OnShapeGeometryEditCompleted(object? sender, ShapeGeometryEventArgs args)
        {
            if (sender is not ShapeVisualBase shape) return;
            _activeGeometryEdits.Remove(shape);
            try
            {
                if (_replayingHistory) return;
                if (args.Before == null || args.After == null) { InvalidateHistory(); return; }
                RecordHistory(new GeometryStep(shape, args.Before, args.After));
            }
            finally { NotifyHistoryChanged(); }
        }

        private void OnShapeGeometryEditStarted(object? sender, EventArgs args)
        {
            if (sender is ShapeVisualBase shape) _activeGeometryEdits.Add(shape);
            NotifyHistoryChanged();
        }

        private DeletionStep? CaptureDeletion((ShapeVisualBase Shape, int Index)[] removed)
        {
            if (_replayingHistory || removed.Length == 0) return null;
            var completed = removed.Where(x => x.Shape.IsGeometryRendered).ToArray();
            if (completed.Length == 0) return null;
            return new DeletionStep(completed, _groups.ToArray(), _selection.ToArray());
        }

        private void CompleteDeletion(DeletionStep? step)
        {
            if (step == null) return;
            step.SelectionAfter = _selection.ToArray();
            RecordHistory(step);
        }

        private void RestoreGroups(IEnumerable<ShapeGroup> groups)
        {
            var changed = false;
            foreach (var group in groups)
            {
                if (_groups.Contains(group) || group.Members.Any(x => !Shapes.Contains(x))) continue;
                foreach (var conflict in group.Members.Select(GetGroup).Where(x => x != null).Distinct().ToArray())
                    RemoveGroup(conflict!);
                _groups.Add(group);
                foreach (var shape in group.Members) _groupsByShape[shape] = group;
                changed = true;
            }
            if (changed) GroupsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RestoreHistorySelection(IEnumerable<ShapeVisualBase> selection)
            => ApplySelection(selection.Where(x => Shapes.Contains(x) && x.IsGeometryRendered
                && IsLayerVisible(x.ShapeLayer.LayerId)).ToList());

        private sealed class HistoryScope : IDisposable
        {
            private SketchBoardDataManager? _owner;
            public HistoryScope(SketchBoardDataManager owner) => _owner = owner;
            public void Dispose()
            {
                var owner = _owner;
                _owner = null;
                owner?.EndHistoryTransaction();
            }
        }

        private abstract class HistoryStep
        {
            public virtual bool IsEmpty => false;
            public abstract void Undo(SketchBoardDataManager owner);
            public abstract void Redo(SketchBoardDataManager owner);
        }

        private sealed class TranslationStep : HistoryStep
        {
            public TranslationStep(ShapeVisualBase shape, ShapeTranslationState before, ShapeTranslationState after)
                => (Shape, Before, After) = (shape, before, after);
            public ShapeVisualBase Shape { get; }
            private ShapeTranslationState Before { get; }
            public ShapeTranslationState After { get; set; }
            public override bool IsEmpty => Before.HasSameContent(After);
            public override void Undo(SketchBoardDataManager owner) => Shape.RestoreTranslationState(Before);
            public override void Redo(SketchBoardDataManager owner) => Shape.RestoreTranslationState(After);
        }

        private sealed class GeometryStep : HistoryStep
        {
            private readonly ShapeVisualBase _shape;
            private readonly ShapeGeometryState _before;
            private readonly ShapeGeometryState _after;
            public GeometryStep(ShapeVisualBase shape, ShapeGeometryState before, ShapeGeometryState after)
                => (_shape, _before, _after) = (shape, before, after);
            public override bool IsEmpty => _before.HasSameContent(_after);
            public override void Undo(SketchBoardDataManager owner) => _shape.RestoreGeometryState(_before);
            public override void Redo(SketchBoardDataManager owner) => _shape.RestoreGeometryState(_after);
        }

        private sealed class DeletionStep : HistoryStep
        {
            private readonly (ShapeVisualBase Shape, int Index)[] _removed;
            private readonly ShapeGroup[] _groupsBefore;
            private readonly ShapeVisualBase[] _selectionBefore;
            public ShapeVisualBase[] SelectionAfter { get; set; } = Array.Empty<ShapeVisualBase>();
            public DeletionStep((ShapeVisualBase Shape, int Index)[] removed, ShapeGroup[] groups, ShapeVisualBase[] selection)
                => (_removed, _groupsBefore, _selectionBefore) = (removed, groups, selection);
            public override void Undo(SketchBoardDataManager owner)
            {
                foreach (var entry in _removed.OrderBy(x => x.Index))
                    owner.AddShape(entry.Shape, Math.Min(entry.Index, owner.Shapes.Count));
                owner.RestoreGroups(_groupsBefore);
                owner.RestoreHistorySelection(_selectionBefore);
            }
            public override void Redo(SketchBoardDataManager owner)
            {
                foreach (var entry in _removed) owner.RemoveShape(entry.Shape);
                owner.RestoreHistorySelection(SelectionAfter);
            }
        }

        private sealed class CompositeStep : HistoryStep
        {
            private readonly HistoryStep[] _steps;
            public CompositeStep(HistoryStep[] steps) => _steps = steps;
            public override void Undo(SketchBoardDataManager owner)
            {
                foreach (var step in _steps.Reverse()) step.Undo(owner);
            }
            public override void Redo(SketchBoardDataManager owner)
            {
                foreach (var step in _steps) step.Redo(owner);
            }
        }
    }
}
