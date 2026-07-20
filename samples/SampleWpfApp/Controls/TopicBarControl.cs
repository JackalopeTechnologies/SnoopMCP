// TopicBarControl.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SampleWpfApp.Controls;

using System.Windows.Automation.Peers;
using System.Windows.Controls;

/// <summary>
/// Reproduces issue #80 ("topic-bar items are invisible to UIA name search"): a hand-rolled
/// navigation strip whose <see cref="AutomationPeer"/> never implemented child-peer support, so
/// its real, name-bearing item elements are structurally pruned from every UI Automation tree
/// walk — <c>find_uia_element</c> included — even though they are genuine <c>UIElement</c>s with
/// their own peers when reached directly. UI Automation's tree walk descends only through the
/// peers a parent peer reports as children, so a parent that reports none prunes everything below
/// it regardless of search scope or tree "view" (raw/control/content) — this is unrelated to and
/// not fixed by choosing a different view. The payload tier's visual-tree walk
/// (<see cref="SnoopMCP.Payload.Inspection.ElementFinder"/>) never consults automation peers at
/// all, so it still finds these items — the documented workaround.
/// </summary>
public sealed class TopicBarControl : ItemsControl
{
    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TopicBarAutomationPeer(this);

    /// <summary>An <see cref="AutomationPeer"/> that never reports its item peers as children.</summary>
    private sealed class TopicBarAutomationPeer : FrameworkElementAutomationPeer
    {
        public TopicBarAutomationPeer(TopicBarControl owner) : base(owner)
        {
        }

        /// <summary>
        /// Always empty: mimics a custom control whose author never implemented child-peer
        /// support, pruning every descendant from a UIA tree walk no matter how it is scoped.
        /// </summary>
        protected override List<AutomationPeer>? GetChildrenCore() => null;

        /// <inheritdoc />
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        /// <inheritdoc />
        protected override string GetClassNameCore() => nameof(TopicBarControl);
    }
}
