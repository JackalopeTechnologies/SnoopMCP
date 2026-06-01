// ElementDescriberTests.cs
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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class ElementDescriberTests
{
    private static ElementDescriber CreateDescriber(ElementRegistry registry)
    {
        return new ElementDescriber(registry, new PathStringEmitter());
    }

    [StaFact]
    public void Describe_PlainButton_ReturnsTypeAndChildCount()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var button = new Button { Name = "SaveButton" };

        DescribeElementResponse response = describer.Describe(button);

        Assert.Equal("Button", response.Type);
        Assert.Equal("SaveButton", response.Name);
        Assert.True(response.IsAlive);
        Assert.Equal(0, response.ChildCount);
    }

    [StaFact]
    public void Describe_AutomationIdSet_IsReturned()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var button = new Button();
        System.Windows.Automation.AutomationProperties.SetAutomationId(button, "ThemePicker");

        DescribeElementResponse response = describer.Describe(button);

        Assert.Equal("ThemePicker", response.AutomationId);
    }

    [StaFact]
    public void Describe_TextBlockWithText_VisibleTextIsContent()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var text = new TextBlock { Text = "Hello world" };

        DescribeElementResponse response = describer.Describe(text);

        Assert.Equal("Hello world", response.VisibleText);
    }

    [StaFact]
    public void Describe_PanelWithTextChildren_AggregatesVisibleText()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = "Alpha" });
        stack.Children.Add(new TextBlock { Text = "Beta" });

        DescribeElementResponse response = describer.Describe(stack);

        Assert.Contains("Alpha", response.VisibleText);
        Assert.Contains("Beta", response.VisibleText);
    }

    [StaFact]
    public void Describe_DataContextSet_TypeNameReturned()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var grid = new Grid { DataContext = new Customer { Name = "Alice" } };

        DescribeElementResponse response = describer.Describe(grid);

        Assert.Equal(typeof(Customer).FullName, response.DataContextType);
    }

    [StaFact]
    public void Describe_NoDataContext_TypeIsNull()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var grid = new Grid();

        DescribeElementResponse response = describer.Describe(grid);

        Assert.Null(response.DataContextType);
    }

    [StaFact]
    public void Describe_BrokenBinding_HasBindingErrorsTrue()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var text = new TextBlock();
        var binding = new Binding("ThisPathDoesNotExist") { Source = new object() };
        BindingOperations.SetBinding(text, TextBlock.TextProperty, binding);

        DescribeElementResponse response = describer.Describe(text);

        Assert.True(response.HasBindingErrors);
    }

    [StaFact]
    public void Describe_PathEmits_WithEmitter()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var grid = new Grid();
        var button = new Button { Name = "SaveBtn" };
        grid.Children.Add(button);

        DescribeElementResponse response = describer.Describe(button);

        Assert.Equal("/Grid/Button[Name='SaveBtn']", response.Path);
    }

    private sealed class Customer
    {
        public string Name { get; set; } = string.Empty;
    }
}
