// CopilotCliWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Registers SnoopMCP in GitHub Copilot CLI's <c>~/.copilot/mcp-config.json</c>, under the
/// <c>mcpServers</c> object, as an SSE server (<c>{ "type": "sse", "url": ... }</c>). The config
/// directory honours the <c>COPILOT_HOME</c> environment variable when set.
/// </summary>
public sealed class CopilotCliWriter : JsonMcpServerWriter
{
    private const string ServersKey = "mcpServers";
    private const string SseType = "sse";
    private const string CopilotHomeEnvVar = "COPILOT_HOME";
    private const string CopilotDirName = ".copilot";
    private const string ConfigFileName = "mcp-config.json";
    private const string CopilotCliClientName = "Copilot CLI";

    /// <summary>Initialises the writer against an explicit config path (used by tests).</summary>
    /// <param name="configPath">Absolute path to Copilot CLI's <c>mcp-config.json</c>.</param>
    public CopilotCliWriter(string configPath) : base(configPath, ServersKey, SseType)
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
        return new CopilotCliWriter(Path.Combine(home, ConfigFileName));
    }
}
