// VisualRootDto.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// A single active visual root in the target process.
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
