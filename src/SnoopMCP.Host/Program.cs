// Program.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Entry point for the SnoopMCP host. Hosts an MCP server over stdio so an LLM client can drive
/// the WPF inspection tools. All logging is routed to stderr because stdout is reserved for the
/// MCP transport's framed JSON.
/// </summary>
public static class Program
{
    /// <summary>
    /// Builds and runs the MCP stdio host.
    /// </summary>
    /// <param name="args">Process command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        using IHost host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
        return 0;
    }
}
