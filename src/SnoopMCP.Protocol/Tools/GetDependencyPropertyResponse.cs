// GetDependencyPropertyResponse.cs
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
