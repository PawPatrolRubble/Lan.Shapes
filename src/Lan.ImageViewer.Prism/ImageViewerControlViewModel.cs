using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        private ObservableCollection<GeometryType> _geometryTypeList = new();

        public ImageViewerControlViewModel(
            IShapeLayerManager shapeLayerManager,
            ISketchBoardDataManager sketchBoardDataManager,
            IGeometryTypeManager geometryTypeManager,
            IGeometryIconProvider? geometryIconProvider = null)
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
            ShowSimpleCanvas = true;
            CreateGeometryTypeList();
            Image = CreateEmptyImageSource(2048, 2048);

            // Repository surface only — no VisualCollection / host init from the VM.
            ShapeRepository.SetShapeLayer(_selectedShapeLayer);

            ZoomOutCommand = new DelegateCommand(() => Scale *= 1 - ScaleIncremental);
            ZoomInCommand = new DelegateCommand(() => Scale *= 1 + ScaleIncremental);
            ScaleToFitCommand = new DelegateCommand(() => Scale = -1);
            ScaleToOriginalSizeCommand = new DelegateCommand(() => Scale = 0);
            ChooseGeometryTypeCommand = new DelegateCommand<GeometryType>(ChooseGeometryTypeCommandImpl);
            DeleteShapeCommand = new DelegateCommand(DeleteShapeCommandExecute);

            Layers = new ObservableCollection<ShapeLayer>(_shapeLayerManager.Layers);

            ShapeRepository.GeometryTypeUnselected += ShapeRepository_GeometryTypeUnselected;

            // Keep SelectedShape in sync when the board changes selection (mouse / keyboard).
            if (sketchBoardDataManager is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += Board_PropertyChanged;
            }
        }

        private void Board_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
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

        /// <inheritdoc />
        public ShapeVisualBase? SelectedShape
        {
            get => ShapeRepository.SelectedGeometry;
            set
            {
                if (ReferenceEquals(ShapeRepository.SelectedGeometry, value))
                {
                    return;
                }

                ShapeRepository.SelectedGeometry = value;
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<GeometryType> GeometryTypeList { get; }

        public ObservableCollection<ShapeLayer> Layers { get; set; }

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

            if (SelectedGeometryType != null)
            {
                SelectedGeometryType.IsSelected = false;
            }

            SelectedGeometryType = geometryType;
            SelectedGeometryType.IsSelected = true;
        }

        private void DeleteShapeCommandExecute()
        {
            // List selection maps to SelectedShape (SelectedGeometry), not the
            // in-progress sketch CurrentGeometryInEdit.
            if (SelectedShape != null)
            {
                ShapeRepository.RemoveShape(SelectedShape);
            }
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
