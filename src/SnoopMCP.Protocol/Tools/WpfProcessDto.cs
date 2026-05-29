namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// A WPF process discovered by <c>listWpfProcesses</c> as a candidate debug target. Reported
/// pre-attach by the host (no injection required), so an LLM client can pick a target by name or
/// window title instead of needing a PID supplied out-of-band.
/// </summary>
/// <param name="Pid">The process id to pass to <c>attach</c>.</param>
/// <param name="ProcessName">The process image name (no extension).</param>
/// <param name="MainWindowTitle">The main window title, or empty if the process has none.</param>
/// <param name="Bitness"><c>x64</c> or <c>x86</c> (best effort; empty if it could not be determined).</param>
/// <param name="RuntimeVersion">The .NET host (<c>hostfxr.dll</c>) version, or <c>Unknown</c>.</param>
/// <param name="FrameworkVersion">The WPF (<c>PresentationFramework.dll</c>) version, or <c>Unknown</c>.</param>
/// <param name="Attachable">True when v1 can attach (x64). Non-x64 targets are listed but not attachable.</param>
public sealed record WpfProcessDto(
    int Pid,
    string ProcessName,
    string MainWindowTitle,
    string Bitness,
    string RuntimeVersion,
    string FrameworkVersion,
    bool Attachable);
