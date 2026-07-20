// PeerPatternNames.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Interaction;

/// <summary>
/// The agent-facing names for the <see cref="System.Windows.Automation.Peers.AutomationPeer"/>
/// patterns SnoopMCP understands. Shared so the set <c>peerInvoke</c> accepts and the set
/// <c>getAutomationPeerInfo</c> reports cannot drift apart — a caller choosing a pattern from the
/// latter must be able to pass it to the former (issue #77). The spellings also match the UIA tier's
/// pattern names, so the two address spaces can be correlated.
/// </summary>
public static class PeerPatternNames
{
    /// <summary>Click-equivalent: <c>IInvokeProvider</c>.</summary>
    public const string Invoke = "Invoke";

    /// <summary>Check/uncheck: <c>IToggleProvider</c>.</summary>
    public const string Toggle = "Toggle";

    /// <summary>Select within a container: <c>ISelectionItemProvider</c>.</summary>
    public const string SelectionItem = "SelectionItem";

    /// <summary>Expand or collapse: <c>IExpandCollapseProvider</c>.</summary>
    public const string ExpandCollapse = "ExpandCollapse";

    /// <summary>
    /// Read/write a value: <c>IValueProvider</c>. Reported by <c>getAutomationPeerInfo</c> for
    /// correlation with the UIA tier, but NOT drivable by <c>peerInvoke</c>, which performs actions
    /// only — set a value through the UIA tier's <c>setUiaValue</c>.
    /// </summary>
    public const string Value = "Value";
}
