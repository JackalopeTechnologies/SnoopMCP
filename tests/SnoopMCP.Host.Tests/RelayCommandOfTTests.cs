// RelayCommandOfTTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using ClientIntegration;
using Host;
using Xunit;

/// <summary>
/// Tests for <see cref="RelayCommand{T}"/>, which backs the per-agent tray install/uninstall menu
/// items: a specific menu item passes its <see cref="McpClient"/>; the "All" item passes null.
/// </summary>
public sealed class RelayCommandOfTTests
{
    [Fact]
    public void Execute_WithEnumParameter_PassesValue()
    {
        McpClient? captured = null;
        var command = new RelayCommand<McpClient>(c => captured = c);

        command.Execute(McpClient.Cursor);

        Assert.Equal(McpClient.Cursor, captured);
    }

    [Fact]
    public void Execute_WithNullParameter_PassesNull()
    {
        bool invoked = false;
        McpClient? captured = McpClient.ClaudeCode;
        var command = new RelayCommand<McpClient>(c =>
        {
            invoked = true;
            captured = c;
        });

        command.Execute(null);

        Assert.True(invoked);
        Assert.Null(captured);
    }

    [Fact]
    public void CanExecute_IsAlwaysTrue()
    {
        var command = new RelayCommand<McpClient>(_ => { });

        Assert.True(command.CanExecute(null));
        Assert.True(command.CanExecute(McpClient.VsCode));
    }
}
