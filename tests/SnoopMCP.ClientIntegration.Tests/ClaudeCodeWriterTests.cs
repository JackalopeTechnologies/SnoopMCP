// ClaudeCodeWriterTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using SnoopMCP.ClientIntegration;
using Xunit;

public sealed class ClaudeCodeWriterTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;
    private readonly string mSettingsPath;
    private readonly string mSkillsDir;

    public ClaudeCodeWriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-cc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, ".claude.json");
        mSettingsPath = Path.Combine(mDir, ".claude", "settings.json");
        mSkillsDir = Path.Combine(mDir, ".claude", "skills");
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private JsonObject ReadConfig()
    {
        string text = File.ReadAllText(mConfigPath);
        return JsonNode.Parse(text) as JsonObject ?? throw new JsonException("not an object");
    }

    [Fact]
    public void Register_OnMissingFile_CreatesConfigWithHttpEntry()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        JsonObject root = ReadConfig();
        JsonNode entry = root["mcpServers"]!["snoopmcp"]!;
        Assert.Equal("http", (string?)entry["type"]);
        Assert.Equal("http://127.0.0.1:6300/mcp", (string?)entry["url"]);
    }

    [Fact]
    public void Register_PreservesOtherServersAndKeys()
    {
        File.WriteAllText(mConfigPath,
            "{\"mcpServers\":{\"other\":{\"type\":\"http\",\"url\":\"http://x\"}},\"numStartups\":7}");
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        writer.Register(McpEndpoint.Default);

        JsonObject root = ReadConfig();
        Assert.Equal("http://x", (string?)root["mcpServers"]!["other"]!["url"]);
        Assert.Equal(7, (int?)root["numStartups"]);
        Assert.Equal("http://127.0.0.1:6300/mcp", (string?)root["mcpServers"]!["snoopmcp"]!["url"]);
    }

    [Fact]
    public void Register_OnFileWithNoServersSection_AddsTheSection()
    {
        File.WriteAllText(mConfigPath, "{\"numStartups\":3}");
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        JsonObject root = ReadConfig();
        Assert.Equal(3, (int?)root["numStartups"]);
        Assert.Equal("http://127.0.0.1:6300/mcp", (string?)root["mcpServers"]!["snoopmcp"]!["url"]);
    }

    [Fact]
    public void Register_WhenAlreadyPresent_IsIdempotent()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);
        writer.Register(McpEndpoint.Default);

        RegisterResult second = writer.Register(McpEndpoint.Default);

        Assert.True(second.Success);
        JsonObject root = ReadConfig();
        Assert.Single(root["mcpServers"]!.AsObject());
    }

    [Fact]
    public void Register_OnMalformedJson_FailsWithoutThrowing()
    {
        File.WriteAllText(mConfigPath, "{ this is not json ");
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.False(result.Success);
    }

    [Fact]
    public void Unregister_RemovesOnlySnoopMcp_PreservingOthers()
    {
        File.WriteAllText(mConfigPath,
            "{\"mcpServers\":{\"snoopmcp\":{\"type\":\"http\",\"url\":\"http://127.0.0.1:6300/mcp\"}," +
            "\"other\":{\"type\":\"http\",\"url\":\"http://x\"}}}");
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        UnregisterResult result = writer.Unregister();

        Assert.True(result.Success, result.Message);
        JsonObject root = ReadConfig();
        Assert.False(root["mcpServers"]!.AsObject().ContainsKey("snoopmcp"));
        Assert.True(root["mcpServers"]!.AsObject().ContainsKey("other"));
    }

    [Fact]
    public void Unregister_OnMissingFile_IsSuccessfulNoOp()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        UnregisterResult result = writer.Unregister();

        Assert.True(result.Success);
        Assert.False(File.Exists(mConfigPath));
    }

    [Fact]
    public void GetStatus_ReflectsRegistration()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);
        Assert.False(writer.GetStatus().IsRegistered);

        writer.Register(McpEndpoint.Default);

        Assert.True(writer.GetStatus().IsRegistered);
    }
}
