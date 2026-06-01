// McpClient.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

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
