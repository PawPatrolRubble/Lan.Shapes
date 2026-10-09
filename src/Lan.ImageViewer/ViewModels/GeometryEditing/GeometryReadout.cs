#nullable enable
using System;
using System.ComponentModel;

namespace Lan.ImageViewer.ViewModels.GeometryEditing;

public sealed class GeometryReadout : INotifyPropertyChanged
{
    private readonly Func<string> _read;
    internal GeometryReadout(string label, Func<string> read)
    {
        Label = label;
        _read = read;
    }
    public string Label { get; }
    public string Value => _read();
    internal void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    public event PropertyChangedEventHandler? PropertyChanged;
}
