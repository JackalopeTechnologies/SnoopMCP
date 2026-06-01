// VsCodeMcpWriter.cs
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
///     Registers SnoopMCP in VS Code's user-profile MCP config (<c>%APPDATA%\Code\User\mcp.json</c>),
///     under the top-level <c>servers</c> object — the location opened by the VS Code
///     <c>MCP: Open User Configuration</c> command.
/// </summary>
public sealed class VsCodeMcpWriter : JsonMcpServerWriter
{
    /// <summary>Initialises the writer against explicit paths (used by tests).</summary>
    /// <param name="configPath">Absolute path to VS Code's user <c>mcp.json</c>.</param>
    /// <param name="detectionPath">Directory whose existence means VS Code is installed.</param>
    public VsCodeMcpWriter(string configPath, string detectionPath)
        : base(configPath, ServersKey, detectionPath)
    {
    }

    /// <inheritdoc />
    public override string ClientName => VsCodeClientName;

    /// <summary>Creates a writer targeting the current user's <c>%APPDATA%\Code\User\mcp.json</c>.</summary>
    public static VsCodeMcpWriter ForCurrentUser()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var config = Path.Combine(appData, CodeDirName, UserDirName, ConfigFileName);
        var detect = Path.Combine(appData, CodeDirName);
        return new VsCodeMcpWriter(config, detect);
    }

    private const string ServersKey = "servers";
    private const string CodeDirName = "Code";
    private const string UserDirName = "User";
    private const string ConfigFileName = "mcp.json";
    private const string VsCodeClientName = "VS Code";
}
