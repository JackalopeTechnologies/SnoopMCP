// ListVisualRootsResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>listVisualRoots</c> tool — a flat list of every
/// active visual root in the target process.
/// </summary>
/// <param name="Roots">The roots, in <c>PresentationSource.CurrentSources</c> order.</param>
public sealed record ListVisualRootsResponse(IReadOnlyList<VisualRootDto> Roots);
