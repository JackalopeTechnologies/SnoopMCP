// ListDependencyPropertiesResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>listDependencyProperties</c> tool.
/// </summary>
/// <param name="Properties">Every dependency property reachable on the element.</param>
public sealed record ListDependencyPropertiesResponse(
    IReadOnlyList<DependencyPropertyDto> Properties);
