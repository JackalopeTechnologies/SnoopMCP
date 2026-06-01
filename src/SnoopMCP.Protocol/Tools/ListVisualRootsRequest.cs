// ListVisualRootsRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>listVisualRoots</c> tool. Carries no fields; the response
/// is a snapshot of every active <c>PresentationSource</c> in the target process.
/// </summary>
public sealed record ListVisualRootsRequest();
