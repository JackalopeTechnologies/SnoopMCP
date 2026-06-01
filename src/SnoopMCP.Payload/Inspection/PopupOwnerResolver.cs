// PopupOwnerResolver.cs
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

using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Media3D;

#endregion

namespace SnoopMCP.Payload.Inspection;

/// <summary>
///     Walks a set of candidate windows looking for the <see cref="Popup" /> whose <see cref="Popup.Child" />
///     is hosted inside the supplied <c>PopupRoot</c> visual.
/// </summary>
public sealed class PopupOwnerResolver
{
    /// <summary>
    ///     Returns the <see cref="Popup" /> that opened <paramref name="popupRoot" />, or <c>null</c>
    ///     when no candidate matches.
    /// </summary>
    /// <param name="popupRoot">The popup root visual to find an owner for.</param>
    /// <param name="candidateWindows">Window roots whose visual subtrees should be searched.</param>
    /// <returns>The owning <see cref="Popup" />, or <c>null</c>.</returns>
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
        while (owner is null && windowIterator.MoveNext()) owner = FindOwnerInWindow(windowIterator.Current, popupRoot);
        windowIterator.Dispose();
        return owner;
    }

    private static Popup? FindOwnerInWindow(Window window, Visual popupRoot)
    {
        var candidates = new List<Popup>();
        CollectPopups(window, candidates);

        Popup? match = null;
        IEnumerator<Popup> openPopups = candidates
            .Where(p => p.IsOpen && p.Child is not null)
            .GetEnumerator();
        while (match is null && openPopups.MoveNext())
        {
            Popup popup = openPopups.Current;
            UIElement? popupChild = popup.Child;
            if (popupChild is not null)
            {
                Visual? childRoot = ResolveChildVisualRoot(popupChild);
                var isMatch = ReferenceEquals(childRoot, popupRoot);
                if (isMatch) match = popup;
            }
        }

        openPopups.Dispose();
        return match;
    }

    private static void CollectPopups(DependencyObject node, List<Popup> sink)
    {
        if (node is Popup p) sink.Add(p);
        var isVisual = node is Visual or Visual3D;
        if (isVisual)
        {
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++) CollectPopups(VisualTreeHelper.GetChild(node, i), sink);
        }
    }

    private static Visual? ResolveChildVisualRoot(UIElement child)
    {
        var source = PresentationSource.FromVisual(child);
        Visual? root = source?.RootVisual;
        return root;
    }
}
