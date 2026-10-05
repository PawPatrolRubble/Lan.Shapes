using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Lan.Shapes;

namespace Lan.ImageViewer
{
    /// <summary>A board-local tree row backed by a layer definition and its shapes.</summary>
    public sealed class ShapeLayerTreeNode : INotifyPropertyChanged, IDisposable
    {
        private readonly Action<int, bool> _setVisibility;
        private bool _isVisible;
        private bool _canEdit = true;

        public ShapeLayerTreeNode(ShapeLayer layer, bool isVisible, Action<int, bool> setVisibility)
        {
            Layer = layer ?? throw new ArgumentNullException(nameof(layer));
            Layer.PropertyChanged += Layer_PropertyChanged;
            _isVisible = isVisible;
            _setVisibility = setVisibility ?? throw new ArgumentNullException(nameof(setVisibility));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ShapeLayer Layer { get; private set; }
        public int LayerId => Layer.LayerId;
        public string Name => Layer.Name;
        public bool IsSelected => false;
        public bool CanEdit
        {
            get => _canEdit;
            set
            {
                if (_canEdit == value) return;
                _canEdit = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanEdit)));
            }
        }
        public ObservableCollection<ShapeVisualBase> Shapes { get; } = new ObservableCollection<ShapeVisualBase>();

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible == value) return;
                _setVisibility(LayerId, value);
                SyncVisibility(value);
            }
        }

        public void SyncVisibility(bool isVisible)
        {
            if (_isVisible == isVisible) return;
            _isVisible = isVisible;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
        }

        public void Update(ShapeLayer layer, IEnumerable<ShapeVisualBase> shapes)
        {
            if (!ReferenceEquals(Layer, layer))
            {
                Layer.PropertyChanged -= Layer_PropertyChanged;
                Layer = layer;
                Layer.PropertyChanged += Layer_PropertyChanged;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Layer)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }

            var desired = shapes.ToList();
            for (var index = 0; index < desired.Count; index++)
            {
                if (index < Shapes.Count && ReferenceEquals(Shapes[index], desired[index])) continue;
                var oldIndex = Shapes.IndexOf(desired[index]);
                if (oldIndex >= 0) Shapes.Move(oldIndex, index);
                else Shapes.Insert(index, desired[index]);
            }
            while (Shapes.Count > desired.Count) Shapes.RemoveAt(Shapes.Count - 1);
        }

        private void Layer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ShapeLayer.Name))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }

        public void Dispose() => Layer.PropertyChanged -= Layer_PropertyChanged;
    }
}
