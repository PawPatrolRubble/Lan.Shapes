using System;
using System.Globalization;
using System.Windows.Data;

namespace Lan.ImageViewer.Converters
{
    public sealed class AvailablePanelHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double height ? Math.Max(0, height - 70) : 0d;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
