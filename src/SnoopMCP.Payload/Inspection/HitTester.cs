// HitTester.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Inspection;

using System.Windows;
using System.Windows.Media;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Wraps <see cref="VisualTreeHelper.HitTest(Visual, Point)"/> to return the deepest hittable visual
/// at a root-relative point, snapshotted via <see cref="ElementDescriber"/>.
/// </summary>
public sealed class HitTester
{
    private readonly ElementDescriber mDescriber;

    /// <summary>
    /// Initialises a new <see cref="HitTester"/>.
    /// </summary>
    /// <param name="describer">The describer used to snapshot the hit element.</param>
    public HitTester(ElementDescriber describer)
    {
        ArgumentNullException.ThrowIfNull(describer);
        mDescriber = describer;
    }

    /// <summary>
    /// Hits the deepest visual under <paramref name="root"/> at the supplied point.
    /// </summary>
    /// <param name="root">The root visual; must be a <see cref="Visual"/>.</param>
    /// <param name="x">Root-relative X coordinate.</param>
    /// <param name="y">Root-relative Y coordinate.</param>
    /// <returns>The hit response, or one whose element is <c>null</c> when nothing was hit.</returns>
    public HitTestResponse HitTest(DependencyObject root, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root is not Visual visualRoot)
        {
            throw new ArgumentException("Hit test root must be a Visual.", nameof(root));
        }

        Point point = new(x, y);
        HitTestResult? result = VisualTreeHelper.HitTest(visualRoot, point);
        DescribeElementResponse? described = null;
        if (result?.VisualHit is DependencyObject hit)
        {
            described = mDescriber.Describe(hit);
        }
        return new HitTestResponse(described);
    }
}
