// ClaudeCodeWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Registers SnoopMCP in Claude Code's <c>~/.claude.json</c>, under the <c>mcpServers</c> object.
/// </summary>
public sealed class ClaudeCodeWriter : JsonMcpServerWriter
{
    private const string ServersKey = "mcpServers";
    private const string ConfigFileName = ".claude.json";
    private const string ClaudeCodeClientName = "Claude Code";

    /// <summary>Initialises the writer against an explicit config path (used by tests).</summary>
    /// <param name="configPath">Absolute path to <c>.claude.json</c>.</param>
    public ClaudeCodeWriter(string configPath) : base(configPath, ServersKey)
    {
    }

    /// <inheritdoc />
    public override string ClientName => ClaudeCodeClientName;

    /// <summary>Creates a writer targeting the current user's <c>~/.claude.json</c>.</summary>
    public static ClaudeCodeWriter ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new ClaudeCodeWriter(Path.Combine(profile, ConfigFileName));
    }
}
