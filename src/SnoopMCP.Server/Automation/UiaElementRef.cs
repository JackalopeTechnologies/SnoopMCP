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
public sealed record UiaElementRef(int Pid, string Handle, string? By, string? Value);
