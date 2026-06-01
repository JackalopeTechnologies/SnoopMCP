// ElementRegistryTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Windows.Controls;
using Payload;
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
        const int UnknownId = 99999;

        bool resolved = registry.TryResolve(UnknownId, out System.Windows.DependencyObject? element);

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
