// IClientWriter.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

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

    /// <summary>
    /// True when the target agent appears to be installed on this machine (its well-known config
    /// directory or file exists). "All"/installer runs use this so absent agents are never touched;
    /// an explicit single-agent register ignores it.
    /// </summary>
    bool IsDetected();
}
