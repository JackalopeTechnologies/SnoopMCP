// VirtualizationDto.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Realisation metadata for an <c>ItemsControl</c>: distinguishes a control that has realised
/// every item from one whose containers are virtualised on demand.
/// </summary>
/// <param name="IsVirtualizing">True when the items control is using a <c>VirtualizingPanel</c>.</param>
/// <param name="RealizedItems">Number of materialised visual children at the time of the call.</param>
/// <param name="TotalItems">Logical item count, or <c>null</c> when the items collection is unbounded.</param>
public sealed record VirtualizationDto(
    bool IsVirtualizing,
    int RealizedItems,
    int? TotalItems);
