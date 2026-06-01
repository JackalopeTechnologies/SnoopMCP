// GetTemplatedParentResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>getTemplatedParent</c> tool.
/// </summary>
/// <param name="TemplatedParent">
/// The templated parent snapshot when the element was generated from a control template;
/// otherwise <c>null</c>.
/// </param>
public sealed record GetTemplatedParentResponse(DescribeElementResponse? TemplatedParent);
