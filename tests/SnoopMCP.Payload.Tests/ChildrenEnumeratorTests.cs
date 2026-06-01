// ChildrenEnumeratorTests.cs
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

namespace SnoopMCP.Payload.Tests;

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class ChildrenEnumeratorTests
{
    private static ChildrenEnumerator CreateEnumerator(ElementRegistry registry)
    {
        var emitter = new PathStringEmitter();
        var describer = new ElementDescriber(registry, emitter);
        return new ChildrenEnumerator(describer);
    }

    [StaFact]
    public void Enumerate_VisualChildrenOfPanel_ReturnsButtons()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);
        var stack = new StackPanel();
        stack.Children.Add(new Button { Name = "A" });
        stack.Children.Add(new Button { Name = "B" });

        GetChildrenResponse response = enumerator.Enumerate(stack, "visual");

        Assert.Equal(2, response.Children.Count);
        Assert.Equal("A", response.Children[0].Name);
        Assert.Equal("B", response.Children[1].Name);
        Assert.Null(response.Virtualization);
    }

    [StaFact]
    public void Enumerate_LogicalChildrenOfContentControl_ReturnsContent()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);
        var inner = new TextBlock { Text = "inside" };
        var content = new ContentControl { Content = inner };

        GetChildrenResponse response = enumerator.Enumerate(content, "logical");

        Assert.Single(response.Children);
        Assert.Equal("TextBlock", response.Children[0].Type);
    }

    [StaFact]
    public void Enumerate_NonVirtualizingItemsControl_ReportsCountsWithoutVirtualizing()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);
        var listBox = new ListBox
        {
            ItemsSource = Enumerable.Range(0, 5).Select(i => $"Item {i}").ToArray()
        };
        VirtualizingPanel.SetIsVirtualizing(listBox, false);
        ForceArrange(listBox);

        GetChildrenResponse response = enumerator.Enumerate(listBox, "visual");

        Assert.NotNull(response.Virtualization);
        Assert.False(response.Virtualization!.IsVirtualizing);
        Assert.Equal(5, response.Virtualization.TotalItems);
    }

    [StaFact]
    public void Enumerate_VirtualizingListBox_ReportsVirtualizationMetadata()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);
        var items = Enumerable.Range(0, 1000).Select(i => new { Id = i }).ToArray();
        var listBox = new ListBox
        {
            ItemsSource = items,
            Width = 200,
            Height = 100
        };
        VirtualizingPanel.SetIsVirtualizing(listBox, true);
        VirtualizingPanel.SetVirtualizationMode(listBox, VirtualizationMode.Recycling);
        ForceArrange(listBox);

        GetChildrenResponse response = enumerator.Enumerate(listBox, "visual");

        Assert.NotNull(response.Virtualization);
        Assert.True(response.Virtualization!.IsVirtualizing);
        Assert.Equal(1000, response.Virtualization.TotalItems);
        Assert.True(response.Virtualization.RealizedItems < 1000,
            $"Expected fewer than 1000 realized items, got {response.Virtualization.RealizedItems}.");
    }

    [StaFact]
    public void Enumerate_UnknownTreeKind_Throws()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);
        var grid = new Grid();

        Assert.Throws<ArgumentException>(() => enumerator.Enumerate(grid, "magical"));
    }

    private static void ForceArrange(FrameworkElement element)
    {
        element.Measure(new Size(200, 100));
        element.Arrange(new Rect(0, 0, 200, 100));
        element.UpdateLayout();
    }
}
