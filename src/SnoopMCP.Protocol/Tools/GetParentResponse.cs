// GetParentResponse.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>getParent</c> tool.
/// </summary>
/// <param name="Parent">The parent snapshot, or <c>null</c> when the element is a root.</param>
public sealed record GetParentResponse(DescribeElementResponse? Parent);
