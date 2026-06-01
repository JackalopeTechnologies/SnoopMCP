// AutostartTask.cs
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

using System.Diagnostics;

#endregion

namespace SnoopMCP.Cli;

/// <summary>
///     Manages the per-user logon scheduled task that launches the SnoopMCP host. Wraps
///     <c>schtasks.exe</c>: the argument builders are pure (and unit-tested); the create/remove/exists
///     methods shell out. The task runs at logon, non-elevated (<c>/RL LIMITED</c>), matching the
///     per-user run model.
/// </summary>
public static class AutostartTask
{
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
        foreach (var arg in arguments) psi.ArgumentList.Add(arg);
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
}
