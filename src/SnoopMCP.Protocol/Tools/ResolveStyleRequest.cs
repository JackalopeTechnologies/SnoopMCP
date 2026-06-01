// ResolveStyleRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>resolveStyle</c> tool.
/// </summary>
/// <param name="Id">Element id whose applied WPF <c>Style</c> is resolved.</param>
public sealed record ResolveStyleRequest(int Id);
