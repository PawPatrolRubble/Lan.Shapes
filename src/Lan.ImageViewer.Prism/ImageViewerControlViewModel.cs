using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lan.Shapes;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Shapes;
using Prism.Commands;
using Prism.Mvvm;

#nullable enable

namespace Lan.ImageViewer.Prism
{
    public class ImageViewerControlViewModel : BindableBase, IImageViewerViewModel
    {
        private const double ScaleIncremental = 0.1;

        private readonly IGeometryTypeManager _geometryTypeManager;
        private readonly IGeometryIconProvider _iconProvider;
        private readonly IShapeLayerManager _shapeLayerManager;

        private double _scale;
        private GeometryType? _selectedGeometryType;
        private ShapeLayer _selectedShapeLayer;
        private Point _mouseDoubleClickPosition;
        private ImageSource _image = new BitmapImage();
        private bool _hideShapeList;
        private bool _showCrossLine = true;
        private ObservableCollection<GeometryType> _geometryTypeList = new();
        private ObservableCollection<ShapeLayer> _layers = new();
        private readonly HashSet<ShapeVisualBase> _observedShapes = new();
        private readonly Dictionary<int, ShapeLayerTreeNode> _layerNodes = new();
        private readonly Dictionary<ShapeVisualBase, int> _shapeLayerIds = new();
        private ShapeLayer? _targetShapeLayer;
        private bool _disposed;

        public ImageViewerControlViewModel(
            IShapeLayerManager shapeLayerManager,
            ISketchBoardDataManager sketchBoardDataManager,
            IGeometryTypeManager geometryTypeManager)
            : this(shapeLayerManager, sketchBoardDataManager, geometryTypeManager, null)
        {
        }

        public ImageViewerControlViewModel(
            IShapeLayerManager shapeLayerManager,
            ISketchBoardDataManager sketchBoardDataManager,
            IGeometryTypeManager geometryTypeManager,
            IGeometryIconProvider? geometryIconProvider)
        {
            SketchBoardDataManager = sketchBoardDataManager
                ?? throw new ArgumentNullException(nameof(sketchBoardDataManager));
            ShapeRepository = sketchBoardDataManager;
            _shapeLayerManager = shapeLayerManager
                ?? throw new ArgumentNullException(nameof(shapeLayerManager));
            _geometryTypeManager = geometryTypeManager
                ?? throw new ArgumentNullException(nameof(geometryTypeManager));
            _iconProvider = geometryIconProvider
                ?? new ResourceDictionaryGeometryIconProvider();

            if (_shapeLayerManager.Layers.Count == 0)
            {
                throw new InvalidOperationException(
                    "IShapeLayerManager must contain at least one layer before creating the view-model.");
            }

            _selectedShapeLayer = _shapeLayerManager.Layers[0];
            GeometryTypeList = new ObservableCollection<GeometryType>();

            Scale = 1;
            ShowSimpleCanvas = false;
            CreateGeometryTypeList();
            Image = CreateEmptyImageSource(2048, 2048);

            // Repository surface only — no VisualCollection / host init from the VM.
            ShapeRepository.SetShapeLayer(_selectedShapeLayer);

            ZoomOutCommand = new DelegateCommand(() => Scale *= 1 - ScaleIncremental);
            ZoomInCommand = new DelegateCommand(() => Scale *= 1 + ScaleIncremental);
            ScaleToFitCommand = new DelegateCommand(() => Scale = -1);
            ScaleToOriginalSizeCommand = new DelegateCommand(() => Scale = 0);
            ChooseGeometryTypeCommand = new DelegateCommand<GeometryType>(ChooseGeometryTypeCommandImpl);
            DeleteShapeCommand = new DelegateCommand(DeleteShapeCommandExecute,
                () => SelectedShapes.Count > 0 && SelectedShapes.All(x => !x.IsLocked));
            GroupShapesCommand = new DelegateCommand(GroupShapesCommandExecute, () => CanGroupSelectedShapes);
            UngroupShapesCommand = new DelegateCommand(UngroupShapesCommandExecute, () => CanUngroupSelectedShapes);
            SelectionModeCommand = new DelegateCommand(() =>
            {
                ShapeRepository.CancelCurrentSketch();
                ShapeRepository.UnselectGeometry();
            });

            _layers = _shapeLayerManager.Layers;
            _layers.CollectionChanged += Layers_CollectionChanged;
            Shapes.CollectionChanged += Shapes_CollectionChanged;
            SyncShapeSubscriptions();
            RebuildLayerGroups();

            ShapeRepository.SelectionChanged += Repository_SelectionChanged;
            ShapeRepository.LayerAssignmentsChanged += Repository_LayerAssignmentsChanged;
            ShapeRepository.GroupsChanged += Repository_GroupsChanged;
            _shapeLayerManager.LayerDefinitionChanged += Manager_LayerDefinitionChanged;
            _shapeLayerManager.ConfigurationChanged += Manager_ConfigurationChanged;
            if (_shapeLayerManager is INotifyPropertyChanged layerNotifications)
                layerNotifications.PropertyChanged += Manager_PropertyChanged;

            ShapeRepository.GeometryTypeUnselected += ShapeRepository_GeometryTypeUnselected;

            // Keep SelectedShape in sync when the board changes selection (mouse / keyboard).
            if (sketchBoardDataManager is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += Board_PropertyChanged;
            }
        }

