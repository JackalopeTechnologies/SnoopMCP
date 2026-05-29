// HealthStatus.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

/// <summary>
/// The <c>/health</c> response: a liveness marker, the host's informational version, and whether a
/// target is currently attached. Serialised to JSON by the endpoint.
/// </summary>
/// <param name="Status">Always <c>ok</c> when the host is serving requests.</param>
/// <param name="Version">The host's informational version.</param>
/// <param name="Attached">True when a WPF target session is open.</param>
public sealed record HealthStatus(string Status, string Version, bool Attached)
{
    private const string OkStatus = "ok";

    /// <summary>Creates an <c>ok</c> health status for the given version and attach state.</summary>
    public static HealthStatus Create(string version, bool attached)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        return new HealthStatus(OkStatus, version, attached);
    }
}
