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
        private ShapeLayer? _targetShapeLayer;

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
            SelectionModeCommand = new DelegateCommand(() =>
            {
                if (ShapeRepository.CurrentGeometryInEdit is { IsGeometryRendered: false } unfinished)
                    ShapeRepository.RemoveShape(unfinished);
                ShapeRepository.UnselectGeometry();
                ShapeRepository.UnselectGeometryType();
            });

            Layers = _shapeLayerManager.Layers;
            Shapes.CollectionChanged += Shapes_CollectionChanged;
            SyncShapeSubscriptions();
            RebuildLayerGroups();

            ShapeRepository.SelectionChanged += (_, _) => RefreshSelection();
            ShapeRepository.LayerAssignmentsChanged += (_, _) =>
            {
                RebuildLayerGroups();
                RefreshSelection();
            };
            _shapeLayerManager.LayerDefinitionChanged += (_, layer) =>
            {
                ShapeRepository.UpdateLayerConfiguration(layer.ToShapeLayerParameter());
                RebuildLayerGroups();
                RefreshSelection();
            };
            if (_shapeLayerManager is INotifyPropertyChanged layerNotifications)
                layerNotifications.PropertyChanged += (_, _) =>
                {
                    RaisePropertyChanged(nameof(LayerConfigurationStatus));
                    RaisePropertyChanged(nameof(LayerConfigurationPath));
                };

            ShapeRepository.GeometryTypeUnselected += ShapeRepository_GeometryTypeUnselected;

            // Keep SelectedShape in sync when the board changes selection (mouse / keyboard).
            if (sketchBoardDataManager is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += Board_PropertyChanged;
            }
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
        public string SelectionPrompt => SelectedShapeCount > 1 ? $"已选 {SelectedShapeCount} 个图形\n可统一修改所属图层"
            : "未选择图形\n请在列表或画布中选择图形";
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

        private void RefreshSelection()
        {
            TargetShapeLayer = SelectedShapeCount > 0 && SelectedShapes.Select(x => x.ShapeLayer.LayerId).Distinct().Count() == 1
                ? Layers.FirstOrDefault(x => x.LayerId == SelectedShapes[0].ShapeLayer.LayerId) : null;
            foreach (var property in new[] { nameof(SelectedShape), nameof(PropertyShape), nameof(SelectedShapeCount),
                nameof(SelectedLayerSummary), nameof(SelectionPrompt), nameof(CanAssignSelectedLayer) })
                RaisePropertyChanged(property);
            ((DelegateCommand)DeleteShapeCommand).RaiseCanExecuteChanged();
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
                if (ReferenceEquals(_layers, value)) return;
                _layers.CollectionChanged -= Layers_CollectionChanged;
                _layers = value ?? throw new ArgumentNullException(nameof(value));
                _layers.CollectionChanged += Layers_CollectionChanged;
                RaisePropertyChanged();
                RebuildLayerGroups();
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
            if (Layers.Count > 0 && !Layers.Contains(_selectedShapeLayer)) SelectedShapeLayer = Layers[0];
            RebuildLayerGroups();
        }

        private void Shapes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SyncShapeSubscriptions();
            RebuildLayerGroups();
        }

        private void SyncShapeSubscriptions()
        {
            var current = new HashSet<ShapeVisualBase>(Shapes);
            foreach (var shape in _observedShapes.Where(x => !current.Contains(x)).ToList())
            {
                shape.PropertyChanged -= Shape_PropertyChanged;
                _observedShapes.Remove(shape);
            }
            foreach (var shape in current.Where(x => !_observedShapes.Contains(x)))
            {
                shape.PropertyChanged += Shape_PropertyChanged;
                _observedShapes.Add(shape);
            }
        }

        private void Shape_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ShapeVisualBase.IsLocked) or nameof(ShapeVisualBase.IsGeometryRendered))
                RefreshSelection();
        }

        private void RebuildLayerGroups()
        {
            var layers = Layers.Concat(Shapes.Select(x => x.ShapeLayer))
                .GroupBy(x => x.LayerId).Select(x => x.First()).ToList();
            var desiredIds = new HashSet<int>(layers.Select(x => x.LayerId));
            foreach (var obsolete in LayerGroups.Where(x => !desiredIds.Contains(x.LayerId)).ToList())
            {
                LayerGroups.Remove(obsolete);
                obsolete.Dispose();
            }

            for (var index = 0; index < layers.Count; index++)
            {
                var layer = layers[index];
                var node = LayerGroups.FirstOrDefault(x => x.LayerId == layer.LayerId);
                if (node == null)
                {
                    node = new ShapeLayerTreeNode(layer,
                        ShapeRepository.IsLayerVisible(layer.LayerId),
                        (id, visible) => ShapeRepository.SetLayerVisibility(id, visible));
                    LayerGroups.Insert(index, node);
                }
                else if (LayerGroups.IndexOf(node) != index)
                    LayerGroups.Move(LayerGroups.IndexOf(node), index);

                node.Update(layer, Shapes.Where(x => x.ShapeLayer.LayerId == layer.LayerId));
            }
        }

        public ShapeLayer SelectedShapeLayer
        {
            get => _selectedShapeLayer;
            set
            {
                if (SetProperty(ref _selectedShapeLayer, value) && value != null)
                {
                    ShapeRepository.SetShapeLayer(value);
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
