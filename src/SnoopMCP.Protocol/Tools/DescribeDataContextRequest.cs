// DescribeDataContextRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>describeDataContext</c> tool.
/// </summary>
/// <param name="Id">Element id whose DataContext is inspected.</param>
public sealed record DescribeDataContextRequest(int Id);
