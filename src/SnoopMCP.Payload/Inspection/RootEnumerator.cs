// RootEnumerator.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Inspection;

using System.Collections;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Snapshots <see cref="PresentationSource.CurrentSources"/> and classifies each root visual
/// as <c>Window</c>, <c>Popup</c>, or <c>Other</c>. Popup roots gain an <c>openedBy</c> back-reference
/// to the <see cref="Popup"/> element that opened them.
/// </summary>
public sealed class RootEnumerator
{
    private const string KindWindow = "Window";
    private const string KindPopup = "Popup";
    private const string KindOther = "Other";
    private const string PopupRootTypeName = "PopupRoot";

    private readonly ElementRegistry mRegistry;
    private readonly PopupOwnerResolver mOwnerResolver;

    /// <summary>
    /// Initialises a new <see cref="RootEnumerator"/>.
    /// </summary>
    /// <param name="registry">Element registry used to assign stable ids to roots and owners.</param>
    /// <param name="ownerResolver">Helper that resolves the popup owner for a popup root.</param>
    public RootEnumerator(ElementRegistry registry, PopupOwnerResolver ownerResolver)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(ownerResolver);
        mRegistry = registry;
        mOwnerResolver = ownerResolver;
    }

    /// <summary>
    /// Returns one <see cref="VisualRootDto"/> per active <see cref="PresentationSource"/> in the
    /// target process.
    /// </summary>
    /// <returns>The populated response.</returns>
    public ListVisualRootsResponse Enumerate()
    {
        IEnumerable sources = PresentationSource.CurrentSources;
        var roots = new List<VisualRootDto>();
        List<Window> windowRoots = CollectWindowRoots(sources);

        int nextRootId = 0;
        foreach (PresentationSource source in sources.OfType<PresentationSource>())
        {
            Visual? rootVisual = source.RootVisual;
            if (rootVisual is not null)
            {
                VisualRootDto dto = BuildRoot(source, rootVisual, nextRootId, windowRoots);
                roots.Add(dto);
                nextRootId++;
            }
        }

        return new ListVisualRootsResponse(roots);
    }

    private static List<Window> CollectWindowRoots(IEnumerable sources)
    {
        var windowRoots = new List<Window>();
        foreach (PresentationSource source in sources.OfType<PresentationSource>())
        {
            if (source.RootVisual is Window window)
            {
                windowRoots.Add(window);
            }
        }
        return windowRoots;
    }

    private VisualRootDto BuildRoot(
        PresentationSource source,
        Visual rootVisual,
        int rootId,
        List<Window> windowRoots)
    {
        string kind = ClassifyKind(rootVisual);
        string? title = (rootVisual as Window)?.Title;
        long hwnd = (source is HwndSource hs) ? hs.Handle.ToInt64() : 0;
        int rootElementId = mRegistry.GetOrAssign(rootVisual);
        int? openedBy = ResolveOpenedBy(kind, rootVisual, windowRoots);

        return new VisualRootDto(
            RootId: rootId,
            Kind: kind,
            Hwnd: hwnd,
            Title: title,
            RootElementId: rootElementId,
            OpenedBy: openedBy);
    }

    private static string ClassifyKind(Visual rootVisual)
    {
        string kind = rootVisual switch
        {
            Window => KindWindow,
            _ when rootVisual.GetType().Name == PopupRootTypeName => KindPopup,
            _ => KindOther
        };
        return kind;
    }

    private int? ResolveOpenedBy(string kind, Visual rootVisual, List<Window> windowRoots)
    {
        int? openedBy = null;
        bool isPopup = kind == KindPopup;
        if (isPopup)
        {
            Popup? owner = mOwnerResolver.ResolveOwner(rootVisual, windowRoots);
            if (owner is not null)
            {
                openedBy = mRegistry.GetOrAssign(owner);
            }
        }
        return openedBy;
    }
}
