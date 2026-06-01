// NamedPartDto.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// A named template part (e.g. <c>PART_Border</c>) reachable in a control's expanded template,
/// with a stable element id the LLM can drill into.
/// </summary>
/// <param name="PartName">The part's <c>x:Name</c>, e.g. <c>PART_Border</c>.</param>
/// <param name="PartType">The part's CLR type (full name when available).</param>
/// <param name="ElementId">The stable registry id assigned to the part.</param>
public sealed record NamedPartDto(string PartName, string PartType, int ElementId);
