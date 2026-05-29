// ListDependencyPropertiesResponse.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>listDependencyProperties</c> tool.
/// </summary>
/// <param name="Properties">Every dependency property reachable on the element.</param>
public sealed record ListDependencyPropertiesResponse(
    IReadOnlyList<DependencyPropertyDto> Properties);
