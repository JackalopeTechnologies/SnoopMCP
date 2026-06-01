// CursorWriter.cs
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
///     Registers SnoopMCP in Cursor's <c>~/.cursor/mcp.json</c> under the <c>mcpServers</c> object as a
///     streamable-HTTP server (a bare <c>{ "url": ... }</c> entry — Cursor needs no <c>type</c> field).
/// </summary>
public sealed class CursorWriter : JsonMcpServerWriter
{
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
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dir = Path.Combine(profile, CursorDirName);
        return new CursorWriter(Path.Combine(dir, ConfigFileName), dir);
    }

    private const string ServersKey = "mcpServers";
    private const string CursorDirName = ".cursor";
    private const string ConfigFileName = "mcp.json";
    private const string CursorClientName = "Cursor";
}
