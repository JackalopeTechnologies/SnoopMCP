// ElementFinder.cs
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

using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using SnoopMCP.Protocol.Tools;

#endregion

namespace SnoopMCP.Payload.Inspection;

/// <summary>
///     Walks a visual subtree evaluating AND-combined element predicates.
/// </summary>
public sealed class ElementFinder
{
    /// <summary>
    ///     Initialises a new <see cref="ElementFinder" />.
    /// </summary>
    /// <param name="describer">The describer used to snapshot each match.</param>
    public ElementFinder(ElementDescriber describer)
    {
        ArgumentNullException.ThrowIfNull(describer);
        mDescriber = describer;
    }

    private readonly ElementDescriber mDescriber;

    /// <summary>
    ///     Returns every element under <paramref name="root" /> (inclusive) whose every supplied
    ///     predicate field matches.
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
        var isVisual = element is Visual or Visual3D;
        if (isVisual)
        {
            if (MatchesPredicate(element, predicate)) sink.Add(mDescriber.Describe(element));
            var count = VisualTreeHelper.GetChildrenCount(element);
            for (var i = 0; i < count; i++) Walk(VisualTreeHelper.GetChild(element, i), predicate, sink);
        }
    }

    private bool MatchesPredicate(DependencyObject element, ElementPredicateDto predicate)
    {
        var matches = true;
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
        var matches = true;
        if (expectedType is not null)
        {
            var fullName = element.GetType().FullName ?? string.Empty;
            matches = fullName.Contains(expectedType, StringComparison.Ordinal);
        }

        return matches;
    }

    private static bool MatchesName(DependencyObject element, string? expectedName)
    {
        var matches = true;
        if (expectedName is not null)
        {
            var actualName = (element as FrameworkElement)?.Name;
            matches = string.Equals(actualName, expectedName, StringComparison.Ordinal);
        }

        return matches;
    }

    private static bool MatchesAutomationId(DependencyObject element, string? expectedId)
    {
        var matches = true;
        if (expectedId is not null)
        {
            var actualId = AutomationProperties.GetAutomationId(element);
            matches = string.Equals(actualId, expectedId, StringComparison.Ordinal);
        }

        return matches;
    }

    private bool MatchesTextContains(DependencyObject element, string? needle)
    {
        var matches = true;
        if (needle is not null)
        {
            DescribeElementResponse description = mDescriber.Describe(element);
            matches = description.VisibleText.Contains(needle, StringComparison.OrdinalIgnoreCase);
        }

        return matches;
    }

    private static bool MatchesPropertyEquals(DependencyObject element, PropertyEqualsDto? request)
    {
        var matches = true;
        if (request is not null)
        {
            matches = false;
            DependencyProperty? dp = ResolveDependencyProperty(element.GetType(), request.Property);
            if (dp is not null)
            {
                var value = element.GetValue(dp);
                var stringified = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                matches = string.Equals(stringified, request.Value, StringComparison.Ordinal);
            }
        }

        return matches;
    }

    private bool MatchesAncestor(DependencyObject element, ElementPredicateDto? inner)
    {
        var matches = true;
        if (inner is not null) matches = AnyAncestorMatches(element, inner);
        return matches;
    }

    private bool MatchesDescendant(DependencyObject element, ElementPredicateDto? inner)
    {
        var matches = true;
        if (inner is not null) matches = AnyDescendantMatches(element, inner);
        return matches;
    }

    private bool MatchesTemplatedParent(DependencyObject element, ElementPredicateDto? inner)
    {
        var matches = true;
        if (inner is not null) matches = TemplatedParentMatches(element, inner);
        return matches;
    }

    private static DependencyProperty? ResolveDependencyProperty(Type ownerType, string propertyName)
    {
        DependencyProperty? found = null;
        var fieldName = propertyName + DependencyPropertySuffix;
        Type? current = ownerType;
        while (current is not null && found is null)
        {
            FieldInfo? field = current.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var isDpField = field is not null && typeof(DependencyProperty).IsAssignableFrom(field.FieldType);
            if (isDpField) found = field?.GetValue(null) as DependencyProperty;
            current = current.BaseType;
        }

        return found;
    }

    private bool AnyAncestorMatches(DependencyObject element, ElementPredicateDto inner)
    {
        var found = false;
        DependencyObject? current = VisualTreeHelper.GetParent(element);
        while (current is not null && !found)
        {
            var matchedHere = MatchesPredicate(current, inner);
            if (matchedHere)
                found = true;
            else
                current = VisualTreeHelper.GetParent(current);
        }

        return found;
    }

    private bool AnyDescendantMatches(DependencyObject element, ElementPredicateDto inner)
    {
        var found = false;
        var isVisual = element is Visual or Visual3D;
        if (isVisual)
        {
            var count = VisualTreeHelper.GetChildrenCount(element);
            var i = 0;
            while (i < count && !found)
            {
                DependencyObject child = VisualTreeHelper.GetChild(element, i);
                var childMatches = MatchesPredicate(child, inner) || AnyDescendantMatches(child, inner);
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
        var matches = false;
        if (templated is not null) matches = MatchesPredicate(templated, inner);
        return matches;
    }

    private const string DependencyPropertySuffix = "Property";
}
