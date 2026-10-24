// CodexWriterTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using ClientIntegration;
using Tomlyn;
using Tomlyn.Model;
using Xunit;

public sealed class CodexWriterTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;

    public CodexWriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-codex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, "config.toml");
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private TomlTable ReadConfig() => Toml.ToModel(File.ReadAllText(mConfigPath));

    private static TomlTable Servers(TomlTable root) => (TomlTable)root["mcp_servers"];

    private static string EntryUrl(TomlTable root, string name) => (string)((TomlTable)Servers(root)[name])["url"];

    [Fact]
    public void Register_OnMissingFile_CreatesConfigWithUrlEntry()
    {
        var writer = new CodexWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        Assert.Equal("http://127.0.0.1:6300/mcp", EntryUrl(ReadConfig(), "snoopmcp"));
    }

    [Fact]
    public void Register_DoesNotAddExperimentalFeatureFlags()
    {
        var writer = new CodexWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        TomlTable root = ReadConfig();
        Assert.False(root.ContainsKey("features"));
    }

    [Fact]
    public void Register_PreservesOtherTablesAndTopLevelKeys()
    {
        File.WriteAllText(mConfigPath, "model = \"gpt-5\"\n\n[mcp_servers.other]\nurl = \"http://x\"\n");
        var writer = new CodexWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        TomlTable root = ReadConfig();
        Assert.Equal("gpt-5", (string)root["model"]);
        Assert.Equal("http://x", EntryUrl(root, "other"));
        Assert.Equal("http://127.0.0.1:6300/mcp", EntryUrl(root, "snoopmcp"));
    }

    [Fact]
    public void Register_OnFileWithNoServersSection_AddsTheSection()
    {
        File.WriteAllText(mConfigPath, "model = \"gpt-5\"\n");
        var writer = new CodexWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        TomlTable root = ReadConfig();
        Assert.Equal("gpt-5", (string)root["model"]);
        Assert.Equal("http://127.0.0.1:6300/mcp", EntryUrl(root, "snoopmcp"));
    }

    [Fact]
    public void Register_WhenAlreadyPresent_IsIdempotent()
    {
        var writer = new CodexWriter(mConfigPath, mDir);
        writer.Register(McpEndpoint.Default);

        RegisterResult second = writer.Register(McpEndpoint.Default);

        Assert.True(second.Success);
        Assert.Single(Servers(ReadConfig()));
    }

    [Fact]
    public void Register_OnMalformedToml_FailsWithoutThrowing()
    {
        File.WriteAllText(mConfigPath, "this is not = valid = toml =");
        var writer = new CodexWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.False(result.Success);
    }

    [Fact]
    public void Unregister_RemovesOnlySnoopMcp_PreservingOthers()
    {
        File.WriteAllText(mConfigPath,
            "[mcp_servers.snoopmcp]\nurl = \"http://127.0.0.1:6300/mcp\"\n\n[mcp_servers.other]\nurl = \"http://x\"\n");
        var writer = new CodexWriter(mConfigPath, mDir);

        UnregisterResult result = writer.Unregister();

        Assert.True(result.Success, result.Message);
        TomlTable servers = Servers(ReadConfig());
        Assert.False(servers.ContainsKey("snoopmcp"));
        Assert.True(servers.ContainsKey("other"));
    }

    [Fact]
    public void Unregister_OnMissingFile_IsSuccessfulNoOp()
    {
        var writer = new CodexWriter(mConfigPath, mDir);

        UnregisterResult result = writer.Unregister();

        Assert.True(result.Success);
        Assert.False(File.Exists(mConfigPath));
    }

    [Fact]
    public void GetStatus_ReflectsRegistration()
    {
        var writer = new CodexWriter(mConfigPath, mDir);
        Assert.False(writer.GetStatus().IsRegistered);

        writer.Register(McpEndpoint.Default);

        Assert.True(writer.GetStatus().IsRegistered);
    }

    [Fact]
    public void Register_WhenUrlChanged_OverwritesExistingEntry()
    {
        File.WriteAllText(mConfigPath, "[mcp_servers.snoopmcp]\nurl = \"http://old:1/mcp\"\n");
        var writer = new CodexWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        Assert.Equal("http://127.0.0.1:6300/mcp", EntryUrl(ReadConfig(), "snoopmcp"));
    }

    [Fact]
    public void Unregister_WhenEntryAbsentButFileExists_IsSuccessfulNoOp()
    {
        File.WriteAllText(mConfigPath, "[mcp_servers.other]\nurl = \"http://x\"\n");
        var writer = new CodexWriter(mConfigPath, mDir);

        UnregisterResult result = writer.Unregister();

        Assert.True(result.Success, result.Message);
        Assert.True(Servers(ReadConfig()).ContainsKey("other"));
    }

    [Fact]
    public void Register_OnModelConflict_FailsWithoutThrowing()
    {
        File.WriteAllText(mConfigPath,
            "[mcp_servers]\nsnoopmcp = \"not-a-table\"\n\n[mcp_servers.snoopmcp]\nurl = \"http://y\"\n");
        var writer = new CodexWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.False(result.Success);
        Assert.False(writer.GetStatus().IsRegistered);
    }

    [Fact]
    public void Register_InstallsCanonicalSkillsAndPreservesPoliciesOnRepeat()
    {
        File.WriteAllText(mConfigPath, """
            model = "my-model"
            [features]
            experimental_use_rmcp_client = false
            [mcp_servers.snoopmcp]
            url = "http://127.0.0.1:6300/mcp"
            enabled = false
            enabled_tools = ["inspect_binding"]
            disabled_tools = ["execute_command"]
            tool_timeout_sec = 120
            [mcp_servers.snoopmcp.tools.inspect_binding]
            approval_mode = "prompt"
            [[skills.config]]
            path = "custom/SKILL.md"
            enabled = false
            """);
        var writer = new CodexWriter(mConfigPath, mDir);
        Assert.True(writer.Register(McpEndpoint.Default).Success);
        string first = File.ReadAllText(mConfigPath);
        Assert.True(writer.Register(McpEndpoint.Default).Success);
        Assert.Equal(first, File.ReadAllText(mConfigPath));
        var entry = (TomlTable)Servers(ReadConfig())["snoopmcp"];
        Assert.False((bool)entry["enabled"]);
        Assert.Equal(120L, entry["tool_timeout_sec"]);
        Assert.False((bool)((TomlTable)ReadConfig()["features"])["experimental_use_rmcp_client"]);
        Assert.Contains("approval_mode = \"prompt\"", first);
        Assert.Contains("custom/SKILL.md", first);
        foreach ((string name, string body) in SnoopSkill.Definitions)
        {
            Assert.Equal(body, File.ReadAllText(Path.Combine(mDir, "skills", name, "SKILL.md")));
        }
    }

    [Fact]
    public void Unregister_WithoutConfig_StillRemovesOwnedSkills()
    {
        var writer = new CodexWriter(mConfigPath, mDir);
        Assert.True(writer.Register(McpEndpoint.Default).Success);
        File.Delete(mConfigPath);
        Assert.True(writer.Unregister().Success);
        Assert.False(File.Exists(Path.Combine(mDir, "skills", SnoopSkill.FirstSkillName, "SKILL.md")));
        Assert.True(writer.Unregister().Success);
    }

    [Fact]
    public void Register_SkillConflictReportsPartialFailureAndPreservesContent()
    {
        string skill = Path.Combine(mDir, "skills", SnoopSkill.FirstSkillName, "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(skill) ?? throw new InvalidOperationException());
        File.WriteAllText(skill, "My custom guidance");
        var writer = new CodexWriter(mConfigPath, mDir);
        Assert.False(writer.Register(McpEndpoint.Default).Success);
        Assert.True(writer.GetStatus().IsRegistered);
        Assert.Equal("My custom guidance", File.ReadAllText(skill));
        Assert.True(writer.Unregister().Success);
        Assert.Equal("My custom guidance", File.ReadAllText(skill));
    }

    [Fact]
    public void Unregister_PreservesReplacementServer()
    {
        File.WriteAllText(mConfigPath, "[mcp_servers.snoopmcp]\nurl = \"https://my-server/mcp\"\n");
        var writer = new CodexWriter(mConfigPath, mDir);
        Assert.True(writer.Unregister().Success);
        Assert.Equal("https://my-server/mcp", EntryUrl(ReadConfig(), "snoopmcp"));
    }
}
