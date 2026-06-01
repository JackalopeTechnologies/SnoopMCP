// PopupOwnerResolver.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Inspection;

using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

/// <summary>
/// Walks a set of candidate windows looking for the <see cref="Popup"/> whose <see cref="Popup.Child"/>
/// is hosted inside the supplied <c>PopupRoot</c> visual.
/// </summary>
public sealed class PopupOwnerResolver
{
    /// <summary>
    /// Returns the <see cref="Popup"/> that opened <paramref name="popupRoot"/>, or <c>null</c>
    /// when no candidate matches.
    /// </summary>
    /// <param name="popupRoot">The popup root visual to find an owner for.</param>
    /// <param name="candidateWindows">Window roots whose visual subtrees should be searched.</param>
    /// <returns>The owning <see cref="Popup"/>, or <c>null</c>.</returns>
    // CA1822 suppression: scaffolded for Phase 4 DI; will gain instance state when popup-owner
    // heuristics evolve (alternative resolvers, caches).
#pragma warning disable CA1822
    public Popup? ResolveOwner(Visual popupRoot, IEnumerable<Window> candidateWindows)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(popupRoot);
        ArgumentNullException.ThrowIfNull(candidateWindows);

        Popup? owner = null;
        IEnumerator<Window> windowIterator = candidateWindows.GetEnumerator();
        while (owner is null && windowIterator.MoveNext())
        {
            owner = FindOwnerInWindow(windowIterator.Current, popupRoot);
        }
        windowIterator.Dispose();
        return owner;
    }

    private static Popup? FindOwnerInWindow(Window window, Visual popupRoot)
    {
        var candidates = new List<Popup>();
        CollectPopups(window, candidates);

        Popup? match = null;
        IEnumerator<Popup> openPopups = candidates
            .Where(p => p is { IsOpen: true, Child: not null })
            .GetEnumerator();
        while (match is null && openPopups.MoveNext())
        {
            Popup popup = openPopups.Current;
            UIElement? popupChild = popup.Child;
            if (popupChild is not null)
            {
                Visual? childRoot = ResolveChildVisualRoot(popupChild);
                bool isMatch = ReferenceEquals(childRoot, popupRoot);
                if (isMatch)
                {
                    match = popup;
                }
            }
        }
        openPopups.Dispose();
        return match;
    }

    private static void CollectPopups(DependencyObject node, List<Popup> sink)
    {
        if (node is Popup p)
        {
            sink.Add(p);
        }
        bool isVisual = node is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                CollectPopups(VisualTreeHelper.GetChild(node, i), sink);
            }
        }
    }

    private static Visual? ResolveChildVisualRoot(UIElement child)
    {
        var source = PresentationSource.FromVisual(child);
        Visual? root = source?.RootVisual;
        return root;
    }
}
