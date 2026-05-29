// AutostartTask.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli;

using System.Diagnostics;

/// <summary>
/// Manages the per-user logon scheduled task that launches the SnoopMCP host. Wraps
/// <c>schtasks.exe</c>: the argument builders are pure (and unit-tested); the create/remove/exists
/// methods shell out. The task runs at logon, non-elevated (<c>/RL LIMITED</c>), matching the
/// per-user run model.
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
    private const string Limited = "LIMITED";
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
            RunLevelSwitch, Limited, TaskRunSwitch, hostExePath, ForceSwitch
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

    /// <summary>Creates (or replaces) the logon task. Returns true on success.</summary>
    /// <param name="hostExePath">Absolute path to <c>SnoopMCP.Host.exe</c>.</param>
    public static bool Create(string hostExePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostExePath);
        return Run(BuildCreateArguments(hostExePath)) == 0;
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
        using (Process? process = Process.Start(psi))
        {
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
        return exit;
    }
}
