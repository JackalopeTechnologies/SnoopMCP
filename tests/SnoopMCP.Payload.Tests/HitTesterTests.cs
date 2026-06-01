// HitTesterTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class HitTesterTests
{
    private static HitTester CreateTester(ElementRegistry registry)
    {
        var emitter = new PathStringEmitter();
        var describer = new ElementDescriber(registry, emitter);
        return new HitTester(describer);
    }

    private static void ForceArrange(FrameworkElement element)
    {
        element.Measure(new Size(200, 200));
        element.Arrange(new Rect(0, 0, 200, 200));
        element.UpdateLayout();
    }

    [StaFact]
    public void HitTest_OnRootWithBackground_ReturnsRoot()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);
        var grid = new Grid
        {
            Width = 100,
            Height = 100,
            Background = Brushes.Red
        };
        ForceArrange(grid);

        HitTestResponse response = tester.HitTest(grid, 50, 50);

        Assert.NotNull(response.Element);
        int gridId = registry.GetOrAssign(grid);
        Assert.Equal(gridId, response.Element!.Id);
    }

    [StaFact]
    public void HitTest_InsideChildBounds_ReturnsChild()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);
        var grid = new Grid { Width = 200, Height = 200 };
        var border = new Border
        {
            Width = 80,
            Height = 60,
            Background = Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10)
        };
        grid.Children.Add(border);
        ForceArrange(grid);

        HitTestResponse response = tester.HitTest(grid, 30, 30);

        Assert.NotNull(response.Element);
        int borderId = registry.GetOrAssign(border);
        Assert.Equal(borderId, response.Element!.Id);
        Assert.Equal("Border", response.Element.Type);
    }

    [StaFact]
    public void HitTest_NestedHittables_ReturnsDeepest()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);
        var outer = new Border
        {
            Width = 200,
            Height = 200,
            Background = Brushes.Red
        };
        var inner = new Border
        {
            Width = 50,
            Height = 50,
            Background = Brushes.Blue,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        outer.Child = inner;
        ForceArrange(outer);

        HitTestResponse response = tester.HitTest(outer, 25, 25);

        Assert.NotNull(response.Element);
        int innerId = registry.GetOrAssign(inner);
        Assert.Equal(innerId, response.Element!.Id);
    }

    [StaFact]
    public void HitTest_OutsideAllHittables_ReturnsNull()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);
        var grid = new Grid { Width = 200, Height = 200 };
        var border = new Border
        {
            Width = 40,
            Height = 40,
            Background = Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        grid.Children.Add(border);
        ForceArrange(grid);

        HitTestResponse response = tester.HitTest(grid, 180, 180);

        Assert.Null(response.Element);
    }

    [StaFact]
    public void HitTest_NullBackgroundPanel_NotHittable()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);
        var grid = new Grid { Width = 200, Height = 200 };
        ForceArrange(grid);

        HitTestResponse response = tester.HitTest(grid, 100, 100);

        Assert.Null(response.Element);
    }

    [StaFact]
    public void HitTest_NullRoot_Throws()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);

        Assert.Throws<ArgumentNullException>(() => tester.HitTest(null!, 0, 0));
    }

    [StaFact]
    public void HitTest_NonVisualRoot_Throws()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);
        var contentElement = new Run("not a visual");

        Assert.Throws<ArgumentException>(() => tester.HitTest(contentElement, 0, 0));
    }
}
