// ProgramArgsTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Cli.Tests;

using ClientIntegration;
using Cli;
using Xunit;

/// <summary>Argument-parsing checks for the CLI verb dispatcher's client selection.</summary>
public sealed class ProgramArgsTests
{
    [Fact]
    public void HasFlag_MatchesExactToken()
    {
        Assert.True(Program.HasFlag(["--vscode"], "--vscode"));
    }

    [Fact]
    public void HasFlag_ReturnsFalse_ForUnknownFlag()
    {
        Assert.False(Program.HasFlag(["--vscode"], "--unknown"));
    }

    [Fact]
    public void SelectClients_NoFlags_ReturnsAllNine()
    {
        IReadOnlyList<McpClient> clients = Program.SelectClients([]);
        Assert.Equal(9, clients.Count);
    }

    [Fact]
    public void SelectClients_SingleFlag_SelectsOnlyThatClient()
    {
        IReadOnlyList<McpClient> clients = Program.SelectClients(["--cursor"]);
        Assert.Single(clients);
        Assert.Equal(McpClient.Cursor, clients[0]);
    }

    [Fact]
    public void SelectClients_MultipleFlags_SelectsThose()
    {
        IReadOnlyList<McpClient> clients = Program.SelectClients(["--gemini-cli", "--windsurf"]);
        Assert.Equal(2, clients.Count);
        Assert.Contains(McpClient.GeminiCli, clients);
        Assert.Contains(McpClient.Windsurf, clients);
    }

    [Fact]
    public void SelectClients_VisualStudioFlag_MapsToVs2022()
    {
        IReadOnlyList<McpClient> clients = Program.SelectClients(["--visual-studio"]);
        Assert.Single(clients);
        Assert.Equal(McpClient.VisualStudio2022, clients[0]);
    }
}
