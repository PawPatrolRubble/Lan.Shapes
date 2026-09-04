using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Styler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lan.ImageViewer.Prism
{
    public class ShapeLayerManager : DependencyObject, IShapeLayerManager, INotifyPropertyChanged
    {

        #region fields

        private string _path = string.Empty;
        private readonly IShapeStylerFactory _stylerFactory;

        #endregion

        #region properties

        public event PropertyChangedEventHandler PropertyChanged;


        private ShapeLayer _selectedLayer;

        public ShapeLayer SelectedLayer
        {
            get => _selectedLayer ?? Layers[0];
            set
            {
                _selectedLayer = value;
                OnPropertyChanged();
            }
        }

        public LanShapesConfiguration Configuration { get; private set; } =
            new LanShapesConfiguration();

        public void SaveConfiguration(string filePath = "")
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                _path = filePath;
            }

            if (string.IsNullOrWhiteSpace(_path))
            {
                throw new InvalidOperationException(
                    "A configuration file path is required before saving.");
            }

            Configuration.ShapeLayers = Layers
                .Select(x => x.ToShapeLayerParameter())
                .ToList();
            Configuration.Validate();

            var serialized = JsonConvert.SerializeObject(
                Configuration,
                Formatting.Indented);
            File.WriteAllText(_path, serialized);
        }

        public void ReadConfiguration(string configurationFilePath)
        {
            if (string.IsNullOrWhiteSpace(configurationFilePath))
            {
                return;
            }

            var json = File.ReadAllText(configurationFilePath);
            var token = JToken.Parse(json);
            var configuration = token.Type == JTokenType.Array
                ? MigrateLegacyConfiguration(token, configurationFilePath)
                : token.ToObject<LanShapesConfiguration>()
                  ?? throw new InvalidOperationException(
                      $"Lan.Shapes configuration '{configurationFilePath}' is empty or invalid.");

            configuration.Validate();

            var layers = configuration.ShapeLayers
                .Select(x => new ShapeLayer(x, configuration.Measurement, _stylerFactory))
                .ToList();

            Layers.Clear();
            CollectionExtension.AddRange(Layers, layers);

            Configuration = configuration;
            _path = configurationFilePath;
            OnPropertyChanged(nameof(Configuration));
        }

        [Obsolete("Use SaveConfiguration.")]
        public void SaveLayerConfigurations(string filePath)
        {
            SaveConfiguration(filePath);
        }

        [Obsolete("Use ReadConfiguration.")]
        public void ReadShapeLayers(string configurationFilePath)
        {
            ReadConfiguration(configurationFilePath);
        }

        public ObservableCollection<ShapeLayer> Layers { get; private set; } = new ObservableCollection<ShapeLayer>();


        #endregion


        #region constructor

        public ShapeLayerManager()
            : this(new ShapeStylerFactory())
        {
        }

        public ShapeLayerManager(IShapeStylerFactory stylerFactory)
        {
            _stylerFactory = stylerFactory ?? throw new ArgumentNullException(nameof(stylerFactory));
        }

        #endregion

        #region public methods

        private static LanShapesConfiguration MigrateLegacyConfiguration(
            JToken token,
            string configurationFilePath)
        {
            var legacyLayers = token.ToObject<List<LegacyShapeLayerParameter>>()
                ?? throw new InvalidOperationException(
                    $"Shape layer configuration '{configurationFilePath}' is empty or invalid.");

            if (legacyLayers.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Shape layer configuration '{configurationFilePath}' contains no layers.");
            }

            var first = legacyLayers[0];
            return new LanShapesConfiguration
            {
                Measurement = new ShapeMeasurementSettings
                {
                    PixelPerUnit = first.PixelPerUnit,
                    UnitsPerMillimeter = first.UnitsPerMillimeter,
                    UnitName = first.UnitName
                },
                ShapeLayers = legacyLayers.Cast<ShapeLayerParameter>().ToList()
            };
        }

        private sealed class LegacyShapeLayerParameter : ShapeLayerParameter
        {
            public double PixelPerUnit { get; set; }
            public int UnitsPerMillimeter { get; set; }
            public string UnitName { get; set; } = string.Empty;
        }

        private Brush ColorWithOpacity(string colorString, double opacity)
        {
            var b = FromHexStringToBrush(colorString);

            b.Opacity = opacity;
            return b;
        }

        private DashStyle ConvertToDashStyleFromString(string s)
        {
            switch (s)
            {
                case var dash when s.Equals("dash", StringComparison.OrdinalIgnoreCase):
                    return DashStyles.Dash;
                case var dash when s.Equals("DashDot", StringComparison.OrdinalIgnoreCase):
                    return DashStyles.DashDot;
                case var dash when s.Equals("DashDotDot", StringComparison.OrdinalIgnoreCase):
                    return DashStyles.DashDotDot;
                case var dash when s.Equals("Dot", StringComparison.OrdinalIgnoreCase):
                    return DashStyles.Dot;
                case var dash when s.Equals("Solid", StringComparison.OrdinalIgnoreCase):
                default:
                    return DashStyles.Solid;
            }
        }

        private Brush FromHexStringToBrush(string hexString)
        {
            var converter = new System.Windows.Media.BrushConverter();
            return (Brush)converter.ConvertFromString(hexString);
        }


        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
