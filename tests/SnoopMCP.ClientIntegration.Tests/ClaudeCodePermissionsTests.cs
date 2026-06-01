// ClaudeCodePermissionsTests.cs
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

/// <summary>Tests Claude Code register also writes timeout (ms), permissions, and the skill.</summary>
public sealed class ClaudeCodePermissionsTests : IDisposable
{
    public ClaudeCodePermissionsTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-ccperm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, ".claude.json");
        mSettingsPath = Path.Combine(mDir, ".claude", "settings.json");
        mSkillsDir = Path.Combine(mDir, ".claude", "skills");
    }

    private readonly string mConfigPath;
    private readonly string mDir;
    private readonly string mSettingsPath;
    private readonly string mSkillsDir;

    public void Dispose()
    {
        if (Directory.Exists(mDir)) Directory.Delete(mDir, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Register_WritesTimeoutInMilliseconds()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        writer.Register(McpEndpoint.Default);

        using var doc = JsonDocument.Parse(File.ReadAllText(mConfigPath));
        JsonElement entry = doc.RootElement.GetProperty("mcpServers").GetProperty("snoopmcp");
        Assert.Equal("http", entry.GetProperty("type").GetString());
        Assert.Equal(120000, entry.GetProperty("timeout").GetInt32());
    }

    [Fact]
    public void Register_AddsWholeServerPermission()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        writer.Register(McpEndpoint.Default);

        using var doc = JsonDocument.Parse(File.ReadAllText(mSettingsPath));
        JsonElement allow = doc.RootElement.GetProperty("permissions").GetProperty("allow");
        var found = allow.EnumerateArray().Any(e => e.GetString() == "mcp__snoopmcp");
        Assert.True(found);
    }

    [Fact]
    public void Register_PermissionsAreIdempotent()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);
        writer.Register(McpEndpoint.Default);
        writer.Register(McpEndpoint.Default);

        using var doc = JsonDocument.Parse(File.ReadAllText(mSettingsPath));
        JsonElement allow = doc.RootElement.GetProperty("permissions").GetProperty("allow");
        var count = allow.EnumerateArray().Count(e => e.GetString() == "mcp__snoopmcp");
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

        using var doc = JsonDocument.Parse(File.ReadAllText(mSettingsPath));
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

        using var doc = JsonDocument.Parse(File.ReadAllText(mSettingsPath));
        JsonElement allow = doc.RootElement.GetProperty("permissions").GetProperty("allow");
        Assert.DoesNotContain(allow.EnumerateArray().Select(e => e.GetString()), s => s == "mcp__snoopmcp");
        Assert.False(Directory.Exists(Path.Combine(mSkillsDir, "snoopmcp-first")));
    }
}
