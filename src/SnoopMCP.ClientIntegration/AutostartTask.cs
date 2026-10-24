// AutostartTask.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;

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
    private const string XmlSwitch = "/XML";
    private const string XmlSuffix = ".xml";
    private const string ForceSwitch = "/F";
    private const string RunSwitch = "/Run";
    private const string EndSwitch = "/End";

    /// <summary>Builds the arguments that import the explicit logon and recovery task definition.</summary>
    /// <param name="xmlPath">Absolute path to the task XML file.</param>
    public static IReadOnlyList<string> BuildCreateArguments(string xmlPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(xmlPath);
        return [CreateSwitch, TaskNameSwitch, TaskName, XmlSwitch, xmlPath, ForceSwitch];
    }

    /// <summary>Creates an interactive per-user task with unlimited runtime and three failure restarts.</summary>
    /// <param name="hostExePath">Absolute executable path; XML characters are escaped.</param>
    /// <param name="userId">The installing user's Windows security identifier.</param>
    public static string BuildTaskXml(string hostExePath, string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostExePath);
        ArgumentException.ThrowIfNullOrEmpty(userId);
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>SnoopMCP WPF inspection for coding assistants. Restarts after failure; Exit stays stopped.</Description></RegistrationInfo>
              <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{SecurityElement.Escape(userId)}</UserId></LogonTrigger></Triggers>
              <Principals><Principal id="User"><UserId>{SecurityElement.Escape(userId)}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <StartWhenAvailable>true</StartWhenAvailable>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <RestartOnFailure><Interval>PT1M</Interval><Count>3</Count></RestartOnFailure>
              </Settings>
              <Actions Context="User"><Exec><Command>{SecurityElement.Escape(hostExePath)}</Command></Exec></Actions>
            </Task>
            """;
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

    /// <summary>Builds the <c>schtasks</c> arguments that run the logon task now (inherits its RunLevel).</summary>
    public static IReadOnlyList<string> BuildRunArguments()
    {
        return [RunSwitch, TaskNameSwitch, TaskName];
    }

    /// <summary>Stops the scheduled run intentionally, preventing failure-restart recovery.</summary>
    public static bool StopNow() => Run([EndSwitch, TaskNameSwitch, TaskName]) == 0;

    /// <summary>
    /// Creates (or replaces) the elevated logon task. Registering a /RL HIGHEST task requires an
    /// elevated caller, so this relaunches schtasks with the "runas" verb, producing one UAC prompt.
    /// Returns true on success.
    /// </summary>
    /// <param name="hostExePath">Absolute path to <c>SnoopMCP.Host.exe</c>.</param>
    public static bool Create(string hostExePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostExePath);
        bool created = false;
        if (OperatingSystem.IsWindows())
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            if (identity.User is { } user)
            {
                string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + XmlSuffix);
                try
                {
                    File.WriteAllText(path, BuildTaskXml(hostExePath, user.Value), Encoding.Unicode);
                    created = RunElevated(BuildCreateArguments(path));
                }
                finally
                {
                    File.Delete(path);
                }
            }
        }
        return created;
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

    /// <summary>Runs the registered task immediately (elevated if it is a HIGHEST task). Returns true on success.</summary>
    public static bool RunNow()
    {
        return Run(BuildRunArguments()) == 0;
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
