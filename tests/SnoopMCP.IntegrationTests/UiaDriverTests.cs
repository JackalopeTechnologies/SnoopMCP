// UiaDriverTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.IntegrationTests;

using System.Diagnostics;
using System.Windows.Automation;
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
    private const string LocatorByAutomationId = "automationId";
    private const string AutomationIdProbeText = "ProbeText";
    private const string AutomationIdRunProbe = "RunProbe";
    private const string AutomationIdProbeStatus = "ProbeStatus";
    private const string ProbeSetValue = "hello-uia";
    private const string ProbeStatusDone = "done";
    private const int ProbeStatusPollWindowMs = 2000;
    private const int ProbeStatusPollIntervalMs = 100;

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

    [Fact]
    public async Task SetValue_OnProbeText_UpdatesValue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var driver = new UiaDriver(new ElementHandleCache());

        IReadOnlyList<UiaElementInfo> found =
            await driver.FindAsync(mApp.Id, LocatorByAutomationId, AutomationIdProbeText, ct);
        Assert.NotEmpty(found);

        await driver.SetValueAsync(found[0].Reference, ProbeSetValue, ct);

        IReadOnlyList<UiaElementInfo> again =
            await driver.FindAsync(mApp.Id, LocatorByAutomationId, AutomationIdProbeText, ct);
        Assert.NotEmpty(again);
        string value = await ReadValueAsync(driver, again[0].Reference, ct);
        Assert.Equal(ProbeSetValue, value);
    }

    [Fact]
    public async Task Invoke_RunProbeButton_SetsProbeStatusDone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var driver = new UiaDriver(new ElementHandleCache());

        IReadOnlyList<UiaElementInfo> found =
            await driver.FindAsync(mApp.Id, LocatorByAutomationId, AutomationIdRunProbe, ct);
        Assert.NotEmpty(found);

        await driver.InvokeAsync(found[0].Reference, null, ct);

        // The bound command runs on the target's UI thread asynchronously relative to Invoke()
        // returning, so poll rather than assume the ProbeStatus text is updated synchronously.
        string status = await PollForNameAsync(
            driver, mApp.Id, AutomationIdProbeStatus, ProbeStatusDone, ProbeStatusPollWindowMs, ct);
        Assert.Equal(ProbeStatusDone, status);
    }

    /// <summary>Reads a live element's <see cref="ValuePattern"/> value, or empty when unsupported.</summary>
    private static async Task<string> ReadValueAsync(UiaDriver driver, UiaElementRef reference, CancellationToken ct)
    {
        AutomationElement element = await driver.ResolveAsync(reference, ct).ConfigureAwait(false);
        string value = string.Empty;
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object raw))
        {
            value = ((ValuePattern)raw).Current.Value;
        }
        return value;
    }

    /// <summary>Re-finds <paramref name="automationId"/> by polling until its Name matches or the timeout elapses.</summary>
    private static async Task<string> PollForNameAsync(
        UiaDriver driver, int pid, string automationId, string expected, int timeoutMs, CancellationToken ct)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        string current = string.Empty;
        bool matched = false;
        while (!matched && DateTimeOffset.UtcNow < deadline)
        {
            IReadOnlyList<UiaElementInfo> found = await driver.FindAsync(pid, LocatorByAutomationId, automationId, ct)
                .ConfigureAwait(false);
            if (found.Count > 0)
            {
                current = found[0].Name;
            }
            matched = string.Equals(current, expected, StringComparison.Ordinal);
            if (!matched)
            {
                await Task.Delay(ProbeStatusPollIntervalMs, ct).ConfigureAwait(false);
            }
        }
        return current;
    }
}
