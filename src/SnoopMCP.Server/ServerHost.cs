// ServerHost.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#region Usings

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using SnoopMCP.Host.Injection;
using SnoopMCP.Host.Tools;

#endregion

namespace SnoopMCP.Host;

/// <summary>
///     Builds the SnoopMCP MCP server as a configured-but-not-started <see cref="WebApplication" />
///     (Kestrel on http://127.0.0.1:6300, endpoints <c>/mcp</c> and <c>/health</c>). The WPF tray shell
///     owns the process and drives the returned app with StartAsync/StopAsync; tests build it directly.
/// </summary>
public static class ServerHost
{
    /// <summary>Builds the MCP Streamable-HTTP host on localhost without starting it.</summary>
    /// <param name="args">Process command-line arguments.</param>
    /// <param name="port">
    ///     Loopback port to listen on; defaults to <see cref="ListenPort" />. Pass 0 to bind a free port (used
    ///     by tests).
    /// </param>
    /// <returns>The configured, not-yet-started web application.</returns>
    public static WebApplication Build(string[] args, int port = ListenPort)
    {
        ArgumentNullException.ThrowIfNull(args);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(port));

        builder.Services.AddSingleton<SessionManager>();
        builder.Services.AddSingleton<IInjectorService, InjectorService>();

        builder.Services
            .AddMcpServer(options => options.ServerInfo = new Implementation
            {
                Name = ServerName, Version = ThisAssembly.InformationalVersion
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
            if (context.Request.Path == RootPath) context.Request.Path = McpEndpointPattern;
            await next(context);
        });
        app.UseRouting();

        app.MapMcp(McpEndpointPattern);
        app.MapGet(HealthEndpointPattern, (SessionManager session) =>
            Results.Ok(HealthStatus.Create(ThisAssembly.InformationalVersion, session.IsAttached)));

        return app;
    }

    private const string McpEndpointPattern = "/mcp";
    private const string HealthEndpointPattern = "/health";
    private const string RootPath = "/";
    private const string ServerName = "SnoopMCP — WPF live-inspection MCP server";
    private const int ListenPort = 6300;
}
