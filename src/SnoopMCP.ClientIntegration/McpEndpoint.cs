// McpEndpoint.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Identity of the SnoopMCP MCP server as written into a client's config: the entry name, the
/// transport type, and the HTTP URL. <see cref="Default"/> is the canonical SnoopMCP endpoint.
/// </summary>
/// <param name="Name">The config entry key (e.g. <c>snoopmcp</c>).</param>
/// <param name="Type">The MCP transport type (e.g. <c>http</c>).</param>
/// <param name="Url">The Streamable-HTTP endpoint URL.</param>
public sealed record McpEndpoint(string Name, string Type, string Url)
{
    private const string DefaultName = "snoopmcp";
    private const string DefaultType = "http";
    private const string DefaultUrl = "http://127.0.0.1:6300/mcp";

    /// <summary>The canonical SnoopMCP HTTP endpoint registered into every supported client.</summary>
    public static McpEndpoint Default { get; } = new(DefaultName, DefaultType, DefaultUrl);
}
