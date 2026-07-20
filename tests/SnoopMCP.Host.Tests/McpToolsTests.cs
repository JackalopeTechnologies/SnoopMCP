// McpToolsTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using Host;
using Host.Automation;
using Host.Tools;
using Protocol.Errors;
using Xunit;

/// <summary>
/// Verifies the MCP tool boundary promotes SnoopMCP's curated <see cref="SnoopMcpException"/> to
/// <see cref="McpException"/>, whose message the SDK propagates to the model - the difference between
/// the client seeing the real failure (code + description) and the SDK's sanitised
/// "An error occurred invoking '…'". Covers both error funnels: the session dispatch path and the
/// distinct attach path.
/// </summary>
public sealed class McpToolsTests
{
    [Fact]
    public async Task InspectionTool_WhenNotAttached_ThrowsMcpExceptionCarryingCodeAndMessage()
    {
        var session = new SessionManager(NullLogger<SessionManager>.Instance, NullLoggerFactory.Instance);
        var tools = new McpTools(session, new NullInjectorService(), CreateGate());

        McpException ex = await Assert.ThrowsAsync<McpException>(
            () => tools.DescribeElement(1, TestContext.Current.CancellationToken));

        Assert.Contains(nameof(ErrorCode.SessionLost), ex.Message);
        Assert.Contains("No attached session", ex.Message);
        // The list_visual_roots hint is scoped to ElementExpired; it must not leak onto other codes.
        Assert.DoesNotContain("list_visual_roots", ex.Message);
    }

    [Fact]
    public async Task Attach_WhenInjectorFails_ThrowsMcpExceptionCarryingCode()
    {
        var session = new SessionManager(NullLogger<SessionManager>.Instance, NullLoggerFactory.Instance);
        var tools = new McpTools(session, new NullInjectorService(), CreateGate());

        McpException ex = await Assert.ThrowsAsync<McpException>(
            () => tools.Attach(1234, TestContext.Current.CancellationToken));

        Assert.Contains(nameof(ErrorCode.AttachFailed), ex.Message);
    }

    /// <summary>Builds a fresh, default-off interaction gate backed by a per-test temp file (unused by these tests).</summary>
    private static InteractionGate CreateGate() =>
        new(Path.Combine(Path.GetTempPath(), "gate-" + Guid.NewGuid().ToString("N") + ".json"));
}
