// FindElementsResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>findElements</c> tool.
/// </summary>
/// <param name="Matches">Matching elements, in pre-order visual-tree traversal order.</param>
public sealed record FindElementsResponse(IReadOnlyList<DescribeElementResponse> Matches);
