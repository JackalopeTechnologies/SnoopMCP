// AutomationPeerDriver.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Interaction;

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Protocol.Errors;

/// <summary>
/// Drives a WPF element's real <see cref="AutomationPeer"/> in-process — the fallback for actions
/// out-of-process UIA2 cannot perform (it has no DoDefaultAction). All calls must run on the UI thread.
/// </summary>
public sealed class AutomationPeerDriver
{
    // Shared with PeerInfoReader so the set peerInvoke accepts and the set getAutomationPeerInfo
    // reports cannot drift apart (issue #77).
    private const string PatternInvoke = PeerPatternNames.Invoke;
    private const string PatternToggle = PeerPatternNames.Toggle;
    private const string PatternSelectionItem = PeerPatternNames.SelectionItem;
    private const string PatternExpandCollapse = PeerPatternNames.ExpandCollapse;

    /// <summary>Invokes the named pattern on the element's automation peer. Runs on the UI thread.</summary>
    /// <param name="element">The element to drive.</param>
    /// <param name="pattern">The peer pattern to invoke: Invoke | Toggle | SelectionItem | ExpandCollapse.</param>
    /// <remarks>
    /// CA1822 disabled: instance method by design so callers (e.g. <c>PeerInvokeToolHandler</c>) hold
    /// and inject an <see cref="AutomationPeerDriver"/> like the other driving-layer collaborators,
    /// consistent with <c>DependencyPropertyInspector</c>. No instance state today; may gain some
    /// (e.g. shared peer caching) in a follow-up phase without an API-shape change.
    /// </remarks>
#pragma warning disable CA1822
    public void Invoke(DependencyObject element, string pattern)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        AutomationPeer? peer = CreatePeer(element);
        if (peer is null)
        {
            throw new SnoopMcpException(ErrorCode.NotDrivable, "Element has no AutomationPeer.");
        }

        switch (NormalizePattern(pattern))
        {
            case PatternInvoke:
                Get<IInvokeProvider>(peer, PatternInterface.Invoke).Invoke();
                break;
            case PatternToggle:
                Get<IToggleProvider>(peer, PatternInterface.Toggle).Toggle();
                break;
            case PatternSelectionItem:
                Get<ISelectionItemProvider>(peer, PatternInterface.SelectionItem).Select();
                break;
            case PatternExpandCollapse:
                Get<IExpandCollapseProvider>(peer, PatternInterface.ExpandCollapse).Expand();
                break;
            default:
                throw new SnoopMcpException(ErrorCode.InvalidArgument, $"Unknown peer pattern '{pattern}'.");
        }
    }

    /// <summary>
    /// Maps <paramref name="pattern"/> to its canonical-case constant via a case-insensitive match, so
    /// callers may pass e.g. <c>"invoke"</c> as readily as <c>"Invoke"</c> — matching the case-insensitive
    /// comparison <c>UiaDriver</c> (Phase A, out-of-process UIA2 tier) already uses for the same four
    /// pattern names. Returns the original string unchanged when it matches none of the known patterns,
    /// so the caller's <c>default</c> case still reports the caller's original (un-normalized) text.
    /// </summary>
    private static string NormalizePattern(string pattern)
    {
        return pattern switch
        {
            _ when string.Equals(pattern, PatternInvoke, StringComparison.OrdinalIgnoreCase) => PatternInvoke,
            _ when string.Equals(pattern, PatternToggle, StringComparison.OrdinalIgnoreCase) => PatternToggle,
            _ when string.Equals(pattern, PatternSelectionItem, StringComparison.OrdinalIgnoreCase) =>
                PatternSelectionItem,
            _ when string.Equals(pattern, PatternExpandCollapse, StringComparison.OrdinalIgnoreCase) =>
                PatternExpandCollapse,
            _ => pattern
        };
    }

    private static AutomationPeer? CreatePeer(DependencyObject element)
    {
        return element switch
        {
            UIElement ui => UIElementAutomationPeer.CreatePeerForElement(ui),
            ContentElement ce => ContentElementAutomationPeer.CreatePeerForElement(ce),
            _ => null
        };
    }

    private static T Get<T>(AutomationPeer peer, PatternInterface pattern) where T : class
    {
        return peer.GetPattern(pattern) as T
            ?? throw new SnoopMcpException(ErrorCode.NotDrivable, $"Element does not support the {pattern} pattern.");
    }
}
