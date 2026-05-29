// VsCodeMcpWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Registers SnoopMCP in VS Code's user-profile MCP config (<c>%APPDATA%\Code\User\mcp.json</c>),
/// under the top-level <c>servers</c> object — the location opened by the VS Code
/// <c>MCP: Open User Configuration</c> command.
/// </summary>
public sealed class VsCodeMcpWriter : JsonMcpServerWriter
{
    private const string ServersKey = "servers";
    private const string CodeDirName = "Code";
    private const string UserDirName = "User";
    private const string ConfigFileName = "mcp.json";
    private const string VsCodeClientName = "VS Code";

    /// <summary>Initialises the writer against an explicit config path (used by tests).</summary>
    /// <param name="configPath">Absolute path to VS Code's user <c>mcp.json</c>.</param>
    public VsCodeMcpWriter(string configPath) : base(configPath, ServersKey)
    {
    }

    /// <inheritdoc />
    public override string ClientName => VsCodeClientName;

    /// <summary>Creates a writer targeting the current user's <c>%APPDATA%\Code\User\mcp.json</c>.</summary>
    public static VsCodeMcpWriter ForCurrentUser()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new VsCodeMcpWriter(Path.Combine(appData, CodeDirName, UserDirName, ConfigFileName));
    }
}
