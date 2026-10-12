using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Lan.ImageViewer.Converters
{
    public sealed class LayerConfigurationFileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string path && !string.IsNullOrWhiteSpace(path)
                ? Path.GetFileName(path) : "未保存到文件";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    public sealed class LayerConfigurationSaveStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value as string ?? string.Empty;
            var saved = !status.StartsWith("未保存到文件", StringComparison.Ordinal)
                && status.EndsWith(" · 已保存", StringComparison.Ordinal);
            if (Equals(parameter, "IsSaved")) return saved;
            return saved ? "已保存" : "未保存";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    public sealed class ShapeTreeIconConverter : IValueConverter
    {
        private readonly ResourceDictionaryGeometryIconProvider _icons = new ResourceDictionaryGeometryIconProvider();
        private readonly Geometry _fallbackIcon = Geometry.Parse("M2,2 H5 V5 H2 Z M11,2 H14 V5 H11 Z M2,11 H5 V14 H2 Z M11,11 H14 V14 H11 Z M4,4 H12 V12 H4 Z");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => _icons.GetIcon(value as string) ?? _fallbackIcon;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    public sealed class ShapeTreeDetailConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4) return string.Empty;
            if (Equals(values[0], "Cross")) return "(十字基准点)";
            if (Equals(values[0], "Circle") && values[1] is Rect bounds && !bounds.IsEmpty
                && values[2] is double pixelsPerUnit && pixelsPerUnit > 0 && values[3] is string unit)
                return string.Format(culture, "(Ø {0:0.###} {1})", bounds.Width / pixelsPerUnit, unit);
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public sealed class ShapeSelectionSummaryConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => values.Length >= 3 && values[0] is int count && count == 1 && values[1] is string type
                ? $"{type} · {values[2]}" : values.Length >= 3 ? values[2] : string.Empty;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
