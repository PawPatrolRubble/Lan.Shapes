using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using Lan.Shapes;
using Lan.Shapes.Models;
using Lan.Shapes.Shapes;
using Xunit;

namespace Lan.SketchBoard.Tests;

public class ShapeTranslationValidationTests
{
    [Fact]
    public void Translation_RejectsAttachedTextOverflowBeforeChangingModelOrText()
    {
        Sta(() =>
        {
            var line = CreateLine(TestShapeLayer.Create(), 20);
            var textLocation = new Point(1.7e308, 0);
            line.AddText("attached", textLocation);
            line.DrawCount = 0;
            var notifications = 0;
            line.PropertyChanged += (_, _) => notifications++;

            Assert.ThrowsAny<ArgumentException>(() => line.Translate(new Vector(1e308, 0)));

            Assert.Equal(new Point(10, 20), line.Start);
            Assert.Equal(new Point(40, 20), line.End);
            Assert.Equal(textLocation, Assert.Single(TextGeometries(line)).Location);
            Assert.Equal(0, notifications);
            Assert.Equal(0, line.DrawCount);
        });
    }

    [Fact]
    public void GroupTranslation_RejectsLaterMembersTextOverflowBeforeMovingAnyMember()
    {
        Sta(() =>
        {
            var manager = new SketchBoardDataManager();
            var layer = TestShapeLayer.Create();
            manager.SetShapeLayer(layer);
            var first = CreateLine(layer, 20);
            var second = CreateLine(layer, 50);
            var textLocation = new Point(1.7e308, 0);
            second.AddText("attached", textLocation);
            manager.AddShape(first);
            manager.AddShape(second);
            var group = manager.GroupShapes(new[] { first, second });
            first.DrawCount = 0;
            second.DrawCount = 0;
            var notifications = 0;
            first.PropertyChanged += (_, _) => notifications++;
            second.PropertyChanged += (_, _) => notifications++;

            Assert.ThrowsAny<ArgumentException>(() => manager.TranslateShapes(new[] { first }, new Vector(1e308, 0)));

            Assert.Equal(new Point(10, 20), first.Start);
            Assert.Equal(new Point(40, 20), first.End);
            Assert.Equal(new Point(10, 50), second.Start);
            Assert.Equal(new Point(40, 50), second.End);
            Assert.Equal(textLocation, Assert.Single(TextGeometries(second)).Location);
            Assert.Same(group, manager.GetGroup(first));
            Assert.Same(group, manager.GetGroup(second));
            Assert.Equal(0, notifications);
            Assert.Equal(0, first.DrawCount);
            Assert.Equal(0, second.DrawCount);
        });
    }

    private static IReadOnlyList<(Point Location, string Content)> TextGeometries(ShapeVisualBase shape)
        => (IReadOnlyList<(Point Location, string Content)>)typeof(ShapeVisualBase)
            .GetField("_textGeometries", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shape)!;

    private static CountingLine CreateLine(ShapeLayer layer, double y)
    {
        var line = new CountingLine(layer);
        line.FromData(new PointsData(layer.LayerId, new List<Point> { new(10, y), new(40, y) }));
        return line;
    }

    private sealed class CountingLine : Line
    {
        public CountingLine(ShapeLayer layer) : base(layer) { }
        public int DrawCount { get; set; }
        public override void UpdateVisual() { DrawCount++; base.UpdateVisual(); }
    }

    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception exception) { failure = exception; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
