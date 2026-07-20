// ToolErrorFilterTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using System.Text.Json;
using ModelContextProtocol;
using Protocol.Errors;
using Tools;
using Xunit;

/// <summary>
/// Covers the classification behind the call-tool error filter. Anything that is not already an
/// <see cref="McpException"/> reaches the client as the SDK's sanitised "An error occurred invoking
/// '…'", naming neither the failure nor the reason — which is why two one-line defects (#72, #73)
/// were only diagnosable from the host's own log file (#81). Argument-binding failures in particular
/// happen INSIDE the SDK's marshaller, before any tool body runs, so no try/catch in a tool can ever
/// reach them; only this filter can.
/// </summary>
public sealed class ToolErrorFilterTests
{
    [Fact]
    public void Describe_WhenSnoopMcpException_KeepsItsCodeAndMessage()
    {
        var source = new SnoopMcpException(ErrorCode.NotDrivable, "Element does not support ValuePattern.");

        McpException described = ToolErrorFilter.Describe(source, "set_uia_value");

        Assert.Contains(nameof(ErrorCode.NotDrivable), described.Message, StringComparison.Ordinal);
        Assert.Contains("Element does not support ValuePattern.", described.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_WhenArgumentBindingFails_ReportsInvalidArgumentAndNamesTheParameter()
    {
        // Exactly what the SDK's ReflectionAIFunction marshaller throws for a missing argument.
        var source = new ArgumentException(
            "The arguments dictionary is missing a value for the required parameter 'dispatch'.",
            "arguments");

        McpException described = ToolErrorFilter.Describe(source, "peer_invoke");

        Assert.Contains(nameof(ErrorCode.InvalidArgument), described.Message, StringComparison.Ordinal);
        Assert.Contains("dispatch", described.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("An error occurred invoking", described.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_WhenResponseCannotSerialize_ReportsProtocolErrorAndNamesTheTool()
    {
        var source = new JsonException("Infinity cannot be written as valid JSON.");

        McpException described = ToolErrorFilter.Describe(source, "find_uia_element");

        Assert.Contains(nameof(ErrorCode.ProtocolError), described.Message, StringComparison.Ordinal);
        Assert.Contains("find_uia_element", described.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_WhenUnclassified_ReportsUnknownAndNamesToolAndExceptionType()
    {
        var source = new InvalidOperationException("peer went away");

        McpException described = ToolErrorFilter.Describe(source, "hit_test");

        Assert.Contains(nameof(ErrorCode.Unknown), described.Message, StringComparison.Ordinal);
        Assert.Contains("hit_test", described.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), described.Message, StringComparison.Ordinal);
        Assert.Contains("peer went away", described.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_PreservesTheOriginalExceptionAsInnerForTheServerLog()
    {
        var source = new InvalidOperationException("root detail");

        McpException described = ToolErrorFilter.Describe(source, "attach");

        Assert.Same(source, described.InnerException);
    }
}
