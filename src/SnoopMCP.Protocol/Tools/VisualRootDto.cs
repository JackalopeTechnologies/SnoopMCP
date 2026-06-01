// VisualRootDto.cs
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
///     A single active visual root in the target process.
/// </summary>
/// <param name="RootId">Sequential id within this enumeration call.</param>
/// <param name="Kind">One of <c>Window</c>, <c>Popup</c>, or <c>Other</c>.</param>
/// <param name="Hwnd">The owning <c>HwndSource</c>'s window handle, or 0 when not an <c>HwndSource</c>.</param>
/// <param name="Title">The window title when the root is a <c>Window</c>; otherwise <c>null</c>.</param>
/// <param name="RootElementId">Stable element id of the root visual.</param>
/// <param name="OpenedBy">For a popup, the element id of the <c>Popup</c> that opened it; <c>null</c> otherwise.</param>
public sealed record VisualRootDto(
    int RootId,
    string Kind,
    long Hwnd,
    string? Title,
    int RootElementId,
    int? OpenedBy);
