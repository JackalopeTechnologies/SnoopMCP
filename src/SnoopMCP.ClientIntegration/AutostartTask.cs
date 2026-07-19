// AutostartTask.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

using System.ComponentModel;
using System.Diagnostics;

/// <summary>
/// Manages the per-user logon scheduled task that launches the SnoopMCP host. Wraps
/// <c>schtasks.exe</c>: the argument builders are pure (and unit-tested); the create/remove/exists
/// methods shell out. The task runs at logon, elevated (<c>/RL HIGHEST</c>) for administrators.
/// </summary>
public static class AutostartTask
{
    private const string SchTasksExe = "schtasks.exe";
    private const string TaskName = "SnoopMCP Host";
    private const string CreateSwitch = "/Create";
    private const string DeleteSwitch = "/Delete";
    private const string QuerySwitch = "/Query";
    private const string TaskNameSwitch = "/TN";
    private const string ScheduleSwitch = "/SC";
    private const string OnLogon = "ONLOGON";
    private const string RunLevelSwitch = "/RL";
    private const string Highest = "HIGHEST";
    private const string TaskRunSwitch = "/TR";
    private const string ForceSwitch = "/F";

    /// <summary>Builds the <c>schtasks</c> arguments that create the logon task for the host exe.</summary>
    /// <param name="hostExePath">Absolute path to <c>SnoopMCP.Host.exe</c>.</param>
    public static IReadOnlyList<string> BuildCreateArguments(string hostExePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostExePath);
        return
        [
            CreateSwitch, TaskNameSwitch, TaskName, ScheduleSwitch, OnLogon,
            RunLevelSwitch, Highest, TaskRunSwitch, hostExePath, ForceSwitch
        ];
    }

    /// <summary>Builds the <c>schtasks</c> arguments that delete the logon task.</summary>
    public static IReadOnlyList<string> BuildDeleteArguments()
    {
        return [DeleteSwitch, TaskNameSwitch, TaskName, ForceSwitch];
    }

    /// <summary>Builds the <c>schtasks</c> arguments that query the logon task.</summary>
    public static IReadOnlyList<string> BuildQueryArguments()
    {
        return [QuerySwitch, TaskNameSwitch, TaskName];
    }

    /// <summary>
    /// Creates (or replaces) the elevated logon task. Registering a /RL HIGHEST task requires an
    /// elevated caller, so this relaunches schtasks with the "runas" verb, producing one UAC prompt.
    /// Returns true on success.
    /// </summary>
    /// <param name="hostExePath">Absolute path to <c>SnoopMCP.Host.exe</c>.</param>
    public static bool Create(string hostExePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostExePath);
        return RunElevated(BuildCreateArguments(hostExePath));
    }

    /// <summary>Removes the logon task. Returns true on success or if it did not exist.</summary>
    public static bool Remove()
    {
        return Run(BuildDeleteArguments()) == 0;
    }

    /// <summary>Reports whether the logon task currently exists.</summary>
    public static bool Exists()
    {
        return Run(BuildQueryArguments()) == 0;
    }

    private static bool RunElevated(IReadOnlyList<string> arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = SchTasksExe,
            // UseShellExecute is required for the "runas" verb (UAC).
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (string arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }
        int exit;
        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                exit = -1;
            }
            else
            {
                process.WaitForExit();
                exit = process.ExitCode;
            }
        }
        catch (Win32Exception)
        {
            // User declined the UAC prompt (ERROR_CANCELLED) or elevation is unavailable.
            exit = -1;
        }
        return exit == 0;
    }

    private static int Run(IReadOnlyList<string> arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = SchTasksExe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }
        int exit;
        using (var process = Process.Start(psi))
        {
            if (process is null)
            {
                exit = -1;
            }
            else
            {
                // Drain the redirected streams before waiting so a verbose schtasks error cannot
                // fill the pipe buffer and deadlock; the text itself is intentionally discarded.
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();
                exit = process.ExitCode;
            }
        }
        return exit;
    }
}
