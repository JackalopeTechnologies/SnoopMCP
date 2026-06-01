// RpcResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Wire;

using System.Text.Json;

/// <summary>
/// Wire-format response envelope returned by an in-process payload to the MCP server.
/// </summary>
public sealed class RpcResponse
{
    /// <summary>Gets the correlation id of the request this response answers.</summary>
    public long Id { get; init; }

    /// <summary>Gets the result payload, populated when the call succeeded.</summary>
    public JsonElement? Result { get; init; }

    /// <summary>Gets the error payload, populated when the call failed.</summary>
    public RpcError? Error { get; init; }

    /// <summary>Gets a value indicating whether this response represents a successful invocation.</summary>
    public bool IsSuccess => Error is null;
}
