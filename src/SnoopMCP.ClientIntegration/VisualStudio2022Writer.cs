// VisualStudio2022Writer.cs
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
