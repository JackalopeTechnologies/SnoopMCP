// ElementHandleCacheTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using System.Windows.Automation;
using Host.Automation;
using Xunit;

public sealed class ElementHandleCacheTests
{
    [Fact]
    public void Add_ThenTryGet_ReturnsElementAndLocator()
    {
        var now = DateTimeOffset.UnixEpoch;
        var cache = new ElementHandleCache(TimeSpan.FromSeconds(60), () => now);
        AutomationElement root = AutomationElement.RootElement;

        string handle = cache.Add(1234, root, "automationId", "X");

        Assert.StartsWith("1234:", handle);
        Assert.True(cache.TryGet(handle, 1234, out AutomationElement? got, out string? by, out string? value));
        Assert.Same(root, got);
        Assert.Equal("automationId", by);
        Assert.Equal("X", value);
    }

    [Fact]
    public void TryGet_AfterTtl_ReturnsFalse()
    {
        var now = DateTimeOffset.UnixEpoch;
        var cache = new ElementHandleCache(TimeSpan.FromSeconds(60), () => now);
        string handle = cache.Add(1, AutomationElement.RootElement, null, null);

        now = now.AddSeconds(61);

        Assert.False(cache.TryGet(handle, 1, out _, out _, out _));
    }

    [Fact]
    public void TryGet_UnknownHandle_ReturnsFalse()
    {
        var cache = new ElementHandleCache();
        Assert.False(cache.TryGet("9:9", 9, out _, out _, out _));
    }

    [Fact]
    public void TryGet_PidMismatch_ReturnsFalse()
    {
        var cache = new ElementHandleCache();
        string handle = cache.Add(1234, AutomationElement.RootElement, "automationId", "X");

        Assert.False(cache.TryGet(handle, 5678, out _, out _, out _));
    }
}
