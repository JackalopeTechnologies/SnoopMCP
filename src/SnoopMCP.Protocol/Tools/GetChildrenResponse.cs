// GetChildrenResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>getChildren</c> tool.
/// </summary>
/// <param name="Children">The realised children, in tree order.</param>
/// <param name="Virtualization">Realisation metadata when the parent is an <c>ItemsControl</c>; otherwise <c>null</c>.</param>
public sealed record GetChildrenResponse(
    IReadOnlyList<DescribeElementResponse> Children,
    VirtualizationDto? Virtualization);
