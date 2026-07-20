// UiaElementRef.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Automation;

/// <summary>
/// A cross-call reference to a UIA element. <see cref="Handle"/> indexes the host-side
/// <c>ElementHandleCache</c>; when it expires, <see cref="By"/>+<see cref="Value"/> re-resolve it.
/// </summary>
/// <param name="Pid">The target process id.</param>
/// <param name="Handle">Opaque cache handle ("&lt;pid&gt;:&lt;seq&gt;"), or empty for a locator-only ref.</param>
/// <param name="By">Durable locator kind used to re-resolve, or null.</param>
/// <param name="Value">Durable locator value, or null.</param>
/// <remarks>
/// <see cref="By"/> and <see cref="Value"/> carry defaults so they are published as optional in the
/// nested tool schema: <see cref="Handle"/> alone identifies a live element, and requiring the locator
/// pair forced callers to invent values for a ref they had just been handed — see issue #74.
/// </remarks>
public sealed record UiaElementRef(int Pid, string Handle, string? By = null, string? Value = null);
