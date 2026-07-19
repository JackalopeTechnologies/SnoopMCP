// ProcessProbe.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Injection;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Protocol.Errors;

public static class ProcessProbe
{
    private const string HostFxrModule = "hostfxr.dll";
    private const string WpfModule = "PresentationFramework.dll";
    private const string UnknownVersion = "Unknown";
    private const string X64 = "x64";
    private const string X86 = "x86";

    public static ProcessProbeResult Probe(int processId)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException ex)
        {
            throw new SnoopMcpException(
                ErrorCode.AttachFailed,
                $"Process id {processId} not found.",
                ex);
        }

        using (process)
        {
            string processName = process.ProcessName;
            string bitness = DetermineBitness(process);
            (string runtime, string framework) = DetermineRuntime(process);
            EnsureWpfLoaded(process);
            EnsureX64(bitness);

            return new ProcessProbeResult(
                ProcessName: processName,
                RuntimeVersion: runtime,
                FrameworkVersion: framework,
                Bitness: bitness);
        }
    }

    private static string DetermineBitness(Process process)
    {
        IntPtr handle;
        try
        {
            handle = process.Handle;
        }
        catch (Win32Exception ex)
        {
            throw new SnoopMcpException(
                ErrorCode.AccessDenied,
                "Could not open the target process — usually means an elevation mismatch (target Admin, host not). "
                + "Run the SnoopMCP host elevated (enable autostart, which registers an elevated logon task).",
                ex);
        }
        bool ok = IsWow64Process(handle, out bool isWow64);
        if (!ok)
        {
            throw new SnoopMcpException(
                ErrorCode.AccessDenied,
                "Could not query process bitness — usually means an elevation mismatch (target Admin, host not).");
        }
        bool osIs64 = Environment.Is64BitOperatingSystem;
        string bitness = (osIs64 && !isWow64) ? X64 : X86;
        return bitness;
    }

    private static (string Runtime, string Framework) DetermineRuntime(Process process)
    {
        string runtime = UnknownVersion;
        string framework = UnknownVersion;
        foreach (ProcessModule module in process.Modules)
        {
            string name = module.ModuleName;
            bool isHostFxr = string.Equals(name, HostFxrModule, StringComparison.OrdinalIgnoreCase);
            if (isHostFxr)
            {
                runtime = module.FileVersionInfo.FileVersion ?? UnknownVersion;
            }
            bool isWpf = string.Equals(name, WpfModule, StringComparison.OrdinalIgnoreCase);
            if (isWpf)
            {
                framework = module.FileVersionInfo.FileVersion ?? UnknownVersion;
            }
        }
        return (runtime, framework);
    }

    private static void EnsureWpfLoaded(Process process)
    {
        bool found = false;
        foreach (ProcessModule module in process.Modules)
        {
            bool isWpf = string.Equals(module.ModuleName, WpfModule, StringComparison.OrdinalIgnoreCase);
            if (isWpf)
            {
                found = true;
            }
        }
        if (!found)
        {
            throw new SnoopMcpException(
                ErrorCode.AttachFailed,
                $"Target process is not a WPF app ({WpfModule} not loaded).");
        }
    }

    private static void EnsureX64(string bitness)
    {
        bool isX64 = string.Equals(bitness, X64, StringComparison.Ordinal);
        if (!isX64)
        {
            throw new SnoopMcpException(
                ErrorCode.AttachFailed,
                $"Target is {bitness}; SnoopMCP v1 supports x64 targets only.");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWow64Process(IntPtr processHandle, [MarshalAs(UnmanagedType.Bool)] out bool wow64Process);
}
