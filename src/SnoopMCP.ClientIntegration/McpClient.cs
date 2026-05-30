// McpClient.cs
// Copyright (c) 2026 Jackalope Technologies

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
    CopilotCli
}
