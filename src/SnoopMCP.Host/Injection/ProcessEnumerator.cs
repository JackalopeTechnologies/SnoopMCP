namespace SnoopMCP.Host.Injection;

using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Host-side, pre-attach discovery of WPF processes. Enumerates running processes that have a main
/// window and have WPF loaded (<c>PresentationFramework.dll</c>), reporting each as a candidate
/// debug target. No injection is involved; processes the host cannot inspect (access denied, exited,
/// cross-architecture) are skipped silently.
/// </summary>
public sealed class ProcessEnumerator
{
    private const string WpfModule = "PresentationFramework.dll";
    private const string HostFxrModule = "hostfxr.dll";
    private const string UnknownVersion = "Unknown";
    private const string X64 = "x64";
    private const string X86 = "x86";

    public IReadOnlyList<WpfProcessDto> ListWpfProcesses()
    {
        var results = new List<WpfProcessDto>();
        Process[] all = Process.GetProcesses();
        try
        {
            foreach (Process process in all)
            {
                WpfProcessDto? dto = TryClassify(process);
                if (dto is not null)
                {
                    results.Add(dto);
                }
            }
        }
        finally
        {
            foreach (Process process in all)
            {
                process.Dispose();
            }
        }
        return results;
    }

    private static WpfProcessDto? TryClassify(Process process)
    {
        WpfProcessDto? dto = null;
        try
        {
            bool hasWindow = process.MainWindowHandle != IntPtr.Zero;
            if (hasWindow)
            {
                (bool isWpf, string framework, string runtime) = ScanModules(process);
                if (isWpf)
                {
                    string bitness = DetermineBitness(process);
                    dto = new WpfProcessDto(
                        Pid: process.Id,
                        ProcessName: process.ProcessName,
                        MainWindowTitle: process.MainWindowTitle,
                        Bitness: bitness,
                        RuntimeVersion: runtime,
                        FrameworkVersion: framework,
                        Attachable: string.Equals(bitness, X64, StringComparison.Ordinal));
                }
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            dto = null;
        }
        return dto;
    }

    private static (bool IsWpf, string Framework, string Runtime) ScanModules(Process process)
    {
        bool isWpf = false;
        string framework = UnknownVersion;
        string runtime = UnknownVersion;
        foreach (ProcessModule module in process.Modules.Cast<ProcessModule>())
        {
            string name = module.ModuleName ?? string.Empty;
            bool isWpfModule = string.Equals(name, WpfModule, StringComparison.OrdinalIgnoreCase);
            if (isWpfModule)
            {
                isWpf = true;
                framework = module.FileVersionInfo.FileVersion ?? UnknownVersion;
            }
            bool isHostFxr = string.Equals(name, HostFxrModule, StringComparison.OrdinalIgnoreCase);
            if (isHostFxr)
            {
                runtime = module.FileVersionInfo.FileVersion ?? UnknownVersion;
            }
        }
        return (isWpf, framework, runtime);
    }

    private static string DetermineBitness(Process process)
    {
        string bitness = string.Empty;
        bool ok = IsWow64Process(process.Handle, out bool isWow64);
        if (ok)
        {
            bool osIs64 = Environment.Is64BitOperatingSystem;
            bitness = (osIs64 && !isWow64) ? X64 : X86;
        }
        return bitness;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWow64Process(IntPtr processHandle, [MarshalAs(UnmanagedType.Bool)] out bool wow64Process);
}
