// HitTesterTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#region Usings

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

#endregion

namespace SnoopMCP.Payload.Tests;

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
        HitTester tester = CreateTester(registry);
        var grid = new Grid { Width = 100, Height = 100, Background = Brushes.Red };
        ForceArrange(grid);

        HitTestResponse response = tester.HitTest(grid, 50, 50);

        Assert.NotNull(response.Element);
        var gridId = registry.GetOrAssign(grid);
        Assert.Equal(gridId, response.Element!.Id);
    }

    [StaFact]
    public void HitTest_InsideChildBounds_ReturnsChild()
    {
        var registry = new ElementRegistry();
        HitTester tester = CreateTester(registry);
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
        var borderId = registry.GetOrAssign(border);
        Assert.Equal(borderId, response.Element!.Id);
        Assert.Equal("Border", response.Element.Type);
    }

    [StaFact]
    public void HitTest_NestedHittables_ReturnsDeepest()
    {
        var registry = new ElementRegistry();
        HitTester tester = CreateTester(registry);
        var outer = new Border { Width = 200, Height = 200, Background = Brushes.Red };
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
        var innerId = registry.GetOrAssign(inner);
        Assert.Equal(innerId, response.Element!.Id);
    }

    [StaFact]
    public void HitTest_OutsideAllHittables_ReturnsNull()
    {
        var registry = new ElementRegistry();
        HitTester tester = CreateTester(registry);
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
        HitTester tester = CreateTester(registry);
        var grid = new Grid { Width = 200, Height = 200 };
        ForceArrange(grid);

        HitTestResponse response = tester.HitTest(grid, 100, 100);

        Assert.Null(response.Element);
    }

    [StaFact]
    public void HitTest_NullRoot_Throws()
    {
        var registry = new ElementRegistry();
        HitTester tester = CreateTester(registry);

        Assert.Throws<ArgumentNullException>(() => tester.HitTest(null!, 0, 0));
    }

    [StaFact]
    public void HitTest_NonVisualRoot_Throws()
    {
        var registry = new ElementRegistry();
        HitTester tester = CreateTester(registry);
        var contentElement = new Run("not a visual");

        Assert.Throws<ArgumentException>(() => tester.HitTest(contentElement, 0, 0));
    }
}
