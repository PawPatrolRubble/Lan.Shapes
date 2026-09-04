#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Scaling;

namespace Lan.SketchBoard
{
    /// <summary>
    /// Coordinates shape state with the WPF visual tree used by <see cref="SketchBoard"/>.
    /// The bindable shape collection is the source of truth; the visual collection mirrors it
    /// only while a visual host is attached.
    /// </summary>
    public class SketchBoardDataManager : ISketchBoardDataManager, IVisualHost, INotifyPropertyChanged
    {
        private readonly Dictionary<string, Type> _drawingTools =
            new Dictionary<string, Type>(StringComparer.Ordinal);

        private readonly ShapeFactory _shapeFactory = new ShapeFactory();
        private readonly ObservableCollection<ShapeVisualBase> _shapes =
            new ObservableCollection<ShapeVisualBase>();

        private Type? _currentGeometryType;
        private ShapeLayer? _currentShapeLayer;
        private SketchBoard? _sketchBoard;
        private VisualCollection? _visualCollection;
        private ShapeVisualBase? _currentGeometryInEdit;
        private ShapeVisualBase? _selectedGeometry;
        private readonly ViewportScalingOptions _scalingOptions;
        private double _viewportScale = 1.0;


        /// <summary>
        /// Creates a manager using <see cref="ViewportScalingOptions.Default"/>.
        /// </summary>
        public SketchBoardDataManager()
            : this(ViewportScalingOptions.Default)
        {
        }

        /// <summary>
        /// Creates a manager with per-board stroke/handle base sizes so concurrent
        /// viewers do not share mutable scale bases.
        /// </summary>
        public SketchBoardDataManager(ViewportScalingOptions scalingOptions)
        {
            _scalingOptions = scalingOptions ?? ViewportScalingOptions.Default;
        }

        /// <summary>Per-board base stroke/handle sizes used by the zoom scale path.</summary>
        public ViewportScalingOptions ScalingOptions => _scalingOptions;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ISketchBoard? SketchBoard => _sketchBoard;

        public ObservableCollection<ShapeVisualBase> Shapes => _shapes;

        public VisualCollection VisualCollection => _visualCollection
            ?? throw new InvalidOperationException(
                "The visual collection is unavailable until InitializeVisualCollection is called.");

        public int ShapeCount => Shapes.Count;

        public ShapeVisualBase? CurrentGeometryInEdit
        {
            get => _currentGeometryInEdit;
            set => SetField(ref _currentGeometryInEdit, value);
        }

        public ShapeVisualBase? SelectedGeometry
        {
            get => _selectedGeometry;
            set
            {
                if (ReferenceEquals(_selectedGeometry, value))
                {
                    return;
                }

                var previous = _selectedGeometry;
                if (previous != null)
                {
                    previous.OnDeselected();
                    previous.State = previous.IsLocked
                        ? ShapeVisualState.Locked
                        : ShapeVisualState.Normal;
                }

                SetField(ref _selectedGeometry, value);

                if (previous != null)
                {
                    ShapeUnselected?.Invoke(this, previous);
                }

                if (_selectedGeometry != null)
                {
                    _selectedGeometry.State = _selectedGeometry.IsLocked
                        ? ShapeVisualState.Locked
                        : ShapeVisualState.Selected;
                    _selectedGeometry.OnSelected();
                    ShapeSelected?.Invoke(this, _selectedGeometry);
                }
            }
        }

        public ShapeLayer? CurrentShapeLayer => _currentShapeLayer;


        public void SetGeometryType(string drawingTool)
        {
            if (string.IsNullOrWhiteSpace(drawingTool))
            {
                throw new ArgumentException("A drawing-tool name is required.", nameof(drawingTool));
            }

            if (!_drawingTools.TryGetValue(drawingTool, out var shapeType))
            {
                throw new KeyNotFoundException($"Drawing tool '{drawingTool}' is not registered.");
            }

            SetGeometryType(shapeType);
        }

        public void SetGeometryType(Type type)
        {
            _shapeFactory.Validate(type);
            _currentGeometryType = type;
            GeometryTypeSelected?.Invoke(this, type);
        }

