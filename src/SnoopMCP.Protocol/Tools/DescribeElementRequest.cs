// DescribeElementRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>describeElement</c> tool.
/// </summary>
/// <param name="Id">The element id, previously handed out by an inspection tool.</param>
public sealed record DescribeElementRequest(int Id);