        private void Repository_SelectionChanged(object? sender, EventArgs e) => RefreshSelection();

        private void Repository_GroupsChanged(object? sender, EventArgs e) => RefreshSelection(syncTarget: false);

        private void Repository_LayerAssignmentsChanged(object? sender, EventArgs e)
        {
            RebuildLayerGroups();
            RefreshSelection();
        }

        private void Manager_LayerDefinitionChanged(object? sender, ShapeLayer layer)
        {
            ShapeRepository.UpdateLayerConfiguration(layer.ToShapeLayerParameter());
            RefreshSelection(syncTarget: false);
        }

        private void Manager_ConfigurationChanged(object? sender, EventArgs e)
        {
            var currentId = _selectedShapeLayer?.LayerId;
            var targetId = TargetShapeLayer?.LayerId;
            ShapeRepository.ApplyLayerDefinitions(Layers);
            var current = Layers.FirstOrDefault(layer => layer.LayerId == currentId) ?? Layers.FirstOrDefault();
            if (current != null) SelectedShapeLayer = current;
            TargetShapeLayer = Layers.FirstOrDefault(layer => layer.LayerId == targetId);
            RebuildLayerGroups();
            RefreshSelection(syncTarget: false);
        }

        private void Manager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(LayerConfigurationStatus));
            RaisePropertyChanged(nameof(LayerConfigurationPath));
        }

        private void Board_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ISketchBoardDataManager.LayerVisibilityRevision))
                foreach (var node in LayerGroups)
                    node.SyncVisibility(ShapeRepository.IsLayerVisible(node.LayerId));
            if (e.PropertyName is null
                or nameof(IShapeRepository.SelectedGeometry)
                or nameof(SketchBoardDataManager.SelectedGeometry))
            {
                RaisePropertyChanged(nameof(SelectedShape));
            }
        }

        private void ShapeRepository_GeometryTypeUnselected(object? sender, Type e)
        {
            if (SelectedGeometryType != null && SelectedGeometryType.Name == e.Name)
            {
                SelectedGeometryType.IsSelected = false;
            }

            SelectedGeometryType = null;
        }

        public ICommand ChooseGeometryTypeCommand { get; }

        public ICommand DeleteShapeCommand { get; }
        public ICommand GroupShapesCommand { get; }
        public ICommand UngroupShapesCommand { get; }

        /// <inheritdoc />
        public ISketchBoardDataManager SketchBoardDataManager { get; }

        /// <inheritdoc />
        public IShapeRepository ShapeRepository { get; }

        /// <inheritdoc />
        public ObservableCollection<ShapeVisualBase> Shapes => ShapeRepository.Shapes;
        public ReadOnlyObservableCollection<ShapeVisualBase> SelectedShapes => ShapeRepository.SelectedGeometries;
        public int SelectedShapeCount => SelectedShapes.Count;
        public ShapeVisualBase? PropertyShape => SelectedShapeCount == 1 ? SelectedShape : null;
        public string SelectedLayerSummary => SelectedShapeCount == 0 ? "未选择图形"
            : SelectedShapes.Select(x => x.ShapeLayer.LayerId).Distinct().Count() == 1
                ? SelectedShapes[0].ShapeLayer.Name : "混合图层";
        public string SelectionPrompt => SelectedGroup != null ? $"已选 {SelectedGroup.Name}\n{GroupingPrompt}"
            : SelectedShapeCount > 1 ? $"已选 {SelectedShapeCount} 个图形\n可组合或统一修改所属图层"
            : "未选择图形\n请在列表或画布中选择图形";
        public ShapeGroup? SelectedGroup
        {
            get
            {
                var group = SelectedShapeCount > 0 ? ShapeRepository.GetGroup(SelectedShapes[0]) : null;
                return group != null && group.Members.Count == SelectedShapeCount
                    && group.Members.All(SelectedShapes.Contains) ? group : null;
            }
        }
        public string SelectedGroupSummary
        {
            get
            {
                if (SelectedGroup is { } group) return $"{group.Name} · {group.Members.Count} 个图形";
                var count = SelectedShapes.Select(ShapeRepository.GetGroup).Where(group => group != null).Distinct().Count();
                return count > 0 ? $"已选 {count} 个组合" : "未选中组合";
            }
        }
        public bool CanGroupSelectedShapes => ShapeRepository.CanGroupShapes(SelectedShapes);
        public bool CanUngroupSelectedShapes => SelectedShapes.Any(shape => ShapeRepository.GetGroup(shape) != null);
        public string GroupingPrompt
        {
            get
            {
                if (SelectedGroup is { } selectedGroup)
                {
                    if (selectedGroup.Members.Any(shape => shape.IsLocked))
                        return "组合包含锁定图形，请双击任一成员解锁后再移动。";
                    if (selectedGroup.Members.Any(shape => !shape.CanTranslate))
                        return "组合包含不支持整体移动的图形，可取消组合后分别编辑。";
                    return "拖动任一成员可一起移动；取消组合后可分别编辑。";
                }
                if (CanUngroupSelectedShapes) return "所选图形包含已有组合，请先取消组合再重新组合。";
                if (SelectedShapeCount < 2) return "请选择同一图层中至少两个可移动、未锁定的独立图形。";
                if (SelectedShapes.Select(shape => shape.ShapeLayer.LayerId).Distinct().Count() > 1)
                    return "组合成员必须属于同一图层，请先统一所属图层。";
                if (SelectedShapes.Any(shape => shape.IsLocked)) return "所选图形包含锁定图形，请先解锁。";
                if (SelectedShapes.Any(shape => !shape.CanTranslate)) return "所选图形包含不支持整体移动的图形。";
                if (!CanGroupSelectedShapes) return "请选择已完成、可见、可移动且未锁定的独立图形。";
                return "组合后，点击任一成员会选中整个组合并一起移动。";
            }
        }
        public ICommand SelectionModeCommand { get; }
        public ShapeLayer? TargetShapeLayer
        {
            get => _targetShapeLayer;
            set
            {
                if (SetProperty(ref _targetShapeLayer, value)) RaisePropertyChanged(nameof(CanAssignSelectedLayer));
            }
        }
        public bool CanAssignSelectedLayer => TargetShapeLayer != null && SelectedShapeCount > 0
            && SelectedShapes.All(x => x.IsGeometryRendered && !x.IsLocked);

        public int AssignSelectedShapesToLayer()
        {
            var target = Layers.FirstOrDefault(x => x.LayerId == TargetShapeLayer?.LayerId)
                ?? throw new InvalidOperationException("请选择目标图层。");
            return ShapeRepository.AssignShapesToLayer(SelectedShapes.ToList(), target);
        }

        private void RefreshSelection(bool syncTarget = true)
        {
            if (syncTarget)
                TargetShapeLayer = SelectedShapeCount > 0 && SelectedShapes.Select(x => x.ShapeLayer.LayerId).Distinct().Count() == 1
                    ? Layers.FirstOrDefault(x => x.LayerId == SelectedShapes[0].ShapeLayer.LayerId) : null;
            foreach (var property in new[] { nameof(SelectedShape), nameof(PropertyShape), nameof(SelectedShapeCount),
                nameof(SelectedLayerSummary), nameof(SelectionPrompt), nameof(CanAssignSelectedLayer),
                nameof(SelectedGroup), nameof(SelectedGroupSummary), nameof(GroupingPrompt),
                nameof(CanGroupSelectedShapes), nameof(CanUngroupSelectedShapes) })
                RaisePropertyChanged(property);
            ((DelegateCommand)DeleteShapeCommand).RaiseCanExecuteChanged();
            ((DelegateCommand)GroupShapesCommand).RaiseCanExecuteChanged();
            ((DelegateCommand)UngroupShapesCommand).RaiseCanExecuteChanged();
        }

        /// <inheritdoc />
        public ShapeVisualBase? SelectedShape
        {
            get => ShapeRepository.SelectedGeometry;
            set
            {
                if (ReferenceEquals(ShapeRepository.SelectedGeometry, value) && SelectedShapeCount <= 1)
                {
                    return;
                }

                ShapeRepository.SelectedGeometry = value;
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<GeometryType> GeometryTypeList { get; }

        public ObservableCollection<ShapeLayer> Layers
        {
            get => _layers;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (!ReferenceEquals(_shapeLayerManager.Layers, value))
                    throw new ArgumentException("Layers must be the collection owned by this viewer's layer manager.", nameof(value));
            }
        }

        public ObservableCollection<ShapeLayerTreeNode> LayerGroups { get; } = new();

        public void UpdateLayerConfiguration(ShapeLayerParameter parameter)
        {
            _shapeLayerManager.UpdateLayer(parameter);
        }

        public ShapeLayer CreateLayer(ShapeLayerParameter parameter)
        {
            var layer = _shapeLayerManager.CreateLayer(parameter);
            SelectedShapeLayer = layer;
            return layer;
        }
        public void SaveLayerConfiguration(string filePath = "") => _shapeLayerManager.SaveConfiguration(filePath);
        public string LayerConfigurationPath => _shapeLayerManager.ConfigurationFilePath;
        public string LayerConfigurationStatus => (string.IsNullOrWhiteSpace(LayerConfigurationPath)
            ? "未保存到文件" : Path.GetFileName(LayerConfigurationPath))
            + (_shapeLayerManager.HasUnsavedChanges ? " · 未保存修改" : " · 已保存");

        private void Layers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // The manager publishes ConfigurationChanged after the complete directory
            // commit; never change the drawing layer during intermediate collection events.
            RebuildLayerGroups();
        }

        private void Shapes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                SyncShapeSubscriptions();
                RebuildLayerGroups();
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                foreach (var layerId in e.NewItems!.Cast<ShapeVisualBase>().Select(shape => shape.ShapeLayer.LayerId).Distinct())
                    _layerNodes[layerId].Update(_layerNodes[layerId].Layer,
                        Shapes.Where(shape => shape.ShapeLayer.LayerId == layerId));
                return;
            }

            if (e.OldItems != null)
                foreach (ShapeVisualBase shape in e.OldItems)
                {
                    UnobserveShape(shape);
                    var layerId = _shapeLayerIds[shape];
                    _shapeLayerIds.Remove(shape);
                    var node = _layerNodes[layerId];
                    node.Shapes.Remove(shape);
                    if (node.Shapes.Count == 0 && !node.CanEdit) RemoveLayerNode(node);
                }

            if (e.NewItems != null)
            {
                var index = e.NewStartingIndex;
                var append = index == Shapes.Count - e.NewItems.Count;
                foreach (ShapeVisualBase shape in e.NewItems)
                {
                    ObserveShape(shape);
                    var layerId = shape.ShapeLayer.LayerId;
                    _shapeLayerIds[shape] = layerId;
                    if (!_layerNodes.TryGetValue(layerId, out var node))
                    {
                        node = CreateLayerNode(shape.ShapeLayer, canEdit: false);
                        LayerGroups.Add(node);
                    }
                    if (append) node.Shapes.Add(shape);
                    else node.Shapes.Insert(Shapes.Take(index).Count(candidate => candidate.ShapeLayer.LayerId == layerId), shape);
                    index++;
                }
            }
        }

        private void ObserveShape(ShapeVisualBase shape)
        {
            if (_observedShapes.Add(shape)) shape.PropertyChanged += Shape_PropertyChanged;
        }

        private void UnobserveShape(ShapeVisualBase shape)
        {
            if (_observedShapes.Remove(shape)) shape.PropertyChanged -= Shape_PropertyChanged;
        }

        private void SyncShapeSubscriptions()
        {
            var current = new HashSet<ShapeVisualBase>(Shapes);
            foreach (var shape in _observedShapes.Where(x => !current.Contains(x)).ToList())
                UnobserveShape(shape);
            foreach (var shape in current.Where(x => !_observedShapes.Contains(x)))
                ObserveShape(shape);
        }

        private void Shape_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ShapeVisualBase.IsLocked) or nameof(ShapeVisualBase.IsGeometryRendered)
                or nameof(ShapeVisualBase.CanTranslate))
                RefreshSelection(syncTarget: false);
        }

        private void RebuildLayerGroups()
        {
            var shapesByLayer = Shapes.GroupBy(shape => shape.ShapeLayer.LayerId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var configuredIds = new HashSet<int>(Layers.Select(layer => layer.LayerId));
            var layers = Layers.Concat(shapesByLayer.Values.Select(group => group[0].ShapeLayer))
                .GroupBy(x => x.LayerId).Select(x => x.First()).ToList();
            _shapeLayerIds.Clear();
            foreach (var group in shapesByLayer)
                foreach (var shape in group.Value) _shapeLayerIds[shape] = group.Key;
            var desiredIds = new HashSet<int>(layers.Select(x => x.LayerId));
            foreach (var obsolete in LayerGroups.Where(x => !desiredIds.Contains(x.LayerId)).ToList())
                RemoveLayerNode(obsolete);

            for (var index = 0; index < layers.Count; index++)
            {
                var layer = layers[index];
                if (!_layerNodes.TryGetValue(layer.LayerId, out var node))
                {
                    node = CreateLayerNode(layer, configuredIds.Contains(layer.LayerId));
                    LayerGroups.Insert(index, node);
                }
                else if (LayerGroups.IndexOf(node) != index)
                    LayerGroups.Move(LayerGroups.IndexOf(node), index);

                node.CanEdit = configuredIds.Contains(layer.LayerId);
                node.Update(layer, shapesByLayer.TryGetValue(layer.LayerId, out var shapes)
                    ? shapes : Enumerable.Empty<ShapeVisualBase>());
            }
        }

        private ShapeLayerTreeNode CreateLayerNode(ShapeLayer layer, bool canEdit)
        {
            var node = new ShapeLayerTreeNode(layer, ShapeRepository.IsLayerVisible(layer.LayerId),
                (id, visible) => ShapeRepository.SetLayerVisibility(id, visible)) { CanEdit = canEdit };
            _layerNodes.Add(layer.LayerId, node);
            return node;
        }

        private void RemoveLayerNode(ShapeLayerTreeNode node)
        {
            LayerGroups.Remove(node);
            _layerNodes.Remove(node.LayerId);
            node.Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _layers.CollectionChanged -= Layers_CollectionChanged;
            Shapes.CollectionChanged -= Shapes_CollectionChanged;
            ShapeRepository.SelectionChanged -= Repository_SelectionChanged;
            ShapeRepository.LayerAssignmentsChanged -= Repository_LayerAssignmentsChanged;
            ShapeRepository.GroupsChanged -= Repository_GroupsChanged;
            ShapeRepository.GeometryTypeUnselected -= ShapeRepository_GeometryTypeUnselected;
            _shapeLayerManager.LayerDefinitionChanged -= Manager_LayerDefinitionChanged;
            _shapeLayerManager.ConfigurationChanged -= Manager_ConfigurationChanged;
            if (_shapeLayerManager is INotifyPropertyChanged layerNotifications)
                layerNotifications.PropertyChanged -= Manager_PropertyChanged;
            if (SketchBoardDataManager is INotifyPropertyChanged boardNotifications)
                boardNotifications.PropertyChanged -= Board_PropertyChanged;
            foreach (var shape in _observedShapes.ToList()) UnobserveShape(shape);
            foreach (var node in LayerGroups) node.Dispose();
            LayerGroups.Clear();
            _layerNodes.Clear();
            _shapeLayerIds.Clear();
            GC.SuppressFinalize(this);
        }

        public ShapeLayer SelectedShapeLayer
        {
            get => _selectedShapeLayer;
            set
            {
                // ComboBox clears its selection while directory objects are replaced.
                // Keep the ID until ConfigurationChanged rebinds the completed directory.
                if (value == null) return;
                var definition = Layers.FirstOrDefault(layer => layer.LayerId == value.LayerId)
                    ?? throw new ArgumentException("The current layer must belong to this viewer's catalogue.", nameof(value));
                if (SetProperty(ref _selectedShapeLayer, definition))
                {
                    ShapeRepository.SetShapeLayer(definition);
                }
            }
        }

        public Point MouseDoubleClickPosition
        {
            get => _mouseDoubleClickPosition;
            set => SetProperty(ref _mouseDoubleClickPosition, value);
        }

        public GeometryType? SelectedGeometryType
        {
            get => _selectedGeometryType;
            set
            {
                SetProperty(ref _selectedGeometryType, value);
                if (_selectedGeometryType != null)
                {
                    ShapeRepository.SetGeometryType(
                        _geometryTypeManager.GetGeometryTypeByName(_selectedGeometryType.Name));
                }
            }
        }

        public ImageSource Image
        {
            get => _image;
            set => SetProperty(ref _image, value);
        }

        public double Scale
        {
            get => _scale;
            set => SetProperty(ref _scale, value);
        }

        public ICommand ZoomOutCommand { get; set; }
        public ICommand ZoomInCommand { get; set; }
        public ICommand ScaleToOriginalSizeCommand { get; set; }
        public ICommand ScaleToFitCommand { get; set; }

        public bool ShowSimpleCanvas
        {
            get => _hideShapeList;
            set => SetProperty(ref _hideShapeList, value);
        }

        public bool ShowShapeTypes { get; set; } = true;

        public bool ShowCrossLine
        {
            get => _showCrossLine;
            set => SetProperty(ref _showCrossLine, value);
        }

        private bool _showGeometries = true;

        /// <inheritdoc />
        public bool ShowGeometries
        {
            get => _showGeometries;
            set => SetProperty(ref _showGeometries, value);
        }

        public void FilterGeometryTypes(Expression<Func<GeometryType, bool>> predicate)
        {
            var func = predicate.Compile();
            GeometryTypeList.Clear();
            GeometryTypeList.AddRange(_geometryTypeList.Where(x => func(x)));
        }

        private void ChooseGeometryTypeCommandImpl(GeometryType? geometryType)
        {
            if (geometryType == null)
            {
                return;
            }

            ShowGeometries = true;

            if (SelectedGeometryType != null)
            {
                SelectedGeometryType.IsSelected = false;
            }

            SelectedGeometryType = geometryType;
            SelectedGeometryType.IsSelected = true;
        }

        private void DeleteShapeCommandExecute()
        {
            var selected = SelectedShapes.ToList();
            if (selected.Any(x => x.IsLocked)) return;
            foreach (var shape in selected) ShapeRepository.RemoveShape(shape);
        }

        private void GroupShapesCommandExecute()
        {
            if (!CanGroupSelectedShapes) return;
            ShapeRepository.CancelCurrentSketch();
            ShapeRepository.GroupShapes(SelectedShapes.ToList());
        }

        private void UngroupShapesCommandExecute()
        {
            if (!CanUngroupSelectedShapes) return;
            ShapeRepository.CancelCurrentSketch();
            ShapeRepository.UngroupShapes(SelectedShapes.ToList());
        }

        private void CreateGeometryTypeList()
        {
            _geometryTypeList = new ObservableCollection<GeometryType>(
                _geometryTypeManager.GetRegisteredGeometryTypes()
                    .Select(name => new GeometryType(name, name, _iconProvider.GetIcon(name))));

            GeometryTypeList.AddRange(_geometryTypeList);
        }

        private static ImageSource CreateEmptyImageSource(int width, int height)
        {
            var stride = width / 8;
            var pixels = new byte[height * stride];
            var colors = new List<Color>
            {
                Colors.Black,
                Colors.Blue,
                Colors.Green
            };
            var myPalette = new BitmapPalette(colors);

            return BitmapSource.Create(
                width, height,
                96, 96,
                PixelFormats.Indexed1,
                myPalette,
                pixels,
                stride);
        }
    }
}
