// ProcessProbeElevationTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using Injection;
using Protocol.Errors;
using Xunit;

/// <summary>
/// Locks the invariant that no raw <see cref="System.ComponentModel.Win32Exception"/> escapes
/// <see cref="ProcessProbe.Probe"/>. PID 4 is the Windows "System" protected process:
/// <c>Process.GetProcessById(4)</c> succeeds (so <see cref="ProcessProbe.Probe"/> gets past the
/// not-found check), but reading <c>.Handle</c> is access-denied regardless of the test host's
/// integrity level, and that read happens inside <c>DetermineBitness</c> before any WPF-module or
/// bitness check — so this is deterministic and CI-safe.
/// </summary>
public sealed class ProcessProbeElevationTests
{
    private const int SystemProcessId = 4;

    [Fact]
    public void Probe_InaccessibleProcess_ThrowsAccessDeniedNotWin32()
    {
        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(() => ProcessProbe.Probe(SystemProcessId));

        Assert.Equal(ErrorCode.AccessDenied, ex.Code);
    }
}
