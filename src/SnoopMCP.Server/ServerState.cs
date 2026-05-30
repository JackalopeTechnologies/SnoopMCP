// ServerState.cs
// Copyright (c) 2026 Jackalope Technologies

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
