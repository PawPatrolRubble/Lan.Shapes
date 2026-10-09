#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Lan.Shapes;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;

namespace Lan.ImageViewer.ViewModels.GeometryEditing;

/// <summary>Owns the lifetime, eligibility and drafts for one selected shape.</summary>
public abstract class ShapeGeometryEditor : INotifyPropertyChanged, IDisposable
{
    private double[] _snapshot = Array.Empty<double>();
    private bool _disposed;
    private bool _applying;
    private DispatcherOperation? _refreshOperation;
    private bool _canEdit;
    private string _unavailableReason = string.Empty;
    private string _statusMessage = string.Empty;
    private Transform? _observedTransform;
    private Matrix _observedTransformMatrix = Matrix.Identity;

    protected ShapeGeometryEditor(ShapeVisualBase target, IShapeRepository repository)
    {
        TargetShape = target;
        Repository = repository;
        target.Dispatcher.VerifyAccess();
    }

    public ShapeVisualBase TargetShape { get; }
    protected IShapeRepository Repository { get; }
    public IReadOnlyList<GeometryPropertyGroup> Groups { get; private set; } = Array.Empty<GeometryPropertyGroup>();
    public IReadOnlyList<GeometryReadout> Readouts { get; private set; } = Array.Empty<GeometryReadout>();
    public bool CanEdit => _canEdit;
    public string UnavailableReason => _unavailableReason;
    public string StatusMessage => _statusMessage;
    public int Revision { get; private set; }

