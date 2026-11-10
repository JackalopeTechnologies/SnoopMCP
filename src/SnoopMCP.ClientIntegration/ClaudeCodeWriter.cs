// ClaudeCodeWriter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

using System.Text.Json.Nodes;

/// <summary>Registers Claude Code's connection and skills without changing developer permissions.</summary>
public sealed class ClaudeCodeWriter : JsonMcpServerWriter
{
    private const int DefaultTimeoutMilliseconds = 120000;
    private const string ServersKey = "mcpServers";
    private const string Name = "Claude Code";
    private const string DirectoryName = ".claude";
    private const string ConfigFileName = ".claude.json";
    private const string SettingsFileName = "settings.json";
    private const string TimeoutKey = "timeout";

    /// <summary>Creates a writer for explicit paths; the settings file is intentionally left untouched.</summary>
    public ClaudeCodeWriter(string configPath, string settingsPath, string skillsDir)
        : base(configPath, ServersKey, configPath, skillsDir: skillsDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(settingsPath);
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
    }

    /// <inheritdoc />
    public override string ClientName => Name;

    /// <summary>Creates a writer targeting the current user's Claude Code configuration.</summary>
    public static ClaudeCodeWriter ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dir = Path.Combine(profile, DirectoryName);
        return new ClaudeCodeWriter(Path.Combine(profile, ConfigFileName),
            Path.Combine(dir, SettingsFileName), Path.Combine(dir, SnoopSkill.SkillsDirectoryName));
    }

    /// <inheritdoc />
    protected override JsonObject BuildEntry(McpEndpoint endpoint)
    {
        JsonObject entry = base.BuildEntry(endpoint);
        entry[TimeoutKey] = DefaultTimeoutMilliseconds;
        return entry;
    }
}
