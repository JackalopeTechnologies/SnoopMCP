// PathStringEmitterTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Payload.PathStrings;
using Xunit;

public sealed class PathStringEmitterTests
{
    [StaFact]
    public void Emit_SingleElement_ReturnsTypeName()
    {
        var window = new Window();
        var emitter = new PathStringEmitter();

        string path = emitter.Emit(window);

        Assert.Equal("/Window", path);
    }

    [StaFact]
    public void Emit_NamedChild_IncludesNameAttribute()
    {
        var grid = new Grid();
        var button = new Button { Name = "SaveBtn" };
        grid.Children.Add(button);
        var emitter = new PathStringEmitter();

        string path = emitter.Emit(button);

        Assert.Equal("/Grid/Button[Name='SaveBtn']", path);
    }

    [StaFact]
    public void Emit_AnonymousSibling_UsesIndex()
    {
        var stack = new StackPanel();
        var first = new Button();
        var second = new Button();
        var third = new Button();
        stack.Children.Add(first);
        stack.Children.Add(second);
        stack.Children.Add(third);
        var emitter = new PathStringEmitter();

        string secondPath = emitter.Emit(second);

        Assert.Equal("/StackPanel/Button[1]", secondPath);
    }

    [StaFact]
    public void Emit_MixedTypes_OnlyIndexesAmongSameType()
    {
        var stack = new StackPanel();
        var label = new TextBlock();
        var button = new Button();
        stack.Children.Add(label);
        stack.Children.Add(button);
        var emitter = new PathStringEmitter();

        string buttonPath = emitter.Emit(button);

        Assert.Equal("/StackPanel/Button", buttonPath);
    }

    [StaFact]
    public void Emit_NameAndAutomationId_IncludesBoth()
    {
        var grid = new Grid();
        var button = new Button { Name = "Save" };
        System.Windows.Automation.AutomationProperties.SetAutomationId(button, "SaveBtn");
        grid.Children.Add(button);
        var emitter = new PathStringEmitter();

        string path = emitter.Emit(button);

        Assert.Equal("/Grid/Button[Name='Save', AutomationId='SaveBtn']", path);
    }
}
