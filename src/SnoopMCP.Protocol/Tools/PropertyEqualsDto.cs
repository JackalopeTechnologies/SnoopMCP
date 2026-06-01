// PropertyEqualsDto.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Requests that a specific dependency property on the candidate element stringify (via
/// <c>Convert.ToString</c> with invariant culture) to <paramref name="Value"/>.
/// </summary>
/// <param name="Property">Property name (no <c>Property</c> suffix).</param>
/// <param name="Value">The required stringified value.</param>
public sealed record PropertyEqualsDto(string Property, string Value);
