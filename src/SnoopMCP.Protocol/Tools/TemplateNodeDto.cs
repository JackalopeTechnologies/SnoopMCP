// TemplateNodeDto.cs
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
///     One node in a control's runtime template tree (the template's visual-tree expansion). Each node
///     carries a stable element id the LLM can drill into via other tools.
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
