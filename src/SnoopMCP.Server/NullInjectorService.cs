// NullInjectorService.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host;

using SnoopMCP.Protocol.Errors;

/// <summary>
/// Default <see cref="IInjectorService"/> registered until Task 31 swaps in the real Snoop-based
/// injector. Every call fails with <see cref="ErrorCode.AttachFailed"/> so an attach attempt surfaces
/// a clear "injector not configured" error instead of silently succeeding.
/// </summary>
public sealed class NullInjectorService : IInjectorService
{
    private const string NotConfiguredMessage =
        "Injector not configured. Task 31 wires in the real ManagedInjector.";

    /// <inheritdoc />
    public Task InjectAsync(int processId, string pipeName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        throw new SnoopMcpException(ErrorCode.AttachFailed, NotConfiguredMessage);
    }

    /// <inheritdoc />
    public Task<ProcessProbeResult> ProbeAsync(int processId, CancellationToken cancellationToken)
    {
        throw new SnoopMcpException(ErrorCode.AttachFailed, NotConfiguredMessage);
    }
}
