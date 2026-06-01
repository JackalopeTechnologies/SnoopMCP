// GetParentRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>getParent</c> tool.
/// </summary>
/// <param name="Id">Child element id.</param>
/// <param name="Tree">Tree to climb: <c>visual</c> or <c>logical</c>.</param>
public sealed record GetParentRequest(int Id, string Tree);
