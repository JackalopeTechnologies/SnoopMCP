// UiaDriverTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.IntegrationTests;

using System.Diagnostics;
using Host.Automation;
using Xunit;

/// <summary>
/// Interactive UIA discovery gate: launches the real SampleWpfApp and drives <see cref="UiaDriver"/>
/// against it out-of-process. Requires an interactive desktop session — UIA2 cannot enumerate a
/// window's automation tree headless. Mirrors the launch fixture in <see cref="EndToEndTests"/>.
/// </summary>
public sealed class UiaDriverTests : IDisposable
{
    private const int WaitForInputIdleMs = 5000;
    private const int SettleDelayMs = 1000;

    private readonly Process mApp;

    public UiaDriverTests()
    {
        string samplePath = Path.Combine(AppContext.BaseDirectory, "SampleWpfApp.exe");
        Assert.True(File.Exists(samplePath), $"SampleWpfApp.exe not found at {samplePath}.");

        mApp = Process.Start(new ProcessStartInfo { FileName = samplePath, UseShellExecute = false })
            ?? throw new InvalidOperationException("Failed to start SampleWpfApp.");
        mApp.WaitForInputIdle(WaitForInputIdleMs);

        // Let the visual tree settle.
        Thread.Sleep(SettleDelayMs);
    }

    public void Dispose()
    {
        if (!mApp.HasExited)
        {
            mApp.Kill(entireProcessTree: true);
            mApp.WaitForExit(5000);
        }
        mApp.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetTree_ReturnsElements_WithReferences()
    {
        var driver = new UiaDriver(new ElementHandleCache());
        IReadOnlyList<UiaElementInfo> tree =
            await driver.GetTreeAsync(mApp.Id, null, 3, TestContext.Current.CancellationToken);

        Assert.NotEmpty(tree);
        Assert.All(tree, e => Assert.False(string.IsNullOrEmpty(e.Reference.Handle)));
    }

    [Fact]
    public async Task Find_ByAutomationId_FindsProbeText()
    {
        var driver = new UiaDriver(new ElementHandleCache());
        IReadOnlyList<UiaElementInfo> found = await driver.FindAsync(
            mApp.Id, "automationId", "ProbeText", TestContext.Current.CancellationToken);

        Assert.True(found.Count >= 1);
    }
}
