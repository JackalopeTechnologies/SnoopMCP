namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Response for <c>listWpfProcesses</c>: the WPF processes currently visible to the host as
/// candidate debug targets.
/// </summary>
/// <param name="Processes">The discovered WPF processes, most-recently-enumerated order.</param>
public sealed record ListWpfProcessesResponse(IReadOnlyList<WpfProcessDto> Processes);
