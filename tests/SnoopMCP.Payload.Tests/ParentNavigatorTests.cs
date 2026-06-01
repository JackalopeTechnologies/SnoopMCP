// ParentNavigatorTests.cs
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
using System.Windows.Media;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

#endregion

namespace SnoopMCP.Payload.Tests;

public sealed class ParentNavigatorTests
{
    private static ParentNavigator CreateNavigator(ElementRegistry registry)
    {
        var emitter = new PathStringEmitter();
        var describer = new ElementDescriber(registry, emitter);
        return new ParentNavigator(describer);
    }

    [StaFact]
    public void GetVisualParent_ChildInPanel_ReturnsPanel()
    {
        var registry = new ElementRegistry();
        ParentNavigator nav = CreateNavigator(registry);
        var stack = new StackPanel();
        var button = new Button { Name = "Save" };
        stack.Children.Add(button);

        GetParentResponse response = nav.GetParent(button, "visual");

        Assert.NotNull(response.Parent);
        Assert.Equal("StackPanel", response.Parent!.Type);
    }

    [StaFact]
    public void GetLogicalParent_ContentControl_ReturnsHost()
    {
        var registry = new ElementRegistry();
        ParentNavigator nav = CreateNavigator(registry);
        var inner = new TextBlock { Text = "x" };
        var host = new ContentControl { Content = inner };

        GetParentResponse response = nav.GetParent(inner, "logical");

        Assert.NotNull(response.Parent);
        Assert.Equal("ContentControl", response.Parent!.Type);
    }

    [StaFact]
    public void GetVisualParent_OfRoot_ReturnsNull()
    {
        var registry = new ElementRegistry();
        ParentNavigator nav = CreateNavigator(registry);
        var orphan = new Button();

        GetParentResponse response = nav.GetParent(orphan, "visual");

        Assert.Null(response.Parent);
    }

    [StaFact]
    public void GetTemplatedParent_OfPlainElement_ReturnsNull()
    {
        var registry = new ElementRegistry();
        ParentNavigator nav = CreateNavigator(registry);
        var button = new Button();

        GetTemplatedParentResponse response = nav.GetTemplatedParent(button);

        Assert.Null(response.TemplatedParent);
    }

    [StaFact]
    public void GetTemplatedParent_OfTemplateChild_ReturnsTemplatedHost()
    {
        var registry = new ElementRegistry();
        ParentNavigator nav = CreateNavigator(registry);

        var button = new Button { Content = "Save" };
        button.ApplyTemplate();

        DependencyObject? insideTemplate = FindFirstTemplateChild(button);
        Assert.NotNull(insideTemplate);

        GetTemplatedParentResponse response = nav.GetTemplatedParent(insideTemplate!);

        Assert.NotNull(response.TemplatedParent);
        Assert.Equal("Button", response.TemplatedParent!.Type);
    }

    [StaFact]
    public void GetParent_UnknownTree_Throws()
    {
        var registry = new ElementRegistry();
        ParentNavigator nav = CreateNavigator(registry);
        var button = new Button();

        Assert.Throws<ArgumentException>(() => nav.GetParent(button, "elven"));
    }

    private static DependencyObject? FindFirstTemplateChild(DependencyObject root)
    {
        DependencyObject? result = null;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count && result is null; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            var fe = child as FrameworkElement;
            var isFromTemplate = fe?.TemplatedParent is not null;
            result = isFromTemplate ? child : FindFirstTemplateChild(child);
        }

        return result;
    }
}
