// PayloadEntryPoint.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SnoopMCP.Payload.Tools;

/// <summary>
/// Static entry point invoked by Snoop's <c>ManagedInjector</c> after the payload assembly is loaded
/// into the target process. The injector convention dictates the signature
/// <c>public static int &lt;MethodName&gt;(string args)</c>; we pass the named-pipe name as <c>args</c>.
/// </summary>
public static class PayloadEntryPoint
{
    private static PipeServer? psServer;

    /// <summary>
    /// Called by <c>ManagedInjector</c> from within the target process.
    /// Starts the pipe server on a background task and returns immediately.
    /// </summary>
    /// <param name="args">The named-pipe instance to bind. Must not be empty.</param>
    /// <returns><c>0</c> on success; non-zero when payload initialisation fails.</returns>
    public static int Inject(string args)
    {
        ArgumentException.ThrowIfNullOrEmpty(args);
        int exitCode = 0;
        try
        {
            string pipeName = args.Trim();
            var registry = new ToolRegistry();
            registry.Register(new EchoToolHandler());

            ILogger<PipeServer> logger = NullLogger<PipeServer>.Instance;
            psServer = new PipeServer(pipeName, registry, logger);
            psServer.Start();
        }
        catch (Exception)
        {
            const int injectionFailedExitCode = 1;
            exitCode = injectionFailedExitCode;
        }
        return exitCode;
    }
}
