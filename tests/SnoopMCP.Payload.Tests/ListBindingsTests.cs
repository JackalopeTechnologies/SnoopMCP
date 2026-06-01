// ListBindingsTests.cs
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

using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class ListBindingsTests
{
    private static BindingInspector CreateInspector(ElementRegistry registry)
    {
        return new BindingInspector(registry);
    }

    [StaFact]
    public void List_ElementWithNoBindings_ReturnsEmpty()
    {
        var registry = new ElementRegistry();
        BindingInspector inspector = CreateInspector(registry);
        var text = new TextBlock { Text = "literal" };

        ListBindingsResponse response = inspector.ListBindings(text, includeDescendants: false);

        Assert.Empty(response.Bindings);
    }

    [StaFact]
    public void List_SingleBinding_IsReturned()
    {
        var registry = new ElementRegistry();
        BindingInspector inspector = CreateInspector(registry);
        var source = new Source { Value = "x" };
        var text = new TextBlock();
        BindingOperations.SetBinding(text, TextBlock.TextProperty, new Binding("Value") { Source = source });

        ListBindingsResponse response = inspector.ListBindings(text, includeDescendants: false);

        Assert.Single(response.Bindings);
        Assert.Equal("Text", response.Bindings[0].Property);
        Assert.Equal("Value", response.Bindings[0].BindingPath);
    }

    [StaFact]
    public void List_MultipleBindingsOnSameElement_AllReturned()
    {
        var registry = new ElementRegistry();
        BindingInspector inspector = CreateInspector(registry);
        var source = new Source { Value = "x" };
        var text = new TextBlock();
        BindingOperations.SetBinding(text, TextBlock.TextProperty, new Binding("Value") { Source = source });
        BindingOperations.SetBinding(text, TextBlock.ToolTipProperty, new Binding("Value") { Source = source });

        ListBindingsResponse response = inspector.ListBindings(text, includeDescendants: false);

        Assert.Equal(2, response.Bindings.Count);
        Assert.Contains(response.Bindings, b => b.Property == "Text");
        Assert.Contains(response.Bindings, b => b.Property == "ToolTip");
    }

    [StaFact]
    public void List_IncludeDescendantsFalse_DoesNotRecurse()
    {
        var registry = new ElementRegistry();
        BindingInspector inspector = CreateInspector(registry);
        var source = new Source { Value = "x" };
        var inner = new TextBlock();
        BindingOperations.SetBinding(inner, TextBlock.TextProperty, new Binding("Value") { Source = source });
        var outer = new ContentControl { Content = inner };

        ListBindingsResponse response = inspector.ListBindings(outer, includeDescendants: false);

        Assert.Empty(response.Bindings);
    }

    [StaFact]
    public void List_IncludeDescendantsTrue_FindsNestedBindings()
    {
        var registry = new ElementRegistry();
        BindingInspector inspector = CreateInspector(registry);
        var source = new Source { Value = "x" };
        var stack = new StackPanel();
        var a = new TextBlock { Name = "A" };
        var b = new TextBlock { Name = "B" };
        BindingOperations.SetBinding(a, TextBlock.TextProperty, new Binding("Value") { Source = source });
        BindingOperations.SetBinding(b, TextBlock.TextProperty, new Binding("Value") { Source = source });
        stack.Children.Add(a);
        stack.Children.Add(b);

        ListBindingsResponse response = inspector.ListBindings(stack, includeDescendants: true);

        Assert.Equal(2, response.Bindings.Count);
        Assert.Contains(response.Bindings, x => x.ElementType == "TextBlock");
    }

    [StaFact]
    public void List_BrokenBinding_HasErrorTrue()
    {
        var registry = new ElementRegistry();
        BindingInspector inspector = CreateInspector(registry);
        var source = new Source { Value = "x" };
        var text = new TextBlock();
        BindingOperations.SetBinding(
            text,
            TextBlock.TextProperty,
            new Binding("DoesNotExist") { Source = source });

        ListBindingsResponse response = inspector.ListBindings(text, includeDescendants: false);

        Assert.Single(response.Bindings);
        Assert.True(
            response.Bindings[0].HasError || response.Bindings[0].State != "Active",
            $"Expected error state; got State={response.Bindings[0].State}, HasError={response.Bindings[0].HasError}");
    }

    [StaFact]
    public void List_NullElement_Throws()
    {
        var registry = new ElementRegistry();
        BindingInspector inspector = CreateInspector(registry);
        Assert.Throws<ArgumentNullException>(() => inspector.ListBindings(null!, false));
    }

    private sealed class Source : INotifyPropertyChanged
    {
        public string Value { get; set; } = string.Empty;

#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    }
}
