// ElementPredicateDto.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

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

    /// <summary>Stringified equality check against a named dependency property.</summary>
    public PropertyEqualsDto? PropertyEquals { get; init; }

    /// <summary>Recursive predicate matched against any visual-tree ancestor.</summary>
    public ElementPredicateDto? HasAncestor { get; init; }

    /// <summary>Recursive predicate matched against any visual-tree descendant.</summary>
    public ElementPredicateDto? HasDescendant { get; init; }

    /// <summary>Recursive predicate matched against the element's templated parent.</summary>
    public ElementPredicateDto? InTemplateOf { get; init; }
}
