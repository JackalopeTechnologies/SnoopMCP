// ServerState.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host;

/// <summary>The lifecycle state of the in-process MCP server, as surfaced by the tray.</summary>
public enum ServerState
{
    /// <summary>No server instance is bound; the port is free.</summary>
    Stopped,

    /// <summary>A server instance is being built and bound.</summary>
    Starting,

    /// <summary>The server is bound and serving on its port.</summary>
    Running,

    /// <summary>The last start attempt failed (for example, the port was already in use).</summary>
    Faulted
}
