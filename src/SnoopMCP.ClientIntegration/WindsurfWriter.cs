// WindsurfWriter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Registers SnoopMCP in Windsurf's <c>~/.codeium/windsurf/mcp_config.json</c> under the
/// <c>mcpServers</c> object with the remote <c>serverUrl</c> field.
/// </summary>
public sealed class WindsurfWriter : JsonMcpServerWriter
{
    private const string ServersKey = "mcpServers";
    private const string ServerUrlKey = "serverUrl";
    private const string CodeiumDirName = ".codeium";
    private const string WindsurfDirName = "windsurf";
    private const string ConfigFileName = "mcp_config.json";
    private const string WindsurfClientName = "Windsurf";

    /// <summary>Initialises the writer against explicit paths (used by tests).</summary>
    /// <param name="configPath">Absolute path to Windsurf's <c>mcp_config.json</c>.</param>
    /// <param name="detectionPath">Directory whose existence means Windsurf is installed.</param>
    public WindsurfWriter(string configPath, string detectionPath)
        : base(configPath, ServersKey, detectionPath, urlKey: ServerUrlKey, emitType: false)
    {
    }

    /// <inheritdoc />
    public override string ClientName => WindsurfClientName;

    /// <summary>Creates a writer targeting the current user's Windsurf MCP config.</summary>
    public static WindsurfWriter ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dir = Path.Combine(profile, CodeiumDirName, WindsurfDirName);
        return new WindsurfWriter(Path.Combine(dir, ConfigFileName), dir);
    }
}
