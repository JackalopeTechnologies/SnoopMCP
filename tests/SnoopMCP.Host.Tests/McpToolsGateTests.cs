// McpToolsGateTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using Host;
using Host.Automation;
using Host.Tools;
using Xunit;

/// <summary>
/// Non-interactive gate-enforcement guard on <see cref="McpTools"/>' two mutating relays
/// (<c>peerInvoke</c>, <c>executeCommand</c>). The host interaction gate defaults off, and
/// <see cref="McpTools"/> checks it before <c>Dispatch</c>, so these assert
/// <see cref="McpException"/>/<c>InteractionDisabled</c> without ever needing an attached session -
/// if the gate check ran after Dispatch, these would instead surface a session/attach failure.
/// </summary>
public sealed class McpToolsGateTests : IDisposable
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
    public async Task PeerInvoke_WhenGateOff_ThrowsInteractionDisabled_BeforeDispatch()
    {
        McpTools tools = CreateTools();

        McpException ex = await Assert.ThrowsAsync<McpException>(
            () => tools.PeerInvoke(1, "Invoke", null, default));

        Assert.Contains("InteractionDisabled", ex.Message);
    }

    [Fact]
    public async Task ExecuteCommand_WhenGateOff_ThrowsInteractionDisabled_BeforeDispatch()
    {
        McpTools tools = CreateTools();

        McpException ex = await Assert.ThrowsAsync<McpException>(
            () => tools.ExecuteCommand(1, null, null, null, default));

        Assert.Contains("InteractionDisabled", ex.Message);
    }

    /// <summary>Builds a <see cref="McpTools"/> instance (session not attached) over a fresh, default-off gate.</summary>
    private McpTools CreateTools()
    {
        var session = new SessionManager(NullLogger<SessionManager>.Instance, NullLoggerFactory.Instance);
        return new McpTools(session, new NullInjectorService(), new InteractionGate(mGatePath));
    }
}
