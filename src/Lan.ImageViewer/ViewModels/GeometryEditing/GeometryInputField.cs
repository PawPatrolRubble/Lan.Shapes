#nullable enable
using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Lan.ImageViewer.ViewModels.GeometryEditing;

/// <summary>A numeric input draft. The original double is retained until the user edits it.</summary>
public sealed class GeometryInputField : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private string _originalText = string.Empty;
    private double _originalValue;
    private bool _hasError;
    internal Action? Changed { get; set; }

    public GeometryInputField(string label, string automationName)
    {
        Label = label;
        AutomationName = automationName;
    }

    public string Label { get; }
    public string AutomationName { get; }
    public string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value ?? string.Empty;
            OnPropertyChanged();
            Changed?.Invoke();
        }
    }

    public bool HasError
    {
        get => _hasError;
        internal set
        {
            if (_hasError == value) return;
            _hasError = value;
            OnPropertyChanged();
        }
    }

    internal bool IsModified => _text != _originalText;

    internal void Load(double value)
    {
        _originalValue = value;
        _originalText = value.ToString("F0", CultureInfo.CurrentCulture);
        _text = _originalText;
        HasError = false;
        OnPropertyChanged(nameof(Text));
    }

    internal bool TryRead(out double value)
    {
        if (!IsModified)
        {
            value = _originalValue;
            return double.IsFinite(value);
        }
        return double.TryParse(_text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            && double.IsFinite(value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
