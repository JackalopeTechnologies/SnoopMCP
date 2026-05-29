// HitTestRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>hitTest</c> tool.
/// </summary>
/// <param name="RootId">Element id of the root whose coordinate space the point is expressed in.</param>
/// <param name="X">Root-relative X coordinate in DIPs.</param>
/// <param name="Y">Root-relative Y coordinate in DIPs.</param>
public sealed record HitTestRequest(int RootId, double X, double Y);
