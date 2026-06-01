// RpcError.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Wire;

using SnoopMCP.Protocol.Errors;

/// <summary>
/// Error payload returned alongside an <see cref="RpcResponse"/> when a tool invocation fails.
/// </summary>
public sealed class RpcError
{
    /// <summary>Gets the structured error code.</summary>
    public ErrorCode Code { get; init; } = ErrorCode.Unknown;

    /// <summary>Gets the human-readable error message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets optional supplementary detail (stack trace fragment, diagnostic context, etc.).</summary>
    public string? Details { get; init; }
}
