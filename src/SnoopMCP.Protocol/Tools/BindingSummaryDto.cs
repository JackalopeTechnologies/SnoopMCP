// BindingSummaryDto.cs
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
///     One row of a broad binding audit: a single bound dependency property on one element, summarised.
///     The same shape as <c>inspectBinding</c>'s response minus the (v1-empty) recent trace lines, plus
///     the owning element's id and type so the LLM can drill into a specific binding.
/// </summary>
/// <param name="ElementId">The stable registry id of the element carrying the binding.</param>
/// <param name="ElementType">The element's CLR type name, e.g. <c>TextBlock</c>.</param>
/// <param name="Property">The bound dependency property's registered name, e.g. <c>Text</c>.</param>
/// <param name="BindingPath">The binding's path string, e.g. <c>Value</c>, or <c>null</c>.</param>
/// <param name="Mode">The binding mode, e.g. <c>OneWay</c>, <c>TwoWay</c>, or <c>null</c>.</param>
/// <param name="State">The binding status, e.g. <c>Active</c>, <c>PathError</c>.</param>
/// <param name="HasError">Whether the binding expression currently reports an error.</param>
/// <param name="ResolvedSourceType">The CLR full name of the resolved source object's type, or <c>null</c>.</param>
/// <param name="CurrentValue">The property's current effective value, stringified via invariant culture, or <c>null</c>.</param>
public sealed record BindingSummaryDto(
    int ElementId,
    string ElementType,
    string Property,
    string? BindingPath,
    string? Mode,
    string State,
    bool HasError,
    string? ResolvedSourceType,
    string? CurrentValue);
