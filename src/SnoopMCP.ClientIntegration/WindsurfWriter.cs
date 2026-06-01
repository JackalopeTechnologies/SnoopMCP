// WindsurfWriter.cs
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
