// PathResolver.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Inspection;

using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using PathStrings;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Reverses <see cref="PathStringEmitter"/>: walks a canonical path string from a known root and
/// returns the element it identifies, or <c>null</c> when no match exists.
/// </summary>
public sealed class PathResolver
{
    private const string AttributeName = "Name";
    private const string AttributeAutomationId = "AutomationId";

    private readonly ElementDescriber mDescriber;
    private readonly PathStringParser mParser;

    /// <summary>
    /// Initialises a new <see cref="PathResolver"/>.
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

    /// <summary>
    /// Walks <paramref name="pathString"/> from <paramref name="root"/>.
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

        for (int i = 1; i < steps.Count && current is not null; i++)
        {
            current = FindChildMatchingStep(current, steps[i]);
        }

        DescribeElementResponse? described = current is null ? null : mDescriber.Describe(current);
        return new ResolvePathResponse(described);
    }

    private static bool MatchesStep(DependencyObject element, PathStep step)
    {
        bool matches = element.GetType().Name == step.TypeName;
        matches = matches && MatchesNameAttribute(element, step);
        matches = matches && MatchesAutomationIdAttribute(element, step);
        return matches;
    }

    private static bool MatchesNameAttribute(DependencyObject element, PathStep step)
    {
        bool matches = true;
        if (step.Attributes.TryGetValue(AttributeName, out string? expectedName))
        {
            string? actualName = (element as FrameworkElement)?.Name;
            matches = string.Equals(actualName, expectedName, StringComparison.Ordinal);
        }
        return matches;
    }

    private static bool MatchesAutomationIdAttribute(DependencyObject element, PathStep step)
    {
        bool matches = true;
        if (step.Attributes.TryGetValue(AttributeAutomationId, out string? expectedAutoId))
        {
            string actualAutoId = AutomationProperties.GetAutomationId(element);
            matches = string.Equals(actualAutoId, expectedAutoId, StringComparison.Ordinal);
        }
        return matches;
    }

    private static DependencyObject? FindChildMatchingStep(DependencyObject parent, PathStep step)
    {
        DependencyObject? match = null;
        bool isVisual = parent is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            List<DependencyObject> candidates = CollectMatchingChildren(parent, step);
            int effectiveIndex = step.Index ?? 0;
            bool inRange = effectiveIndex >= 0 && effectiveIndex < candidates.Count;
            if (inRange)
            {
                match = candidates[effectiveIndex];
            }
        }
        return match;
    }

    private static List<DependencyObject> CollectMatchingChildren(DependencyObject parent, PathStep step)
    {
        var candidates = new List<DependencyObject>();
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (MatchesStep(child, step))
            {
                candidates.Add(child);
            }
        }
        return candidates;
    }
}
