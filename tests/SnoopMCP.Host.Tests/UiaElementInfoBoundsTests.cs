// UiaElementInfoBoundsTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using System.Text.Json;
using Automation;
using Protocol.Wire;
using Xunit;

/// <summary>
/// Guards the serialization of an OFFSCREEN element's bounds. UI Automation reports
/// <c>Rect.Empty</c> for an element with no on-screen rectangle, whose fields are <c>±Infinity</c>,
/// and <see cref="JsonSerializer"/> refuses to write non-finite doubles at all. Because the whole
/// result set is serialized in one call, a single offscreen match aborted the ENTIRE response —
/// which is why find_uia_element worked for "CheckBox" (all matches on-screen) and blew up for
/// "Button"/"Text"/"Custom" (issue #73). The failure scaled with what happened to be offscreen, not
/// with result count, so paging would not have helped.
/// </summary>
public sealed class UiaElementInfoBoundsTests
{
    [Fact]
    public void Create_WhenBoundsAreNonFinite_NormalizesToNullSoTheResponseStillSerializes()
    {
        var reference = new UiaElementRef(1234, "1234:7");

        // Exactly what AutomationElement.Cached.BoundingRectangle yields for an offscreen element.
        UiaElementInfo info = UiaElementInfo.Create(
            reference,
            automationId: string.Empty,
            name: "Save",
            controlType: "ControlType.Button",
            helpText: string.Empty,
            x: double.PositiveInfinity,
            y: double.PositiveInfinity,
            width: double.NegativeInfinity,
            height: double.NegativeInfinity,
            patterns: []);

        Assert.Null(info.X);
        Assert.Null(info.Y);
        Assert.Null(info.Width);
        Assert.Null(info.Height);

        // The regression itself: this threw ArgumentException before the fix.
        string json = JsonSerializer.Serialize(info, WireSerializer.JsonOptions);

        // The wire options ignore nulls, so an offscreen element simply carries no bounds keys —
        // a caller sees them absent rather than as a position it could mistake for the origin.
        Assert.DoesNotContain("\"x\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"height\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", json, StringComparison.Ordinal);
        Assert.Contains("\"Save\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_WhenBoundsAreFinite_PreservesThemVerbatim()
    {
        var reference = new UiaElementRef(1234, "1234:8");

        UiaElementInfo info = UiaElementInfo.Create(
            reference,
            automationId: "SaveButton",
            name: "Save",
            controlType: "ControlType.Button",
            helpText: string.Empty,
            x: 12.5,
            y: 30,
            width: 80,
            height: 24,
            patterns: ["Invoke"]);

        Assert.Equal(12.5, info.X);
        Assert.Equal(30, info.Y);
        Assert.Equal(80, info.Width);
        Assert.Equal(24, info.Height);
    }
}
