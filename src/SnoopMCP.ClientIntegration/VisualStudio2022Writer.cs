// VisualStudio2022Writer.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Registers SnoopMCP in Visual Studio 2022's global <c>~/.mcp.json</c> under the <c>servers</c> object
/// as a streamable-HTTP server (a bare <c>{ "url": ... }</c> entry — the documented minimal remote
/// form). Detection prefers the Visual Studio data directory under <c>%LOCALAPPDATA%</c>.
/// </summary>
public sealed class VisualStudio2022Writer : JsonMcpServerWriter
{
    private const string ServersKey = "servers";
    private const string ConfigFileName = ".mcp.json";
    private const string VsDataDirName = "Microsoft\\VisualStudio";
    private const string VsClientName = "Visual Studio 2022";

    /// <summary>Initialises the writer against explicit paths (used by tests).</summary>
    /// <param name="configPath">Absolute path to <c>~/.mcp.json</c>.</param>
    /// <param name="detectionPath">Directory whose existence means Visual Studio 2022 is installed.</param>
    public VisualStudio2022Writer(string configPath, string detectionPath)
        : base(configPath, ServersKey, detectionPath, emitType: false)
    {
    }

    /// <inheritdoc />
    public override string ClientName => VsClientName;

    /// <summary>Creates a writer targeting the current user's global <c>~/.mcp.json</c>.</summary>
    public static VisualStudio2022Writer ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string config = Path.Combine(profile, ConfigFileName);
        string detect = Path.Combine(localAppData, VsDataDirName);
        return new VisualStudio2022Writer(config, detect);
    }
}
