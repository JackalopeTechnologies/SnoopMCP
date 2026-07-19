// HealthStatus.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host;

/// <summary>
/// The <c>/health</c> response: a liveness marker, the host's informational version, whether a
/// target is currently attached, and whether the interaction gate permits driving tools.
/// Serialised to JSON by the endpoint.
/// </summary>
/// <param name="Status">Always <c>ok</c> when the host is serving requests.</param>
/// <param name="Version">The host's informational version.</param>
/// <param name="Attached">True when a WPF target session is open.</param>
/// <param name="InteractionEnabled">True when the host's <see cref="Automation.InteractionGate"/> permits mutating driving tools.</param>
public sealed record HealthStatus(string Status, string Version, bool Attached, bool InteractionEnabled)
{
    private const string OkStatus = "ok";

    /// <summary>Creates an <c>ok</c> health status for the given version, attach state, and interaction-gate state.</summary>
    public static HealthStatus Create(string version, bool attached, bool interactionEnabled)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        return new HealthStatus(OkStatus, version, attached, interactionEnabled);
    }
}
