// ServerStateInfo.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

/// <summary>
/// Pure mapping from <see cref="ServerState"/> to the tray's tooltip text and the availability of
/// the Start/Stop actions. No WPF or Win32 dependency, so it is unit-testable in isolation.
/// </summary>
public static class ServerStateInfo
{
    private const string RunningTip = "SnoopMCP — running on http://127.0.0.1:6300";
    private const string StartingTip = "SnoopMCP — starting…";
    private const string StoppedTip = "SnoopMCP — stopped";
    private const string FaultedTip = "SnoopMCP — error (is port 6300 already in use?)";

    /// <summary>Gets the tooltip text describing the supplied state.</summary>
    /// <param name="state">The current server state.</param>
    /// <returns>A non-empty tooltip string.</returns>
    public static string Tooltip(ServerState state) => state switch
    {
        ServerState.Running => RunningTip,
        ServerState.Starting => StartingTip,
        ServerState.Faulted => FaultedTip,
        _ => StoppedTip
    };

    /// <summary>Gets a value indicating whether Start is available in the supplied state.</summary>
    /// <param name="state">The current server state.</param>
    /// <returns><c>true</c> when stopped or faulted.</returns>
    public static bool CanStart(ServerState state) => state is ServerState.Stopped or ServerState.Faulted;

    /// <summary>Gets a value indicating whether Stop is available in the supplied state.</summary>
    /// <param name="state">The current server state.</param>
    /// <returns><c>true</c> when running.</returns>
    public static bool CanStop(ServerState state) => state is ServerState.Running;
}
