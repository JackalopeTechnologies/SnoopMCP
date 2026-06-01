// DescribeDataContextRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>describeDataContext</c> tool.
/// </summary>
/// <param name="Id">Element id whose DataContext is inspected.</param>
public sealed record DescribeDataContextRequest(int Id);
