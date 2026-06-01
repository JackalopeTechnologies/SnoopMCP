// PathResolver.cs
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
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;

#endregion

namespace SnoopMCP.Payload.Inspection;

/// <summary>
///     Reverses <see cref="PathStringEmitter" />: walks a canonical path string from a known root and
///     returns the element it identifies, or <c>null</c> when no match exists.
/// </summary>
public sealed class PathResolver
{
    /// <summary>
    ///     Initialises a new <see cref="PathResolver" />.
    /// </summary>
    /// <param name="describer">The describer used to snapshot the resolved element.</param>
    /// <param name="parser">The parser used to convert the path string into ordered steps.</param>
    public PathResolver(ElementDescriber describer, PathStringParser parser)
    {
        ArgumentNullException.ThrowIfNull(describer);
        ArgumentNullException.ThrowIfNull(parser);
        mDescriber = describer;
        mParser = parser;
    }

    private readonly ElementDescriber mDescriber;
    private readonly PathStringParser mParser;

    /// <summary>
    ///     Walks <paramref name="pathString" /> from <paramref name="root" />.
    /// </summary>
    /// <param name="root">The root element; must match the first path step for resolution to begin.</param>
    /// <param name="pathString">The canonical path string.</param>
    /// <returns>The resolved element snapshot, or one whose element is <c>null</c> when no match exists.</returns>
    public ResolvePathResponse Resolve(DependencyObject root, string pathString)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrEmpty(pathString);

        IReadOnlyList<PathStep> steps = mParser.Parse(pathString);
        DependencyObject? current = MatchesStep(root, steps[0]) ? root : null;

        for (var i = 1; i < steps.Count && current is not null; i++) current = FindChildMatchingStep(current, steps[i]);

        DescribeElementResponse? described = current is null ? null : mDescriber.Describe(current);
        return new ResolvePathResponse(described);
    }

    private static bool MatchesStep(DependencyObject element, PathStep step)
    {
        var matches = element.GetType().Name == step.TypeName;
        matches = matches && MatchesNameAttribute(element, step);
        matches = matches && MatchesAutomationIdAttribute(element, step);
        return matches;
    }

    private static bool MatchesNameAttribute(DependencyObject element, PathStep step)
    {
        var matches = true;
        if (step.Attributes.TryGetValue(AttributeName, out var expectedName))
        {
            var actualName = (element as FrameworkElement)?.Name;
            matches = string.Equals(actualName, expectedName, StringComparison.Ordinal);
        }

        return matches;
    }

    private static bool MatchesAutomationIdAttribute(DependencyObject element, PathStep step)
    {
        var matches = true;
        if (step.Attributes.TryGetValue(AttributeAutomationId, out var expectedAutoId))
        {
            var actualAutoId = AutomationProperties.GetAutomationId(element);
            matches = string.Equals(actualAutoId, expectedAutoId, StringComparison.Ordinal);
        }

        return matches;
    }

    private static DependencyObject? FindChildMatchingStep(DependencyObject parent, PathStep step)
    {
        DependencyObject? match = null;
        var isVisual = parent is Visual or Visual3D;
        if (isVisual)
        {
            List<DependencyObject> candidates = CollectMatchingChildren(parent, step);
            var effectiveIndex = step.Index ?? 0;
            var inRange = effectiveIndex >= 0 && effectiveIndex < candidates.Count;
            if (inRange) match = candidates[effectiveIndex];
        }

        return match;
    }

    private static List<DependencyObject> CollectMatchingChildren(DependencyObject parent, PathStep step)
    {
        var candidates = new List<DependencyObject>();
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (MatchesStep(child, step)) candidates.Add(child);
        }

        return candidates;
    }

    private const string AttributeName = "Name";
    private const string AttributeAutomationId = "AutomationId";
}
