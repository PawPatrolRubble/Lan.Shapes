using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
        public event EventHandler ConfigurationChanged;
        private readonly IShapeStylerFactory _stylerFactory;
        private readonly ManagedLayerCollection _layers;
        private readonly HashSet<ShapeLayer> _observedLayers = new();
        private bool _restoringDefinition;

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
            _layers.VerifyMutationAllowed();
            var candidate = PrepareDefinition(definition, existingId: null);
            var parameter = candidate.ToShapeLayerParameter();
            parameter.LayerId = checked(Layers.Select(x => x.LayerId).DefaultIfEmpty(0).Max() + 1);
            candidate = new ShapeLayer(parameter, Configuration.Measurement, _stylerFactory);
            var configuration = BuildConfiguration(Layers.Concat(new[] { candidate }));
            PersistCandidate(configuration);
            PublishCatalogue(configuration, Layers.Concat(new[] { candidate }).ToList(),
                () => _layers.InsertCommitted(Layers.Count, candidate), string.IsNullOrWhiteSpace(_path));
            LayerDefinitionChanged?.Invoke(this, candidate);
            return candidate;
        }

        public void UpdateLayer(ShapeLayerParameter definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var existing = Layers.FirstOrDefault(x => x.LayerId == definition.LayerId)
                ?? throw new ArgumentException("The layer is not configured.", nameof(definition));
            var candidate = PrepareDefinition(definition, existing.LayerId);
            var commit = existing.PrepareConfiguration(candidate.ToShapeLayerParameter());
            var configuration = BuildConfiguration(Layers.Select(x => ReferenceEquals(x, existing) ? candidate : x));
            PersistCandidate(configuration);
            commit();
            Configuration = configuration;
            _hasUnsavedChanges = string.IsNullOrWhiteSpace(_path);
            existing.NotifyConfigurationChanged(committedToCatalogue: true);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(Configuration));
        }

        private ShapeLayer PrepareDefinition(ShapeLayerParameter definition, int? existingId)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition = definition.CreateValidatedCopy();
            var name = definition.Name;
            if (Layers.Any(x => x.LayerId != existingId &&
                string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("图层名称已存在，请使用其他名称。");
            return new ShapeLayer(definition, Configuration.Measurement, _stylerFactory);
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

            _layers.VerifyMutationAllowed();
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

            _path = Path.GetFullPath(configurationFilePath);
            PublishCatalogue(configuration, layers, () => _layers.ReplaceCommitted(layers), dirty: false);
            OnPropertyChanged(nameof(ConfigurationFilePath));
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

        public ObservableCollection<ShapeLayer> Layers => _layers;


        #endregion


        #region constructor

        public ShapeLayerManager()
            : this(new ShapeStylerFactory())
        {
        }

        public ShapeLayerManager(IShapeStylerFactory stylerFactory)
        {
            _stylerFactory = stylerFactory ?? throw new ArgumentNullException(nameof(stylerFactory));
            _layers = new ManagedLayerCollection(this);
        }

        private void PublishCatalogue(LanShapesConfiguration configuration, IReadOnlyCollection<ShapeLayer> next,
            Action publish, bool dirty)
        {
            _layers.VerifyMutationAllowed();
            foreach (var removed in _observedLayers.Where(layer => !next.Contains(layer)).ToList())
            {
                removed.ConfigurationChanging -= Layer_ConfigurationChanging;
                removed.DefinitionChanged -= Layer_DefinitionChanged;
                _observedLayers.Remove(removed);
            }
            foreach (var added in next.Where(layer => !_observedLayers.Contains(layer)))
            {
                added.ConfigurationChanging += Layer_ConfigurationChanging;
                added.DefinitionChanged += Layer_DefinitionChanged;
                _observedLayers.Add(added);
            }
            Configuration = configuration;
            if (_selectedLayer != null && !next.Contains(_selectedLayer)) _selectedLayer = next.FirstOrDefault();
            _hasUnsavedChanges = dirty;
            // Publish only after the complete state is installed. External callbacks are
            // ordinary edits and must retain validation, snapshots and dirty notifications.
            publish();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(Configuration));
            OnPropertyChanged(nameof(SelectedLayer));
            ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ChangeCatalogue(List<ShapeLayer> next, Action publish)
        {
            if (next.Count == 0) throw new InvalidOperationException("The layer catalogue must contain at least one layer.");
            var ids = new HashSet<int>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var layer in next)
            {
                if (layer == null) throw new ArgumentNullException(nameof(next));
                var parameter = layer.ToShapeLayerParameter().CreateValidatedCopy();
                if (!ids.Add(layer.LayerId) || !names.Add(parameter.Name))
                    throw new ArgumentException("Layer IDs and names must be unique.", nameof(next));
            }
            var measurement = Layers.Count == 0 ? next[0].Measurement : Configuration.Measurement;
            var configuration = BuildConfiguration(next);
            configuration.Measurement = measurement;
            configuration.Validate();
            foreach (var layer in next.Where(layer => !_observedLayers.Contains(layer)))
                if (!ReferenceEquals(layer.Measurement, measurement))
                    layer.ApplyDefinition(new ShapeLayer(layer.ToShapeLayerParameter(), measurement, _stylerFactory));
            PublishCatalogue(configuration, next, publish, dirty: true);
        }

        private void Layer_ConfigurationChanging(object sender, ShapeLayerParameter parameter)
        {
            if (Layers.Any(layer => !ReferenceEquals(layer, sender)
                && string.Equals(layer.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("A layer with that name already exists.", nameof(parameter));
        }

        private void Layer_DefinitionChanged(object sender, EventArgs e)
        {
            if (_restoringDefinition || sender is not ShapeLayer layer) return;
            if (ReferenceEquals(e, ShapeLayer.CatalogueCommitNotification))
            {
                // The command already installed the saved snapshot. Nested external edits
                // have ordinary event arguments and still update snapshots and dirty state.
                LayerDefinitionChanged?.Invoke(this, layer);
                return;
            }
            LanShapesConfiguration candidate;
            try { candidate = BuildConfiguration(Layers); }
            catch
            {
                var previous = Configuration.ShapeLayers.First(parameter => parameter.LayerId == layer.LayerId);
                _restoringDefinition = true;
                try { layer.ApplyConfiguration(previous); }
                finally { _restoringDefinition = false; }
                throw;
            }
            Configuration = candidate;
            SetUnsavedChanges(true);
            OnPropertyChanged(nameof(Configuration));
            LayerDefinitionChanged?.Invoke(this, layer);
        }

        private sealed class ManagedLayerCollection : ObservableCollection<ShapeLayer>
        {
            private readonly ShapeLayerManager _owner;
            public ManagedLayerCollection(ShapeLayerManager owner) => _owner = owner;
            public void VerifyMutationAllowed() => CheckReentrancy();
            protected override void InsertItem(int index, ShapeLayer item)
            {
                CheckReentrancy();
                var next = this.ToList();
                next.Insert(index, item);
                _owner.ChangeCatalogue(next, () => base.InsertItem(index, item));
            }
            protected override void RemoveItem(int index)
            {
                CheckReentrancy();
                var next = this.ToList();
                next.RemoveAt(index);
                _owner.ChangeCatalogue(next, () => base.RemoveItem(index));
            }
            protected override void SetItem(int index, ShapeLayer item)
            {
                CheckReentrancy();
                var next = this.ToList();
                next[index] = item;
                _owner.ChangeCatalogue(next, () => base.SetItem(index, item));
            }
            protected override void MoveItem(int oldIndex, int newIndex)
            {
                CheckReentrancy();
                var next = this.ToList();
                var item = next[oldIndex];
                next.RemoveAt(oldIndex);
                next.Insert(newIndex, item);
                _owner.ChangeCatalogue(next, () => base.MoveItem(oldIndex, newIndex));
            }
            protected override void ClearItems()
            {
                if (Count != 0) throw new InvalidOperationException("Read a complete configuration to replace the layer catalogue.");
            }
            public void InsertCommitted(int index, ShapeLayer item) => base.InsertItem(index, item);
            public void ReplaceCommitted(IEnumerable<ShapeLayer> layers)
            {
                CheckReentrancy();
                Items.Clear();
                foreach (var layer in layers) Items.Add(layer);
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
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
