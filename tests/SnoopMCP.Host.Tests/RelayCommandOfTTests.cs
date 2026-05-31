// RelayCommandOfTTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host.Tests;

using SnoopMCP.ClientIntegration;
using SnoopMCP.Host;
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
