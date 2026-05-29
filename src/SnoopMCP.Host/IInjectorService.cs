// IInjectorService.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

/// <summary>
/// Abstraction over injecting the SnoopMCP payload into a target process and probing it. The real
/// implementation (wired in Task 31) drives the Snoop generic injector; until then
/// <see cref="NullInjectorService"/> is registered so an attach attempt surfaces a clear error.
/// </summary>
public interface IInjectorService
{
    /// <summary>
    /// Injects the payload into the target process and points it at the supplied pipe.
    /// </summary>
    /// <param name="processId">The target process id.</param>
    /// <param name="pipeName">The named pipe the payload should serve on.</param>
    /// <param name="cancellationToken">A token to observe while injecting.</param>
    Task InjectAsync(int processId, string pipeName, CancellationToken cancellationToken);

    /// <summary>
    /// Probes the target process for the metadata reported back to the client on attach.
    /// </summary>
    /// <param name="processId">The target process id.</param>
    /// <param name="cancellationToken">A token to observe while probing.</param>
    /// <returns>The probe result describing the target process.</returns>
    Task<ProcessProbeResult> ProbeAsync(int processId, CancellationToken cancellationToken);
}
