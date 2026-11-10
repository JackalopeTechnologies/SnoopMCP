// LegacyClientPaths.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.InstallerActions;

using System.IO;

/// <summary>Paths touched by the cleanup shipped in the v1.4.2 and earlier installers.</summary>
public static class LegacyClientPaths
{
    private const string ClaudeConfig = ".claude.json";
    private const string ClaudePermissions = ".claude/settings.json";
    private const string CodexConfig = ".codex/config.toml";
    private const string CursorConfig = ".cursor/mcp.json";
    private const string GeminiConfig = ".gemini/settings.json";
    private const string CopilotConfig = ".copilot/mcp-config.json";
    private const string WindsurfConfig = ".codeium/windsurf/mcp_config.json";
    private const string VisualStudioConfig = ".mcp.json";
    private const string DesktopConfig = "Claude/claude_desktop_config.json";
    private const string VsCodeConfig = "Code/User/mcp.json";
    private const string FirstSkill = ".claude/skills/snoopmcp-first";
    private const string UiaSkill = ".claude/skills/snoopmcp-uia";
    private static readonly string[] smProfileFiles =
    [
        ClaudeConfig, ClaudePermissions, CodexConfig, CursorConfig,
        GeminiConfig, CopilotConfig, WindsurfConfig, VisualStudioConfig
    ];
    private static readonly string[] smRoamingFiles = [DesktopConfig, VsCodeConfig];
    private static readonly string[] smSkillDirectories = [FirstSkill, UiaSkill];
    private const string CopilotHomeVariable = "COPILOT_HOME";
    private const string CopilotConfigName = "mcp-config.json";

    /// <summary>Includes permission settings and all configuration files legacy cleanup could edit.</summary>
    public static IEnumerable<string> Configurations(string profile, string roaming)
    {
        profile = profile ?? throw new ArgumentNullException(nameof(profile));
        roaming = roaming ?? throw new ArgumentNullException(nameof(roaming));
        var result = smProfileFiles.Select(path => Path.Combine(profile, path))
            .Concat(smRoamingFiles.Select(path => Path.Combine(roaming, path))).ToList();
        string? copilot = Environment.GetEnvironmentVariable(CopilotHomeVariable);
        if (!string.IsNullOrEmpty(copilot))
        {
            result.Add(Path.Combine(copilot, CopilotConfigName));
        }
        return result;
    }

    /// <summary>Includes complete skill folders so personal supporting files survive recursive legacy cleanup.</summary>
    public static IEnumerable<string> Skills(string profile)
    {
        profile = profile ?? throw new ArgumentNullException(nameof(profile));
        return smSkillDirectories.Select(path => Path.Combine(profile, path));
    }
}
