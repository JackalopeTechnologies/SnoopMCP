// GetDependencyPropertyResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>getDependencyProperty</c> tool: the current effective value plus a
/// best-effort precedence trace explaining which source won and what the losing values were.
/// </summary>
/// <param name="Name">The dependency property's registered name.</param>
/// <param name="CurrentValue">The current effective value, stringified via invariant culture.</param>
/// <param name="CurrentValueType">The CLR full name of the current value's runtime type, or <c>null</c>.</param>
/// <param name="Precedence">The best-effort precedence trace, highest-priority sources first.</param>
/// <param name="WinningSource">
/// The <c>BaseValueSource</c> name reported by <c>DependencyPropertyHelper.GetValueSource</c>.
/// </param>
public sealed record GetDependencyPropertyResponse(
    string Name,
    string? CurrentValue,
    string? CurrentValueType,
    IReadOnlyList<PrecedenceEntryDto> Precedence,
    string WinningSource);
