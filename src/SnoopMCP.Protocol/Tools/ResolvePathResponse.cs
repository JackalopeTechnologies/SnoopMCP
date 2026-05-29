// ResolvePathResponse.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>resolvePath</c> tool.
/// </summary>
/// <param name="Element">Snapshot of the resolved element, or <c>null</c> when no element matches.</param>
public sealed record ResolvePathResponse(DescribeElementResponse? Element);
