#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private readonly ObservableCollection<ShapeVisualBase> _selection = new();
        private readonly Dictionary<int, ShapeLayer> _ownedLayers = new();
        private readonly Dictionary<int, ShapeLayer> _layerDefinitions = new();
        private bool _updatingLayers;

        private Type? _currentGeometryType;
        private ShapeLayer? _currentShapeLayer;
        private SketchBoard? _sketchBoard;
        private VisualCollection? _visualCollection;
        private ShapeVisualBase? _currentGeometryInEdit;
        private ShapeVisualBase? _selectedGeometry;
        private readonly ViewportScalingOptions _scalingOptions;
        private readonly Dictionary<ShapeLayer, Dictionary<ShapeVisualState, double>> _configuredHandleSizes =
            new Dictionary<ShapeLayer, Dictionary<ShapeVisualState, double>>();
        private readonly Dictionary<ShapeLayer, Dictionary<ShapeVisualState, double>> _configuredStrokeThicknesses =
            new Dictionary<ShapeLayer, Dictionary<ShapeVisualState, double>>();
        private readonly HashSet<int> _hiddenLayerIds = new HashSet<int>();
        private long _layerVisibilityRevision;
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
            SelectedGeometries = new ReadOnlyObservableCollection<ShapeVisualBase>(_selection);
        }

        /// <summary>Per-board fallback stroke/handle sizes used when a shape has no configured baseline.</summary>
        public ViewportScalingOptions ScalingOptions => _scalingOptions;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ISketchBoard? SketchBoard => _sketchBoard;

        public double ViewportScale => NormalizeScale(_viewportScale);

        public long LayerVisibilityRevision => _layerVisibilityRevision;

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
                if (value != null && !IsLayerVisible(value.ShapeLayer.LayerId)) return;
                ApplySelection(value == null ? new List<ShapeVisualBase>() : new List<ShapeVisualBase> { value });
            }
        }

        public ReadOnlyObservableCollection<ShapeVisualBase> SelectedGeometries { get; }
        public event EventHandler? SelectionChanged;
        public event EventHandler? LayerAssignmentsChanged;

        public void SetSelection(IEnumerable<ShapeVisualBase> shapes)
        {
            if (shapes == null) throw new ArgumentNullException(nameof(shapes));
            var desired = shapes.Distinct().ToList();
            if (desired.Any(x => x == null || !Shapes.Contains(x) || !x.IsGeometryRendered
                || !IsLayerVisible(x.ShapeLayer.LayerId)))
                throw new ArgumentException("Selection must contain completed, visible shapes on this board.", nameof(shapes));
            ApplySelection(desired);
        }

        private void ApplySelection(List<ShapeVisualBase> desired)
        {
            if (_selection.SequenceEqual(desired)) return;
            foreach (var shape in _selection.Except(desired).ToList())
            {
                var locked = shape.IsLocked;
                shape.OnDeselected();
                shape.State = locked || shape.IsLocked ? ShapeVisualState.Locked : ShapeVisualState.Normal;
                shape.SetSelectionAppearance(false, true);
                _selection.Remove(shape);
                ShapeUnselected?.Invoke(this, shape);
            }
            foreach (var shape in desired.Except(_selection).ToList())
            {
                var locked = shape.IsLocked;
                shape.State = locked ? ShapeVisualState.Locked : ShapeVisualState.Selected;
                shape.OnSelected();
                if (locked) shape.State = ShapeVisualState.Locked;
                _selection.Add(shape);
                ShapeSelected?.Invoke(this, shape);
            }
            for (var i = 0; i < desired.Count; i++)
            {
                var index = _selection.IndexOf(desired[i]);
                if (index != i) _selection.Move(index, i);
                desired[i].SetSelectionAppearance(true, desired.Count == 1);
            }
            SetField(ref _selectedGeometry, desired.LastOrDefault(), nameof(SelectedGeometry));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public int AssignShapesToLayer(IEnumerable<ShapeVisualBase> shapes, ShapeLayer targetLayer)
        {
            if (shapes == null) throw new ArgumentNullException(nameof(shapes));
            if (targetLayer == null) throw new ArgumentNullException(nameof(targetLayer));
            var candidates = shapes.Distinct().ToList();
            if (candidates.Any(x => x == null || !Shapes.Contains(x) || !x.IsGeometryRendered || x.IsLocked))
                throw new ArgumentException("Only completed, unlocked shapes on this board can change layers.", nameof(shapes));
            var moved = candidates.Where(x => x.ShapeLayer.LayerId != targetLayer.LayerId).ToList();
            if (moved.Count == 0) return 0;
            var target = GetOwnedLayer(targetLayer);
            var previous = moved.ToDictionary(x => x, x => x.ShapeLayer);
            var oldSelection = _selection.ToList();
            _updatingLayers = true;
            try
            {
                foreach (var shape in moved) shape.ShapeLayer = target;
                ApplyScaleToOwnedLayers(_viewportScale, refreshShapes: true);
                ApplySelection(_selection.Where(x => IsLayerVisible(x.ShapeLayer.LayerId)).ToList());
            }
            catch
            {
                foreach (var entry in previous) entry.Key.ShapeLayer = entry.Value;
                ApplySelection(oldSelection);
                throw;
            }
            finally
            {
                _updatingLayers = false;
                RebuildVisualCollection();
            }
            _layerVisibilityRevision++;
            OnPropertyChanged(nameof(LayerVisibilityRevision));
            LayerAssignmentsChanged?.Invoke(this, EventArgs.Empty);
            return moved.Count;
        }

        public ShapeLayer? CurrentShapeLayer => _currentShapeLayer;

        public Type? CurrentGeometryType => _currentGeometryType;


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
            SetField(ref _currentGeometryType, type, nameof(CurrentGeometryType));
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
            SetField(ref _currentGeometryType, null, nameof(CurrentGeometryType));
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

            if (_selection.Contains(shape)) ApplySelection(_selection.Where(x => !ReferenceEquals(x, shape)).ToList());

            if (ReferenceEquals(CurrentGeometryInEdit, shape))
            {
                CurrentGeometryInEdit = null;
            }

            shape.ShapeCreationCancelled -= OnShapeCreationCancelled;
            shape.PropertyChanged -= OnManagedShapePropertyChanged;
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
            _currentShapeLayer = GetOwnedLayer(layer);

            ApplyScaleToOwnedLayers(_viewportScale, refreshShapes: false);
        }

        private ShapeLayer GetOwnedLayer(ShapeLayer definition)
        {
            if (!_ownedLayers.TryGetValue(definition.LayerId, out var layer))
            {
                layer = definition.CreateIndependentCopy();
                _ownedLayers.Add(layer.LayerId, layer);
                _layerDefinitions.Add(layer.LayerId, definition);
                CaptureConfiguredHandleSizes(layer);
            }
            else if (!ReferenceEquals(layer, definition) && !ReferenceEquals(_layerDefinitions[layer.LayerId], definition))
            {
                layer.ApplyConfiguration(definition.ToShapeLayerParameter());
                _layerDefinitions[layer.LayerId] = definition;
                _configuredHandleSizes.Remove(layer);
                _configuredStrokeThicknesses.Remove(layer);
                CaptureConfiguredHandleSizes(layer);
            }
            return layer;
        }

        public bool IsLayerVisible(int layerId) => !_hiddenLayerIds.Contains(layerId);

        public void SetLayerVisibility(int layerId, bool isVisible)
        {
            if (isVisible ? !_hiddenLayerIds.Remove(layerId) : !_hiddenLayerIds.Add(layerId)) return;
            if (!isVisible)
            {
                ApplySelection(_selection.Where(x => x.ShapeLayer.LayerId != layerId).ToList());
                if (CurrentGeometryInEdit?.ShapeLayer.LayerId == layerId)
                {
                    CurrentGeometryInEdit = null;
                    UnselectGeometryType();
                }
            }
            RebuildVisualCollection();
            _layerVisibilityRevision++;
            OnPropertyChanged(nameof(LayerVisibilityRevision));
        }

        public void UpdateLayerConfiguration(ShapeLayerParameter parameter)
        {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));
            foreach (var layer in GetLayersAffectedByScale().Where(x => x.LayerId == parameter.LayerId))
            {
                layer.ApplyConfiguration(parameter);
                _configuredHandleSizes.Remove(layer);
                _configuredStrokeThicknesses.Remove(layer);
            }
            ApplyScaleToOwnedLayers(_viewportScale, refreshShapes: true);
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
                var image = _sketchBoard.Image as BitmapSource;
                contextAware.OnBoardContextAvailable(
                    image != null ? image.PixelWidth : _sketchBoard.ActualWidth,
                    image != null ? image.PixelHeight : _sketchBoard.ActualHeight);
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
                if (IsLayerVisible(shape.ShapeLayer.LayerId)) _visualCollection.Add(shape);
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
            OnPropertyChanged(nameof(ViewportScale));
        }

        /// <summary>
        /// Applies <c>base / max(scale, ε)</c> to every layer this manager owns
        /// (current layer plus any layer referenced by shapes on the board).
        /// When <paramref name="refreshShapes"/> is true, existing shapes re-read
        /// handle size and redraw so on-screen thickness tracks zoom.
        /// </summary>
        private void ApplyScaleToOwnedLayers(double scale, bool refreshShapes)
        {
            foreach (var layer in GetLayersAffectedByScale())
            {
                var configuredSizes = GetConfiguredHandleSizes(layer);
                var configuredStrokes = GetConfiguredStrokeThicknesses(layer);
                foreach (var entry in layer.Stylers)
                {
                    var configuredSize = configuredSizes.TryGetValue(entry.Key, out var size)
                        ? size
                        : 0;
                    var handleSize = configuredSize > 0 || entry.Key == ShapeVisualState.Locked
                        ? configuredSize
                        : _scalingOptions.BaseDragHandleSize;

                    var shapeStyler = entry.Value;
                    var stroke = configuredStrokes.TryGetValue(entry.Key, out var configuredStroke)
                        && configuredStroke > 0 ? configuredStroke : _scalingOptions.BaseStrokeThickness;
                    shapeStyler.SetStrokeThickness(stroke / NormalizeScale(scale));
                    shapeStyler.DragHandleSize = handleSize / NormalizeScale(scale);
                }
            }

            if (!refreshShapes)
            {
                return;
            }

            foreach (var shape in Shapes)
            {
                shape.RefreshScaleDependentVisuals(_viewportScale);
            }
        }

        private IEnumerable<ShapeLayer> GetLayersAffectedByScale()
        {
            var layers = new HashSet<ShapeLayer>();
            foreach (var layer in _ownedLayers.Values) layers.Add(layer);
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

        private void CaptureConfiguredHandleSizes(ShapeLayer layer)
        {
            if (_configuredHandleSizes.ContainsKey(layer))
            {
                return;
            }

            _configuredHandleSizes[layer] = layer.Stylers.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.DragHandleSize);
            _configuredStrokeThicknesses[layer] = layer.Stylers.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.SketchPen.Thickness);
        }

        private Dictionary<ShapeVisualState, double> GetConfiguredHandleSizes(ShapeLayer layer)
        {
            CaptureConfiguredHandleSizes(layer);
            return _configuredHandleSizes[layer];
        }

        private Dictionary<ShapeVisualState, double> GetConfiguredStrokeThicknesses(ShapeLayer layer)
        {
            CaptureConfiguredHandleSizes(layer);
            return _configuredStrokeThicknesses[layer];
        }

        private void RebuildVisualCollection()
        {
            if (_visualCollection == null) return;
            _visualCollection.Clear();
            foreach (var shape in Shapes)
                if (IsLayerVisible(shape.ShapeLayer.LayerId)) _visualCollection.Add(shape);
        }

        private void OnManagedShapePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ShapeVisualBase.ShapeLayer) || sender is not ShapeVisualBase shape)
                return;
            if (_updatingLayers) return;
            if (!IsLayerVisible(shape.ShapeLayer.LayerId))
            {
                ApplySelection(_selection.Where(x => !ReferenceEquals(x, shape)).ToList());
                if (ReferenceEquals(CurrentGeometryInEdit, shape)) CurrentGeometryInEdit = null;
            }
            ApplyScaleToOwnedLayers(_viewportScale, refreshShapes: true);
            RebuildVisualCollection();
            _layerVisibilityRevision++;
            OnPropertyChanged(nameof(LayerVisibilityRevision));
            LayerAssignmentsChanged?.Invoke(this, EventArgs.Empty);
        }

        private static double NormalizeScale(double scale)
        {
            return scale > 0 && !double.IsNaN(scale) && !double.IsInfinity(scale)
                ? scale
                : 1.0;
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
            if (_visualCollection != null && IsLayerVisible(shape.ShapeLayer.LayerId))
            {
                var visualIndex = Shapes.Take(index).Count(x => IsLayerVisible(x.ShapeLayer.LayerId));
                _visualCollection.Insert(visualIndex, shape);
            }
            Shapes.Insert(index, shape);
            shape.ShapeCreationCancelled += OnShapeCreationCancelled;
            shape.PropertyChanged += OnManagedShapePropertyChanged;
            // Shapes created after a zoom change must initialize adornment
            // positions with the manager's current viewport scale.
            shape.RefreshScaleDependentVisuals(_viewportScale);
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
