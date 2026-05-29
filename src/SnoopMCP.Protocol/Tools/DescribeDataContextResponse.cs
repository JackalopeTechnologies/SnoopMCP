// DescribeDataContextResponse.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>describeDataContext</c> tool.
/// </summary>
/// <param name="DataContext">The CLR type shape of the element's DataContext, or <c>null</c> when none is present.</param>
public sealed record DescribeDataContextResponse(DataContextInfo? DataContext);
