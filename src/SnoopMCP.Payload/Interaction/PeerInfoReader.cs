// PeerInfoReader.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Interaction;

using System.Windows;
using System.Windows.Automation.Peers;
using Protocol.Errors;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Reads an element's automation identity via its <see cref="AutomationPeer"/>, so callers can
/// cross between Snoop element ids and the out-of-process UIA locator space. Runs on the UI thread.
/// </summary>
public sealed class PeerInfoReader
{
    /// <summary>Projects the element's automation peer identity.</summary>
    /// <param name="element">The element whose automation peer identity is read.</param>
    /// <returns>The peer's AutomationId, Name, ClassName, and ControlType.</returns>
    /// <remarks>
    /// CA1822 disabled: instance method by design so callers (e.g. <c>GetAutomationPeerInfoToolHandler</c>)
    /// hold and inject a <see cref="PeerInfoReader"/> like the other driving-layer collaborators,
    /// consistent with <c>AutomationPeerDriver</c> and <c>DependencyPropertyInspector</c>. No instance
    /// state today; may gain some (e.g. shared peer caching) in a follow-up phase without an
    /// API-shape change.
    /// </remarks>
#pragma warning disable CA1822
    public GetAutomationPeerInfoResponse Read(DependencyObject element)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(element);

        AutomationPeer peer = element switch
        {
            UIElement ui => UIElementAutomationPeer.CreatePeerForElement(ui),
            ContentElement ce => ContentElementAutomationPeer.CreatePeerForElement(ce),
            _ => throw new SnoopMcpException(ErrorCode.NotDrivable, "Element has no AutomationPeer.")
        } ?? throw new SnoopMcpException(ErrorCode.NotDrivable, "Element has no AutomationPeer.");

        return new GetAutomationPeerInfoResponse(
            AutomationId: peer.GetAutomationId() ?? string.Empty,
            Name: peer.GetName() ?? string.Empty,
            ClassName: peer.GetClassName() ?? string.Empty,
            ControlType: peer.GetAutomationControlType().ToString());
    }
}
