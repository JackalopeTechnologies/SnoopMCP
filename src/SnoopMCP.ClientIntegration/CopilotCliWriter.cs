// CopilotCliWriter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

using System.Text.Json.Nodes;

/// <summary>
/// Registers SnoopMCP in GitHub Copilot CLI's <c>~/.copilot/mcp-config.json</c>, under the
/// <c>mcpServers</c> object, as a streamable-HTTP server
/// (<c>{ "type": "http", "url": ..., "tools": ["*"] }</c>). The config directory honours the
/// <c>COPILOT_HOME</c> environment variable when set.
/// </summary>
public sealed class CopilotCliWriter : JsonMcpServerWriter
{
    private const string ServersKey = "mcpServers";
    private const string HttpType = "http";
    private const string ToolsKey = "tools";
    private const string ToolsWildcard = "*";
    private const string CopilotHomeEnvVar = "COPILOT_HOME";
    private const string CopilotDirName = ".copilot";
    private const string ConfigFileName = "mcp-config.json";
    private const string CopilotCliClientName = "Copilot CLI";

    /// <summary>Initialises the writer against explicit paths (used by tests).</summary>
    /// <param name="configPath">Absolute path to Copilot CLI's <c>mcp-config.json</c>.</param>
    /// <param name="detectionPath">Directory whose existence means Copilot CLI is installed.</param>
    public CopilotCliWriter(string configPath, string detectionPath)
        : base(configPath, ServersKey, detectionPath, serverType: HttpType)
    {
    }

    /// <inheritdoc />
    public override string ClientName => CopilotCliClientName;

    /// <summary>
    /// Creates a writer targeting the current user's Copilot CLI config
    /// (<c>$COPILOT_HOME\mcp-config.json</c>, falling back to <c>~/.copilot\mcp-config.json</c>).
    /// </summary>
    public static CopilotCliWriter ForCurrentUser()
    {
        string home = Environment.GetEnvironmentVariable(CopilotHomeEnvVar) is { Length: > 0 } copilotHome
            ? copilotHome
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), CopilotDirName);
        return new CopilotCliWriter(Path.Combine(home, ConfigFileName), home);
    }

    /// <inheritdoc />
    /// <remarks>Adds <c>"tools": ["*"]</c> so every SnoopMCP tool is enabled.</remarks>
    protected override JsonObject BuildEntry(McpEndpoint endpoint)
    {
        JsonObject entry = base.BuildEntry(endpoint);
        entry[ToolsKey] = new JsonArray { ToolsWildcard };
        return entry;
    }
}
