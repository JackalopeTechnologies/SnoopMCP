// StyleResolverTests.cs
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
using System.Windows.Media;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class StyleResolverTests
{
    [StaFact]
    public void Resolve_NoStyle_BasedOnAndSettersEmpty()
    {
        var resolver = new StyleResolver();
        var button = new Button();

        ResolveStyleResponse response = resolver.Resolve(button);

        Assert.Empty(response.BasedOnChain);
        Assert.Empty(response.Setters);
        Assert.Empty(response.Triggers);
    }

    [StaFact]
    public void Resolve_WithSingleSetter_SetterReported()
    {
        var resolver = new StyleResolver();
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Crimson));
        var button = new Button { Style = style };

        ResolveStyleResponse response = resolver.Resolve(button);

        Assert.Contains(response.Setters, s => s.Property == "Background");
    }

    [StaFact]
    public void Resolve_WithBasedOnChain_ChainReported()
    {
        var resolver = new StyleResolver();
        var baseStyle = new Style(typeof(Button));
        var middle = new Style(typeof(Button)) { BasedOn = baseStyle };
        var top = new Style(typeof(Button)) { BasedOn = middle };
        var button = new Button { Style = top };

        ResolveStyleResponse response = resolver.Resolve(button);

        Assert.Equal(3, response.BasedOnChain.Count);
        Assert.Equal(0, response.BasedOnChain[0].Depth);
        Assert.Equal(2, response.BasedOnChain[2].Depth);
    }

    [StaFact]
    public void Resolve_ExplicitStyle_SourceIsExplicit()
    {
        var resolver = new StyleResolver();
        var style = new Style(typeof(Button));
        var button = new Button { Style = style };

        ResolveStyleResponse response = resolver.Resolve(button);

        Assert.Equal("Explicit", response.AppliedStyleSource);
    }

    [StaFact]
    public void Resolve_WithTrigger_TriggerReported()
    {
        var resolver = new StyleResolver();
        var style = new Style(typeof(Button));
        var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Blue));
        style.Triggers.Add(trigger);
        var button = new Button { Style = style };

        ResolveStyleResponse response = resolver.Resolve(button);

        Assert.Contains(response.Triggers, t => t.Kind == "Trigger");
        TriggerSummaryDto summary = response.Triggers.First(t => t.Kind == "Trigger");
        Assert.Contains("IsMouseOver", summary.Condition, StringComparison.Ordinal);
        Assert.Contains(summary.Setters, s => s.Property == "Background");
    }

    [StaFact]
    public void Resolve_NullElement_Throws()
    {
        var resolver = new StyleResolver();
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
    }
}
