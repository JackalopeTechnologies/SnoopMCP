// ServerHost.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using SnoopMCP.Host.Tools;

/// <summary>
/// Builds the SnoopMCP MCP server as a configured-but-not-started <see cref="WebApplication"/>
/// (Kestrel on http://127.0.0.1:6300, endpoints <c>/mcp</c> and <c>/health</c>). The WPF tray shell
/// owns the process and drives the returned app with StartAsync/StopAsync; tests build it directly.
/// </summary>
public static class ServerHost
{
    private const string McpEndpointPattern = "/mcp";
    private const string HealthEndpointPattern = "/health";
    private const string ServerName = "SnoopMCP — WPF live-inspection MCP server";
    private const int ListenPort = 6300;

    /// <summary>Builds the MCP Streamable-HTTP host on localhost:6300 without starting it.</summary>
    /// <param name="args">Process command-line arguments.</param>
    /// <returns>The configured, not-yet-started web application.</returns>
    public static WebApplication Build(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(ListenPort));

        builder.Services.AddSingleton<SessionManager>();
        builder.Services.AddSingleton<IInjectorService, Injection.InjectorService>();

        builder.Services
            .AddMcpServer(options => options.ServerInfo = new Implementation
            {
                Name = ServerName,
                Version = ThisAssembly.InformationalVersion
            })
            .WithHttpTransport(transport => transport.Stateless = true)
            .WithToolsFromAssembly(typeof(McpTools).Assembly);

        WebApplication app = builder.Build();
        app.MapMcp(McpEndpointPattern);
        app.MapGet(HealthEndpointPattern, (SessionManager session) =>
            Results.Ok(HealthStatus.Create(ThisAssembly.InformationalVersion, session.IsAttached)));

        return app;
    }
}
