// Program.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

/// <summary>Console entry point: builds the MCP host and runs it until shutdown.</summary>
public static class Program
{
    /// <summary>Builds and runs the MCP host.</summary>
    /// <param name="args">Process command-line arguments.</param>
    public static async Task Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        WebApplication app = ServerHost.Build(args);
        await app.RunAsync().ConfigureAwait(false);
    }
}
