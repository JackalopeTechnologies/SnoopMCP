// ElementDescriberTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Windows.Controls;
using System.Windows.Data;
using Payload;
using Inspection;
using PathStrings;
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
    public void Describe_DataContextSet_ReturnsDataContextHashCode()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var customer = new Customer { Name = "Alice" };
        var grid = new Grid { DataContext = customer };

        DescribeElementResponse response = describer.Describe(grid);

        Assert.Equal(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(customer), response.DataContextHashCode);
    }

    [StaFact]
    public void Describe_NoDataContext_DataContextHashCodeIsNull()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var grid = new Grid();

        DescribeElementResponse response = describer.Describe(grid);

        Assert.Null(response.DataContextHashCode);
    }

    /// <summary>
    /// Issue #78: a virtualizing panel can recycle the same container for a different row with no
    /// error and no id change. This asserts the actual detection signal: describing the SAME
    /// container after its DataContext is swapped (simulating recycling) returns the same <c>Id</c>
    /// and the same container <c>HashCode</c> — proving <c>HashCode</c> alone cannot reveal the
    /// swap, since it identifies the container, not its content — but a different
    /// <c>DataContextHashCode</c>, which a caller can compare against a previously cached value to
    /// learn its facts about this id are stale before acting on them.
    /// </summary>
    [StaFact]
    public void Describe_ContainerRecycledForDifferentRow_SameIdAndHashCode_ButDifferentDataContextHashCode()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var container = new Grid { DataContext = new Customer { Name = "Alice" } };

        DescribeElementResponse before = describer.Describe(container);

        // Simulate a virtualizing panel recycling this exact container instance for a new row.
        container.DataContext = new Customer { Name = "Bob" };
        DescribeElementResponse after = describer.Describe(container);

        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.HashCode, after.HashCode);
        Assert.NotEqual(before.DataContextHashCode, after.DataContextHashCode);
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
