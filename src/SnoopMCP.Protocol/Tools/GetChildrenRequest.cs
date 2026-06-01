// GetChildrenRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>getChildren</c> tool.
/// </summary>
/// <param name="Id">Parent element id.</param>
/// <param name="Tree">Tree to walk: <c>visual</c> or <c>logical</c>.</param>
public sealed record GetChildrenRequest(int Id, string Tree);
