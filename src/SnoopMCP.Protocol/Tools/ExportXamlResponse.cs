// ExportXamlResponse.cs
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
/// Wire response for the <c>exportXaml</c> tool: a XAML snapshot of an element's live state.
/// Bindings appear as their evaluated values, not as markup; for the live binding spec use
/// <c>listBindings</c> or <c>inspectBinding</c>.
/// </summary>
/// <param name="Xaml">The serialized XAML (possibly truncated to the soft cap).</param>
/// <param name="ByteCount">The full UTF-8 byte count of the serialized XAML before truncation.</param>
/// <param name="Truncated">Whether the returned <paramref name="Xaml"/> was truncated at the soft cap.</param>
/// <param name="Warning">A human-readable truncation warning, or <c>null</c> when not truncated.</param>
public sealed record ExportXamlResponse(
    string Xaml,
    int ByteCount,
    bool Truncated,
    string? Warning);
