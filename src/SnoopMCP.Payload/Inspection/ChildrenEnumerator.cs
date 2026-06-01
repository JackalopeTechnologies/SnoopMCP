// ChildrenEnumerator.cs
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
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using SnoopMCP.Protocol.Tools;

#endregion

namespace SnoopMCP.Payload.Inspection;

/// <summary>
///     Walks the visual or logical children of a parent element, optionally reporting
///     virtualisation metadata when the parent is an <see cref="ItemsControl" />.
/// </summary>
public sealed class ChildrenEnumerator
{
    /// <summary>
    ///     Initialises a new <see cref="ChildrenEnumerator" />.
    /// </summary>
    /// <param name="describer">The describer used to build per-child snapshots.</param>
    public ChildrenEnumerator(ElementDescriber describer)
    {
        ArgumentNullException.ThrowIfNull(describer);
        mDescriber = describer;
    }

    private readonly ElementDescriber mDescriber;

    /// <summary>
    ///     Enumerates <paramref name="parent" />'s children in the specified tree.
    /// </summary>
    /// <param name="parent">The parent element.</param>
    /// <param name="tree">Either <c>visual</c> or <c>logical</c>.</param>
    /// <returns>The child snapshots and (for items controls) virtualisation metadata.</returns>
    public GetChildrenResponse Enumerate(DependencyObject parent, string tree)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(tree);

        IReadOnlyList<DependencyObject> kids = tree switch
        {
            TreeKindVisual => CollectVisualChildren(parent),
            TreeKindLogical => CollectLogicalChildren(parent),
            _ => throw new ArgumentException(
                $"Tree '{tree}' must be '{TreeKindVisual}' or '{TreeKindLogical}'.",
                nameof(tree))
        };

        var described = kids.Select(k => mDescriber.Describe(k)).ToList();
        VirtualizationDto? virtualization = ComputeVirtualization(parent, described.Count);

        return new GetChildrenResponse(described, virtualization);
    }

    private static List<DependencyObject> CollectVisualChildren(DependencyObject parent)
    {
        var children = new List<DependencyObject>();
        var isVisual = parent is Visual or Visual3D;
        if (isVisual)
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++) children.Add(VisualTreeHelper.GetChild(parent, i));
        }

        return children;
    }

    private static List<DependencyObject> CollectLogicalChildren(DependencyObject parent)
    {
        var children = new List<DependencyObject>();
        IEnumerable logical = LogicalTreeHelper.GetChildren(parent);
        foreach (var child in logical)
            if (child is DependencyObject dep)
                children.Add(dep);

        return children;
    }

    private static VirtualizationDto? ComputeVirtualization(DependencyObject parent, int realizedCount)
    {
        VirtualizationDto? virtualization = null;
        if (parent is ItemsControl ic)
        {
            var isVirtualizing = VirtualizingPanel.GetIsVirtualizing(ic);
            var total = ic.Items?.Count;
            virtualization = new VirtualizationDto(
                isVirtualizing,
                realizedCount,
                total);
        }

        return virtualization;
    }

    /// <summary>Tree kind: walk visual children.</summary>
    public const string TreeKindVisual = "visual";

    /// <summary>Tree kind: walk logical children.</summary>
    public const string TreeKindLogical = "logical";
}
