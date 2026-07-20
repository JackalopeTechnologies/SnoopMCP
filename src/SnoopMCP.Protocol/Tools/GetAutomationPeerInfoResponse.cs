// GetAutomationPeerInfoResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>getAutomationPeerInfo</c> tool. Forward-only: intentionally omits
/// <c>RuntimeId</c> because <c>AutomationPeer.GetRuntimeId()</c> is <c>protected internal</c> and
/// unreachable from a reader.
/// </summary>
/// <param name="AutomationId">The peer's <c>AutomationId</c>, or empty when unset.</param>
/// <param name="Name">The peer's accessible <c>Name</c>.</param>
/// <param name="ClassName">The peer's <c>ClassName</c> as reported by UIA.</param>
/// <param name="ControlType">The peer's <c>ControlType</c> localized/programmatic name.</param>
public sealed record GetAutomationPeerInfoResponse(string AutomationId, string Name, string ClassName, string ControlType);
