// WindsurfWriterTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration.Tests;

using System.Text.Json;
using SnoopMCP.ClientIntegration;
using Xunit;

public sealed class WindsurfWriterTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;

    public WindsurfWriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-windsurf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, "mcp_config.json");
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
    public void Register_WritesServerUrlField()
    {
        var writer = new WindsurfWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mConfigPath));
        JsonElement entry = doc.RootElement.GetProperty("mcpServers").GetProperty("snoopmcp");
        Assert.Equal("http://127.0.0.1:6300/mcp", entry.GetProperty("serverUrl").GetString());
        Assert.False(entry.TryGetProperty("type", out _));
    }

    [Fact]
    public void Status_TrueAfterRegister()
    {
        var writer = new WindsurfWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        Assert.True(writer.GetStatus().IsRegistered);
    }

    [Fact]
    public void Unregister_RemovesEntry()
    {
        var writer = new WindsurfWriter(mConfigPath, mDir);
        writer.Register(McpEndpoint.Default);

        writer.Unregister();

        Assert.False(writer.GetStatus().IsRegistered);
    }
}
