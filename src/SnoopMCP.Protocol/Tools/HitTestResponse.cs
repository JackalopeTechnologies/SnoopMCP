// HitTestResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>hitTest</c> tool.
/// </summary>
/// <param name="Element">
/// Snapshot of the deepest hittable visual at the requested point, or <c>null</c> when nothing is hit.
/// </param>
public sealed record HitTestResponse(DescribeElementResponse? Element);
