// FindElementsResponse.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>findElements</c> tool.
/// </summary>
/// <param name="Matches">Matching elements, in pre-order visual-tree traversal order.</param>
public sealed record FindElementsResponse(IReadOnlyList<DescribeElementResponse> Matches);
