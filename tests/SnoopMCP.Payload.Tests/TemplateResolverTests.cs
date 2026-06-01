// TemplateResolverTests.cs
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
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

#endregion

namespace SnoopMCP.Payload.Tests;

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
        TemplateResolver resolver = CreateResolver(registry);
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
        TemplateResolver resolver = CreateResolver(registry);
        var button = new Button { Content = "x" };
        button.ApplyTemplate();

        ResolveTemplateResponse response = resolver.Resolve(button);

        Assert.NotNull(response.TemplateType);
    }

    [StaFact]
    public void Resolve_TemplateTree_HasChildren()
    {
        var registry = new ElementRegistry();
        TemplateResolver resolver = CreateResolver(registry);
        var button = new Button { Content = "x" };
        button.ApplyTemplate();

        ResolveTemplateResponse response = resolver.Resolve(button);

        Assert.NotNull(response.TemplateTree);
    }

    [StaFact]
    public void Resolve_CustomTemplate_NamedPartsReported()
    {
        var registry = new ElementRegistry();
        TemplateResolver resolver = CreateResolver(registry);
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border), "PART_Border");
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
        TemplateResolver resolver = CreateResolver(registry);
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
    }
}
