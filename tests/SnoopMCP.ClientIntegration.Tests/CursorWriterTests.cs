// CursorWriterTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using System.Text.Json;
using ClientIntegration;
using Xunit;

public sealed class CursorWriterTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;

    public CursorWriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-cursor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, "mcp.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
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
