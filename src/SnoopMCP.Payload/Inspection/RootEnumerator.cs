// RootEnumerator.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#region Usings

using System.Collections;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using SnoopMCP.Protocol.Tools;

#endregion

namespace SnoopMCP.Payload.Inspection;

/// <summary>
///     Snapshots <see cref="PresentationSource.CurrentSources" /> and classifies each root visual
///     as <c>Window</c>, <c>Popup</c>, or <c>Other</c>. Popup roots gain an <c>openedBy</c> back-reference
///     to the <see cref="Popup" /> element that opened them.
/// </summary>
public sealed class RootEnumerator
{
    /// <summary>
    ///     Initialises a new <see cref="RootEnumerator" />.
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

    private readonly PopupOwnerResolver mOwnerResolver;

    private readonly ElementRegistry mRegistry;

    /// <summary>
    ///     Returns one <see cref="VisualRootDto" /> per active <see cref="PresentationSource" /> in the
    ///     target process.
    /// </summary>
    /// <returns>The populated response.</returns>
    public ListVisualRootsResponse Enumerate()
    {
        IEnumerable sources = PresentationSource.CurrentSources;
        var roots = new List<VisualRootDto>();
        List<Window> windowRoots = CollectWindowRoots(sources);

        var nextRootId = 0;
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
            if (source.RootVisual is Window window)
                windowRoots.Add(window);

        return windowRoots;
    }

    private VisualRootDto BuildRoot(
        PresentationSource source,
        Visual rootVisual,
        int rootId,
        List<Window> windowRoots)
    {
        var kind = ClassifyKind(rootVisual);
        var title = (rootVisual as Window)?.Title;
        var hwnd = source is HwndSource hs ? hs.Handle.ToInt64() : 0;
        var rootElementId = mRegistry.GetOrAssign(rootVisual);
        var openedBy = ResolveOpenedBy(kind, rootVisual, windowRoots);

        return new VisualRootDto(
            rootId,
            kind,
            hwnd,
            title,
            rootElementId,
            openedBy);
    }

    private static string ClassifyKind(Visual rootVisual)
    {
        var kind = rootVisual switch
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
        var isPopup = kind == KindPopup;
        if (isPopup)
        {
            Popup? owner = mOwnerResolver.ResolveOwner(rootVisual, windowRoots);
            if (owner is not null) openedBy = mRegistry.GetOrAssign(owner);
        }

        return openedBy;
    }

    private const string KindWindow = "Window";
    private const string KindPopup = "Popup";
    private const string KindOther = "Other";
    private const string PopupRootTypeName = "PopupRoot";
}