    public static ShapeGeometryEditor? Create(ShapeVisualBase target, IShapeRepository repository)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(repository);
        var type = target.GetType();
        if (type == typeof(Line)) return new LineGeometryEditor((Line)target, repository);
        if (type == typeof(Rectangle)) return new RectangleGeometryEditor((Rectangle)target, repository);
        if (type == typeof(Circle)) return new CircleGeometryEditor((Circle)target, repository);
        return null;
    }

    protected void Initialize(GeometryPropertyGroup[] groups, params GeometryReadout[] readouts)
    {
        Groups = Array.AsReadOnly(groups);
        Readouts = Array.AsReadOnly(readouts);
        _snapshot = ReadSnapshot();
        foreach (var group in Groups) group.Load();
        UpdateAvailability();
        TargetShape.PropertyChanged += ShapeChanged;
        Repository.SelectionChanged += SelectionChanged;
        Repository.GroupsChanged += RepositoryChanged;
        Repository.LayerAssignmentsChanged += RepositoryChanged;
        Repository.Shapes.CollectionChanged += ShapesChanged;
        if (Repository is INotifyPropertyChanged notifier) notifier.PropertyChanged += RepositoryPropertyChanged;
        ObserveTransform();
        CompositionTarget.Rendering += Rendering;
    }

    public bool Commit(GeometryPropertyGroup group)
    {
        TargetShape.Dispatcher.VerifyAccess();
        if (_disposed || !Groups.Contains(group)) return false;
        group.Invalidate();
        UpdateAvailability();
        if (!group.IsEnabled)
        {
            group.SetError(CanEdit ? "请先设置有效端点，再修改长度。" : UnavailableReason);
            return false;
        }
        var current = ReadSnapshot();
        if (!_snapshot.SequenceEqual(current))
        {
            Refresh();
            SetStatus("图形已更新，请重新输入。");
            return false;
        }
        if (!group.IsDirty)
        {
            group.SetError(string.Empty);
            return true;
        }
        try
        {
            _applying = true;
            if (!group.TryApply()) return false;
            _snapshot = ReadSnapshot();
            foreach (var other in Groups)
                if (ReferenceEquals(other, group) || !other.IsDirty) other.Load();
            SetStatus(string.Empty);
            UpdateAvailability();
            foreach (var readout in Readouts) readout.Refresh();
            return true;
        }
        catch (ArgumentException)
        {
            group.SetError("数值必须有限，长度和尺寸须大于 0，且不能超出几何范围。");
            return false;
        }
        catch (InvalidOperationException)
        {
            UpdateAvailability();
            group.SetError(CanEdit ? "图形状态已改变，请重新输入。" : UnavailableReason);
            return false;
        }
        finally { _applying = false; }
    }

    public void Reset(GeometryPropertyGroup group)
    {
        TargetShape.Dispatcher.VerifyAccess();
        if (_disposed || !Groups.Contains(group)) return;
        if (!_snapshot.SequenceEqual(ReadSnapshot())) Refresh();
        else group.Load();
        SetStatus(string.Empty);
    }

    private void Refresh()
    {
        if (_disposed) return;
        var current = ReadSnapshot();
        if (!_snapshot.SequenceEqual(current))
        {
            var hadDraft = Groups.Any(group => group.IsDirty);
            _snapshot = current;
            foreach (var group in Groups) group.Load();
            Revision++;
            if (hadDraft) SetStatus("图形已更新，请重新输入。");
            foreach (var readout in Readouts) readout.Refresh();
        }
        UpdateAvailability();
    }

    private void UpdateAvailability()
    {
        var reason = GetUnavailableReason();
        var canEdit = reason.Length == 0;
        if (_canEdit != canEdit)
        {
            _canEdit = canEdit;
            Revision++;
            OnPropertyChanged(nameof(CanEdit));
        }
        if (_unavailableReason != reason)
        {
            _unavailableReason = reason;
            OnPropertyChanged(nameof(UnavailableReason));
        }
        foreach (var group in Groups) group.UpdateAvailability(canEdit);
    }

    private string GetUnavailableReason()
    {
        if (_disposed) return "编辑器已关闭。";
        if (!Repository.Shapes.Contains(TargetShape)) return "图形已移除。";
        if (Repository.SelectedGeometries.Count != 1 || !ReferenceEquals(Repository.SelectedGeometries[0], TargetShape))
            return "请选择一个独立图形。";
        if (Repository.GetGroup(TargetShape) != null) return "请先取消组合，再编辑几何。";
        if (!TargetShape.IsGeometryRendered) return "请先完成图形绘制。";
        if (TargetShape.IsLocked) return "图形已锁定，请先解锁。";
        if (!Repository.IsLayerVisible(TargetShape.ShapeLayer.LayerId)) return "图形所在图层已隐藏。";
        if (TargetShape.Transform != null && !TargetShape.Transform.Value.IsIdentity)
            return "该图形带独立变换，暂不支持图像坐标编辑。";
        return string.Empty;
    }

    private void ShapeChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_applying) return;
        if (args.PropertyName is nameof(ShapeVisualBase.IsLocked) or nameof(ShapeVisualBase.IsGeometryRendered)
            or nameof(ShapeVisualBase.CanTranslate))
        {
            Revision++;
            foreach (var group in Groups) group.Invalidate();
        }
        ScheduleRefresh();
    }

    private void SelectionChanged(object? sender, EventArgs args)
    {
        Revision++;
        foreach (var group in Groups) group.Load();
        UpdateAvailability();
        ScheduleRefresh();
    }

    private void RepositoryChanged(object? sender, EventArgs args)
    {
        Revision++;
        foreach (var group in Groups) group.Invalidate();
        ScheduleRefresh();
    }

    private void ShapesChanged(object? sender, NotifyCollectionChangedEventArgs args) => RepositoryChanged(sender, EventArgs.Empty);
    private void RepositoryPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        // A custom repository need not expose the concrete manager's revision property.
        if (string.IsNullOrEmpty(args.PropertyName)
            || args.PropertyName == nameof(ISketchBoardDataManager.LayerVisibilityRevision))
            RepositoryChanged(sender, EventArgs.Empty);
    }

    private void ScheduleRefresh()
    {
        if (_disposed || _refreshOperation?.Status == DispatcherOperationStatus.Pending) return;
        _refreshOperation = TargetShape.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _refreshOperation = null;
            Refresh();
        }));
    }

    private bool ObserveTransform()
    {
        var transform = TargetShape.Transform;
        var matrix = transform?.Value ?? Matrix.Identity;
        if (ReferenceEquals(transform, _observedTransform) && matrix == _observedTransformMatrix) return false;
        if (_observedTransform is { IsFrozen: false }) _observedTransform.Changed -= TransformChanged;
        _observedTransform = transform;
        _observedTransformMatrix = matrix;
        if (transform is { IsFrozen: false }) transform.Changed += TransformChanged;
        return true;
    }

    private void TransformChanged(object? sender, EventArgs args)
    {
        if (_disposed) return;
        ObserveTransform();
        InvalidateTransform();
    }

    private void Rendering(object? sender, EventArgs args)
    {
        // DrawingVisual.Transform is a CLR property: replacements have no property notification.
        // Only compare this one matrix per displayed frame; full geometry refresh remains event driven.
        if (!_disposed && ObserveTransform()) InvalidateTransform();
    }

    private void InvalidateTransform()
    {
        Revision++;
        foreach (var group in Groups) group.Invalidate();
        UpdateAvailability();
    }

    private void SetStatus(string status)
    {
        if (_statusMessage == status) return;
        _statusMessage = status;
        OnPropertyChanged(nameof(StatusMessage));
    }

    protected abstract double[] ReadSnapshot();

    protected static GeometryInputField Field(string label, string automationName) => new(label, automationName);
    protected static string Format(double value) => value.ToString("F0", CultureInfo.CurrentCulture);
    protected static void RequirePositive(double value)
    {
        if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
    }

    protected static void RequirePoint(Point point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentOutOfRangeException(nameof(point));
    }

    protected static void RequireBounds(Rect bounds)
    {
        if (bounds.IsEmpty) throw new ArgumentOutOfRangeException(nameof(bounds));
        RequirePoint(bounds.TopLeft);
        RequirePoint(bounds.BottomRight);
        RequirePositive(bounds.Width);
        RequirePositive(bounds.Height);
        if (bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top) throw new ArgumentOutOfRangeException(nameof(bounds));
    }

    protected static void RequireRepresentable(double actual, double expected)
    {
        if (!double.IsFinite(actual) || !double.IsFinite(expected)) throw new ArgumentOutOfRangeException(nameof(expected));
        if (actual == expected) return;
        // Allow ordinary double rounding, while rejecting cancellation which changes position or size.
        const double relativeRoundingTolerance = 8 * 2.2204460492503131e-16;
        var scale = Math.Max(Math.Abs(actual), Math.Abs(expected));
        if (Math.Abs(actual - expected) / scale > relativeRoundingTolerance)
            throw new ArgumentOutOfRangeException(nameof(expected), "The requested geometry cannot be represented by this displacement.");
    }

    protected void Move(Vector delta)
    {
        if (!double.IsFinite(delta.X) || !double.IsFinite(delta.Y)) throw new ArgumentOutOfRangeException(nameof(delta));
        if (delta.X == 0 && delta.Y == 0) return;
        if (!TargetShape.CanTranslate) throw new InvalidOperationException("图形不支持移动。");
        // Preflight model bounds here; the repository validates attached text for every member too.
        foreach (var point in TargetShape.GetSnapPoints()) RequirePoint(point + delta);
        var translatedBounds = TargetShape.RenderGeometry.Bounds;
        translatedBounds.Offset(delta);
        RequirePoint(translatedBounds.TopLeft);
        RequirePoint(translatedBounds.BottomRight);
        Repository.TranslateShapes(new[] { TargetShape }, delta);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Revision++;
        _refreshOperation?.Abort();
        TargetShape.PropertyChanged -= ShapeChanged;
        Repository.SelectionChanged -= SelectionChanged;
        Repository.GroupsChanged -= RepositoryChanged;
        Repository.LayerAssignmentsChanged -= RepositoryChanged;
        Repository.Shapes.CollectionChanged -= ShapesChanged;
        if (Repository is INotifyPropertyChanged notifier) notifier.PropertyChanged -= RepositoryPropertyChanged;
        CompositionTarget.Rendering -= Rendering;
        if (_observedTransform is { IsFrozen: false }) _observedTransform.Changed -= TransformChanged;
        UpdateAvailability();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
