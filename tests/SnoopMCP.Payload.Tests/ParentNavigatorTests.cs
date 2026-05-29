// ParentNavigatorTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

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
        var nav = CreateNavigator(registry);
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
        var nav = CreateNavigator(registry);
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
        var nav = CreateNavigator(registry);
        var orphan = new Button();

        GetParentResponse response = nav.GetParent(orphan, "visual");

        Assert.Null(response.Parent);
    }

    [StaFact]
    public void GetTemplatedParent_OfPlainElement_ReturnsNull()
    {
        var registry = new ElementRegistry();
        var nav = CreateNavigator(registry);
        var button = new Button();

        GetTemplatedParentResponse response = nav.GetTemplatedParent(button);

        Assert.Null(response.TemplatedParent);
    }

    [StaFact]
    public void GetTemplatedParent_OfTemplateChild_ReturnsTemplatedHost()
    {
        var registry = new ElementRegistry();
        var nav = CreateNavigator(registry);

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
        var nav = CreateNavigator(registry);
        var button = new Button();

        Assert.Throws<ArgumentException>(() => nav.GetParent(button, "elven"));
    }

    private static DependencyObject? FindFirstTemplateChild(DependencyObject root)
    {
        DependencyObject? result = null;
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count && result is null; i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            FrameworkElement? fe = child as FrameworkElement;
            bool isFromTemplate = fe?.TemplatedParent is not null;
            result = isFromTemplate ? child : FindFirstTemplateChild(child);
        }
        return result;
    }
}
