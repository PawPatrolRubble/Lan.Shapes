using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
        public string ConfigurationFilePath => _path;
        private bool _hasUnsavedChanges;
        public bool HasUnsavedChanges => _hasUnsavedChanges;
        public event EventHandler<ShapeLayer> LayerDefinitionChanged;
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
            var destination = string.IsNullOrWhiteSpace(filePath) ? _path : filePath;
            if (string.IsNullOrWhiteSpace(destination))
                throw new InvalidOperationException(
                    "A configuration file path is required before saving.");
            var candidate = BuildConfiguration(Layers);
            destination = Path.GetFullPath(destination);
            WriteConfiguration(candidate, destination);
            Configuration = candidate;
            _path = destination;
            SetUnsavedChanges(false);
            OnPropertyChanged(nameof(ConfigurationFilePath));
            OnPropertyChanged(nameof(Configuration));
        }

        public ShapeLayer CreateLayer(ShapeLayerParameter definition)
        {
            var candidate = PrepareDefinition(definition, existingId: null);
            var parameter = candidate.ToShapeLayerParameter();
            parameter.LayerId = checked(Layers.Select(x => x.LayerId).DefaultIfEmpty(0).Max() + 1);
            candidate = new ShapeLayer(parameter, Configuration.Measurement, _stylerFactory);
            var configuration = BuildConfiguration(Layers.Concat(new[] { candidate }));
            PersistCandidate(configuration);
            Layers.Add(candidate);
            Configuration = configuration;
            SetUnsavedChanges(string.IsNullOrWhiteSpace(_path));
            LayerDefinitionChanged?.Invoke(this, candidate);
            return candidate;
        }

        public void UpdateLayer(ShapeLayerParameter definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var existing = Layers.FirstOrDefault(x => x.LayerId == definition.LayerId)
                ?? throw new ArgumentException("The layer is not configured.", nameof(definition));
            var candidate = PrepareDefinition(definition, existing.LayerId);
            var configuration = BuildConfiguration(Layers.Select(x => ReferenceEquals(x, existing) ? candidate : x));
            PersistCandidate(configuration);
            existing.ApplyConfiguration(candidate.ToShapeLayerParameter());
            Configuration = configuration;
            SetUnsavedChanges(string.IsNullOrWhiteSpace(_path));
            LayerDefinitionChanged?.Invoke(this, existing);
        }

        private ShapeLayer PrepareDefinition(ShapeLayerParameter definition, int? existingId)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var name = definition.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("请输入图层名称。");
            if (Layers.Any(x => x.LayerId != existingId &&
                string.Equals(x.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("图层名称已存在，请使用其他名称。");
            if (definition.StyleSchema == null) throw new ArgumentException("图层样式不能为空。");
            foreach (var entry in definition.StyleSchema)
            {
                var style = entry.Value;
                if (style == null || !double.IsFinite(style.StrokeThickness) || style.StrokeThickness < 0
                    || !double.IsFinite(style.FillOpacity) || style.FillOpacity < 0 || style.FillOpacity > 1
                    || !double.IsFinite(style.DragHandleSize) || style.DragHandleSize < 0
                    || style.StrokeColor == null || style.FillColor == null)
                    throw new ArgumentException("图层颜色、线宽、句柄大小或透明度无效。");
                if (!string.IsNullOrEmpty(style.DashStyle) && !new[] { "Solid", "Dash", "Dot", "DashDot", "DashDotDot" }
                    .Contains(style.DashStyle, StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException("图层线型无效。");
            }
            var layer = new ShapeLayer(definition, Configuration.Measurement, _stylerFactory);
            if (definition.StyleSchema[Lan.Shapes.Enums.ShapeVisualState.Normal].StrokeThickness <= 0)
                throw new ArgumentException("普通状态线宽必须大于 0。");
            var parameter = layer.ToShapeLayerParameter();
            parameter.Name = name;
            layer.ApplyConfiguration(parameter);
            return layer;
        }

        private LanShapesConfiguration BuildConfiguration(IEnumerable<ShapeLayer> layers)
        {
            var configuration = new LanShapesConfiguration
            {
                Measurement = Configuration.Measurement,
                AvailableGeometryTypes = Configuration.AvailableGeometryTypes?.ToList(),
                ShapeLayers = layers.Select(x => x.ToShapeLayerParameter()).ToList()
            };
            configuration.Validate();
            return configuration;
        }

        private void PersistCandidate(LanShapesConfiguration candidate)
        {
            if (!string.IsNullOrWhiteSpace(_path)) WriteConfiguration(candidate, _path);
        }

        private static void WriteConfiguration(LanShapesConfiguration configuration, string destination)
        {
            var serialized = JsonConvert.SerializeObject(configuration, Formatting.Indented);
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
                    writer.Write(serialized);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private void SetUnsavedChanges(bool value)
        {
            _hasUnsavedChanges = value;
            OnPropertyChanged(nameof(HasUnsavedChanges));
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
            _path = Path.GetFullPath(configurationFilePath);
            SetUnsavedChanges(false);
            OnPropertyChanged(nameof(ConfigurationFilePath));
            OnPropertyChanged(nameof(Configuration));
            foreach (var layer in Layers) LayerDefinitionChanged?.Invoke(this, layer);
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
            Layers.CollectionChanged += (_, _) => SetUnsavedChanges(true);
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
