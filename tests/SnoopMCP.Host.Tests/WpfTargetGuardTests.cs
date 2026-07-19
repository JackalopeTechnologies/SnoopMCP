// WpfTargetGuardTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using System.Diagnostics;
using Host.Automation;
using Protocol.Errors;
using Xunit;

/// <summary>
/// Non-interactive guard on <see cref="WpfTargetGuard"/>: exercises the "module enumeration succeeded
/// but PresentationFramework.dll is absent" branch against a deterministic, CI-safe, same-user Win32
/// process. A backgrounded <c>cmd.exe /c pause</c> (stdin redirected, never fed) is used instead of
/// relying on notepad/explorer being present on the machine.
/// </summary>
public sealed class WpfTargetGuardTests : IDisposable
{
    private const string CmdExe = "cmd.exe";
    private const string PauseArgs = "/c pause";
    private const int KillWaitMs = 5000;

    private readonly Process mProcess;

    public WpfTargetGuardTests()
    {
        mProcess = Process.Start(new ProcessStartInfo(CmdExe, PauseArgs)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Failed to start cmd.exe.");
    }

    public void Dispose()
    {
        if (!mProcess.HasExited)
        {
            mProcess.Kill(entireProcessTree: true);
            mProcess.WaitForExit(KillWaitMs);
        }
        mProcess.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void EnsureWpf_NonWpfProcess_ThrowsAttachFailed()
    {
        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(() => WpfTargetGuard.EnsureWpf(mProcess.Id));

        Assert.Equal(ErrorCode.AttachFailed, ex.Code);
    }
}
