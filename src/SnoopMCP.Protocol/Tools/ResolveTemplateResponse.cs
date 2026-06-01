// ResolveTemplateResponse.cs
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
/// Wire response for the <c>resolveTemplate</c> tool: the applied <c>ControlTemplate</c>'s type, its
/// key and source (best-effort), the runtime template tree under the control, and the named parts.
/// </summary>
/// <param name="TemplateType">The template's CLR type (full name when available), or <c>null</c> when none is applied.</param>
/// <param name="TemplateKey">The template's target-type name when determinable; recovering <c>x:Key</c> is Phase 2.</param>
/// <param name="TemplateSource">Always <c>null</c> in v1; reliably surfacing this requires resource-tree walking.</param>
/// <param name="TemplateTree">The control's visual children after template expansion, or <c>null</c> when none.</param>
/// <param name="NamedParts">The named parts reachable in the expanded template.</param>
public sealed record ResolveTemplateResponse(
    string? TemplateType,
    string? TemplateKey,
    string? TemplateSource,
    TemplateNodeDto? TemplateTree,
    IReadOnlyList<NamedPartDto> NamedParts);
