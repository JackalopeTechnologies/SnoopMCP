// RpcRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Wire;

using System.Text.Json;

/// <summary>
/// Wire-format request envelope sent from the MCP server to an in-process payload.
/// </summary>
public sealed class RpcRequest
{
    /// <summary>Gets the correlation id used to pair this request with its response.</summary>
    public long Id { get; init; }

    /// <summary>Gets the tool name to dispatch.</summary>
    public string Tool { get; init; } = string.Empty;

    /// <summary>Gets the tool-specific argument payload.</summary>
    public JsonElement Arguments { get; init; }
}
