#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Lan.ImageViewer.ViewModels.GeometryEditing;

/// <summary>Fields which are validated and applied together as one geometry operation.</summary>
public sealed class GeometryPropertyGroup : INotifyPropertyChanged
{
    private string _errorMessage = string.Empty;
    private bool _isEnabled;
    private readonly Func<double[]> _read;
    private readonly Action<double[]> _apply;
    private readonly Func<bool> _available;

    internal GeometryPropertyGroup(string label, string hint, GeometryInputField[] fields,
        Func<double[]> read, Action<double[]> apply, Func<bool>? available = null)
    {
        Label = label;
        Hint = hint;
        Fields = Array.AsReadOnly(fields);
        _read = read;
        _apply = apply;
        _available = available ?? (() => true);
        foreach (var field in Fields) field.Changed = OnDraftChanged;
    }

    public string Label { get; }
    public string Hint { get; }
    public IReadOnlyList<GeometryInputField> Fields { get; }
    public string ErrorMessage => _errorMessage;
    public bool HasError => _errorMessage.Length != 0;
    public bool IsDirty => Fields.Any(field => field.IsModified);
    public int Revision { get; private set; }
    public bool IsEnabled => _isEnabled;

    internal void UpdateAvailability(bool canEdit)
    {
        var enabled = canEdit && _available();
        if (_isEnabled == enabled) return;
        _isEnabled = enabled;
        OnPropertyChanged(nameof(IsEnabled));
    }

    internal void Load()
    {
        var values = _read();
        for (var index = 0; index < Fields.Count; index++) Fields[index].Load(values[index]);
        Revision++;
        SetError(string.Empty);
        OnPropertyChanged(nameof(IsDirty));
    }

    internal bool TryApply()
    {
        var values = new double[Fields.Count];
        for (var index = 0; index < Fields.Count; index++)
        {
            if (Fields[index].TryRead(out values[index])) continue;
            SetError("请输入有效的有限数字。");
            return false;
        }
        _apply(values);
        return true;
    }

    internal void Invalidate() => Revision++;

    internal void SetError(string message)
    {
        _errorMessage = message;
        foreach (var field in Fields) field.HasError = HasError;
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
    }

    private void OnDraftChanged()
    {
        Revision++;
        SetError(string.Empty);
        OnPropertyChanged(nameof(IsDirty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
