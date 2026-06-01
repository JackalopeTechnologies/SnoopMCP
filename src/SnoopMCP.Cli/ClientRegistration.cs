// ClientRegistration.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Cli;

using SnoopMCP.ClientIntegration;

/// <summary>
/// Drives a set of <see cref="IClientWriter"/> over register / unregister / status, logging each
/// outcome and collapsing the per-writer results into a process exit code (0 = all succeeded,
/// 2 = at least one failed).
/// </summary>
public static class ClientRegistration
{
    private const int ExitOk = 0;
    private const int ExitPartialFailure = 2;

    /// <summary>Registers the endpoint in every writer.</summary>
    /// <param name="writers">The set of client writers to register with.</param>
    /// <param name="endpoint">The MCP endpoint to register.</param>
    /// <param name="log">Writer receiving per-client outcome messages.</param>
    public static int RegisterAll(IReadOnlyList<IClientWriter> writers, McpEndpoint endpoint, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(log);
        int failures = 0;
        foreach (IClientWriter writer in writers)
        {
            RegisterResult result = writer.Register(endpoint);
            log.WriteLine(result.Message);
            failures += result.Success ? 0 : 1;
        }
        return failures == 0 ? ExitOk : ExitPartialFailure;
    }

    /// <summary>Removes the SnoopMCP entry from every writer.</summary>
    /// <param name="writers">The set of client writers to unregister from.</param>
    /// <param name="log">Writer receiving per-client outcome messages.</param>
    public static int UnregisterAll(IReadOnlyList<IClientWriter> writers, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(log);
        int failures = 0;
        foreach (IClientWriter writer in writers)
        {
            UnregisterResult result = writer.Unregister();
            log.WriteLine(result.Message);
            failures += result.Success ? 0 : 1;
        }
        return failures == 0 ? ExitOk : ExitPartialFailure;
    }

    /// <summary>Logs each writer's registration status. Always returns success.</summary>
    /// <param name="writers">The set of client writers to report status for.</param>
    /// <param name="log">Writer receiving per-client status messages.</param>
    public static int Status(IReadOnlyList<IClientWriter> writers, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(log);
        foreach (IClientWriter writer in writers)
        {
            StatusResult result = writer.GetStatus();
            log.WriteLine(result.Message);
        }
        return ExitOk;
    }
}