        public void RegisterDrawingTool(string name, Type type)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A drawing-tool name is required.", nameof(name));
            }

            _shapeFactory.Validate(type);
            _drawingTools[name] = type;
        }

        public void UnselectGeometry()
        {
            SelectedGeometry = null;
            CurrentGeometryInEdit = null;
        }

        public void UnselectGeometryType()
        {
            if (_currentGeometryType == null)
            {
                return;
            }

            var previousType = _currentGeometryType;
            _currentGeometryType = null;
            GeometryTypeUnselected?.Invoke(this, previousType);
        }

        public void AddShape(ShapeVisualBase shape)
        {
            AddShapeCore(shape, Shapes.Count);
        }

        public void AddShape(ShapeVisualBase shape, int index)
        {
            if (index < 0 || index > Shapes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            AddShapeCore(shape, index);
        }

        public void RemoveShape(ShapeVisualBase shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            var index = Shapes.IndexOf(shape);
            if (index < 0)
            {
                return;
            }

            if (ReferenceEquals(SelectedGeometry, shape))
            {
                SelectedGeometry = null;
            }

            if (ReferenceEquals(CurrentGeometryInEdit, shape))
            {
                CurrentGeometryInEdit = null;
            }

            shape.ShapeCreationCancelled -= OnShapeCreationCancelled;
            _visualCollection?.Remove(shape);
            Shapes.RemoveAt(index);
            ShapeRemoved?.Invoke(this, shape);
        }

        public void RemoveShapes(Func<ShapeVisualBase, bool> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            foreach (var shape in Shapes.Where(predicate).ToList())
            {
                RemoveShape(shape);
            }
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Shapes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            RemoveShape(Shapes[index]);
        }

        public void RemoveAt(int index, int count)
        {
            if (index < 0 || index > Shapes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (count < 0 || index + count > Shapes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            foreach (var shape in Shapes.Skip(index).Take(count).ToList())
            {
                RemoveShape(shape);
            }
        }

        public void ClearAllShapes()
        {
            foreach (var shape in Shapes.ToList())
            {
                RemoveShape(shape);
            }

            CurrentGeometryInEdit = null;
        }

        public ShapeVisualBase? GetShapeVisual(int index)
        {
            return index >= 0 && index < Shapes.Count ? Shapes[index] : null;
        }

        public ShapeVisualBase LoadShape<T, TP>(TP parameter)
            where T : ShapeVisualBase, IDataExport<TP>
            where TP : IGeometryMetaData
        {
            var shape = CreateShape<T, TP>(parameter);
            AddShape(shape);
            shape.UpdateVisual();
            return shape;
        }

        public ShapeVisualBase CreateShape<T, TP>(TP parameter)
            where T : ShapeVisualBase, IDataExport<TP>
            where TP : IGeometryMetaData
        {
            var shape = (T)_shapeFactory.Create(typeof(T), GetRequiredShapeLayer());
            shape.FromData(parameter);
            return shape;
        }

        public void SetShapeLayer(ShapeLayer layer)
        {
            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            // Own independent styler instances so concurrent viewers that share
            // config-layer objects cannot clobber each other's zoom scale.
            if (!ReferenceEquals(_currentShapeLayer, layer))
            {
                _currentShapeLayer = layer.CreateIndependentCopy();
            }

            ApplyScaleToOwnedLayers(_viewportScale, refreshShapes: false);
        }

        public ShapeVisualBase? CreateNewGeometry(Point mousePosition)
        {
            _ = mousePosition;

            if (_currentGeometryType == null || _currentShapeLayer == null)
            {
                return null;
            }

            var shape = _shapeFactory.Create(_currentGeometryType, _currentShapeLayer);
            AddShape(shape);
            CurrentGeometryInEdit = shape;

            if (shape is IBoardContextAware contextAware && _sketchBoard != null)
            {
                contextAware.OnBoardContextAvailable(
                    _sketchBoard.ActualWidth,
                    _sketchBoard.ActualHeight);
            }

            return shape;
        }

        public void InitializeVisualCollection(Visual visual)
        {
            if (visual == null)
            {
                throw new ArgumentNullException(nameof(visual));
            }

            // A Visual may only have one parent. Detach the existing mirror before
            // rebuilding it for a new host, while retaining the source collection.
            _visualCollection?.Clear();
            _visualCollection = new VisualCollection(visual);

            foreach (var shape in Shapes)
            {
                _visualCollection.Add(shape);
            }

            _sketchBoard = visual as SketchBoard;
            OnPropertyChanged(nameof(SketchBoard));
            OnPropertyChanged(nameof(VisualCollection));

            SketchBoardManagerInitialized?.Invoke(this, this);
            HostInitialized?.Invoke(this, this);
        }

        public void OnImageViewerPropertyChanged(double scale)
        {
            _viewportScale = scale;
            ApplyScaleToOwnedLayers(scale, refreshShapes: true);
        }

        /// <summary>
        /// Applies <c>base / max(scale, ε)</c> to every layer this manager owns
        /// (current layer plus any layer referenced by shapes on the board).
        /// When <paramref name="refreshShapes"/> is true, existing shapes re-read
        /// handle size and redraw so on-screen thickness tracks zoom.
        /// </summary>
        private void ApplyScaleToOwnedLayers(double scale, bool refreshShapes)
        {
            var thickness = ViewportScalingService.CalculateStrokeThickness(scale, _scalingOptions);
            var handleSize = ViewportScalingService.CalculateDragHandleSize(scale, _scalingOptions);

            foreach (var layer in GetLayersAffectedByScale())
            {
                foreach (var shapeStyler in layer.Stylers.Values)
                {
                    shapeStyler.SetStrokeThickness(thickness);
                    shapeStyler.DragHandleSize = handleSize;
                }
            }

            if (!refreshShapes)
            {
                return;
            }

            foreach (var shape in Shapes)
            {
                shape.RefreshScaleDependentVisuals();
            }
        }

        private IEnumerable<ShapeLayer> GetLayersAffectedByScale()
        {
            var layers = new HashSet<ShapeLayer>();
            if (_currentShapeLayer != null)
            {
                layers.Add(_currentShapeLayer);
            }

            foreach (var shape in Shapes)
            {
                if (shape.ShapeLayer != null)
                {
                    layers.Add(shape.ShapeLayer);
                }
            }

            return layers;
        }

        public void RaiseNewShapeSketched(ShapeVisualBase shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            NewShapeSketched?.Invoke(this, shape);
        }

        private void AddShapeCore(ShapeVisualBase shape, int index)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (Shapes.Contains(shape))
            {
                throw new InvalidOperationException("The shape is already managed by this sketch board.");
            }

            // Update the visual mirror first. ObservableCollection listeners then see
            // a consistent state when the collection-changed event is raised.
            _visualCollection?.Insert(index, shape);
            Shapes.Insert(index, shape);
            shape.ShapeCreationCancelled += OnShapeCreationCancelled;
            ShapeCreated?.Invoke(this, shape);
        }

        private ShapeLayer GetRequiredShapeLayer()
        {
            return CurrentShapeLayer
                ?? throw new InvalidOperationException("A shape layer must be selected before creating a shape.");
        }

        private void OnShapeCreationCancelled(object? sender, EventArgs e)
        {
            if (sender is ShapeVisualBase shape)
            {
                RemoveShape(shape);
                UnselectGeometryType();
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public event EventHandler<ISketchBoardDataManager>? SketchBoardManagerInitialized;
        public event EventHandler<IShapeRepository>? HostInitialized;
        public event EventHandler<ShapeVisualBase>? ShapeCreated;
        public event EventHandler<ShapeVisualBase>? ShapeRemoved;
        public event EventHandler<ShapeVisualBase>? ShapeSelected;
        public event EventHandler<ShapeVisualBase>? ShapeUnselected;
        public event EventHandler<Type>? GeometryTypeSelected;
        public event EventHandler<Type>? GeometryTypeUnselected;
        public event EventHandler<ShapeVisualBase>? NewShapeSketched;
    }
}
