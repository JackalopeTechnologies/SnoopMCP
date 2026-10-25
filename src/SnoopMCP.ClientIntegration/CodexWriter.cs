// CodexWriter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

/// <summary>Registers Codex's connection and its documented user-level skills.</summary>
public sealed class CodexWriter : TomlMcpServerWriter
{
    private const string Name = "Codex";
    private const string HomeVariable = "CODEX_HOME";
    private const string DirectoryName = ".codex";
    private const string ConfigFileName = "config.toml";
    /// <summary>Creates a writer for explicit configuration and skill paths.</summary>
    public CodexWriter(string configPath, string detectionPath, string? skillsDir = null)
        : base(configPath, detectionPath, skillsDir ?? Path.Combine(detectionPath, SnoopSkill.SkillsDirectoryName))
    {
    }

    /// <inheritdoc />
    public override string ClientName => Name;

    /// <summary>Honors CODEX_HOME for configuration; skills use the documented home discovery path.</summary>
    public static CodexWriter ForCurrentUser()
    {
        string dir = Environment.GetEnvironmentVariable(HomeVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DirectoryName);
        return new CodexWriter(Path.Combine(dir, ConfigFileName), dir, SnoopSkill.SharedUserDirectory);
    }
}
