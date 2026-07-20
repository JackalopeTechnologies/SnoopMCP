// ServerHost.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using Automation;
using Logging;
using Tools;

/// <summary>
/// Builds the SnoopMCP MCP server as a configured-but-not-started <see cref="WebApplication"/>
/// (Kestrel on http://127.0.0.1:6300, endpoints <c>/mcp</c> and <c>/health</c>). The WPF tray shell
/// owns the process and drives the returned app with StartAsync/StopAsync; tests build it directly.
/// </summary>
public static class ServerHost
{
    private const string McpEndpointPattern = "/mcp";
    private const string HealthEndpointPattern = "/health";
    private const string RootPath = "/";
    private const string ServerName = "SnoopMCP — WPF live-inspection MCP server";
    private const int ListenPort = 6300;
    private const string AspNetCoreLogCategory = "Microsoft.AspNetCore";

    /// <summary>Builds the MCP Streamable-HTTP host on localhost without starting it.</summary>
    /// <param name="args">Process command-line arguments.</param>
    /// <param name="port">Loopback port to listen on; defaults to <see cref="ListenPort"/>. Pass 0 to bind a free port (used by tests).</param>
    /// <param name="logPath">Absolute path for the server log file; defaults to
    /// <see cref="FileLoggerProvider.DefaultLogPath"/> under %LOCALAPPDATA%. Tests pass a temp path so they never touch the real log.</param>
    /// <param name="gate">The interaction gate instance to register; defaults to <see cref="InteractionGate.ForCurrentUser"/>.
    /// Tests pass an explicit gate backed by a temp state path so they never touch the real per-user state file.</param>
    /// <returns>The configured, not-yet-started web application.</returns>
    public static WebApplication Build(string[] args, int port = ListenPort, string? logPath = null, InteractionGate? gate = null)
    {
        ArgumentNullException.ThrowIfNull(args);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(port));

        // The tray host is windowless, so the default Console logger has nowhere to surface errors.
        // Persist the MCP server's ILogger output - crucially the SDK's full exception detail when a
        // tool handler throws, which it logs before returning a sanitised message to the client - to
        // a file beside the host log. ASP.NET's per-request chatter is dropped to keep the rolling
        // file focused on warnings, errors, and lifecycle.
        builder.Logging.AddProvider(new FileLoggerProvider(logPath ?? FileLoggerProvider.DefaultLogPath()));
        builder.Logging.AddFilter(AspNetCoreLogCategory, LogLevel.Warning);

        InteractionGate interactionGate = gate ?? InteractionGate.ForCurrentUser();
        builder.Services.AddSingleton(interactionGate);
        builder.Services.AddSingleton<ElementHandleCache>();
        builder.Services.AddSingleton<IUiaDriver, UiaDriver>();
        builder.Services.AddSingleton<IScreenCapture, PrintWindowCapture>();

        builder.Services.AddSingleton<SessionManager>();
        builder.Services.AddSingleton<IInjectorService, Injection.InjectorService>();

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = ServerName,
                    Version = ThisAssembly.InformationalVersion
                };

                // Give every failure a typed, self-describing message. Without this the SDK replaces
                // anything that is not an McpException with "An error occurred invoking '…'", leaving
                // the real cause only in the server log - including argument-binding and serialization
                // failures, which occur outside any tool body and so cannot be caught within one.
                options.Filters.Request.CallToolFilters.Add(ToolErrorFilter.Create());
            })
            .WithHttpTransport(transport => transport.Stateless = true)
            .WithToolsFromAssembly(typeof(McpTools).Assembly);

        WebApplication app = builder.Build();

        // A client pointed at the bare site root (/) instead of /mcp still reaches the MCP handler:
        // rewrite root requests to the MCP endpoint, transparently for every HTTP method. This must
        // run BEFORE endpoint routing selects a handler, so the explicit UseRouting() below pins this
        // middleware ahead of it (WebApplication otherwise auto-inserts routing at the pipeline start,
        // which would 404 on "/" before the rewrite ran).
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == RootPath)
            {
                context.Request.Path = McpEndpointPattern;
            }
            await next(context);
        });
        app.UseRouting();

        app.MapMcp(McpEndpointPattern);
        app.MapGet(HealthEndpointPattern, (SessionManager session, InteractionGate healthGate) =>
            Results.Ok(HealthStatus.Create(
                ThisAssembly.InformationalVersion, session.IsAttached, healthGate.IsEnabled)));

        return app;
    }
}
