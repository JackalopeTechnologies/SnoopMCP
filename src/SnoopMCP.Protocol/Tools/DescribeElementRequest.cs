// DescribeElementRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>describeElement</c> tool.
/// </summary>
/// <param name="Id">The element id, previously handed out by an inspection tool.</param>
public sealed record DescribeElementRequest(int Id);
