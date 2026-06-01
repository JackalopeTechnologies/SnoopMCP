// InspectBindingResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>inspectBinding</c> tool: the live state of a single
/// <c>BindingExpression</c> on a dependency property.
/// </summary>
/// <param name="BindingPath">The binding's path string, e.g. <c>Value</c>, or <c>null</c> when there is no binding.</param>
/// <param name="Mode">The binding mode, e.g. <c>OneWay</c>, <c>TwoWay</c>, or <c>null</c>.</param>
/// <param name="ResolvedSourceType">The CLR full name of the resolved source object's type, or <c>null</c>.</param>
/// <param name="ResolvedSourceHashCode">A runtime identity hash of the resolved source, or <c>null</c>.</param>
/// <param name="CurrentValue">The property's current effective value, stringified via invariant culture, or <c>null</c>.</param>
/// <param name="State">The binding status, e.g. <c>Active</c>, <c>PathError</c>, <c>NoBinding</c>.</param>
/// <param name="RecentTraceLines">Captured binding trace lines; always empty in v1.</param>
public sealed record InspectBindingResponse(
    string? BindingPath,
    string? Mode,
    string? ResolvedSourceType,
    int? ResolvedSourceHashCode,
    string? CurrentValue,
    string State,
    IReadOnlyList<BindingTraceLineDto> RecentTraceLines);
