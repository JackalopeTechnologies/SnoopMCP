// GeminiCliWriter.cs
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
