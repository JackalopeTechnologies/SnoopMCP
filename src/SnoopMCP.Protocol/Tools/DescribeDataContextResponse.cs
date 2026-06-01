// DescribeDataContextResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>describeDataContext</c> tool.
/// </summary>
/// <param name="DataContext">The CLR type shape of the element's DataContext, or <c>null</c> when none is present.</param>
public sealed record DescribeDataContextResponse(DataContextInfo? DataContext);
