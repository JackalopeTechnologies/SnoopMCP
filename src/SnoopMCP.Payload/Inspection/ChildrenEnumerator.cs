// ChildrenEnumerator.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Inspection;

using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Walks the visual or logical children of a parent element, optionally reporting
/// virtualisation metadata when the parent is an <see cref="ItemsControl"/>.
/// </summary>
public sealed class ChildrenEnumerator
{
    /// <summary>Tree kind: walk visual children.</summary>
    public const string TreeKindVisual = "visual";

    /// <summary>Tree kind: walk logical children.</summary>
    public const string TreeKindLogical = "logical";

    private readonly ElementDescriber mDescriber;

    /// <summary>
    /// Initialises a new <see cref="ChildrenEnumerator"/>.
    /// </summary>
    /// <param name="describer">The describer used to build per-child snapshots.</param>
    public ChildrenEnumerator(ElementDescriber describer)
    {
        ArgumentNullException.ThrowIfNull(describer);
        mDescriber = describer;
    }

    /// <summary>
    /// Enumerates <paramref name="parent"/>'s children in the specified tree.
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

        List<DescribeElementResponse> described = kids.Select(k => mDescriber.Describe(k)).ToList();
        VirtualizationDto? virtualization = ComputeVirtualization(parent, described.Count);

        return new GetChildrenResponse(described, virtualization);
    }

    private static List<DependencyObject> CollectVisualChildren(DependencyObject parent)
    {
        var children = new List<DependencyObject>();
        bool isVisual = parent is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                children.Add(VisualTreeHelper.GetChild(parent, i));
            }
        }
        return children;
    }

    private static List<DependencyObject> CollectLogicalChildren(DependencyObject parent)
    {
        var children = new List<DependencyObject>();
        IEnumerable logical = LogicalTreeHelper.GetChildren(parent);
        foreach (object? child in logical)
        {
            if (child is DependencyObject dep)
            {
                children.Add(dep);
            }
        }
        return children;
    }

    private static VirtualizationDto? ComputeVirtualization(DependencyObject parent, int realizedCount)
    {
        VirtualizationDto? virtualization = null;
        if (parent is ItemsControl ic)
        {
            bool isVirtualizing = VirtualizingPanel.GetIsVirtualizing(ic);
            int? total = ic.Items?.Count;
            virtualization = new VirtualizationDto(
                IsVirtualizing: isVirtualizing,
                RealizedItems: realizedCount,
                TotalItems: total);
        }
        return virtualization;
    }
}
