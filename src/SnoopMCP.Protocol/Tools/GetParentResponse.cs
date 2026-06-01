// GetParentResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>getParent</c> tool.
/// </summary>
/// <param name="Parent">The parent snapshot, or <c>null</c> when the element is a root.</param>
public sealed record GetParentResponse(DescribeElementResponse? Parent);
