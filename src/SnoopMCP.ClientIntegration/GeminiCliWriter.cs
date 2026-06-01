// GeminiCliWriter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Registers SnoopMCP in Gemini CLI's <c>~/.gemini/settings.json</c> under the <c>mcpServers</c> object
/// with the streamable-HTTP <c>httpUrl</c> field.
/// </summary>
public sealed class GeminiCliWriter : JsonMcpServerWriter
{
    private const string ServersKey = "mcpServers";
    private const string HttpUrlKey = "httpUrl";
    private const string GeminiDirName = ".gemini";
    private const string ConfigFileName = "settings.json";
    private const string GeminiClientName = "Gemini CLI";

    /// <summary>Initialises the writer against explicit paths (used by tests).</summary>
    /// <param name="configPath">Absolute path to Gemini CLI's <c>settings.json</c>.</param>
    /// <param name="detectionPath">Directory whose existence means Gemini CLI is installed.</param>
    public GeminiCliWriter(string configPath, string detectionPath)
        : base(configPath, ServersKey, detectionPath, urlKey: HttpUrlKey, emitType: false)
    {
    }

    /// <inheritdoc />
    public override string ClientName => GeminiClientName;

    /// <summary>Creates a writer targeting the current user's <c>~/.gemini/settings.json</c>.</summary>
    public static GeminiCliWriter ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dir = Path.Combine(profile, GeminiDirName);
        return new GeminiCliWriter(Path.Combine(dir, ConfigFileName), dir);
    }
}
