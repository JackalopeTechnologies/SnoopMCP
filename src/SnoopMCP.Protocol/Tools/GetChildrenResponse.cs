// GetChildrenResponse.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>getChildren</c> tool.
/// </summary>
/// <param name="Children">The realised children, in tree order.</param>
/// <param name="Virtualization">Realisation metadata when the parent is an <c>ItemsControl</c>; otherwise <c>null</c>.</param>
public sealed record GetChildrenResponse(
    IReadOnlyList<DescribeElementResponse> Children,
    VirtualizationDto? Virtualization);
