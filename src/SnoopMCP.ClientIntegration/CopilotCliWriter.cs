// CopilotCliWriter.cs
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

using System.Text.Json.Nodes;

#endregion

namespace SnoopMCP.ClientIntegration;

/// <summary>
///     Registers SnoopMCP in GitHub Copilot CLI's <c>~/.copilot/mcp-config.json</c>, under the
///     <c>mcpServers</c> object, as a streamable-HTTP server
///     (<c>{ "type": "http", "url": ..., "tools": ["*"] }</c>). The config directory honours the
///     <c>COPILOT_HOME</c> environment variable when set.
/// </summary>
public sealed class CopilotCliWriter : JsonMcpServerWriter
{
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
    ///     Creates a writer targeting the current user's Copilot CLI config
    ///     (<c>$COPILOT_HOME\mcp-config.json</c>, falling back to <c>~/.copilot\mcp-config.json</c>).
    /// </summary>
    public static CopilotCliWriter ForCurrentUser()
    {
        var home = Environment.GetEnvironmentVariable(CopilotHomeEnvVar) is { Length: > 0 } copilotHome
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

    private const string ServersKey = "mcpServers";
    private const string HttpType = "http";
    private const string ToolsKey = "tools";
    private const string ToolsWildcard = "*";
    private const string CopilotHomeEnvVar = "COPILOT_HOME";
    private const string CopilotDirName = ".copilot";
    private const string ConfigFileName = "mcp-config.json";
    private const string CopilotCliClientName = "Copilot CLI";
}
