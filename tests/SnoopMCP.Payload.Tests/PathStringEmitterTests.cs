// PathStringEmitterTests.cs
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
using System.Windows.Controls;
using SnoopMCP.Payload.PathStrings;
using Xunit;

#endregion

namespace SnoopMCP.Payload.Tests;

public sealed class PathStringEmitterTests
{
    [StaFact]
    public void Emit_SingleElement_ReturnsTypeName()
    {
        var window = new Window();
        var emitter = new PathStringEmitter();

        var path = emitter.Emit(window);

        Assert.Equal("/Window", path);
    }

    [StaFact]
    public void Emit_NamedChild_IncludesNameAttribute()
    {
        var grid = new Grid();
        var button = new Button { Name = "SaveBtn" };
        grid.Children.Add(button);
        var emitter = new PathStringEmitter();

        var path = emitter.Emit(button);

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

        var secondPath = emitter.Emit(second);

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

        var buttonPath = emitter.Emit(button);

        Assert.Equal("/StackPanel/Button", buttonPath);
    }

    [StaFact]
    public void Emit_NameAndAutomationId_IncludesBoth()
    {
        var grid = new Grid();
        var button = new Button { Name = "Save" };
        AutomationProperties.SetAutomationId(button, "SaveBtn");
        grid.Children.Add(button);
        var emitter = new PathStringEmitter();

        var path = emitter.Emit(button);

        Assert.Equal("/Grid/Button[Name='Save', AutomationId='SaveBtn']", path);
    }
}
