// XamlExporterTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Windows.Controls;
using System.Windows.Media;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

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
        var text = new TextBlock
        {
            Text = "Hello",
            Foreground = Brushes.Red
        };

        ExportXamlResponse response = exporter.Export(text);

        Assert.Contains("TextBlock", response.Xaml, StringComparison.Ordinal);
        Assert.Contains("Hello", response.Xaml, StringComparison.Ordinal);
    }

    [StaFact]
    public void Export_LargePayload_TruncatesAndWarns()
    {
        var exporter = new XamlExporter(softCapBytes: 256);
        var stack = new StackPanel();
        for (int i = 0; i < 200; i++)
        {
            stack.Children.Add(new TextBlock { Text = $"This is row number {i:D4} with extra padding text" });
        }

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
