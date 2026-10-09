using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Lan.ImageViewer.Converters;

/// <summary>Shows child branches and ends a sibling spine at the last item's header.</summary>
public sealed class TreeBranchVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 0 || values[0] is not TreeViewItem) return Visibility.Collapsed;
        if (!Equals(parameter, "Tail")) return Visibility.Visible;
        return values.Length >= 3 && values[1] is int index && values[2] is int count && index < count - 1
            ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
