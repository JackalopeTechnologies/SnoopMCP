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
    public void Invoke(DependencyObject element, string pattern)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        Prepare(element, pattern)();
    }

    /// <summary>
    /// Validates that <paramref name="element"/> can be driven through <paramref name="pattern"/> and
    /// returns the action that drives it, without running it. Every failure a client can act on is
    /// raised here — no AutomationPeer (<see cref="ErrorCode.NotDrivable"/>), a disabled element
    /// (<see cref="ErrorCode.ElementNotEnabled"/>), an unsupported or unknown pattern — so a
    /// fire-and-forget caller can validate on the UI thread first and post only the action itself.
    /// Runs on the UI thread.
    /// </summary>
    /// <param name="element">The element to drive.</param>
    /// <param name="pattern">The peer pattern to invoke: Invoke | Toggle | SelectionItem | ExpandCollapse.</param>
    /// <returns>The action that drives the pattern; run it on the UI thread.</returns>
    /// <remarks>
    /// CA1822 disabled: instance method by design so callers (e.g. <c>PeerInvokeToolHandler</c>) hold
    /// and inject an <see cref="AutomationPeerDriver"/> like the other driving-layer collaborators,
    /// consistent with <c>DependencyPropertyInspector</c>. No instance state today; may gain some
    /// (e.g. shared peer caching) in a follow-up phase without an API-shape change.
    /// </remarks>
#pragma warning disable CA1822
    public Action Prepare(DependencyObject element, string pattern)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        AutomationPeer? peer = CreatePeer(element);
        if (peer is null)
        {
            throw new SnoopMcpException(ErrorCode.NotDrivable, "Element has no AutomationPeer.");
        }

        // Every WPF pattern provider (Button, ToggleButton, ListBoxItem, Expander, ...) throws its own
        // ElementNotEnabledException when the peer reports IsEnabled() false. Refuse first with a
        // structured code so the client learns the element is merely disabled — a state it can wait
        // for or change — rather than receiving an opaque WPF exception (Raygun group 290284826201).
        if (!peer.IsEnabled())
        {
            throw new SnoopMcpException(
                ErrorCode.ElementNotEnabled,
                "Element is disabled (IsEnabled is false); enable it before driving it.");
        }

        Action fire = NormalizePattern(pattern) switch
        {
            PatternInvoke => Get<IInvokeProvider>(peer, PatternInterface.Invoke).Invoke,
            PatternToggle => Get<IToggleProvider>(peer, PatternInterface.Toggle).Toggle,
            PatternSelectionItem => Get<ISelectionItemProvider>(peer, PatternInterface.SelectionItem).Select,
            PatternExpandCollapse => Get<IExpandCollapseProvider>(peer, PatternInterface.ExpandCollapse).Expand,
            _ => throw new SnoopMcpException(ErrorCode.InvalidArgument, $"Unknown peer pattern '{pattern}'.")
        };
        return fire;
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
