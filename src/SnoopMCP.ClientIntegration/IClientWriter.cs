// IClientWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Registers, removes, and reports the SnoopMCP MCP server entry in one LLM client's configuration
/// file, idempotently and without disturbing the client's other settings.
/// </summary>
public interface IClientWriter
{
    /// <summary>Display name of the client (for logs / status), e.g. <c>Claude Code</c>.</summary>
    string ClientName { get; }

    /// <summary>Adds or updates the SnoopMCP entry; preserves all other content.</summary>
    RegisterResult Register(McpEndpoint endpoint);

    /// <summary>Removes the SnoopMCP entry if present; a missing file or entry is a successful no-op.</summary>
    UnregisterResult Unregister();

    /// <summary>Reports whether the SnoopMCP entry is currently present with the expected URL.</summary>
    StatusResult GetStatus();
}
