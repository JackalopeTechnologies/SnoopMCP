// DataContextPath.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Interaction;

using System.Reflection;

/// <summary>
/// Walks a dotted chain of public instance properties starting from a root object — the shared
/// DataContext path-walk used by both <see cref="CommandInvoker"/> (resolving a bound
/// <see cref="System.Windows.Input.ICommand"/>) and <see cref="ValueWaiter"/> (polling a bound
/// value). The two callers disagree on what an unresolved path MEANS (a driving error vs. "not
/// there yet, keep polling"), so this helper only reports whether the walk resolved; it never
/// throws for an unresolved path, leaving each caller to apply its own semantics to a
/// <c>false</c> result.
/// </summary>
public static class DataContextPath
{
    private const BindingFlags WalkBindingFlags = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>
    /// Walks the dotted public-instance-property <paramref name="path"/> starting at <paramref name="root"/>.
    /// </summary>
    /// <param name="root">The object the walk starts from. Must not be null.</param>
    /// <param name="path">
    /// Dotted property-name path, e.g. <c>"Selected.Item.Name"</c>. Empty segments (from leading,
    /// trailing, or doubled dots) are ignored.
    /// </param>
    /// <param name="value">
    /// When the method returns <c>true</c>, the non-null value at the end of the path. When it
    /// returns <c>false</c>, always <c>null</c>.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> and the resolved value only when every segment resolves to a non-null
    /// value; <c>false</c> (value=null) otherwise — whether because a segment's property could not
    /// be found on the current type, a <c>null</c> was encountered before the final segment (so
    /// there was nothing left to walk onto), or the final segment itself resolved to <c>null</c>.
    /// </returns>
    public static bool TryWalk(object root, string path, out object? value)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(path);

        object? current = root;
        bool resolved = true;
        foreach (string segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is null)
            {
                resolved = false;
                break;
            }

            PropertyInfo? property = current.GetType().GetProperty(segment, WalkBindingFlags);
            if (property is null)
            {
                resolved = false;
                break;
            }

            current = property.GetValue(current);
        }

        resolved = resolved && current is not null;
        value = resolved ? current : null;
        return resolved;
    }
}
