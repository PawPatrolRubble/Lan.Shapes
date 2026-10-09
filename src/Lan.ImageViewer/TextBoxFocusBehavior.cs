#nullable enable
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Lan.ImageViewer;

/// <summary>Selects a property value when its input receives keyboard or mouse focus.</summary>
public static class TextBoxFocusBehavior
{
    public static readonly DependencyProperty SelectAllOnFocusProperty = DependencyProperty.RegisterAttached(
        "SelectAllOnFocus", typeof(bool), typeof(TextBoxFocusBehavior),
        new PropertyMetadata(false, OnSelectAllOnFocusChanged));

    public static bool GetSelectAllOnFocus(DependencyObject element)
        => (bool)element.GetValue(SelectAllOnFocusProperty);

    public static void SetSelectAllOnFocus(DependencyObject element, bool value)
        => element.SetValue(SelectAllOnFocusProperty, value);

    private static void OnSelectAllOnFocusChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not TextBox textBox) return;
        if ((bool)args.NewValue)
        {
            textBox.GotKeyboardFocus += OnGotKeyboardFocus;
            textBox.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        }
        else
        {
            textBox.GotKeyboardFocus -= OnGotKeyboardFocus;
            textBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        }
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
    {
        if (sender is TextBox textBox && ReferenceEquals(args.NewFocus, textBox)) textBox.SelectAll();
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
    {
        if (sender is not TextBox textBox || textBox.IsKeyboardFocusWithin) return;
        // Prevent the first click from collapsing the selection made by the focus handler.
        if (textBox.Focus()) args.Handled = true;
    }
}
