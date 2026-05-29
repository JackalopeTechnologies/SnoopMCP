// GetChildrenRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>getChildren</c> tool.
/// </summary>
/// <param name="Id">Parent element id.</param>
/// <param name="Tree">Tree to walk: <c>visual</c> or <c>logical</c>.</param>
public sealed record GetChildrenRequest(int Id, string Tree);
