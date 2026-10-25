// HostRecovery.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

/// <summary>Bounds recovery of an owned host process; a successful exit always ends supervision.</summary>
public static class HostRecovery
{
    private const int MaximumRestarts = 3;
    private const string NormalExitMessage = "Host exited normally; recovery stopped.";
    private static readonly TimeSpan smRestartDelay = TimeSpan.FromMinutes(1);

    /// <summary>Runs one host at a time, with at most three delayed retries after nonzero exits.</summary>
    public static async Task<int> RunAsync(Func<Task<int>> runHost, Func<TimeSpan, Task> delay, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(runHost);
        ArgumentNullException.ThrowIfNull(delay);
        ArgumentNullException.ThrowIfNull(log);
        int exitCode = await runHost().ConfigureAwait(false);
        int restarted = 0;
        while (exitCode != 0 && restarted < MaximumRestarts)
        {
            restarted++;
            log($"Host exited with code {exitCode}; restart {restarted}/{MaximumRestarts} in one minute.");
            await delay(smRestartDelay).ConfigureAwait(false);
            exitCode = await runHost().ConfigureAwait(false);
        }
        log(exitCode == 0 ? NormalExitMessage
            : $"Host exited with code {exitCode}; restart limit reached. Start SnoopMCP again when ready.");
        return exitCode;
    }
}
