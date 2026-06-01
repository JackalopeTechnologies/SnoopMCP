// ProcessProbe.cs
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
using System.Runtime.InteropServices;
using SnoopMCP.Protocol.Errors;

#endregion

namespace SnoopMCP.Host.Injection;

public static class ProcessProbe
{
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
            var processName = process.ProcessName;
            var bitness = DetermineBitness(process);
            var (runtime, framework) = DetermineRuntime(process);
            EnsureWpfLoaded(process);
            EnsureX64(bitness);

            return new ProcessProbeResult(
                processName,
                runtime,
                framework,
                bitness);
        }
    }

    private static string DetermineBitness(Process process)
    {
        var handle = process.Handle;
        var ok = IsWow64Process(handle, out var isWow64);
        if (!ok)
            throw new SnoopMcpException(
                ErrorCode.AccessDenied,
                "Could not query process bitness — usually means an elevation mismatch (target Admin, host not).");
        var osIs64 = Environment.Is64BitOperatingSystem;
        var bitness = osIs64 && !isWow64 ? X64 : X86;
        return bitness;
    }

    private static (string Runtime, string Framework) DetermineRuntime(Process process)
    {
        var runtime = UnknownVersion;
        var framework = UnknownVersion;
        foreach (ProcessModule module in process.Modules)
        {
            var name = module.ModuleName ?? string.Empty;
            var isHostFxr = string.Equals(name, HostFxrModule, StringComparison.OrdinalIgnoreCase);
            if (isHostFxr) runtime = module.FileVersionInfo.FileVersion ?? UnknownVersion;
            var isWpf = string.Equals(name, WpfModule, StringComparison.OrdinalIgnoreCase);
            if (isWpf) framework = module.FileVersionInfo.FileVersion ?? UnknownVersion;
        }

        return (runtime, framework);
    }

    private static void EnsureWpfLoaded(Process process)
    {
        var found = false;
        foreach (ProcessModule module in process.Modules)
        {
            var isWpf = string.Equals(module.ModuleName, WpfModule, StringComparison.OrdinalIgnoreCase);
            if (isWpf) found = true;
        }

        if (!found)
            throw new SnoopMcpException(
                ErrorCode.AttachFailed,
                $"Target process is not a WPF app ({WpfModule} not loaded).");
    }

    private static void EnsureX64(string bitness)
    {
        var isX64 = string.Equals(bitness, X64, StringComparison.Ordinal);
        if (!isX64)
            throw new SnoopMcpException(
                ErrorCode.AttachFailed,
                $"Target is {bitness}; SnoopMCP v1 supports x64 targets only.");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWow64Process(IntPtr processHandle,
        [MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

    private const string HostFxrModule = "hostfxr.dll";
    private const string WpfModule = "PresentationFramework.dll";
    private const string UnknownVersion = "Unknown";
    private const string X64 = "x64";
    private const string X86 = "x86";
}
