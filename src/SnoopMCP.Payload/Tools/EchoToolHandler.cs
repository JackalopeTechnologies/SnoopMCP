// EchoToolHandler.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tools;

using System.Text.Json;

/// <summary>
/// Diagnostic handler that returns the supplied arguments wrapped in an <c>echoed</c> string.
/// Used to prove the host-to-payload pipe round-trip end-to-end before real tools land.
/// </summary>
public sealed class EchoToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name for the echo handler.</summary>
    public const string EchoToolName = "echo";

    /// <inheritdoc />
    public string ToolName => EchoToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var payload = new { echoed = arguments.GetRawText() };
        string json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
