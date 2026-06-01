// PathStringEmitter.cs
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

namespace SnoopMCP.Payload.PathStrings;

using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

/// <summary>
/// Emits a canonical path string for a <see cref="DependencyObject"/> by walking the visual
/// tree from the element up to the root.
/// </summary>
public sealed class PathStringEmitter
{
    private const string AttributeSeparator = ", ";

    /// <summary>
    /// Returns the canonical path string identifying <paramref name="element"/> within its
    /// visual tree (root-first, slash-separated).
    /// </summary>
    /// <param name="element">The element to describe.</param>
    /// <returns>A path of the form <c>/TypeName[Name='X'][n]/...</c>.</returns>
    // CA1822 suppression: instance method by design — host-side DI replaces this with an
    // alternate emitter when path semantics evolve. Forcing static would lock that out.
#pragma warning disable CA1822
    public string Emit(DependencyObject element)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(element);

        var chain = new List<DependencyObject>();
        DependencyObject? current = element;
        while (current is not null)
        {
            chain.Add(current);
            current = GetParent(current);
        }
        chain.Reverse();

        var builder = new StringBuilder();
        foreach (DependencyObject node in chain)
        {
            builder.Append('/');
            builder.Append(BuildStep(node));
        }
        return builder.ToString();
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        DependencyObject? parent = null;
        bool isVisual = element is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            parent = VisualTreeHelper.GetParent(element);
        }
        return parent;
    }

    private static string BuildStep(DependencyObject element)
    {
        string typeName = element.GetType().Name;
        string? name = TryGetName(element);
        string? automationId = TryGetAutomationId(element);
        int? siblingIndex = TryGetSiblingIndex(element);

        var attrs = new List<string>();
        if (!string.IsNullOrEmpty(name))
        {
            attrs.Add($"Name='{name}'");
        }
        if (!string.IsNullOrEmpty(automationId))
        {
            attrs.Add($"AutomationId='{automationId}'");
        }

        var builder = new StringBuilder(typeName);
        if (attrs.Count > 0)
        {
            builder.Append('[').Append(string.Join(AttributeSeparator, attrs)).Append(']');
        }
        if (siblingIndex is int idx && attrs.Count == 0)
        {
            builder.Append('[').Append(idx).Append(']');
        }
        return builder.ToString();
    }

    private static string? TryGetName(DependencyObject element)
    {
        string? name = (element as FrameworkElement)?.Name;
        bool hasName = !string.IsNullOrEmpty(name);
        return hasName ? name : null;
    }

    private static string? TryGetAutomationId(DependencyObject element)
    {
        string? id = AutomationProperties.GetAutomationId(element);
        bool hasId = !string.IsNullOrEmpty(id);
        return hasId ? id : null;
    }

    private static int? TryGetSiblingIndex(DependencyObject element)
    {
        DependencyObject? parent = GetParent(element);
        int? result = null;
        if (parent is not null)
        {
            result = ComputeSiblingIndex(parent, element);
        }
        return result;
    }

    private static int? ComputeSiblingIndex(DependencyObject parent, DependencyObject element)
    {
        int siblingCount = VisualTreeHelper.GetChildrenCount(parent);
        Type myType = element.GetType();
        int sameTypeIndex = 0;
        int foundAt = -1;
        for (int i = 0; i < siblingCount; i++)
        {
            DependencyObject sibling = VisualTreeHelper.GetChild(parent, i);
            bool sameType = sibling.GetType() == myType;
            if (sameType)
            {
                bool isThisOne = ReferenceEquals(sibling, element);
                foundAt = isThisOne ? sameTypeIndex : foundAt;
                sameTypeIndex++;
            }
        }
        int? result = null;
        bool needsIndex = sameTypeIndex > 1 && foundAt >= 0;
        if (needsIndex)
        {
            result = foundAt;
        }
        return result;
    }
}
