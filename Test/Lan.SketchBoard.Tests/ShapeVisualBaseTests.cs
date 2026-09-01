#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Lan.Shapes;
using Lan.Shapes.Enums;
using Lan.Shapes.Handle;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeVisualBaseTests
{
    [Fact]
    public void State_KeepsLockFlagConsistent()
    {
        var shape = new ProbeShape(TestShapeLayer.Create());

        shape.State = ShapeVisualState.Locked;

        Assert.True(shape.IsLocked);

        shape.State = ShapeVisualState.Selected;

        Assert.False(shape.IsLocked);
    }

    [Fact]
    public void LockAndUnlock_RaiseStateAndLockNotifications()
    {
        var shape = new ProbeShape(TestShapeLayer.Create());
        var changes = new List<string?>();
        ((INotifyPropertyChanged)shape).PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        shape.Lock();

        Assert.Contains(nameof(ShapeVisualBase.State), changes);
        Assert.Contains(nameof(ShapeVisualBase.IsLocked), changes);
        Assert.Contains(nameof(ShapeVisualBase.ShapeStyler), changes);

        changes.Clear();
        shape.UnLock();

        Assert.Contains(nameof(ShapeVisualBase.State), changes);
        Assert.Contains(nameof(ShapeVisualBase.IsLocked), changes);
    }

    [Fact]
    public void Tag_RaisesNotificationOnlyWhenValueChanges()
    {
        var shape = new ProbeShape(TestShapeLayer.Create());
        var changes = new List<string?>();
        ((INotifyPropertyChanged)shape).PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        shape.Tag = "measurement";
        shape.Tag = "measurement";

        Assert.Equal(1, changes.Count(x => x == nameof(ShapeVisualBase.Tag)));
    }

    [Fact]
    public void ReplacingShapeLayer_RefreshesHandleSizeAndNotifies()
    {
        var shape = new ProbeShape(TestShapeLayer.CreateWithThickness(stroke: 1, handle: 8));
        var replacement = TestShapeLayer.CreateWithThickness(stroke: 2, handle: 20);
        var changes = new List<string?>();
        ((INotifyPropertyChanged)shape).PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        shape.ShapeLayer = replacement;

        Assert.Same(replacement, shape.ShapeLayer);
        Assert.Equal(20, shape.HandleSize.Width);
        Assert.Contains(nameof(ShapeVisualBase.ShapeLayer), changes);
        Assert.Contains(nameof(ShapeVisualBase.ShapeStyler), changes);
    }

    [Fact]
    public void CreateFormattedText_UsesVisualPixelsPerDip()
    {
        var shape = new ProbeShape(TestShapeLayer.Create());

        var text = shape.CreateText("label");

        Assert.Equal(VisualTreeHelper.GetDpi(shape).PixelsPerDip, text.PixelsPerDip);
    }

    private sealed class ProbeShape : ShapeVisualBase
    {
        private readonly DragHandle _handle;

        public ProbeShape(ShapeLayer layer)
            : base(layer)
        {
            _handle = RegisterHandle(new RectDragHandle(DragHandleSize, default, 1));
        }

        public Size HandleSize => _handle.HandleSize;

        public FormattedText CreateText(string text)
        {
            return CreateFormattedText(text, Brushes.Black);
        }

        public override Rect BoundsRect => Rect.Empty;

        protected override void CreateHandles()
        {
        }

        protected override void HandleResizing(Point point)
        {
        }

        protected override void HandleTranslate(Point newPoint)
        {
        }
    }
}
