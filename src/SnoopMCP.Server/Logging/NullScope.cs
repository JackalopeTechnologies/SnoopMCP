// NullScope.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Logging;

/// <summary>
/// A no-op disposable returned by <see cref="FileLogger.BeginScope{TState}"/>. The file log renders
/// each entry as a flat line and does not project logging scopes, so beginning a scope yields this
/// shared instance whose disposal does nothing.
/// </summary>
internal sealed class NullScope : IDisposable
{
    /// <summary>Gets the shared singleton instance.</summary>
    public static NullScope Instance { get; } = new();

    private NullScope()
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Intentionally empty: there is no scope state to unwind.
    }
}
