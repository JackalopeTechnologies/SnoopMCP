// PeerInvokeResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>peerInvoke</c> tool.
/// </summary>
/// <param name="Ok">
/// Whether the invocation succeeded. <c>true</c> in <c>"wait"</c> mode once the action completed;
/// <c>null</c> (omitted on the wire) in <c>"post"</c> mode, where the outcome is not observed.
/// </param>
/// <param name="Dispatched">
/// <c>true</c> when the request used <c>"post"</c> dispatch and was fired-and-forgotten rather
/// than awaited; <c>false</c> when the request was awaited to completion (<c>"wait"</c> mode).
/// </param>
public sealed record PeerInvokeResponse(bool? Ok, bool Dispatched);
