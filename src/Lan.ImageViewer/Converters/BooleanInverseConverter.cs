using System;
using System.Globalization;
using System.Windows.Data;

namespace Lan.ImageViewer.Converters
{
    public sealed class BooleanInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool original && !original;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool original && !original;
    }
}
