using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Lan.ImageViewer;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class GeometryTypeTemplateTests
{
    [Fact]
    public void GeometryTypeTemplate_UnselectedPathHasFillWithoutAppTheme()
    {
        Exception? error = null;
        Geometry? fillGeometry = null;
        Brush? fill = null;

        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    new Application();
                }

                var dictionary = new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/Lan.ImageViewer;component/Style.xaml",
                        UriKind.Absolute)
                };

                var template = Assert.IsType<DataTemplate>(dictionary["GeometryTypeTemplate"]);
                var geometryType = new GeometryType(
                    "Line",
                    "Line",
                    Geometry.Parse("M0,0 L10,0 L10,10 Z"));
                var content = template.LoadContent();
                if (content is FrameworkElement element)
                {
                    element.DataContext = geometryType;
                    element.Measure(new Size(64, 64));
                    element.Arrange(new Rect(0, 0, 64, 64));
                    element.UpdateLayout();
                }

                var path = FindDescendant<Path>(content);
                Assert.NotNull(path);
                fillGeometry = path!.Data;
                fill = path.Fill;
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null)
        {
            throw error;
        }

        Assert.NotNull(fillGeometry);
        Assert.NotNull(fill);
        Assert.Equal(Colors.Black, ((SolidColorBrush)fill!).Color);
    }

    [Fact]
    public void DefaultIconProvider_LoadsPaletteGeometriesFromPackUri()
    {
        Exception? error = null;
        var icons = new Dictionary<string, bool>();

        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    new Application();
                }

                IGeometryIconProvider provider = new ResourceDictionaryGeometryIconProvider();
                foreach (var name in new[] { "Line", "Rectangle", "Rectangle2", "Circle", "Cross", "DxfGeometry" })
                {
                    icons[name] = provider.GetIcon(name) != null;
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null)
        {
            throw error;
        }

        Assert.Equal(
            new Dictionary<string, bool>
            {
                ["Line"] = true,
                ["Rectangle"] = true,
                ["Rectangle2"] = true,
                ["Circle"] = true,
                ["Cross"] = true,
                ["DxfGeometry"] = true
            },
            icons);
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T match)
        {
            return match;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindDescendant<T>(VisualTreeHelper.GetChild(root, i));
            if (found != null)
            {
                return found;
            }
        }

        if (root is ContentControl contentControl && contentControl.Content is DependencyObject content)
        {
            return FindDescendant<T>(content);
        }

        if (root is Border border && border.Child is DependencyObject child)
        {
            return FindDescendant<T>(child);
        }

        return null;
    }
}
