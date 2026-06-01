// ElementRegistryTests.cs
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

namespace SnoopMCP.Payload.Tests;

using System.Windows.Controls;
using SnoopMCP.Payload;
using Xunit;

public sealed class ElementRegistryTests
{
    [StaFact]
    public void GetOrAssign_FirstTime_AssignsNewId()
    {
        var registry = new ElementRegistry();
        var button = new Button();

        int id = registry.GetOrAssign(button);

        Assert.True(id > 0, $"Expected positive id, got {id}.");
    }

    [StaFact]
    public void GetOrAssign_SameElementTwice_ReturnsSameId()
    {
        var registry = new ElementRegistry();
        var button = new Button();

        int first = registry.GetOrAssign(button);
        int second = registry.GetOrAssign(button);

        Assert.Equal(first, second);
    }

    [StaFact]
    public void GetOrAssign_DifferentElements_ReturnsDifferentIds()
    {
        var registry = new ElementRegistry();
        var buttonA = new Button();
        var buttonB = new Button();

        int idA = registry.GetOrAssign(buttonA);
        int idB = registry.GetOrAssign(buttonB);

        Assert.NotEqual(idA, idB);
    }

    [StaFact]
    public void TryResolve_LiveElement_ReturnsTrueAndSameInstance()
    {
        var registry = new ElementRegistry();
        var button = new Button();
        int id = registry.GetOrAssign(button);

        bool resolved = registry.TryResolve(id, out System.Windows.DependencyObject? element);

        Assert.True(resolved);
        Assert.Same(button, element);
    }

    [StaFact]
    public void TryResolve_UnknownId_ReturnsFalse()
    {
        var registry = new ElementRegistry();
        const int unknownId = 99999;

        bool resolved = registry.TryResolve(unknownId, out System.Windows.DependencyObject? element);

        Assert.False(resolved);
        Assert.Null(element);
    }

    [StaFact]
    public void TryResolve_AfterElementCollected_ReturnsFalse()
    {
        var registry = new ElementRegistry();
        int id = AssignAndDropReference(registry);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        bool resolved = registry.TryResolve(id, out System.Windows.DependencyObject? element);

        Assert.False(resolved);
        Assert.Null(element);
    }

    private static int AssignAndDropReference(ElementRegistry registry)
    {
        var button = new Button();
        int id = registry.GetOrAssign(button);
        return id;
    }
}
