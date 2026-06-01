// ToolRegistry.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tools;

/// <summary>
/// Lookup table mapping wire-protocol tool names to <see cref="IToolHandler"/> instances.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, IToolHandler> mHandlers = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a handler keyed by its <see cref="IToolHandler.ToolName"/>.
    /// </summary>
    /// <param name="handler">The handler instance to register.</param>
    public void Register(IToolHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        mHandlers[handler.ToolName] = handler;
    }

    /// <summary>
    /// Returns the handler registered for <paramref name="toolName"/>, or <c>null</c> if none is registered.
    /// </summary>
    /// <param name="toolName">The wire-protocol tool name.</param>
    /// <returns>The registered handler, or <c>null</c> when no handler is registered.</returns>
    public IToolHandler? Find(string toolName)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        mHandlers.TryGetValue(toolName, out IToolHandler? candidate);
        return candidate;
    }
}
