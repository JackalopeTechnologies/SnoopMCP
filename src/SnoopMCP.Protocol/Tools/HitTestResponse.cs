// HitTestResponse.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>hitTest</c> tool.
/// </summary>
/// <param name="Element">
/// Snapshot of the deepest hittable visual at the requested point, or <c>null</c> when nothing is hit.
/// </param>
public sealed record HitTestResponse(DescribeElementResponse? Element);
