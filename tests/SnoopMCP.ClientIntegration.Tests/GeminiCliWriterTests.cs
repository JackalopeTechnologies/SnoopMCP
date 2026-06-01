// GeminiCliWriterTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using System.Text.Json;
using ClientIntegration;
using Xunit;

public sealed class GeminiCliWriterTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;

    public GeminiCliWriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-gemini-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, "settings.json");
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
    public void Register_WritesHttpUrlField()
    {
        var writer = new GeminiCliWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        using var doc = JsonDocument.Parse(File.ReadAllText(mConfigPath));
        JsonElement entry = doc.RootElement.GetProperty("mcpServers").GetProperty("snoopmcp");
        Assert.Equal("http://127.0.0.1:6300/mcp", entry.GetProperty("httpUrl").GetString());
        Assert.False(entry.TryGetProperty("type", out _));
        Assert.False(entry.TryGetProperty("url", out _));
    }

    [Fact]
    public void Register_PreservesOtherKeys()
    {
        File.WriteAllText(mConfigPath, "{\"theme\":\"dark\",\"mcpServers\":{\"other\":{\"httpUrl\":\"http://x\"}}}");
        var writer = new GeminiCliWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        using var doc = JsonDocument.Parse(File.ReadAllText(mConfigPath));
        Assert.Equal("dark", doc.RootElement.GetProperty("theme").GetString());
        JsonElement servers = doc.RootElement.GetProperty("mcpServers");
        Assert.True(servers.TryGetProperty("other", out _));
        Assert.True(servers.TryGetProperty("snoopmcp", out _));
    }

    [Fact]
    public void Status_TrueAfterRegister()
    {
        var writer = new GeminiCliWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        Assert.True(writer.GetStatus().IsRegistered);
    }

    [Fact]
    public void Unregister_RemovesEntry()
    {
        var writer = new GeminiCliWriter(mConfigPath, mDir);
        writer.Register(McpEndpoint.Default);

        writer.Unregister();

        Assert.False(writer.GetStatus().IsRegistered);
    }
}
