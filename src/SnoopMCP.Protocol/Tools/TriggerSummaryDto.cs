// TriggerSummaryDto.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// A best-effort summary of one style trigger: its kind, a condition string, and the setters it
/// applies when active. Full condition introspection (multi-conditions, data triggers) is deferred.
/// </summary>
/// <param name="Kind">The trigger's CLR type name, e.g. <c>Trigger</c>, <c>DataTrigger</c>, <c>MultiTrigger</c>.</param>
/// <param name="Condition">A summary of the trigger condition, e.g. <c>IsMouseOver=True</c> for a property trigger.</param>
/// <param name="Setters">The setters the trigger applies when active.</param>
public sealed record TriggerSummaryDto(
    string Kind,
    string Condition,
    IReadOnlyList<StyleSetterDto> Setters);
