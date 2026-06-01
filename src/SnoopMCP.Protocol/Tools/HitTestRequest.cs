// HitTestRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>hitTest</c> tool.
/// </summary>
/// <param name="RootId">Element id of the root whose coordinate space the point is expressed in.</param>
/// <param name="X">Root-relative X coordinate in DIPs.</param>
/// <param name="Y">Root-relative Y coordinate in DIPs.</param>
public sealed record HitTestRequest(int RootId, double X, double Y);
