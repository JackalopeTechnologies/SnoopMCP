// PathResolverTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class PathResolverTests
{
    private static PathResolver CreateResolver(ElementRegistry registry)
    {
        var emitter = new PathStringEmitter();
        var describer = new ElementDescriber(registry, emitter);
        var parser = new PathStringParser();
        return new PathResolver(describer, parser);
    }

    [StaFact]
    public void Resolve_SingleStep_MatchingRoot_ReturnsRoot()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();

        ResolvePathResponse response = resolver.Resolve(grid, "/Grid");

        Assert.NotNull(response.Element);
        int gridId = registry.GetOrAssign(grid);
        Assert.Equal(gridId, response.Element!.Id);
    }

    [StaFact]
    public void Resolve_RootTypeMismatch_ReturnsNull()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();

        ResolvePathResponse response = resolver.Resolve(grid, "/Window");

        Assert.Null(response.Element);
    }

    [StaFact]
    public void Resolve_PathToChild_ByName_ReturnsChild()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();
        var button = new Button { Name = "SaveBtn" };
        grid.Children.Add(button);

        ResolvePathResponse response = resolver.Resolve(grid, "/Grid/Button[Name='SaveBtn']");

        Assert.NotNull(response.Element);
        int buttonId = registry.GetOrAssign(button);
        Assert.Equal(buttonId, response.Element!.Id);
    }

    [StaFact]
    public void Resolve_PathToChild_ByAutomationId_ReturnsChild()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();
        var tagged = new Button();
        AutomationProperties.SetAutomationId(tagged, "ThemePicker");
        grid.Children.Add(tagged);
        grid.Children.Add(new Button());

        ResolvePathResponse response = resolver.Resolve(grid, "/Grid/Button[AutomationId='ThemePicker']");

        Assert.NotNull(response.Element);
        int taggedId = registry.GetOrAssign(tagged);
        Assert.Equal(taggedId, response.Element!.Id);
    }

    [StaFact]
    public void Resolve_PathWithIndex_PicksNthSameTypeSibling()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var stack = new StackPanel();
        var first = new Button();
        var second = new Button();
        var third = new Button();
        stack.Children.Add(first);
        stack.Children.Add(second);
        stack.Children.Add(third);

        ResolvePathResponse response = resolver.Resolve(stack, "/StackPanel/Button[1]");

        Assert.NotNull(response.Element);
        int secondId = registry.GetOrAssign(second);
        Assert.Equal(secondId, response.Element!.Id);
    }

    [StaFact]
    public void Resolve_DeepPath_TraversesMultipleLevels()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();
        var stack = new StackPanel();
        var border = new Border();
        var button = new Button { Name = "Deep" };
        grid.Children.Add(stack);
        stack.Children.Add(border);
        border.Child = button;

        ResolvePathResponse response = resolver.Resolve(
            grid,
            "/Grid/StackPanel/Border/Button[Name='Deep']");

        Assert.NotNull(response.Element);
        int buttonId = registry.GetOrAssign(button);
        Assert.Equal(buttonId, response.Element!.Id);
    }

    [StaFact]
    public void Resolve_RoundTripWithEmitter_ReturnsOriginal()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var emitter = new PathStringEmitter();
        var grid = new Grid();
        var button = new Button { Name = "Save" };
        grid.Children.Add(button);

        string path = emitter.Emit(button);
        ResolvePathResponse response = resolver.Resolve(grid, path);

        Assert.NotNull(response.Element);
        int buttonId = registry.GetOrAssign(button);
        Assert.Equal(buttonId, response.Element!.Id);
    }

    [StaFact]
    public void Resolve_NoMatchingChild_ReturnsNull()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();
        grid.Children.Add(new Button { Name = "Save" });

        ResolvePathResponse response = resolver.Resolve(grid, "/Grid/Button[Name='DoesNotExist']");

        Assert.Null(response.Element);
    }

    [StaFact]
    public void Resolve_IndexOutOfRange_ReturnsNull()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var stack = new StackPanel();
        stack.Children.Add(new Button());
        stack.Children.Add(new Button());

        ResolvePathResponse response = resolver.Resolve(stack, "/StackPanel/Button[9]");

        Assert.Null(response.Element);
    }

    [StaFact]
    public void Resolve_InvalidPath_ThrowsPathParseError()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(
            () => resolver.Resolve(grid, "no-leading-slash"));
        Assert.Equal(ErrorCode.PathParseError, ex.Code);
    }

    [StaFact]
    public void Resolve_NullRoot_Throws()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);

        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!, "/Grid"));
    }
}
