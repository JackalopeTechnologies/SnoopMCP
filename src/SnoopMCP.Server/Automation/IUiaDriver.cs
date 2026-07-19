// IUiaDriver.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Automation;

using System.Windows.Automation;

/// <summary>Out-of-process UI Automation driver for a target WPF window, keyed by process id.</summary>
public interface IUiaDriver
{
    /// <summary>
    /// Walks the UIA subtree under the target window (or under <paramref name="fromElement"/> when
    /// given) to a bounded depth. When <paramref name="fromElement"/> is supplied it is resolved using
    /// the same handle→locator lookup as <see cref="ResolveAsync"/> — a stale subtree reference throws
    /// rather than silently re-rooting to the window.
    /// </summary>
    Task<IReadOnlyList<UiaElementInfo>> GetTreeAsync(int pid, UiaElementRef? fromElement, int depth, CancellationToken ct);

    /// <summary>Finds elements under the target window matching a single locator.</summary>
    Task<IReadOnlyList<UiaElementInfo>> FindAsync(int pid, string by, string value, CancellationToken ct);

    /// <summary>Polls until one element matches the locator or the timeout elapses.</summary>
    Task<UiaElementInfo> WaitForAsync(int pid, string by, string value, int timeoutMs, CancellationToken ct);

    /// <summary>
    /// Resolves a cross-call reference to a live element: the handle cache first, falling back to the
    /// durable <see cref="UiaElementRef.By"/>/<see cref="UiaElementRef.Value"/> locator on a cache miss.
    /// </summary>
    Task<AutomationElement> ResolveAsync(UiaElementRef reference, CancellationToken ct);
}
