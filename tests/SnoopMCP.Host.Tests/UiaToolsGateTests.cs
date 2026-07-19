// UiaToolsGateTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using Host.Automation;
using Host.Tools;
using ModelContextProtocol;
using Xunit;

/// <summary>
/// Non-interactive gate-enforcement guard on <see cref="UiaTools"/>' mutating tools. The host
/// interaction gate defaults off, and <see cref="UiaTools"/> checks it before the WPF target guard
/// (R2-D), so these assert <see cref="McpException"/>/<c>InteractionDisabled</c> without ever needing
/// a live window.
/// </summary>
public sealed class UiaToolsGateTests : IDisposable
{
    private readonly string mGatePath =
        Path.Combine(Path.GetTempPath(), "gate-" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        if (File.Exists(mGatePath))
        {
            File.Delete(mGatePath);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task InvokeUia_WhenGateOff_ThrowsInteractionDisabled()
    {
        UiaTools tools = CreateTools();
        var reference = new UiaElementRef(1234, "1234:1", "automationId", "X");

        McpException ex = await Assert.ThrowsAsync<McpException>(() => tools.InvokeUia(reference, null, default));

        Assert.Contains("InteractionDisabled", ex.Message);
    }

    [Fact]
    public async Task SetUiaValue_WhenGateOff_ThrowsInteractionDisabled()
    {
        UiaTools tools = CreateTools();
        var reference = new UiaElementRef(1234, "1234:1", "automationId", "X");

        McpException ex = await Assert.ThrowsAsync<McpException>(() => tools.SetUiaValue(reference, "X", default));

        Assert.Contains("InteractionDisabled", ex.Message);
    }

    /// <summary>Builds a <see cref="UiaTools"/> instance over a fresh, default-off gate.</summary>
    private UiaTools CreateTools() =>
        new(new UiaDriver(new ElementHandleCache()), new PrintWindowCapture(), new InteractionGate(mGatePath));
}
