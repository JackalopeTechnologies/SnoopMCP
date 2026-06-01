// ClientRegistration.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Console-free core for registering, unregistering, and reporting the SnoopMCP entry across the
/// supported LLM clients. Callers (the management CLI and the tray host) select clients or supply
/// writers and receive the per-client result records; presentation (Console, message box) is left to
/// the caller. The CLI's own <c>ClientRegistration</c> is a thin Console-printing wrapper over this.
/// </summary>
public static class ClientRegistration
{
    /// <summary>Every client SnoopMCP can register itself with.</summary>
    public static IReadOnlyList<McpClient> AllClients { get; } = Enum.GetValues<McpClient>();

    /// <summary>Creates the configuration writer for a single client, targeting the current user.</summary>
    public static IClientWriter CreateWriter(McpClient client)
    {
        return client switch
        {
            McpClient.ClaudeCode => ClaudeCodeWriter.ForCurrentUser(),
            McpClient.ClaudeDesktop => ClaudeDesktopWriter.ForCurrentUser(),
            McpClient.VsCode => VsCodeMcpWriter.ForCurrentUser(),
            McpClient.Codex => CodexWriter.ForCurrentUser(),
            McpClient.CopilotCli => CopilotCliWriter.ForCurrentUser(),
            McpClient.Cursor => CursorWriter.ForCurrentUser(),
            McpClient.GeminiCli => GeminiCliWriter.ForCurrentUser(),
            McpClient.Windsurf => WindsurfWriter.ForCurrentUser(),
            McpClient.VisualStudio2022 => VisualStudio2022Writer.ForCurrentUser(),
            _ => throw new ArgumentOutOfRangeException(nameof(client), client, "Unknown client.")
        };
    }

    /// <summary>Creates the configuration writers for a set of clients, targeting the current user.</summary>
    public static IReadOnlyList<IClientWriter> CreateWriters(IEnumerable<McpClient> clients)
    {
        ArgumentNullException.ThrowIfNull(clients);
        return clients.Select(CreateWriter).ToArray();
    }

    /// <summary>The writers for every client whose agent is detected as installed on this machine.</summary>
    public static IReadOnlyList<IClientWriter> DetectedWriters()
    {
        return AllClients.Select(CreateWriter).Where(w => w.IsDetected()).ToArray();
    }

    /// <summary>Registers the endpoint with every supplied writer.</summary>
    public static IReadOnlyList<RegisterResult> Register(IReadOnlyList<IClientWriter> writers, McpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(endpoint);
        return writers.Select(w => w.Register(endpoint)).ToArray();
    }

    /// <summary>Removes the SnoopMCP entry from every supplied writer.</summary>
    public static IReadOnlyList<UnregisterResult> Unregister(IReadOnlyList<IClientWriter> writers)
    {
        ArgumentNullException.ThrowIfNull(writers);
        return writers.Select(w => w.Unregister()).ToArray();
    }

    /// <summary>Reports the SnoopMCP registration status of every supplied writer.</summary>
    public static IReadOnlyList<StatusResult> GetStatus(IReadOnlyList<IClientWriter> writers)
    {
        ArgumentNullException.ThrowIfNull(writers);
        return writers.Select(w => w.GetStatus()).ToArray();
    }

    /// <summary>Registers the canonical SnoopMCP endpoint with the given clients (tray convenience).</summary>
    public static IReadOnlyList<RegisterResult> RegisterClients(IEnumerable<McpClient> clients)
    {
        ArgumentNullException.ThrowIfNull(clients);
        return Register(CreateWriters(clients), McpEndpoint.Default);
    }
}
