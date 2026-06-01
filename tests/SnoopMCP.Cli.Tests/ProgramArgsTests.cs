// ProgramArgsTests.cs
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

namespace SnoopMCP.Cli.Tests;

using SnoopMCP.ClientIntegration;
using SnoopMCP.Cli;
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
