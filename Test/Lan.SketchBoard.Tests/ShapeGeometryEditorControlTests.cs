using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lan.ImageViewer;
using Lan.ImageViewer.Prism;
using Lan.Shapes;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;
using Rectangle = Lan.Shapes.Shapes.Rectangle;

namespace Lan.SketchBoard.Tests;

[Collection("Geometry editing UI")]
public class ShapeGeometryEditorControlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeometryTextBox_SelectsAllOnFocus(bool navigateFromPreviousField)
    {
        RunOnSta(() =>
        {
            var (control, _, _) = CreateLineEditor();
            using var host = new FocusHost(control);
            var fields = Descendants<TextBox>(control).ToArray();
            Focus(host.Canvas);
            Focus(fields[0]);
            var target = fields[0];
            if (navigateFromPreviousField)
            {
                Assert.True(fields[0].MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
                target = fields[1];
                Assert.Same(target, Keyboard.FocusedElement);
            }
            Assert.Equal(0, target.SelectionStart);
            Assert.Equal(target.Text.Length, target.SelectionLength);
            target.Select(1, 0);
            Focus(host.Canvas);
            Focus(target);
            Assert.Equal(target.Text.Length, target.SelectionLength);
        });
    }

    [Fact]
    public void FirstMouseFocus_SelectsAllAndSubsequentClicksAllowCaretPlacement()
    {
        RunOnSta(() =>
        {
            var (control, _, _) = CreateLineEditor();
            using var host = new FocusHost(control);
            var field = Descendants<TextBox>(control).First();
            Focus(host.Canvas);
            var firstClick = Click(field);
            Assert.True(firstClick.Handled);
            Assert.Same(field, Keyboard.FocusedElement);
            Assert.Equal(field.Text.Length, field.SelectionLength);
            field.Select(1, 0);
            var secondClick = Click(field);
            Assert.False(secondClick.Handled);
            Assert.Equal(0, field.SelectionLength);
        });
    }

    [Fact]
    public void PropertyPanelTagTextBox_AlsoSelectsAllOnFocus()
    {
        RunOnSta(() =>
        {
            var (control, _, line) = CreateLineEditor();
            line.Tag = "line-tag";
            var resources = control.Resources.MergedDictionaries[0];
            var template = Assert.IsType<DataTemplate>(resources[new DataTemplateKey(typeof(Line))]);
            var card = Assert.IsAssignableFrom<FrameworkElement>(template.LoadContent());
            card.Resources.MergedDictionaries.Add(resources);
            card.DataContext = line;
            var wrapper = new UserControl { Content = card };
            using var host = new FocusHost(wrapper);
            var tag = Assert.Single(Descendants<TextBox>(card), field =>
                BindingOperations.GetBinding(field, TextBox.TextProperty)?.Path.Path == "Tag");
            Focus(host.Canvas);
            Focus(tag);
            Assert.Equal("line-tag", tag.SelectedText);
            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void EditorLifetime_StartsOnFirstLoadAndDisposesOnUnload()
    {
        RunOnSta(() =>
        {
            var repository = new SketchBoardDataManager();
            repository.SetShapeLayer(TestShapeLayer.Create());
            var line = (Line)repository.LoadShape<Line, PointsData>(new PointsData(1,
                new List<Point> { new(10, 20), new(40, 60) }));
            repository.SelectedGeometry = line;
            var control = new ShapeGeometryEditorControl
            {
                TargetShape = line,
                ShapeRepository = repository
            };
            Assert.Null(control.Editor);
            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            var editor = control.Editor;
            Assert.NotNull(editor);
            var group = editor.Groups[0];
            group.Fields[0].Text = "15";
            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Null(control.Editor);
            Assert.False(editor.Commit(group));
            Assert.Equal(new Point(10, 20), line.Start);
        });
    }

    [Fact]
    public void ViewerPropertyPanel_EditsTheSelectedLineAtItsActualPanelWidth()
    {
        RunOnSta(() =>
        {
            var bindingTrace = PresentationTraceSources.DataBindingSource;
            var previousLevel = bindingTrace.Switch.Level;
            using var listener = new BindingErrorListener();
            bindingTrace.Listeners.Add(listener);
            bindingTrace.Switch.Level = SourceLevels.Error;
            try
            {
                var layers = new ShapeLayerManager();
                layers.Layers.Add(TestShapeLayer.Create());
                var types = new GeometryTypeManager();
                types.RegisterGeometryType<Line>();
                var repository = new SketchBoardDataManager();
                using var viewModel = new ImageViewerControlViewModel(layers, repository, types);
                var line = (Line)repository.LoadShape<Line, PointsData>(new PointsData(1,
                    new List<Point> { new(10, 20), new(40, 60) }));
                repository.SelectedGeometry = line;
                var viewer = new ImageViewerControl { DataContext = viewModel };
                using var host = new FocusHost(viewer, new Size(1000, 850), includeOutsideControls: false);
                var editor = Assert.Single(Descendants<UserControl>(viewer),
                    element => element.GetType().FullName == "Lan.ImageViewer.ShapeGeometryEditorControl");
                Assert.Same(line, editor.GetValue(DependencyPropertyField(editor.GetType(), "TargetShapeProperty")));
                Assert.Same(repository, editor.GetValue(DependencyPropertyField(editor.GetType(), "ShapeRepositoryProperty")));
                var fields = Descendants<TextBox>(editor).ToArray();
                var length = fields[4];
                Focus(length);
                length.Text = "100";
                Assert.True(Press(length, Key.Enter).Handled);
                Pump();
                Assert.Equal(new Point(70, 100), line.End);
                var panel = Assert.IsType<Border>(viewer.FindName("LayerPanel"));
                Assert.Equal(260, panel.ActualWidth);
                var editorBounds = editor.TransformToAncestor(panel).TransformBounds(new Rect(editor.RenderSize));
                Assert.True(editorBounds.Left >= 0 && editorBounds.Right <= panel.ActualWidth);
                foreach (var field in fields)
                {
                    var bounds = field.TransformToAncestor(editor).TransformBounds(new Rect(field.RenderSize));
                    Assert.True(bounds.Left >= 0 && bounds.Right <= editor.ActualWidth);
                    Assert.True(field.ActualWidth >= 32);
                }
                SaveScreenshot(viewer, "viewer-geometry-editing");
                Assert.Empty(listener.Messages);
            }
            finally
            {
                bindingTrace.Listeners.Remove(listener);
                bindingTrace.Switch.Level = previousLevel;
            }
        });
    }

    [Fact]
    public void RapidFocusAcrossGroups_CommitsBothEndpointDrafts()
    {
        RunOnSta(() =>
        {
            var (control, _, line) = CreateLineEditor();
            using var host = new FocusHost(control);
            var fields = Descendants<TextBox>(control).ToArray();
            Focus(fields[0]);
            fields[0].Text = "15";
            Focus(fields[2]);
            fields[2].Text = "70";
            Focus(host.OutsideField);
            Pump();
            Assert.Equal(new Point(15, 20), line.Start);
            Assert.Equal(new Point(70, 60), line.End);
        });
    }

    [Fact]
    public void FocusWithinTheCoordinateGroup_KeepsTheDraftUntilTheWholeGroupLosesFocus()
    {
        RunOnSta(() =>
        {
            var (control, _, line) = CreateLineEditor();
            using var host = new FocusHost(control);
            var fields = Descendants<TextBox>(control).ToArray();
            Focus(fields[0]);
            fields[0].Text = "15";
            Focus(fields[1]);
            Pump();
            Assert.Equal(new Point(10, 20), line.Start);
            fields[1].Text = "25";
            Focus(host.Canvas);
            Pump();
            Assert.Equal(new Point(15, 25), line.Start);
        });
    }

    [Fact]
    public void InvalidDraft_CanBeCorrectedAndCommittedByLeavingTheGroup()
    {
        RunOnSta(() =>
        {
            var (control, _, line) = CreateLineEditor();
            using var host = new FocusHost(control);
            var fields = Descendants<TextBox>(control).ToArray();
            Focus(fields[0]);
            fields[0].Text = "-";
            Focus(host.Canvas);
            Pump();
            Assert.Equal(new Point(10, 20), line.Start);
            Focus(fields[0]);
            fields[0].Text = "15";
            Focus(fields[1]);
            fields[1].Text = "25";
            Focus(host.Canvas);
            Pump();
            Assert.Equal(new Point(15, 25), line.Start);
            Assert.DoesNotContain(Descendants<TextBlock>(control), element =>
                element.Visibility == Visibility.Visible && !string.IsNullOrEmpty(element.Text)
                && element.Foreground is SolidColorBrush brush && brush.Color == Colors.Salmon);
        });
    }

    [Fact]
    public void LockingTheShape_DisablesInputsAndPreventsThePendingDraftFromCommitting()
    {
        RunOnSta(() =>
        {
            var (control, _, line) = CreateLineEditor();
            using var host = new FocusHost(control);
            var fields = Descendants<TextBox>(control).ToArray();
            Focus(fields[0]);
            fields[0].Text = "15";
            line.Lock();
            Pump();
            Assert.All(fields, field => Assert.False(field.IsEnabled));
            Assert.Equal(new Point(10, 20), line.Start);
        });
    }

    [Fact]
    public void GeometryEditor_KeepsTheParentDataContextAndExposesShapeAndRepositoryBindings()
    {
        RunOnSta(() =>
        {
            var type = EditorControlType();
            var editor = Assert.IsAssignableFrom<UserControl>(Activator.CreateInstance(type));
            var shapeProperty = DependencyPropertyField(type, "TargetShapeProperty");
            var repositoryProperty = DependencyPropertyField(type, "ShapeRepositoryProperty");
            Assert.Equal(typeof(ShapeVisualBase), shapeProperty.PropertyType);
            Assert.Equal(typeof(IShapeRepository), repositoryProperty.PropertyType);
            var context = new object();
            editor.DataContext = context;
            Assert.Same(context, editor.DataContext);
        });
    }

    [Theory]
    [InlineData(typeof(Line))]
    [InlineData(typeof(Rectangle))]
    [InlineData(typeof(Circle))]
    public void BasicShapeTemplates_BindTheGeometryEditorToTheShapeAndViewerRepository(Type shapeType)
    {
        RunOnSta(() =>
        {
            _ = new ImageViewerControl();
            var resources = new ResourceDictionary
            {
                Source = new Uri("/Lan.ImageViewer;component/PropertyPanelTheme.xaml", UriKind.Relative)
            };
            var template = Assert.IsType<DataTemplate>(resources[new DataTemplateKey(shapeType)]);
            var root = Assert.IsAssignableFrom<FrameworkElement>(template.LoadContent());
            Layout(root);
            var editor = Assert.Single(Descendants<FrameworkElement>(root),
                element => element.GetType().FullName == "Lan.ImageViewer.ShapeGeometryEditorControl");
            var targetBinding = BindingOperations.GetBinding(editor, DependencyPropertyField(editor.GetType(), "TargetShapeProperty"));
            Assert.NotNull(targetBinding);
            Assert.True(string.IsNullOrEmpty(targetBinding.Path?.Path));
            var repositoryBinding = BindingOperations.GetBinding(editor, DependencyPropertyField(editor.GetType(), "ShapeRepositoryProperty"));
            Assert.NotNull(repositoryBinding);
            Assert.Equal("DataContext.ShapeRepository", repositoryBinding.Path.Path);
            Assert.Equal(typeof(ImageViewerControl), repositoryBinding.RelativeSource.AncestorType);
        });
    }

    [Fact]
    public void Enter_CommitsTheWholeEndpointAndEscapeRestoresItsDraft()
    {
        RunOnSta(() =>
        {
            var (control, _, line) = CreateLineEditor();
            var fields = Descendants<TextBox>(control).ToArray();
            Assert.Equal(5, fields.Length);
            fields[0].Text = "15";
            fields[1].Text = "25";
            Assert.Equal(new Point(10, 20), line.Start);
            var enter = Press(fields[0], Key.Enter);
            Assert.True(enter.Handled);
            Assert.Equal(new Point(15, 25), line.Start);
            fields[0].Text = "90";
            var escape = Press(fields[0], Key.Escape);
            Assert.True(escape.Handled);
            Assert.Equal("15", fields[0].Text);
            Assert.Equal(new Point(15, 25), line.Start);
        });
    }

    [Fact]
    public void InvalidEndpoint_LeavesTheShapeAndDraftIntact()
    {
        RunOnSta(() =>
        {
            var (control, _, line) = CreateLineEditor();
            var fields = Descendants<TextBox>(control).ToArray();
            fields[0].Text = "-";
            fields[1].Text = "25";
            Press(fields[0], Key.Enter);
            Assert.Equal(new Point(10, 20), line.Start);
            Assert.Equal("-", fields[0].Text);
            Assert.Contains(Descendants<TextBlock>(control), element =>
                element.Visibility == Visibility.Visible && !string.IsNullOrEmpty(element.Text)
                && element.Foreground is SolidColorBrush brush && brush.Color == Colors.Salmon);
        });
    }

    [Fact]
    public void DeferredFocusCommit_IsDiscardedAfterTheTargetChanges()
    {
        RunOnSta(() =>
        {
            var (control, repository, first) = CreateLineEditor();
            using var host = new FocusHost(control);
            var fields = Descendants<TextBox>(control).ToArray();
            Focus(fields[0]);
            fields[0].Text = "15";
            var second = (Line)repository.LoadShape<Line, PointsData>(new PointsData(1,
                new List<Point> { new(50, 60), new(80, 100) }));
            Focus(host.Canvas);
            repository.SelectedGeometry = second;
            control.SetValue(DependencyPropertyField(control.GetType(), "TargetShapeProperty"), second);
            Pump();
            Assert.Equal(new Point(10, 20), first.Start);
            Assert.Equal(new Point(50, 60), second.Start);
        });
    }

    [Fact]
    public void PropertyCards_RenderAllThreeEditorsAndTheirValidationErrors()
    {
        RunOnSta(() =>
        {
            var (_, repository, line) = CreateLineEditor();
            var rectangle = repository.LoadShape<Rectangle, PointsData>(new PointsData(1,
                new List<Point> { new(30, 40), new(130, 100) }));
            var circle = repository.LoadShape<Circle, EllipseData>(new EllipseData
            {
                Center = new Point(80, 90), RadiusX = 25, RadiusY = 25
            });
            foreach (var shape in new[] { line, rectangle, circle })
            {
                repository.SelectedGeometry = shape;
                var resources = new ResourceDictionary
                {
                    Source = new Uri("/Lan.ImageViewer;component/PropertyPanelTheme.xaml", UriKind.Relative)
                };
                var template = Assert.IsType<DataTemplate>(resources[new DataTemplateKey(shape.GetType())]);
                var card = Assert.IsAssignableFrom<FrameworkElement>(template.LoadContent());
                card.Resources.MergedDictionaries.Add(resources);
                card.DataContext = shape;
                LayoutCard(card);
                var editor = Assert.Single(Descendants<UserControl>(card),
                    element => element.GetType().FullName == "Lan.ImageViewer.ShapeGeometryEditorControl");
                editor.SetValue(DependencyPropertyField(editor.GetType(), "ShapeRepositoryProperty"), repository);
                editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                LayoutCard(card);
                SaveScreenshot(card, "property-panel-" + shape.GetType().Name.ToLowerInvariant());
                var input = Descendants<TextBox>(editor).First();
                input.Text = "-";
                Press(input, Key.Enter);
                LayoutCard(card);
                Assert.Contains(Descendants<TextBlock>(editor), element =>
                    element.Visibility == Visibility.Visible && !string.IsNullOrEmpty(element.Text)
                    && element.Foreground is SolidColorBrush brush && brush.Color == Colors.Salmon);
                SaveScreenshot(card, "property-panel-" + shape.GetType().Name.ToLowerInvariant() + "-error");
                editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            }
        });
    }

    [Fact]
    public void Unload_DiscardsQueuedCommitAndReloadRefreshesTheShape()
    {
        RunOnSta(() =>
        {
            var (control, _, line) = CreateLineEditor();
            var firstField = Descendants<TextBox>(control).First();
            firstField.Text = "15";
            firstField.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, firstField, null)
            {
                RoutedEvent = Keyboard.LostKeyboardFocusEvent
            });
            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Pump();
            Assert.Equal(new Point(10, 20), line.Start);
            line.Start = new Point(18, 28);
            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Layout(control);
            Assert.Equal("18", Descendants<TextBox>(control).First().Text);
        });
    }

    private static (UserControl Control, SketchBoardDataManager Repository, Line Line) CreateLineEditor()
    {
        var repository = new SketchBoardDataManager();
        repository.SetShapeLayer(TestShapeLayer.Create());
        var line = (Line)repository.LoadShape<Line, PointsData>(new PointsData(1,
            new List<Point> { new(10, 20), new(40, 60) }));
        repository.SelectedGeometry = line;
        var control = Assert.IsAssignableFrom<UserControl>(Activator.CreateInstance(EditorControlType()));
        control.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/Lan.ImageViewer;component/PropertyPanelTheme.xaml", UriKind.Relative)
        });
        control.SetValue(DependencyPropertyField(control.GetType(), "TargetShapeProperty"), line);
        control.SetValue(DependencyPropertyField(control.GetType(), "ShapeRepositoryProperty"), repository);
        control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Layout(control);
        return (control, repository, line);
    }

    private static KeyEventArgs Press(UIElement field, Key key)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, new TestPresentationSource(), 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        field.RaiseEvent(args);
        return args;
    }

    private static MouseButtonEventArgs Click(TextBox field)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent
        };
        field.RaiseEvent(args);
        return args;
    }

    private sealed class TestPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private sealed class FocusHost : IDisposable
    {
        private readonly Window _window;
        public TextBox OutsideField { get; } = new();
        public Button Canvas { get; } = new() { Content = "画布", MinHeight = 40, Focusable = true };

        public FocusHost(UserControl control, Size? size = null, bool includeOutsideControls = true)
        {
            var content = new StackPanel();
            if (includeOutsideControls)
            {
                content.Children.Add(control);
                content.Children.Add(OutsideField);
                content.Children.Add(Canvas);
            }
            var bounds = size ?? new Size(320, 600);
            _window = new Window
            {
                Width = bounds.Width,
                Height = bounds.Height,
                Left = -10000,
                Top = -10000,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Opacity = 0,
                ShowInTaskbar = false,
                ShowActivated = false,
                Content = includeOutsideControls ? content : control
            };
            _window.Show();
            _window.UpdateLayout();
            Pump();
        }

        public void Dispose() => _window.Close();
    }

    private sealed class BindingErrorListener : TraceListener
    {
        public List<string> Messages { get; } = new();
        public override void Write(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message)) Messages.Add(message);
        }
        public override void WriteLine(string? message) => Write(message);
    }

    private static void Focus(UIElement element)
        => Assert.Same(element, Keyboard.Focus(element));

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(300, 600));
        element.Arrange(new Rect(0, 0, 300, 600));
        element.UpdateLayout();
    }

    private static void LayoutCard(FrameworkElement card)
    {
        card.Measure(new Size(300, double.PositiveInfinity));
        card.Arrange(new Rect(0, 0, 300, card.DesiredSize.Height));
        card.UpdateLayout();
    }

    private static void SaveScreenshot(FrameworkElement card, string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "src", "Lan.ImageViewer")))
            root = root.Parent;
        Assert.NotNull(root);
        var directory = Path.Combine(root.FullName, "TestResults", "GeometryEditing");
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(card.ActualWidth), (int)Math.Ceiling(card.ActualHeight),
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(card);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, name + ".png"));
        encoder.Save(file);
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static Type EditorControlType()
    {
        var type = typeof(ImageViewerControl).Assembly.GetType("Lan.ImageViewer.ShapeGeometryEditorControl");
        Assert.NotNull(type);
        return type;
    }

    private static DependencyProperty DependencyPropertyField(Type owner, string fieldName)
        => Assert.IsType<DependencyProperty>(owner.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null));

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

[CollectionDefinition("Geometry editing UI", DisableParallelization = true)]
public class GeometryEditingUiCollection
{
}
