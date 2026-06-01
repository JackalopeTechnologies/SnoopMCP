// TemplateResolverTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Windows.Controls;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class TemplateResolverTests
{
    private static TemplateResolver CreateResolver(ElementRegistry registry)
    {
        var emitter = new PathStringEmitter();
        var describer = new ElementDescriber(registry, emitter);
        return new TemplateResolver(registry, describer);
    }

    [StaFact]
    public void Resolve_NonControl_ReturnsEmptyResponse()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();

        ResolveTemplateResponse response = resolver.Resolve(grid);

        Assert.Null(response.TemplateType);
        Assert.Null(response.TemplateTree);
        Assert.Empty(response.NamedParts);
    }

    [StaFact]
    public void Resolve_ButtonWithDefaultTemplate_TemplateTypeReported()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var button = new Button { Content = "x" };
        button.ApplyTemplate();

        ResolveTemplateResponse response = resolver.Resolve(button);

        Assert.NotNull(response.TemplateType);
    }

    [StaFact]
    public void Resolve_TemplateTree_HasChildren()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var button = new Button { Content = "x" };
        button.ApplyTemplate();

        ResolveTemplateResponse response = resolver.Resolve(button);

        Assert.NotNull(response.TemplateTree);
    }

    [StaFact]
    public void Resolve_CustomTemplate_NamedPartsReported()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var template = new System.Windows.Controls.ControlTemplate(typeof(Button));
        var border = new System.Windows.FrameworkElementFactory(typeof(Border), "PART_Border");
        template.VisualTree = border;
        var button = new Button { Template = template, Content = "x" };
        button.ApplyTemplate();

        ResolveTemplateResponse response = resolver.Resolve(button);

        Assert.Contains(response.NamedParts, p => p.PartName == "PART_Border");
    }

    [StaFact]
    public void Resolve_NullElement_Throws()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
    }
}
