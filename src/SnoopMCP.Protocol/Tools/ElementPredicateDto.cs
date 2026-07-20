// ElementPredicateDto.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// AND-combined predicate for <c>findElements</c>. Every field is optional; an element matches
/// only when every supplied field matches. Recursive fields (<c>HasAncestor</c>, <c>HasDescendant</c>,
/// <c>InTemplateOf</c>) nest the same predicate shape.
/// </summary>
public sealed record ElementPredicateDto
{
    /// <summary>Case-sensitive substring match against the element's full type name.</summary>
    public string? Type { get; init; }

    /// <summary>Exact match against the element's <c>x:Name</c>.</summary>
    public string? Name { get; init; }

    /// <summary>Exact match against <c>AutomationProperties.AutomationId</c>.</summary>
    public string? AutomationId { get; init; }

    /// <summary>Case-insensitive substring search inside the element's capped visible text.</summary>
    public string? TextContains { get; init; }

    /// <summary>Restricts <see cref="TextContains"/> matches to leaf nodes only.</summary>
    public bool LeafOnly { get; init; }

    /// <summary>Limits the number of matches returned for <see cref="TextContains"/>.</summary>
    public int? MaxResults { get; init; }

    /// <summary>Suppresses the canonical path in returned matches when <see cref="TextContains"/> is used.</summary>
    public bool SuppressPath { get; init; }

    /// <summary>Stringified equality check against a named dependency property.</summary>
    public PropertyEqualsDto? PropertyEquals { get; init; }

    /// <summary>Recursive predicate matched against any visual-tree ancestor.</summary>
    public ElementPredicateDto? HasAncestor { get; init; }

    /// <summary>Recursive predicate matched against any visual-tree descendant.</summary>
    public ElementPredicateDto? HasDescendant { get; init; }

    /// <summary>Recursive predicate matched against the element's templated parent.</summary>
    public ElementPredicateDto? InTemplateOf { get; init; }
}
