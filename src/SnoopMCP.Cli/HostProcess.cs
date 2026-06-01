// HostProcess.cs
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
///     Starts and stops the SnoopMCP host process (<c>SnoopMCP.Host.exe</c>), which sits next to this
///     CLI in the install directory. <see cref="ExePath" /> is the one place the host-exe location is
///     resolved. Start is fire-and-forget (the host runs until stopped); Stop kills any running host
///     instances by process name.
/// </summary>
public static class HostProcess
{
    /// <summary>Absolute path to the host exe alongside this CLI.</summary>
    public static string ExePath()
    {
        return Path.Combine(AppContext.BaseDirectory, HostExeName);
    }

    /// <summary>Launches the host. Returns true if a process was started.</summary>
    public static bool Start()
    {
        var psi = new ProcessStartInfo { FileName = ExePath(), UseShellExecute = false };
        // Dispose only the handle, not the process: the host keeps running after this returns.
        using var process = Process.Start(psi);
        return process is not null;
    }

    /// <summary>Stops any running host instances. Returns the count stopped.</summary>
    public static int Stop()
    {
        Process[] running = Process.GetProcessesByName(HostProcessName);
        var stopped = 0;
        foreach (Process process in running)
        {
            process.Kill(true);
            process.WaitForExit();
            process.Dispose();
            stopped++;
        }

        return stopped;
    }

    private const string HostExeName = "SnoopMCP.Host.exe";
    private const string HostProcessName = "SnoopMCP.Host";
}
