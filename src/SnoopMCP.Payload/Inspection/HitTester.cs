// HitTester.cs
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
using System.Windows.Media;
using SnoopMCP.Protocol.Tools;

#endregion

namespace SnoopMCP.Payload.Inspection;

/// <summary>
///     Wraps <see cref="VisualTreeHelper.HitTest(Visual, Point)" /> to return the deepest hittable visual
///     at a root-relative point, snapshotted via <see cref="ElementDescriber" />.
/// </summary>
public sealed class HitTester
{
    /// <summary>
    ///     Initialises a new <see cref="HitTester" />.
    /// </summary>
    /// <param name="describer">The describer used to snapshot the hit element.</param>
    public HitTester(ElementDescriber describer)
    {
        ArgumentNullException.ThrowIfNull(describer);
        mDescriber = describer;
    }

    private readonly ElementDescriber mDescriber;

    /// <summary>
    ///     Hits the deepest visual under <paramref name="root" /> at the supplied point.
    /// </summary>
    /// <param name="root">The root visual; must be a <see cref="Visual" />.</param>
    /// <param name="x">Root-relative X coordinate.</param>
    /// <param name="y">Root-relative Y coordinate.</param>
    /// <returns>The hit response, or one whose element is <c>null</c> when nothing was hit.</returns>
    public HitTestResponse HitTest(DependencyObject root, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root is not Visual visualRoot) throw new ArgumentException("Hit test root must be a Visual.", nameof(root));

        Point point = new(x, y);
        HitTestResult? result = VisualTreeHelper.HitTest(visualRoot, point);
        DescribeElementResponse? described = null;
        if (result?.VisualHit is DependencyObject hit) described = mDescriber.Describe(hit);
        return new HitTestResponse(described);
    }
}
