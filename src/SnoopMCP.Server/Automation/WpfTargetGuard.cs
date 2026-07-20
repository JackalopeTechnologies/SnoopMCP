// WpfTargetGuard.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Automation;

using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Protocol.Errors;

/// <summary>
/// Confirms a target process actually hosts WPF (has <c>PresentationFramework.dll</c> loaded) before
/// any UIA driving/capture tool is allowed to touch it — the driving surface is a WPF-only tool, so an
/// arbitrary Win32/WinForms process must never be walkable through it. No x64 restriction: unlike
/// pre-attach payload injection, out-of-process UIA/PrintWindow work against any bitness.
/// </summary>
public static class WpfTargetGuard
{
    private const string PresentationFrameworkModule = "PresentationFramework.dll";

    /// <summary>
    /// Throws unless process <paramref name="pid"/> has <c>PresentationFramework.dll</c> loaded.
    /// </summary>
    /// <param name="pid">The target process id.</param>
    /// <exception cref="SnoopMcpException">
    /// <see cref="ErrorCode.AccessDenied"/> when module enumeration is refused (e.g. an elevated
    /// target this process cannot inspect); <see cref="ErrorCode.AttachFailed"/> when the pid does not
    /// resolve to a running, inspectable process, or resolves but has no
    /// <c>PresentationFramework.dll</c> module.
    /// </exception>
    public static void EnsureWpf(int pid)
    {
        bool found;
        try
        {
            found = HasPresentationFramework(pid);
        }
        catch (Win32Exception ex)
        {
            throw new SnoopMcpException(
                ErrorCode.AccessDenied,
                $"Access denied enumerating modules for process {pid}.",
                ex);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new SnoopMcpException(
                ErrorCode.AttachFailed,
                $"Process {pid} is not an inspectable WPF target.",
                ex);
        }

        if (!found)
        {
            throw new SnoopMcpException(
                ErrorCode.AttachFailed,
                $"Process {pid} is not a WPF app ({PresentationFrameworkModule} not loaded).");
        }
    }

    /// <summary>Enumerates <paramref name="pid"/>'s modules looking for <see cref="PresentationFrameworkModule"/>.</summary>
    private static bool HasPresentationFramework(int pid)
    {
        using Process process = Process.GetProcessById(pid);
        bool found = false;
        foreach (ProcessModule module in process.Modules.Cast<ProcessModule>())
        {
            if (string.Equals(module.ModuleName, PresentationFrameworkModule, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
            }
        }
        return found;
    }
}
