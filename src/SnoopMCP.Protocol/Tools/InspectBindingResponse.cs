// InspectBindingResponse.cs
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
