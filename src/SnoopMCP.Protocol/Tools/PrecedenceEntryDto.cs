// PrecedenceEntryDto.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// One entry in a dependency property's precedence trace: a candidate value from a particular source.
/// </summary>
/// <param name="Source">The source family, e.g. <c>Local</c>, <c>StyleSetter</c>, <c>Inherited</c>, <c>Default</c>.</param>
/// <param name="Value">The candidate value from this source, stringified via invariant culture.</param>
/// <param name="ValueType">The CLR full name of the candidate value's runtime type, or <c>null</c>.</param>
/// <param name="SourceDescription">A human-readable description of where this candidate came from.</param>
public sealed record PrecedenceEntryDto(
    string Source,
    string? Value,
    string? ValueType,
    string SourceDescription);
