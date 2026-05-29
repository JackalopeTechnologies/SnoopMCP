// Program.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;

/// <summary>
/// Entry point for the SnoopMCP host. Hosts an MCP server over Streamable HTTP (Kestrel, bound to
/// <c>http://127.0.0.1:6300</c>, endpoint <c>/mcp</c>) so an LLM client can drive the WPF inspection
/// tools. The MCP transport is host-to-client only and is independent of the host-to-payload named
/// pipe, so logging is plain console — stdout is not reserved.
/// </summary>
public static class Program
{
    private const string McpEndpointPattern = "/mcp";
    private const string ServerName = "SnoopMCP — WPF live-inspection MCP server";
    private const int ListenPort = 6300;

    /// <summary>
    /// Builds and runs the MCP Streamable-HTTP host on localhost:6300.
    /// </summary>
    /// <param name="args">Process command-line arguments.</param>
    public static async Task Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(ListenPort));

        builder.Services.AddSingleton<SessionManager>();
        builder.Services.AddSingleton<Injection.ProcessProbe>();
        builder.Services.AddSingleton<IInjectorService, Injection.InjectorService>();

        builder.Services
            .AddMcpServer(options => options.ServerInfo = new Implementation
            {
                Name = ServerName,
                Version = ThisAssembly.InformationalVersion
            })
            .WithHttpTransport(transport => transport.Stateless = true)
            .WithToolsFromAssembly();

        WebApplication app = builder.Build();
        app.MapMcp(McpEndpointPattern);

        await app.RunAsync().ConfigureAwait(false);
    }
}
