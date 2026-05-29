// ListBindingsRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>listBindings</c> tool.
/// </summary>
/// <param name="Id">Element id at which the binding audit begins.</param>
/// <param name="IncludeDescendants">When <c>true</c>, recurses through the visual subtree.</param>
public sealed record ListBindingsRequest(int Id, bool IncludeDescendants);
