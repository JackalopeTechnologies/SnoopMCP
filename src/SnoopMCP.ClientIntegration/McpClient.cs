// McpClient.cs
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

/// <summary>The LLM clients SnoopMCP can register itself with.</summary>
public enum McpClient
{
    /// <summary>Anthropic Claude Code (<c>~/.claude.json</c>).</summary>
    ClaudeCode,

    /// <summary>Anthropic Claude Desktop (<c>%APPDATA%\Claude\claude_desktop_config.json</c>).</summary>
    ClaudeDesktop,

    /// <summary>Visual Studio Code (<c>%APPDATA%\Code\User\mcp.json</c>).</summary>
    VsCode,

    /// <summary>OpenAI Codex CLI (<c>~/.codex/config.toml</c>).</summary>
    Codex,

    /// <summary>GitHub Copilot CLI (<c>~/.copilot/mcp-config.json</c>).</summary>
    CopilotCli,

    /// <summary>Cursor (<c>~/.cursor/mcp.json</c>).</summary>
    Cursor,

    /// <summary>Gemini CLI (<c>~/.gemini/settings.json</c>).</summary>
    GeminiCli,

    /// <summary>Windsurf (<c>~/.codeium/windsurf/mcp_config.json</c>).</summary>
    Windsurf,

    /// <summary>Visual Studio 2022 (<c>~/.mcp.json</c>).</summary>
    VisualStudio2022
}
