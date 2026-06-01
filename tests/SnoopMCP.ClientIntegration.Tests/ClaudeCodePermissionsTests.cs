// ClaudeCodePermissionsTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using System.Text.Json;
using SnoopMCP.ClientIntegration;
using Xunit;

/// <summary>Tests Claude Code register also writes timeout (ms), permissions, and the skill.</summary>
public sealed class ClaudeCodePermissionsTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;
    private readonly string mSettingsPath;
    private readonly string mSkillsDir;

    public ClaudeCodePermissionsTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-ccperm-" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public void Register_WritesTimeoutInMilliseconds()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        writer.Register(McpEndpoint.Default);

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mConfigPath));
        JsonElement entry = doc.RootElement.GetProperty("mcpServers").GetProperty("snoopmcp");
        Assert.Equal("http", entry.GetProperty("type").GetString());
        Assert.Equal(120000, entry.GetProperty("timeout").GetInt32());
    }

    [Fact]
    public void Register_AddsWholeServerPermission()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        writer.Register(McpEndpoint.Default);

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mSettingsPath));
        JsonElement allow = doc.RootElement.GetProperty("permissions").GetProperty("allow");
        bool found = allow.EnumerateArray().Any(e => e.GetString() == "mcp__snoopmcp");
        Assert.True(found);
    }

    [Fact]
    public void Register_PermissionsAreIdempotent()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);
        writer.Register(McpEndpoint.Default);
        writer.Register(McpEndpoint.Default);

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mSettingsPath));
        JsonElement allow = doc.RootElement.GetProperty("permissions").GetProperty("allow");
        int count = allow.EnumerateArray().Count(e => e.GetString() == "mcp__snoopmcp");
        Assert.Equal(1, count);
    }

    [Fact]
    public void Register_InstallsSkill()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        writer.Register(McpEndpoint.Default);

        Assert.True(File.Exists(Path.Combine(mSkillsDir, "snoopmcp-first", "SKILL.md")));
    }

    [Fact]
    public void Register_PreservesExistingPermissions()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(mSettingsPath)!);
        File.WriteAllText(mSettingsPath, """{ "permissions": { "allow": ["Bash(ls:*)"] } }""");
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        writer.Register(McpEndpoint.Default);

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mSettingsPath));
        JsonElement allow = doc.RootElement.GetProperty("permissions").GetProperty("allow");
        var values = allow.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("Bash(ls:*)", values);
        Assert.Contains("mcp__snoopmcp", values);
    }

    [Fact]
    public void Unregister_RemovesPermissionAndSkill()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);
        writer.Register(McpEndpoint.Default);

        writer.Unregister();

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mSettingsPath));
        JsonElement allow = doc.RootElement.GetProperty("permissions").GetProperty("allow");
        Assert.DoesNotContain(allow.EnumerateArray().Select(e => e.GetString()), s => s == "mcp__snoopmcp");
        Assert.False(Directory.Exists(Path.Combine(mSkillsDir, "snoopmcp-first")));
    }
}
