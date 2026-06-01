// GetTemplatedParentRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>getTemplatedParent</c> tool.
/// </summary>
/// <param name="Id">Element id; must be inside a template for a non-null response.</param>
public sealed record GetTemplatedParentRequest(int Id);
