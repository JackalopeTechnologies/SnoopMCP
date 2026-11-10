// GrokBuildWriter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

/// <summary>Registers Grok Build's documented TOML connection and shared user skills.</summary>
public sealed class GrokBuildWriter : TomlMcpServerWriter
{
    private const string Name = "Grok Build";
    private const string HomeVariable = "GROK_HOME";
    private const string DirectoryName = ".grok";
    private const string ConfigFileName = "config.toml";
    /// <summary>Creates a writer for explicit configuration and skill paths.</summary>
    public GrokBuildWriter(string configPath, string detectionPath, string? skillsDir = null)
        : base(configPath, detectionPath, skillsDir ?? Path.Combine(detectionPath, SnoopSkill.SkillsDirectoryName))
    {
    }

    /// <inheritdoc />
    public override string ClientName => Name;

    /// <summary>Honors GROK_HOME; shared skills use the documented home discovery path.</summary>
    public static GrokBuildWriter ForCurrentUser()
    {
        string dir = Environment.GetEnvironmentVariable(HomeVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DirectoryName);
        return new GrokBuildWriter(Path.Combine(dir, ConfigFileName), dir, SnoopSkill.SharedUserDirectory);
    }
}
