// DrivingE2ETests.cs
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
using Xunit;

/// <summary>
/// End-to-end payload-driving round-trip: spawns the real SampleWpfApp, attaches via the host's
/// injector + session, then drives it through the full host relay -> pipe -> payload path. Executes
/// the RunProbe button's bound <c>RunProbeCommand</c> via <c>executeCommand</c>, confirms the
/// ground-truth <c>MainViewModel.ProbeStatus</c> DataContext value actually changed via
/// <c>waitForValue</c>, and proves the host-side <see cref="InteractionGate"/> blocks
/// <c>executeCommand</c> once disabled. Mirrors the launch fixture in <see cref="EndToEndTests"/> but
/// owns its own gate so the test can flip it mid-run.
/// </summary>
public sealed class DrivingE2ETests : IAsyncLifetime
{
    private const int WindowInitDelaySeconds = 5;
    private const string RunProbeAutomationId = "RunProbe";
    private const string ProbeStatusDataContextPath = "ProbeStatus";
    private const string ProbeStatusDoneValue = "done";
    private const int WaitForValueTimeoutMs = 3000;

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
    public async Task ExecuteCommand_RunProbe_ThenWaitForValue_MatchesDone_AndGateOffBlocks()
    {
        Assert.NotNull(mTools);
        Assert.NotNull(mGate);
        Assert.NotNull(mSampleProcess);
        CancellationToken ct = TestContext.Current.CancellationToken;

        JsonElement attach = await mTools!.Attach(mSampleProcess!.Id, ct);
        Assert.True(attach.TryGetProperty("visualRoots", out JsonElement roots));
        Assert.True(roots.GetArrayLength() > 0);
        int rootElementId = roots[0].GetProperty("rootElementId").GetInt32();

        // Locate the RunProbe button by its AutomationProperties.AutomationId (its x:Name is
        // "RunProbeButton", but the AutomationId is the stable identity the fixture documents).
        JsonElement found = await mTools.FindElements(
            rootElementId,
            new Protocol.Tools.ElementPredicateDto { AutomationId = RunProbeAutomationId },
            ct);
        Assert.True(found.TryGetProperty("matches", out JsonElement matches));
        Assert.True(matches.GetArrayLength() > 0, "RunProbe button not found via findElements(AutomationId=RunProbe).");
        int runProbeId = matches[0].GetProperty("id").GetInt32();

        // Gate ON: execute the button's own ICommandSource.Command (path=null) -> RunProbeCommand.
        JsonElement exec = await mTools.ExecuteCommand(runProbeId, null, null, null, ct);
        Assert.True(exec.GetProperty("executed").GetBoolean());

        // Ground-truth check: the root Window's DataContext is the MainViewModel, so
        // dataContextPath="ProbeStatus" reads MainViewModel.ProbeStatus directly (dependencyProperty
        // stays null - exactly one source, per the waiter's XOR rule).
        JsonElement wait = await mTools.WaitForValue(
            rootElementId,
            ProbeStatusDoneValue,
            WaitForValueTimeoutMs,
            dataContextPath: ProbeStatusDataContextPath,
            cancellationToken: ct);
        Assert.True(wait.GetProperty("matched").GetBoolean());
        if (wait.TryGetProperty("actualValue", out JsonElement actual) && actual.ValueKind == JsonValueKind.String)
        {
            Assert.Equal(ProbeStatusDoneValue, actual.GetString());
        }

        // Gate OFF: the host-side InteractionGate must block the mutating tool before it ever reaches
        // the payload, reporting InteractionDisabled rather than a session/attach failure.
        mGate!.SetEnabled(false);
        McpException ex = await Assert.ThrowsAsync<McpException>(
            () => mTools.ExecuteCommand(runProbeId, null, null, null, ct));
        Assert.Contains(nameof(ErrorCode.InteractionDisabled), ex.Message);
    }
}
