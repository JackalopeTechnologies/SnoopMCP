// UiaElementInfo.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Automation;

/// <summary>Facts about a discovered UIA element, plus a fresh cache reference for driving it.</summary>
/// <param name="Reference">A cross-call reference for driving or re-resolving the element.</param>
/// <param name="AutomationId">The element's AutomationId, or empty.</param>
/// <param name="Name">The element's UIA Name, or empty.</param>
/// <param name="ControlType">The element's programmatic control-type name, or empty.</param>
/// <param name="HelpText">The element's HelpText, or empty.</param>
/// <param name="X">The bounding rectangle's left edge, or null when the element is offscreen.</param>
/// <param name="Y">The bounding rectangle's top edge, or null when the element is offscreen.</param>
/// <param name="Width">The bounding rectangle's width, or null when the element is offscreen.</param>
/// <param name="Height">The bounding rectangle's height, or null when the element is offscreen.</param>
/// <param name="Patterns">The action/value patterns the element supports.</param>
/// <remarks>
/// The bounds are nullable because UI Automation reports <c>Rect.Empty</c> — <c>±Infinity</c> in every
/// field — for an element with no on-screen rectangle, and <c>System.Text.Json</c> cannot write
/// non-finite doubles. Emitting zeros instead would serialize but claim the element sits at the origin;
/// null says "offscreen", which is the truth. Build instances through <see cref="Create"/> so the
/// normalization cannot be bypassed. See issue #73.
/// </remarks>
public sealed record UiaElementInfo(
    UiaElementRef Reference,
    string AutomationId,
    string Name,
    string ControlType,
    string HelpText,
    double? X,
    double? Y,
    double? Width,
    double? Height,
    IReadOnlyList<string> Patterns)
{
    /// <summary>
    /// Creates an instance from raw UIA bounds, mapping any non-finite coordinate to null so an
    /// offscreen element cannot abort serialization of the response carrying it.
    /// </summary>
    /// <param name="reference">A cross-call reference for driving or re-resolving the element.</param>
    /// <param name="automationId">The element's AutomationId, or empty.</param>
    /// <param name="name">The element's UIA Name, or empty.</param>
    /// <param name="controlType">The element's programmatic control-type name, or empty.</param>
    /// <param name="helpText">The element's HelpText, or empty.</param>
    /// <param name="x">The raw bounding-rectangle left edge, possibly non-finite.</param>
    /// <param name="y">The raw bounding-rectangle top edge, possibly non-finite.</param>
    /// <param name="width">The raw bounding-rectangle width, possibly non-finite.</param>
    /// <param name="height">The raw bounding-rectangle height, possibly non-finite.</param>
    /// <param name="patterns">The action/value patterns the element supports.</param>
    /// <returns>An instance whose bounds are finite values or null.</returns>
    public static UiaElementInfo Create(
        UiaElementRef reference,
        string automationId,
        string name,
        string controlType,
        string helpText,
        double x,
        double y,
        double width,
        double height,
        IReadOnlyList<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(automationId);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(controlType);
        ArgumentNullException.ThrowIfNull(helpText);
        ArgumentNullException.ThrowIfNull(patterns);

        return new UiaElementInfo(
            reference,
            automationId,
            name,
            controlType,
            helpText,
            Finite(x),
            Finite(y),
            Finite(width),
            Finite(height),
            patterns);
    }

    /// <summary>Maps a non-finite coordinate — UIA's offscreen <c>Rect.Empty</c> — to null.</summary>
    /// <param name="value">The raw coordinate.</param>
    /// <returns>The coordinate when finite; otherwise null.</returns>
    private static double? Finite(double value) => double.IsFinite(value) ? value : null;
}
