// ElementFinder.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Inspection;

using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Walks a visual subtree evaluating AND-combined element predicates.
/// </summary>
public sealed class ElementFinder
{
    private const string DependencyPropertySuffix = "Property";

    private readonly ElementDescriber mDescriber;

    /// <summary>
    /// Initialises a new <see cref="ElementFinder"/>.
    /// </summary>
    /// <param name="describer">The describer used to snapshot each match.</param>
    public ElementFinder(ElementDescriber describer)
    {
        ArgumentNullException.ThrowIfNull(describer);
        mDescriber = describer;
    }

    /// <summary>
    /// Returns every element under <paramref name="root"/> (inclusive) whose every supplied
    /// predicate field matches.
    /// </summary>
    /// <param name="root">The subtree root.</param>
    /// <param name="predicate">The predicate to evaluate.</param>
    /// <returns>The matching elements, in pre-order traversal order.</returns>
    public FindElementsResponse Find(DependencyObject root, ElementPredicateDto predicate)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(predicate);

        var matches = new List<DescribeElementResponse>();
        Walk(root, predicate, matches);
        return new FindElementsResponse(matches);
    }

    private void Walk(DependencyObject element, ElementPredicateDto predicate, List<DescribeElementResponse> sink)
    {
        bool isVisual = element is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            if (MatchesPredicate(element, predicate))
            {
                sink.Add(mDescriber.Describe(element));
            }
            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                Walk(VisualTreeHelper.GetChild(element, i), predicate, sink);
            }
        }
    }

    private bool MatchesPredicate(DependencyObject element, ElementPredicateDto predicate)
    {
        bool matches = true;
        matches = matches && MatchesType(element, predicate.Type);
        matches = matches && MatchesName(element, predicate.Name);
        matches = matches && MatchesAutomationId(element, predicate.AutomationId);
        matches = matches && MatchesTextContains(element, predicate.TextContains);
        matches = matches && MatchesPropertyEquals(element, predicate.PropertyEquals);
        matches = matches && MatchesAncestor(element, predicate.HasAncestor);
        matches = matches && MatchesDescendant(element, predicate.HasDescendant);
        matches = matches && MatchesTemplatedParent(element, predicate.InTemplateOf);
        return matches;
    }

    private static bool MatchesType(DependencyObject element, string? expectedType)
    {
        bool matches = true;
        if (expectedType is not null)
        {
            string fullName = element.GetType().FullName ?? string.Empty;
            matches = fullName.Contains(expectedType, StringComparison.Ordinal);
        }
        return matches;
    }

    private static bool MatchesName(DependencyObject element, string? expectedName)
    {
        bool matches = true;
        if (expectedName is not null)
        {
            string? actualName = (element as FrameworkElement)?.Name;
            matches = string.Equals(actualName, expectedName, StringComparison.Ordinal);
        }
        return matches;
    }

    private static bool MatchesAutomationId(DependencyObject element, string? expectedId)
    {
        bool matches = true;
        if (expectedId is not null)
        {
            string actualId = AutomationProperties.GetAutomationId(element);
            matches = string.Equals(actualId, expectedId, StringComparison.Ordinal);
        }
        return matches;
    }

    private bool MatchesTextContains(DependencyObject element, string? needle)
    {
        bool matches = true;
        if (needle is not null)
        {
            DescribeElementResponse description = mDescriber.Describe(element);
            matches = description.VisibleText.Contains(needle, StringComparison.OrdinalIgnoreCase);
        }
        return matches;
    }

    private static bool MatchesPropertyEquals(DependencyObject element, PropertyEqualsDto? request)
    {
        bool matches = true;
        if (request is not null)
        {
            matches = false;
            DependencyProperty? dp = ResolveDependencyProperty(element.GetType(), request.Property);
            if (dp is not null)
            {
                object? value = element.GetValue(dp);
                string stringified = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                matches = string.Equals(stringified, request.Value, StringComparison.Ordinal);
            }
        }
        return matches;
    }

    private bool MatchesAncestor(DependencyObject element, ElementPredicateDto? inner)
    {
        bool matches = true;
        if (inner is not null)
        {
            matches = AnyAncestorMatches(element, inner);
        }
        return matches;
    }

    private bool MatchesDescendant(DependencyObject element, ElementPredicateDto? inner)
    {
        bool matches = true;
        if (inner is not null)
        {
            matches = AnyDescendantMatches(element, inner);
        }
        return matches;
    }

    private bool MatchesTemplatedParent(DependencyObject element, ElementPredicateDto? inner)
    {
        bool matches = true;
        if (inner is not null)
        {
            matches = TemplatedParentMatches(element, inner);
        }
        return matches;
    }

    private static DependencyProperty? ResolveDependencyProperty(Type ownerType, string propertyName)
    {
        DependencyProperty? found = null;
        string fieldName = propertyName + DependencyPropertySuffix;
        Type? current = ownerType;
        while (current is not null && found is null)
        {
            FieldInfo? field = current.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            bool isDpField = field is not null && typeof(DependencyProperty).IsAssignableFrom(field.FieldType);
            if (isDpField)
            {
                found = field?.GetValue(null) as DependencyProperty;
            }
            current = current.BaseType;
        }
        return found;
    }

    private bool AnyAncestorMatches(DependencyObject element, ElementPredicateDto inner)
    {
        bool found = false;
        DependencyObject? current = VisualTreeHelper.GetParent(element);
        while (current is not null && !found)
        {
            bool matchedHere = MatchesPredicate(current, inner);
            if (matchedHere)
            {
                found = true;
            }
            else
            {
                current = VisualTreeHelper.GetParent(current);
            }
        }
        return found;
    }

    private bool AnyDescendantMatches(DependencyObject element, ElementPredicateDto inner)
    {
        bool found = false;
        bool isVisual = element is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            int count = VisualTreeHelper.GetChildrenCount(element);
            int i = 0;
            while (i < count && !found)
            {
                DependencyObject child = VisualTreeHelper.GetChild(element, i);
                bool childMatches = MatchesPredicate(child, inner) || AnyDescendantMatches(child, inner);
                found = childMatches;
                i++;
            }
        }
        return found;
    }

    private bool TemplatedParentMatches(DependencyObject element, ElementPredicateDto inner)
    {
        DependencyObject? templated = element switch
        {
            FrameworkElement fe => fe.TemplatedParent,
            FrameworkContentElement fce => fce.TemplatedParent,
            _ => null
        };
        bool matches = false;
        if (templated is not null)
        {
            matches = MatchesPredicate(templated, inner);
        }
        return matches;
    }
}
