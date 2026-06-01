// CursorWriterTests.cs
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

using System.Text.Json;
using Xunit;

#endregion

namespace SnoopMCP.ClientIntegration.Tests;

public sealed class CursorWriterTests : IDisposable
{
    public CursorWriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-cursor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, "mcp.json");
    }

    private readonly string mConfigPath;
    private readonly string mDir;

    public void Dispose()
    {
        if (Directory.Exists(mDir)) Directory.Delete(mDir, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Register_WritesBareUrlUnderMcpServers()
    {
        var writer = new CursorWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        using var doc = JsonDocument.Parse(File.ReadAllText(mConfigPath));
        JsonElement entry = doc.RootElement.GetProperty("mcpServers").GetProperty("snoopmcp");
        Assert.Equal("http://127.0.0.1:6300/mcp", entry.GetProperty("url").GetString());
        Assert.False(entry.TryGetProperty("type", out _));
    }

    [Fact]
    public void Register_PreservesOtherServers()
    {
        File.WriteAllText(mConfigPath, "{\"mcpServers\":{\"other\":{\"url\":\"http://x\"}}}");
        var writer = new CursorWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        using var doc = JsonDocument.Parse(File.ReadAllText(mConfigPath));
        JsonElement servers = doc.RootElement.GetProperty("mcpServers");
        Assert.Equal("http://x", servers.GetProperty("other").GetProperty("url").GetString());
        Assert.True(servers.TryGetProperty("snoopmcp", out _));
    }

    [Fact]
    public void Status_TrueAfterRegister()
    {
        var writer = new CursorWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        Assert.True(writer.GetStatus().IsRegistered);
    }

    [Fact]
    public void Unregister_RemovesEntry()
    {
        var writer = new CursorWriter(mConfigPath, mDir);
        writer.Register(McpEndpoint.Default);

        writer.Unregister();

        Assert.False(writer.GetStatus().IsRegistered);
    }
}
