// WaitForValueRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>waitForValue</c> tool. Exactly one of <see cref="DependencyProperty"/> and
/// <see cref="DataContextPath"/> must be set; that invariant is enforced by the handler, not by this type.
/// </summary>
/// <param name="Id">Element id whose DP or DataContext is polled.</param>
/// <param name="DependencyProperty">The DP registered name to poll, or null.</param>
/// <param name="DataContextPath">The dotted DataContext path to poll, or null.</param>
/// <param name="Expected">The expected value, compared via ordinal string equality.</param>
/// <param name="TimeoutMs">Maximum time to poll.</param>
public sealed record WaitForValueRequest(int Id, string? DependencyProperty, string? DataContextPath, string Expected, int TimeoutMs);
