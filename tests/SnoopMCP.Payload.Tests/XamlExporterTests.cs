// XamlExporterTests.cs
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

using System.Windows.Controls;
using System.Windows.Media;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

#endregion

namespace SnoopMCP.Payload.Tests;

public sealed class XamlExporterTests
{
    [StaFact]
    public void Export_PlainButton_ReturnsXamlContainingButton()
    {
        var exporter = new XamlExporter();
        var button = new Button { Content = "Click me", Width = 80 };

        ExportXamlResponse response = exporter.Export(button);

        Assert.Contains("Button", response.Xaml, StringComparison.Ordinal);
        Assert.True(response.ByteCount > 0);
        Assert.False(response.Truncated);
    }

    [StaFact]
    public void Export_StackPanelWithChildren_IncludesChildrenInline()
    {
        var exporter = new XamlExporter();
        var stack = new StackPanel();
        stack.Children.Add(new Button { Content = "A" });
        stack.Children.Add(new Button { Content = "B" });

        ExportXamlResponse response = exporter.Export(stack);

        Assert.Contains("StackPanel", response.Xaml, StringComparison.Ordinal);
        Assert.Contains("Button", response.Xaml, StringComparison.Ordinal);
    }

    [StaFact]
    public void Export_TextBlockWithExplicitForeground_ReflectsLiveValue()
    {
        var exporter = new XamlExporter();
        var text = new TextBlock { Text = "Hello", Foreground = Brushes.Red };

        ExportXamlResponse response = exporter.Export(text);

        Assert.Contains("TextBlock", response.Xaml, StringComparison.Ordinal);
        Assert.Contains("Hello", response.Xaml, StringComparison.Ordinal);
    }

    [StaFact]
    public void Export_LargePayload_TruncatesAndWarns()
    {
        var exporter = new XamlExporter(256);
        var stack = new StackPanel();
        for (var i = 0; i < 200; i++)
            stack.Children.Add(new TextBlock { Text = $"This is row number {i:D4} with extra padding text" });

        ExportXamlResponse response = exporter.Export(stack);

        Assert.True(response.Truncated);
        Assert.NotNull(response.Warning);
        Assert.True(response.Xaml.Length <= 256 + 256);
    }

    [StaFact]
    public void Export_NullElement_Throws()
    {
        var exporter = new XamlExporter();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(null!));
    }
}
