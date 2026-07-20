// PeerInvokeRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>peerInvoke</c> tool.
/// </summary>
/// <param name="Id">Element id whose AutomationPeer is driven.</param>
/// <param name="Pattern">The peer pattern to invoke: Invoke | Toggle | SelectionItem | ExpandCollapse.</param>
/// <param name="Dispatch">
/// Dispatch mode: <c>null</c> or <c>"wait"</c> (default) waits for the action to complete before
/// returning; <c>"post"</c> fires-and-forgets and returns <c>Dispatched=true</c> immediately —
/// verify the effect separately (e.g. via <c>waitForValue</c>). In <c>"post"</c> mode the action's
/// outcome is not surfaced to the caller: a <c>CommandNotExecutable</c> (CanExecute==false) or any
/// exception thrown by the posted work runs unobserved on the target's dispatcher and is not reported.
/// </param>
public sealed record PeerInvokeRequest(int Id, string Pattern, string? Dispatch);
