// RenderBoundsTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Payload;
using Inspection;
using PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

/// <summary>
/// Covers the geometry that explains a hit. WPF hit-tests PAINTED CONTENT, not <c>RenderSize</c>, so
/// an element that draws outside its layout box legitimately answers a hit while its layout bounds
/// are zero. Reporting only the layout rect made such a result look impossible — a 0x0 element at one
/// corner said to contain a point far away — which is what issue #75 recorded against a modal-overlay
/// host. Excluding zero-size elements from the walk would have been the wrong fix: it would make
/// hit_test return nothing over any such overlay.
/// </summary>
public sealed class RenderBoundsTests
{
    [StaFact]
    public void Describe_ElementPaintingOutsideItsLayoutBox_ReportsPaintedExtentAsRenderBounds()
    {
        var registry = new ElementRegistry();
        var describer = new ElementDescriber(registry, new PathStringEmitter());
        var grid = new Grid { Width = 800, Height = 600 };
        var painter = new OverlayPainter
        {
            Width = 0,
            Height = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        grid.Children.Add(painter);
        Arrange(grid);

        DescribeElementResponse described = describer.Describe(painter);

        // Layout says nothing is there...
        Assert.Equal(0d, described.Bounds.Width);
        Assert.Equal(0d, described.Bounds.Height);

        // ...while the paint that answered the hit covers the window.
        Assert.Equal(800d, described.RenderBounds.Width);
        Assert.Equal(600d, described.RenderBounds.Height);
    }

    [StaFact]
    public void HitTest_OverElementPaintingOutsideItsLayoutBox_ReturnsItWithRenderBoundsContainingThePoint()
    {
        var registry = new ElementRegistry();
        var describer = new ElementDescriber(registry, new PathStringEmitter());
        var tester = new HitTester(describer);
        var grid = new Grid { Width = 800, Height = 600 };
        var painter = new OverlayPainter
        {
            Width = 0,
            Height = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        grid.Children.Add(painter);
        Arrange(grid);

        // The coordinates from the issue report.
        HitTestResponse response = tester.HitTest(grid, 712, 80);

        Assert.NotNull(response.Element);
        BoundsDto rendered = response.Element!.RenderBounds;
        Assert.InRange(712d, rendered.X, rendered.X + rendered.Width);
        Assert.InRange(80d, rendered.Y, rendered.Y + rendered.Height);
    }

    [StaFact]
    public void Describe_OrdinaryElement_ReportsRenderBoundsMatchingItsLayoutBounds()
    {
        var registry = new ElementRegistry();
        var describer = new ElementDescriber(registry, new PathStringEmitter());
        var border = new Border { Width = 80, Height = 40, Background = Brushes.Red };
        Arrange(border);

        DescribeElementResponse described = describer.Describe(border);

        Assert.Equal(described.Bounds.Width, described.RenderBounds.Width);
        Assert.Equal(described.Bounds.Height, described.RenderBounds.Height);
    }

    private static void Arrange(FrameworkElement element)
    {
        element.Measure(new Size(800, 600));
        element.Arrange(new Rect(0, 0, 800, 600));
        element.UpdateLayout();
    }
}
