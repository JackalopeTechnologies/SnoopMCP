// Program.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Recovery;

using System.Diagnostics;
using ClientIntegration;

/// <summary>Windowless scheduled launcher. Observes only its child and never takes over an inspection.</summary>
internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\SnoopMCP.Recovery";
    private const string HostExecutable = "SnoopMCP.Host.exe";
    private const string AppFolder = "SnoopMCP";
    private const string LogsFolder = "logs";
    private const string LogName = "recovery.log";
    private const int FailureExitCode = 1;
    private const long MaxLogBytes = 5L * 1024 * 1024;

    private static async Task<int> Main()
    {
        using var instance = new Mutex(initiallyOwned: false, SingleInstanceMutexName, out bool created);
        int exitCode = 0;
        if (created)
        {
            try
            {
                exitCode = await HostRecovery.RunAsync(RunHostAsync, Task.Delay, WriteLog).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                WriteLog($"Recovery launcher stopped: {ex}");
                exitCode = FailureExitCode;
            }
        }
        return exitCode;
    }

    private static async Task<int> RunHostAsync()
    {
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(AppContext.BaseDirectory, HostExecutable),
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Could not start the SnoopMCP host.");
        WriteLog($"Started owned host process {child.Id}.");
        await child.WaitForExitAsync().ConfigureAwait(false);
        return child.ExitCode;
    }

    private static void WriteLog(string message)
    {
        try
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolder, LogsFolder);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, LogName);
            if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
            {
                File.Delete(path);
            }
            File.AppendAllText(path, FormattableString.Invariant($"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"));
        }
        catch (Exception)
        {
            // Diagnostic I/O cannot start a retry loop or prevent an intentional exit.
        }
    }
}
