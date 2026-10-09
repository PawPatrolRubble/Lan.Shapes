#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Lan.ImageViewer.ViewModels.GeometryEditing;
using Lan.Shapes;
using Lan.Shapes.Interfaces;

namespace Lan.ImageViewer
{
    /// <summary>Edits one selected shape without replacing the host template's data context.</summary>
    public partial class ShapeGeometryEditorControl : UserControl
    {
        private readonly Dictionary<GeometryPropertyGroup, int> _keyboardRevisions = new();
        private int _lifecycleRevision;
        private bool _isUnloaded = true;

        public static readonly DependencyProperty TargetShapeProperty = DependencyProperty.Register(
            nameof(TargetShape), typeof(ShapeVisualBase), typeof(ShapeGeometryEditorControl),
            new PropertyMetadata(null, OnTargetChanged));

        public static readonly DependencyProperty ShapeRepositoryProperty = DependencyProperty.Register(
            nameof(ShapeRepository), typeof(IShapeRepository), typeof(ShapeGeometryEditorControl),
            new PropertyMetadata(null, OnTargetChanged));

        public ShapeVisualBase? TargetShape
        {
            get => (ShapeVisualBase?)GetValue(TargetShapeProperty);
            set => SetValue(TargetShapeProperty, value);
        }

        public IShapeRepository? ShapeRepository
        {
            get => (IShapeRepository?)GetValue(ShapeRepositoryProperty);
            set => SetValue(ShapeRepositoryProperty, value);
        }

        public ShapeGeometryEditor? Editor { get; private set; }

        public ShapeGeometryEditorControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnTargetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
            => ((ShapeGeometryEditorControl)sender).RebuildEditor();

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            _isUnloaded = false;
            if (Editor == null) RebuildEditor();
        }

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            _isUnloaded = true;
            RebuildEditor();
        }

        private void RebuildEditor()
        {
            _lifecycleRevision++;
            _keyboardRevisions.Clear();
            Editor?.Dispose();
            Editor = !_isUnloaded && TargetShape != null && ShapeRepository != null
                ? ShapeGeometryEditor.Create(TargetShape, ShapeRepository)
                : null;
            EditorRoot.DataContext = Editor;
        }

        private void OnGroupPreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Enter && args.Key != Key.Escape) return;
            if (sender is not FrameworkElement element || element.DataContext is not GeometryPropertyGroup group
                || Editor == null || !Editor.Groups.Contains(group)) return;

            _keyboardRevisions[group] = KeyboardRevision(group) + 1;
            if (args.Key == Key.Enter) Editor.Commit(group);
            else Editor.Reset(group);
            args.Handled = true;
        }

        private void OnGroupLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            if (sender is not FrameworkElement element || element.DataContext is not GeometryPropertyGroup group
                || Editor == null || !Editor.Groups.Contains(group)) return;

            var editor = Editor;
            var editorRevision = editor.Revision;
            var groupRevision = group.Revision;
            var lifecycleRevision = _lifecycleRevision;
            var keyboardRevision = KeyboardRevision(group);

            // Canvas focus may change before repository selection; inspect both after the input event ends.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (_isUnloaded || lifecycleRevision != _lifecycleRevision || !ReferenceEquals(Editor, editor)
                    || editorRevision != editor.Revision || groupRevision != group.Revision
                    || keyboardRevision != KeyboardRevision(group) || element.IsKeyboardFocusWithin)
                    return;
                editor.Commit(group);
            }));
        }

        private int KeyboardRevision(GeometryPropertyGroup group)
            => _keyboardRevisions.TryGetValue(group, out var revision) ? revision : 0;
    }
}
