// UnobservedFaultE2ETests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.IntegrationTests;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using Host;
using Host.Automation;
using Host.Injection;
using Host.Tools;
using Protocol.Errors;
using Protocol.Tools;
using Xunit;

/// <summary>
/// Host-side regression guard for the Raygun handoff of 2026-09-09. Three ordinary payload tool
/// errors (element has no AutomationPeer, button disabled, command's CanExecute false) were recorded
/// as six crash groups in the HOST application's Raygun, tagged <c>UnobservedTaskException</c>,
/// because each fault crossed the dispatcher boundary inside a <c>DispatcherOperation</c> whose Task
/// nothing ever observed. This test spawns the real SampleWpfApp, injects the real payload, drives
/// every one of those failure shapes (plus a deliberately slow action that faults after the wait gave
/// up) in both dispatch modes, then forces a finalizer pass inside the target and reads back the count
/// of <see cref="TaskScheduler.UnobservedTaskException"/> events the target recorded. A positive
/// control leaks a genuinely unobserved fault afterwards to prove the target's detector fires.
/// Mirrors the launch fixture in <see cref="DrivingE2ETests"/>.
/// </summary>
public sealed class UnobservedFaultE2ETests : IAsyncLifetime
{
    private const int WindowInitDelaySeconds = 5;
    private const string DisabledProbeAutomationId = "DisabledProbe";
    private const string BlockedProbeAutomationId = "BlockedProbe";
    private const string NoPeerProbeAutomationId = "NoPeerProbe";
    private const string SlowFailingProbeAutomationId = "SlowFailingProbe";
    private const string ForceGcCommandPath = "ForceGcCommand";
    private const string LeakUnobservedFaultCommandPath = "LeakUnobservedFaultCommand";
    private const string UnobservedCountPath = "UnobservedTaskExceptionCount";
    private const string InvokePattern = "Invoke";
    private const string PostDispatch = "post";

    private Process? mSampleProcess;
    private SessionManager? mSession;
    private McpTools? mTools;
    private InteractionGate? mGate;
    private string mGatePath = string.Empty;

