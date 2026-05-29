// GetParentRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>getParent</c> tool.
/// </summary>
/// <param name="Id">Child element id.</param>
/// <param name="Tree">Tree to climb: <c>visual</c> or <c>logical</c>.</param>
public sealed record GetParentRequest(int Id, string Tree);
