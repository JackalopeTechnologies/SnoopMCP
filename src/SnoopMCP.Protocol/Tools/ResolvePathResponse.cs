// ResolvePathResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>resolvePath</c> tool.
/// </summary>
/// <param name="Element">Snapshot of the resolved element, or <c>null</c> when no element matches.</param>
public sealed record ResolvePathResponse(DescribeElementResponse? Element);
