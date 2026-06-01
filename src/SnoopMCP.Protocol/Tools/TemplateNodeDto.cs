// TemplateNodeDto.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// One node in a control's runtime template tree (the template's visual-tree expansion). Each node
/// carries a stable element id the LLM can drill into via other tools.
/// </summary>
/// <param name="ElementId">The stable registry id assigned to this element.</param>
/// <param name="Type">The element's CLR type name, e.g. <c>Border</c>.</param>
/// <param name="Name">The element's <c>x:Name</c> when set, otherwise <c>null</c>.</param>
/// <param name="Children">The element's visual children in the template tree.</param>
public sealed record TemplateNodeDto(
    int ElementId,
    string Type,
    string? Name,
    IReadOnlyList<TemplateNodeDto> Children);
