// GetAutomationPeerInfoRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>getAutomationPeerInfo</c> tool.
/// </summary>
/// <param name="Id">Element id whose UIA identity is read via its AutomationPeer.</param>
public sealed record GetAutomationPeerInfoRequest(int Id);
