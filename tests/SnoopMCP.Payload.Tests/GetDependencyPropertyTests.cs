// GetDependencyPropertyTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Inspection;
using Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class GetDependencyPropertyTests
{
    [StaFact]
    public void Get_LocalValue_CurrentValueAndWinningSourceLocal()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button { Width = 123 };

        GetDependencyPropertyResponse response = inspector.GetProperty(button, "Width");

        Assert.Equal("Width", response.Name);
        Assert.Equal("123", response.CurrentValue);
        Assert.Equal("Local", response.WinningSource);
    }

    [StaFact]
    public void Get_NotSet_CurrentValueIsDefault_WinningSourceDefault()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        GetDependencyPropertyResponse response = inspector.GetProperty(button, "IsCancel");

        Assert.Equal("False", response.CurrentValue);
        Assert.Equal("Default", response.WinningSource);
    }

    [StaFact]
    public void Get_HasDefaultEntryInPrecedence()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        GetDependencyPropertyResponse response = inspector.GetProperty(button, "IsCancel");

        Assert.Contains(response.Precedence, e => e.Source == "Default");
    }

    [StaFact]
    public void Get_WithStyleSetter_StyleSetterAppearsInPrecedence()
    {
        var inspector = new DependencyPropertyInspector();
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Crimson));
        var button = new Button { Style = style };

        GetDependencyPropertyResponse response = inspector.GetProperty(button, "Background");

        Assert.Contains(response.Precedence, e => e.Source == "StyleSetter");
    }

    [StaFact]
    public void Get_LocalOverridesStyle_WinnerIsLocal()
    {
        var inspector = new DependencyPropertyInspector();
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Crimson));
        var button = new Button
        {
            Style = style,
            Background = Brushes.Lime
        };

        GetDependencyPropertyResponse response = inspector.GetProperty(button, "Background");

        Assert.Equal("Local", response.WinningSource);
        Assert.Contains(response.Precedence, e => e.Source == "Local");
        Assert.Contains(response.Precedence, e => e.Source == "StyleSetter");
    }

    [StaFact]
    public void Get_BasedOnChain_RecordsEachLevel()
    {
        var inspector = new DependencyPropertyInspector();
        var baseStyle = new Style(typeof(Button));
        baseStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Gray));
        var derivedStyle = new Style(typeof(Button)) { BasedOn = baseStyle };
        derivedStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Blue));
        var button = new Button { Style = derivedStyle };

        GetDependencyPropertyResponse response = inspector.GetProperty(button, "Background");

        int styleSetterCount = response.Precedence.Count(e => e.Source == "StyleSetter");
        Assert.Equal(2, styleSetterCount);
    }

    [StaFact]
    public void Get_UnknownProperty_ThrowsInvalidArgument()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(
            () => inspector.GetProperty(button, "NoSuchProperty"));
        Assert.Equal(ErrorCode.InvalidArgument, ex.Code);
    }

    [StaFact]
    public void Get_NullElement_Throws()
    {
        var inspector = new DependencyPropertyInspector();
        Assert.Throws<ArgumentNullException>(() => inspector.GetProperty(null!, "X"));
    }

    [StaFact]
    public void Get_EmptyName_Throws()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();
        Assert.Throws<ArgumentException>(() => inspector.GetProperty(button, ""));
    }
}
