// CursorWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Registers SnoopMCP in Cursor's <c>~/.cursor/mcp.json</c> under the <c>mcpServers</c> object as a
/// streamable-HTTP server (a bare <c>{ "url": ... }</c> entry — Cursor needs no <c>type</c> field).
/// </summary>
public sealed class CursorWriter : JsonMcpServerWriter
{
    private const string ServersKey = "mcpServers";
    private const string CursorDirName = ".cursor";
    private const string ConfigFileName = "mcp.json";
    private const string CursorClientName = "Cursor";

    /// <summary>Initialises the writer against explicit paths (used by tests).</summary>
    /// <param name="configPath">Absolute path to Cursor's <c>mcp.json</c>.</param>
    /// <param name="detectionPath">Directory whose existence means Cursor is installed.</param>
    public CursorWriter(string configPath, string detectionPath)
        : base(configPath, ServersKey, detectionPath, emitType: false)
    {
    }

    /// <inheritdoc />
    public override string ClientName => CursorClientName;

    /// <summary>Creates a writer targeting the current user's <c>~/.cursor/mcp.json</c>.</summary>
    public static CursorWriter ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dir = Path.Combine(profile, CursorDirName);
        return new CursorWriter(Path.Combine(dir, ConfigFileName), dir);
    }
}