    public async ValueTask InitializeAsync()
    {
        string samplePath = Path.Combine(AppContext.BaseDirectory, "SampleWpfApp.exe");
        Assert.True(File.Exists(samplePath), $"SampleWpfApp.exe not found at {samplePath}.");

        mSampleProcess = Process.Start(new ProcessStartInfo
        {
            FileName = samplePath,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Failed to start SampleWpfApp.");

        await Task.Delay(TimeSpan.FromSeconds(WindowInitDelaySeconds));

        mSession = new SessionManager(NullLogger<SessionManager>.Instance, NullLoggerFactory.Instance);
        var injector = new InjectorService(NullLogger<InjectorService>.Instance);
        mGatePath = Path.Combine(Path.GetTempPath(), "gate-" + Guid.NewGuid().ToString("N") + ".json");
        mGate = new InteractionGate(mGatePath);
        mGate.SetEnabled(true);
        mTools = new McpTools(mSession, injector, mGate);
    }

    public async ValueTask DisposeAsync()
    {
        if (mTools is not null)
        {
            try
            {
                await mTools.Detach(CancellationToken.None);
            }
            catch (Exception)
            {
            }
        }
        if (mSampleProcess is not null && !mSampleProcess.HasExited)
        {
            mSampleProcess.Kill(entireProcessTree: true);
            mSampleProcess.WaitForExit(5000);
            mSampleProcess.Dispose();
        }
        if (mSession is not null)
        {
            await mSession.DisposeAsync();
        }
        if (!string.IsNullOrEmpty(mGatePath) && File.Exists(mGatePath))
        {
            File.Delete(mGatePath);
        }
    }

    [Fact]
    public async Task PayloadToolErrors_NeverReachTargetUnobservedTaskException()
    {
        Assert.NotNull(mTools);
        Assert.NotNull(mSampleProcess);
        CancellationToken ct = TestContext.Current.CancellationToken;

        JsonElement attach = await mTools!.Attach(mSampleProcess!.Id, ct);
        Assert.True(attach.TryGetProperty("visualRoots", out JsonElement roots));
        Assert.True(roots.GetArrayLength() > 0);
        int rootId = roots[0].GetProperty("rootElementId").GetInt32();

        int disabledId = await FindByAutomationIdAsync(rootId, DisabledProbeAutomationId, ct);
        int blockedId = await FindByAutomationIdAsync(rootId, BlockedProbeAutomationId, ct);
        int noPeerId = await FindByAutomationIdAsync(rootId, NoPeerProbeAutomationId, ct);
        int slowId = await FindByAutomationIdAsync(rootId, SlowFailingProbeAutomationId, ct);

        // Raygun group 290284826201: a disabled button. Wait mode reports the structured code; post
        // mode (the recorded path) fires and forgets, and the fault must stay inside the payload.
        await AssertToolErrorAsync(ErrorCode.ElementNotEnabled, () => mTools.PeerInvoke(disabledId, InvokePattern, null, ct));
        JsonElement posted = await mTools.PeerInvoke(disabledId, InvokePattern, PostDispatch, ct);
        Assert.True(posted.GetProperty("dispatched").GetBoolean());

        // Raygun group 290708134232: a command whose CanExecute returns false.
        await AssertToolErrorAsync(ErrorCode.CommandNotExecutable, () => mTools.ExecuteCommand(blockedId, null, null, null, ct));
        posted = await mTools.ExecuteCommand(blockedId, null, null, PostDispatch, ct);
        Assert.True(posted.GetProperty("dispatched").GetBoolean());

        // Raygun group 291737304572: an element with no AutomationPeer.
        await AssertToolErrorAsync(ErrorCode.NotDrivable, () => mTools.PeerInvoke(noPeerId, InvokePattern, null, ct));
        posted = await mTools.PeerInvoke(noPeerId, InvokePattern, PostDispatch, ct);
        Assert.True(posted.GetProperty("dispatched").GetBoolean());

        // The ActionPending path: the action outlives the dispatcher wait and THEN throws, after the
        // caller has already been told ActionPending and stopped waiting.
        await AssertToolErrorAsync(ErrorCode.ActionPending, () => mTools.ExecuteCommand(slowId, null, null, null, ct));

        await ForceGcAsync(rootId, ct);
        Assert.Equal("0", await ReadUnobservedCountAsync(rootId, ct));

        // Positive control: a genuinely unobserved fault must be counted, or the zero above is empty.
        JsonElement leak = await mTools.ExecuteCommand(rootId, LeakUnobservedFaultCommandPath, null, null, ct);
        Assert.True(leak.GetProperty("executed").GetBoolean());
        await ForceGcAsync(rootId, ct);
        Assert.Equal("1", await ReadUnobservedCountAsync(rootId, ct));
    }

    private async Task<int> FindByAutomationIdAsync(int rootId, string automationId, CancellationToken ct)
    {
        JsonElement found = await mTools!.FindElements(
            rootId,
            new ElementPredicateDto { AutomationId = automationId },
            ct);
        Assert.True(found.TryGetProperty("matches", out JsonElement matches));
        Assert.True(matches.GetArrayLength() > 0, $"No element with AutomationId={automationId} in SampleWpfApp.");
        return matches[0].GetProperty("id").GetInt32();
    }

    private static async Task AssertToolErrorAsync(ErrorCode expected, Func<Task<JsonElement>> call)
    {
        McpException ex = await Assert.ThrowsAsync<McpException>(call);
        Assert.Contains($"[{expected}]", ex.Message, StringComparison.Ordinal);
    }

    private async Task ForceGcAsync(int rootId, CancellationToken ct)
    {
        JsonElement gc = await mTools!.ExecuteCommand(rootId, ForceGcCommandPath, null, null, ct);
        Assert.True(gc.GetProperty("executed").GetBoolean());
    }

    private async Task<string?> ReadUnobservedCountAsync(int rootId, CancellationToken ct)
    {
        JsonElement read = await mTools!.ReadDataContextPath(rootId, UnobservedCountPath, ct);
        Assert.True(read.GetProperty("pathReachable").GetBoolean());
        return read.GetProperty("value").GetString();
    }
}
