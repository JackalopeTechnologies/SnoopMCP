// HostProcess.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Cli;

using System.Diagnostics;

/// <summary>
/// Starts and stops the SnoopMCP host process (<c>SnoopMCP.Host.exe</c>), which sits next to this
/// CLI in the install directory. <see cref="ExePath"/> is the one place the host-exe location is
/// resolved. Start is fire-and-forget (the host runs until stopped); Stop kills any running host
/// instances by process name.
/// </summary>
public static class HostProcess
{
    private const string HostExeName = "SnoopMCP.Host.exe";
    private const string HostProcessName = "SnoopMCP.Host";

    /// <summary>Absolute path to the host exe alongside this CLI.</summary>
    public static string ExePath()
    {
        return Path.Combine(AppContext.BaseDirectory, HostExeName);
    }

    /// <summary>Launches the host. Returns true if a process was started.</summary>
    public static bool Start()
    {
        var psi = new ProcessStartInfo
        {
            FileName = ExePath(),
            UseShellExecute = false
        };
        // Dispose only the handle, not the process: the host keeps running after this returns.
        using Process? process = Process.Start(psi);
        return process is not null;
    }

    /// <summary>Stops any running host instances. Returns the count stopped.</summary>
    public static int Stop()
    {
        Process[] running = Process.GetProcessesByName(HostProcessName);
        int stopped = 0;
        foreach (Process process in running)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            process.Dispose();
            stopped++;
        }
        return stopped;
    }
}
