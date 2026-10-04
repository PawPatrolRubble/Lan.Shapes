using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Enums;

namespace Lan.ImageViewer
{
    public partial class LayerEditorWindow : Window
    {
        private readonly ShapeLayerParameter _parameter;
        private readonly Action<ShapeLayerParameter> _apply;
        private ShapeVisualState _editingState = ShapeVisualState.Normal;
        private ShapeStylerParameter _displayedStyle;
        private bool _loadingStyle;

        public LayerEditorWindow(ShapeLayer layer) : this(layer, false, null) { }

        public LayerEditorWindow(ShapeLayer layer, bool isNew, Action<ShapeLayerParameter> apply)
        {
            InitializeComponent();
            _parameter = layer.ToShapeLayerParameter();
            _apply = apply;
            Title = isNew ? "新增图层" : "编辑图层";
            NameBox.Text = isNew ? string.Empty : _parameter.Name;
            DescriptionBox.Text = _parameter.Description;
            _loadingStyle = true;
            StyleStateBox.SelectedIndex = 0;
            _loadingStyle = false;
            LoadStyle();
        }

        private void LoadStyle()
        {
            _displayedStyle = _parameter.StyleSchema.TryGetValue(_editingState, out var style)
                ? style : _parameter.StyleSchema[ShapeVisualState.Normal];
            StrokeColorPicker.SelectedColor = (_displayedStyle.StrokeColor as SolidColorBrush)?.Color;
            FillColorPicker.SelectedColor = (_displayedStyle.FillColor as SolidColorBrush)?.Color;
            DashStyleBox.SelectedItem = DashStyleBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(x => (string)x.Content == _displayedStyle.DashStyle) ?? DashStyleBox.Items[0];
            StrokeThicknessBox.Text = _displayedStyle.StrokeThickness.ToString(CultureInfo.CurrentCulture);
            FillOpacityBox.Text = _displayedStyle.FillOpacity.ToString(CultureInfo.CurrentCulture);
        }

        public ShapeLayerParameter EditedParameter => _parameter;

        private void StyleState_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingStyle || _parameter == null ||
                StyleStateBox.SelectedItem is not ComboBoxItem item ||
                !Enum.TryParse((string)item.Tag, out ShapeVisualState state) || state == _editingState)
                return;
            if (!TrySaveCurrentStyle(out var error))
            {
                _loadingStyle = true;
                StyleStateBox.SelectedItem = StyleStateBox.Items.Cast<ComboBoxItem>()
                    .Single(x => (string)x.Tag == _editingState.ToString());
                _loadingStyle = false;
                ShowValidationError(error);
                return;
            }
            _editingState = state;
            LoadStyle();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show(this, "请输入图层名称。", "图层", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameBox.Focus();
                return;
            }
            if (!TrySaveCurrentStyle(out var error))
            {
                ShowValidationError(error);
                return;
            }
            _parameter.Name = NameBox.Text.Trim();
            _parameter.Description = DescriptionBox.Text.Trim();
            try { _apply?.Invoke(_parameter); }
            catch (Exception saveError)
            {
                MessageBox.Show(this, saveError.Message, "图层保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            DialogResult = true;
        }

        private bool TrySaveCurrentStyle(out string error)
        {
            error = string.Empty;
            if (!TryReadDouble(StrokeThicknessBox.Text, out var thickness) || thickness <= 0 ||
                !TryReadDouble(FillOpacityBox.Text, out var opacity) || opacity < 0 || opacity > 1)
            {
                error = "线宽必须大于 0，填充不透明度必须在 0 到 1 之间。";
                return false;
            }
            if (StrokeColorPicker.SelectedColor is not Color stroke ||
                FillColorPicker.SelectedColor is not Color fill)
            {
                error = "请选择线条和填充颜色。";
                return false;
            }
            var dash = (string)((ComboBoxItem)DashStyleBox.SelectedItem).Content;
            // Viewing an optional state must preserve its fallback to Normal.
            if (stroke != (_displayedStyle.StrokeColor as SolidColorBrush)?.Color ||
                fill != (_displayedStyle.FillColor as SolidColorBrush)?.Color ||
                thickness != _displayedStyle.StrokeThickness || opacity != _displayedStyle.FillOpacity ||
                dash != _displayedStyle.DashStyle)
            {
                _parameter.StyleSchema[_editingState] = new ShapeStylerParameter
                {
                    StrokeColor = new SolidColorBrush(stroke), FillColor = new SolidColorBrush(fill),
                    DashStyle = dash, StrokeThickness = thickness, FillOpacity = opacity,
                    DragHandleSize = _displayedStyle.DragHandleSize
                };
                _displayedStyle = _parameter.StyleSchema[_editingState];
            }
            return true;
        }

        private void ShowValidationError(string error)
            => MessageBox.Show(this, error, "图层", MessageBoxButton.OK, MessageBoxImage.Warning);

        private static bool TryReadDouble(string text, out double value)
        {
            return (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                    double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) &&
                   double.IsFinite(value);
        }
    }
}
