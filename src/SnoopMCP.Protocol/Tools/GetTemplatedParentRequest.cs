// GetTemplatedParentRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>getTemplatedParent</c> tool.
/// </summary>
/// <param name="Id">Element id; must be inside a template for a non-null response.</param>
public sealed record GetTemplatedParentRequest(int Id);
