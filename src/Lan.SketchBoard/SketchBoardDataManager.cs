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
        private readonly ManagedShapeCollection _shapes;
        private readonly ObservableCollection<ShapeVisualBase> _selection = new();
        private readonly ObservableCollection<ShapeGroup> _groups = new();
        private readonly Dictionary<ShapeVisualBase, ShapeGroup> _groupsByShape = new();
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
            _shapes = new ManagedShapeCollection(this);
            _scalingOptions = scalingOptions ?? ViewportScalingOptions.Default;
            SelectedGeometries = new ReadOnlyObservableCollection<ShapeVisualBase>(_selection);
            Groups = new ReadOnlyObservableCollection<ShapeGroup>(_groups);
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
                ApplySelection(value == null ? new List<ShapeVisualBase>() : ExpandGroups(new[] { value }));
            }
        }

        public ReadOnlyObservableCollection<ShapeVisualBase> SelectedGeometries { get; }
        public ReadOnlyObservableCollection<ShapeGroup> Groups { get; }
        public event EventHandler? SelectionChanged;
        public event EventHandler? LayerAssignmentsChanged;
        public event EventHandler? GroupsChanged;

        public ShapeGroup? GetGroup(ShapeVisualBase shape)
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            return _groupsByShape.TryGetValue(shape, out var group) ? group : null;
        }

        public bool CanGroupShapes(IEnumerable<ShapeVisualBase> shapes)
        {
            if (shapes == null) throw new ArgumentNullException(nameof(shapes));
            var candidates = shapes.Distinct().ToList();
            return candidates.Count >= 2
                && candidates.All(x => x != null && Shapes.Contains(x) && x.IsGeometryRendered
                    && !x.IsLocked && x.CanTranslate && IsLayerVisible(x.ShapeLayer.LayerId)
                    && !_groupsByShape.ContainsKey(x) && TryGetTranslationMatrix(x, out _))
                && candidates.Select(x => x.ShapeLayer.LayerId).Distinct().Count() == 1;
        }

        public ShapeGroup GroupShapes(IEnumerable<ShapeVisualBase> shapes, string? name = null)
        {
            if (shapes == null) throw new ArgumentNullException(nameof(shapes));
            var candidates = shapes.Distinct().ToList();
            if (!CanGroupShapes(candidates))
                throw new ArgumentException("Groups require at least two independent, completed, visible, unlocked, movable shapes on one board layer.", nameof(shapes));
            var group = new ShapeGroup(candidates, name);
            foreach (var member in group.Members) _groupsByShape.Add(member, group);
            _groups.Add(group);
            ApplySelection(ExpandGroups(_selection));
            GroupsChanged?.Invoke(this, EventArgs.Empty);
            return group;
        }

        public int UngroupShapes(IEnumerable<ShapeVisualBase> shapes)
        {
            if (shapes == null) throw new ArgumentNullException(nameof(shapes));
            var candidates = shapes.Distinct().ToList();
            if (candidates.Any(x => x == null || !Shapes.Contains(x)))
                throw new ArgumentException("Only shapes on this board can be ungrouped.", nameof(shapes));
            var groups = candidates.Select(GetGroup).Where(x => x != null).Distinct().ToList();
            foreach (var group in groups) RemoveGroup(group!);
            return groups.Count;
        }

        public int TranslateShapes(IEnumerable<ShapeVisualBase> shapes, Vector delta)
        {
            if (shapes == null) throw new ArgumentNullException(nameof(shapes));
            if (!IsFinite(delta.X) || !IsFinite(delta.Y))
                throw new ArgumentException("A finite displacement is required.", nameof(delta));
            var candidates = ExpandGroups(shapes);
            if (candidates.Any(x => x == null || !Shapes.Contains(x) || !x.IsGeometryRendered
                || x.IsLocked || !x.CanTranslate || !IsLayerVisible(x.ShapeLayer.LayerId)))
                throw new ArgumentException("Only completed, visible, unlocked, movable shapes on this board can be translated.", nameof(shapes));

            var displacements = new Dictionary<ShapeVisualBase, Vector>();
            foreach (var shape in candidates)
            {
                if (!TryGetTranslationMatrix(shape, out var matrix))
                    throw new ArgumentException("Every shape must have a finite, invertible visual transform.", nameof(shapes));
                var localDelta = matrix.Transform(delta);
                if (!IsFinite(localDelta.X) || !IsFinite(localDelta.Y))
                    throw new ArgumentException("Every shape must have a finite local displacement.", nameof(shapes));
                shape.ValidateTranslation(localDelta);
                displacements.Add(shape, localDelta);
            }
            if (delta.X == 0 && delta.Y == 0) return 0;
            foreach (var shape in candidates) shape.Translate(displacements[shape]);
            return candidates.Count;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool TryGetTranslationMatrix(ShapeVisualBase shape, out Matrix inverse)
        {
            inverse = shape.Transform?.Value ?? Matrix.Identity;
            if (!IsFiniteMatrix(inverse) || !inverse.HasInverse) return false;
            inverse.Invert();
            return IsFiniteMatrix(inverse);
        }

        private static bool IsFiniteMatrix(Matrix matrix) => IsFinite(matrix.M11) && IsFinite(matrix.M12)
            && IsFinite(matrix.M21) && IsFinite(matrix.M22)
            && IsFinite(matrix.OffsetX) && IsFinite(matrix.OffsetY);

        private List<ShapeVisualBase> ExpandGroups(IEnumerable<ShapeVisualBase> shapes)
        {
            var expanded = new List<ShapeVisualBase>();
            var seen = new HashSet<ShapeVisualBase>();
            foreach (var shape in shapes)
            {
                if (shape != null && _groupsByShape.TryGetValue(shape, out var group))
                {
                    foreach (var member in group.Members)
                        if (seen.Add(member)) expanded.Add(member);
                }
                else if (seen.Add(shape!)) expanded.Add(shape!);
            }
            return expanded;
        }

        private void RemoveGroup(ShapeGroup group)
        {
            foreach (var member in group.Members) _groupsByShape.Remove(member);
            _groups.Remove(group);
            GroupsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetSelection(IEnumerable<ShapeVisualBase> shapes)
        {
            if (shapes == null) throw new ArgumentNullException(nameof(shapes));
            var desired = ExpandGroups(shapes);
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
                shape.CancelInteraction();
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
            var candidates = ExpandGroups(shapes);
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

        public void CancelCurrentSketch()
        {
            if (CurrentGeometryInEdit is { IsGeometryRendered: false } unfinished)
                RemoveShape(unfinished);
            CurrentGeometryInEdit = null;
            UnselectGeometryType();
        }

        public void AddShape(ShapeVisualBase shape)
        {
            Shapes.Add(shape);
        }

        public void AddShape(ShapeVisualBase shape, int index)
        {
            if (index < 0 || index > Shapes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            Shapes.Insert(index, shape);
        }

        public void RemoveShape(ShapeVisualBase shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            Shapes.Remove(shape);
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
            Shapes.Clear();
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
            SetField(ref _currentShapeLayer, GetOwnedLayer(layer), nameof(CurrentShapeLayer));

            ApplyScaleToOwnedLayers(_viewportScale, refreshShapes: true);
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
            else if (!ReferenceEquals(layer, definition)
                && (!_layerDefinitions.TryGetValue(layer.LayerId, out var previousDefinition)
                    || !ReferenceEquals(previousDefinition, definition)))
            {
                layer.ApplyDefinition(definition);
                _layerDefinitions[layer.LayerId] = definition;
                _configuredHandleSizes.Remove(layer);
                _configuredStrokeThicknesses.Remove(layer);
                CaptureConfiguredHandleSizes(layer);
            }
            return layer;
        }

        private ShapeLayer GetLayerForShape(ShapeLayer layer)
        {
            // A shape may arrive with a shared definition or another board's scaled copy.
            // An existing board layer is authoritative; only definition APIs replace it.
            return _ownedLayers.TryGetValue(layer.LayerId, out var owned) ? owned : GetOwnedLayer(layer);
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
                    CancelCurrentSketch();
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

        public void ApplyLayerDefinitions(IEnumerable<ShapeLayer> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var layers = definitions.ToList();
            if (layers.Any(x => x == null) || layers.Select(x => x.LayerId).Distinct().Count() != layers.Count)
                throw new ArgumentException("Layer definitions must have unique IDs and contain no null entries.", nameof(definitions));
            var configuredIds = new HashSet<int>(layers.Select(x => x.LayerId));
            if (CurrentGeometryInEdit is { IsGeometryRendered: false } unfinished
                && !configuredIds.Contains(unfinished.ShapeLayer.LayerId))
                CancelCurrentSketch();

            foreach (var definition in layers) GetOwnedLayer(definition);
            var referencedIds = new HashSet<int>(Shapes.Select(x => x.ShapeLayer.LayerId));
            foreach (var id in _ownedLayers.Keys.Where(x => !configuredIds.Contains(x)).ToList())
            {
                _layerDefinitions.Remove(id);
                if (referencedIds.Contains(id))
                    _hiddenLayerIds.Remove(id);
                else
                {
                    var obsolete = _ownedLayers[id];
                    _ownedLayers.Remove(id);
                    _configuredHandleSizes.Remove(obsolete);
                    _configuredStrokeThicknesses.Remove(obsolete);
                    _hiddenLayerIds.Remove(id);
                }
            }
            // Previously orphaned layers may also have been hidden since the last reload.
            foreach (var id in referencedIds.Where(x => !configuredIds.Contains(x))) _hiddenLayerIds.Remove(id);
            var current = _currentShapeLayer != null && configuredIds.Contains(_currentShapeLayer.LayerId)
                ? _ownedLayers[_currentShapeLayer.LayerId]
                : layers.Count > 0 ? _ownedLayers[layers[0].LayerId] : null;
            SetField(ref _currentShapeLayer, current, nameof(CurrentShapeLayer));
            if (current == null) UnselectGeometryType();
            ApplyScaleToOwnedLayers(_viewportScale, refreshShapes: true);
            RebuildVisualCollection();
            _layerVisibilityRevision++;
            OnPropertyChanged(nameof(LayerVisibilityRevision));
            LayerAssignmentsChanged?.Invoke(this, EventArgs.Empty);
        }

        public ShapeVisualBase? CreateNewGeometry(Point mousePosition)
        {
            _ = mousePosition;

            if (_currentGeometryType == null || _currentShapeLayer == null || !IsLayerVisible(_currentShapeLayer.LayerId))
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

        public void DetachVisualHost()
        {
            _visualCollection?.Clear();
            _visualCollection = null;
            _sketchBoard = null;
            OnPropertyChanged(nameof(SketchBoard));
            OnPropertyChanged(nameof(VisualCollection));
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
            return _ownedLayers.Values;
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

        private void RebuildVisualCollection(IEnumerable<ShapeVisualBase>? shapes = null)
        {
            if (_visualCollection == null) return;
            _visualCollection.Clear();
            foreach (var shape in shapes ?? Shapes)
                if (IsLayerVisible(shape.ShapeLayer.LayerId)) _visualCollection.Add(shape);
        }

        private void OnManagedShapePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not ShapeVisualBase shape) return;
            if (e.PropertyName == nameof(ShapeVisualBase.IsGeometryRendered) && !shape.IsGeometryRendered)
            {
                if (_groupsByShape.TryGetValue(shape, out var unfinishedGroup)) RemoveGroup(unfinishedGroup);
                ApplySelection(_selection.Where(x => !ReferenceEquals(x, shape)).ToList());
                return;
            }
            if (e.PropertyName != nameof(ShapeVisualBase.ShapeLayer)) return;
            if (_updatingLayers) return;
            _updatingLayers = true;
            try
            {
                shape.ShapeLayer = GetLayerForShape(shape.ShapeLayer);
            }
            finally
            {
                _updatingLayers = false;
            }
            if (_groupsByShape.TryGetValue(shape, out var group)
                && group.Members.Select(x => x.ShapeLayer.LayerId).Distinct().Count() != 1)
                RemoveGroup(group);
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

        private void PrepareShape(ShapeVisualBase shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            if (Shapes.Contains(shape))
            {
                throw new InvalidOperationException("The shape is already managed by this sketch board.");
            }
            if (VisualTreeHelper.GetParent(shape) != null)
                throw new ArgumentException("The shape already belongs to another visual host.", nameof(shape));

            var layer = GetLayerForShape(shape.ShapeLayer);
            ApplyScaleToOwnedLayers(_viewportScale, refreshShapes: false);
            shape.ShapeLayer = layer;
            shape.RefreshScaleDependentVisuals(_viewportScale);
        }

        private void InsertShapeCore(ShapeVisualBase shape, int index, Action publish)
        {
            PrepareShape(shape);

            // Update the visual mirror first. ObservableCollection listeners then see
            // a consistent state when the collection-changed event is raised.
            if (_visualCollection != null && IsLayerVisible(shape.ShapeLayer.LayerId))
            {
                var visualIndex = Shapes.Take(index).Count(x => IsLayerVisible(x.ShapeLayer.LayerId));
                _visualCollection.Insert(visualIndex, shape);
            }
            shape.ShapeCreationCancelled += OnShapeCreationCancelled;
            shape.PropertyChanged += OnManagedShapePropertyChanged;
            publish();
            ShapeCreated?.Invoke(this, shape);
        }

        private void DetachShape(ShapeVisualBase shape)
        {
            if (_groupsByShape.TryGetValue(shape, out var group)) RemoveGroup(group);
            if (_selection.Contains(shape)) ApplySelection(_selection.Where(x => !ReferenceEquals(x, shape)).ToList());
            if (ReferenceEquals(CurrentGeometryInEdit, shape)) CurrentGeometryInEdit = null;
            shape.ShapeCreationCancelled -= OnShapeCreationCancelled;
            shape.PropertyChanged -= OnManagedShapePropertyChanged;
            _visualCollection?.Remove(shape);
        }

        private void RemoveShapeCore(int index, Action publish)
        {
            var shape = Shapes[index];
            DetachShape(shape);
            publish();
            ShapeRemoved?.Invoke(this, shape);
        }

        private void ReplaceShapeCore(int index, ShapeVisualBase shape, Action publish)
        {
            var previous = Shapes[index];
            if (ReferenceEquals(previous, shape)) return;
            PrepareShape(shape);
            if (_visualCollection != null && IsLayerVisible(shape.ShapeLayer.LayerId))
            {
                try
                {
                    // Attach first: a rejected visual must not detach the original shape.
                    _visualCollection.Insert(Shapes.Take(index).Count(x => IsLayerVisible(x.ShapeLayer.LayerId)), shape);
                }
                catch
                {
                    // WPF can attach the child before OnVisualParentChanged throws.
                    _visualCollection.Remove(shape);
                    throw;
                }
            }
            DetachShape(previous);
            shape.ShapeCreationCancelled += OnShapeCreationCancelled;
            shape.PropertyChanged += OnManagedShapePropertyChanged;
            publish();
            ShapeRemoved?.Invoke(this, previous);
            ShapeCreated?.Invoke(this, shape);
        }

        private void ClearShapesCore(Action publish)
        {
            var removed = Shapes.ToList();
            ApplySelection(new List<ShapeVisualBase>());
            if (_groups.Count > 0)
            {
                _groupsByShape.Clear();
                _groups.Clear();
                GroupsChanged?.Invoke(this, EventArgs.Empty);
            }
            CurrentGeometryInEdit = null;
            foreach (var shape in removed)
            {
                shape.ShapeCreationCancelled -= OnShapeCreationCancelled;
                shape.PropertyChanged -= OnManagedShapePropertyChanged;
            }
            _visualCollection?.Clear();
            publish();
            foreach (var shape in removed) ShapeRemoved?.Invoke(this, shape);
        }

        private void MoveShapeCore(int oldIndex, int newIndex, Action publish)
        {
            var ordered = Shapes.ToList();
            var moved = ordered[oldIndex];
            ordered.RemoveAt(oldIndex);
            ordered.Insert(newIndex, moved);
            RebuildVisualCollection(ordered);
            publish();
        }

        // Keep the public ObservableCollection contract while routing every write through
        // the same ownership, visual-tree, selection, and event lifecycle as repository methods.
        private sealed class ManagedShapeCollection : ObservableCollection<ShapeVisualBase>
        {
            private readonly SketchBoardDataManager _owner;
            public ManagedShapeCollection(SketchBoardDataManager owner) => _owner = owner;
            protected override void InsertItem(int index, ShapeVisualBase item)
            {
                CheckReentrancy();
                _owner.InsertShapeCore(item, index, () => base.InsertItem(index, item));
            }
            protected override void RemoveItem(int index)
            {
                CheckReentrancy();
                _owner.RemoveShapeCore(index, () => base.RemoveItem(index));
            }
            protected override void SetItem(int index, ShapeVisualBase item)
            {
                CheckReentrancy();
                _owner.ReplaceShapeCore(index, item, () => base.SetItem(index, item));
            }
            protected override void ClearItems()
            {
                CheckReentrancy();
                _owner.ClearShapesCore(base.ClearItems);
            }
            protected override void MoveItem(int oldIndex, int newIndex)
            {
                CheckReentrancy();
                _owner.MoveShapeCore(oldIndex, newIndex, () => base.MoveItem(oldIndex, newIndex));
            }
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
