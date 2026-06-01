// RootEnumeratorTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Payload;
using Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class RootEnumeratorTests
{
    private static RootEnumerator CreateEnumerator(ElementRegistry registry)
    {
        return new RootEnumerator(registry, new PopupOwnerResolver());
    }

    [StaFact]
    public void Enumerate_VisibleWindow_AppearsAsWindowRoot()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);

        var window = new Window
        {
            Title = "Test Window",
            Width = 200,
            Height = 100,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
            Visibility = Visibility.Hidden
        };

        try
        {
            window.Show();
            ListVisualRootsResponse response = enumerator.Enumerate();

            VisualRootDto? match = response.Roots.FirstOrDefault(r => r.Title == "Test Window");
            Assert.NotNull(match);
            Assert.Equal("Window", match.Kind);
            Assert.Null(match.OpenedBy);
            Assert.NotEqual(0, match.Hwnd);
        }
        finally
        {
            window.Close();
        }
    }

    [StaFact]
    public void Enumerate_OpenPopup_AppearsAsPopupRoot_WithOpenedBy()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);

        var popupContent = new Border
        {
            Width = 50,
            Height = 50,
            Background = System.Windows.Media.Brushes.Yellow
        };
        var popup = new Popup
        {
            Child = popupContent,
            IsOpen = false,
            StaysOpen = true
        };

        var grid = new Grid();
        grid.Children.Add(popup);

        var window = new Window
        {
            Title = "Popup Host",
            Width = 200,
            Height = 100,
            Content = grid,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
            Visibility = Visibility.Hidden
        };

        try
        {
            window.Show();
            popup.IsOpen = true;
            popup.UpdateLayout();

            ListVisualRootsResponse response = enumerator.Enumerate();

            VisualRootDto? popupRoot = response.Roots.FirstOrDefault(r => r.Kind == "Popup");
            Assert.NotNull(popupRoot);
            Assert.NotNull(popupRoot.OpenedBy);

            int expectedOwnerId = registry.GetOrAssign(popup);
            Assert.Equal(expectedOwnerId, popupRoot.OpenedBy);
        }
        finally
        {
            popup.IsOpen = false;
            window.Close();
        }
    }

    [StaFact]
    public void Enumerate_MultipleWindows_AllReturned()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);

        var a = NewHiddenWindow("Alpha");
        var b = NewHiddenWindow("Beta");

        try
        {
            a.Show();
            b.Show();

            ListVisualRootsResponse response = enumerator.Enumerate();

            Assert.Contains(response.Roots, r => r.Title == "Alpha");
            Assert.Contains(response.Roots, r => r.Title == "Beta");
        }
        finally
        {
            a.Close();
            b.Close();
        }
    }

    private static Window NewHiddenWindow(string title) => new()
    {
        Title = title,
        Width = 200,
        Height = 100,
        ShowInTaskbar = false,
        WindowStyle = WindowStyle.None,
        ShowActivated = false,
        Visibility = Visibility.Hidden
    };
}
