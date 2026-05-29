// WalkthroughCaptureTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.IntegrationTests;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SnoopMCP.Host;
using SnoopMCP.Host.Injection;
using SnoopMCP.Host.Tools;
using SnoopMCP.Protocol.Tools;
using Xunit;

/// <summary>
/// Skip-by-default capture run that drives every v1.1 tool against a live SampleWpfApp in document
/// order and writes walkthrough-transcript.json. NOT a regression gate — it generates the doc's
/// source-of-truth transcript.
///
/// To refresh the transcript: remove the Skip below, then run
///   dotnet test tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj \
///       --filter FullyQualifiedName~CaptureWalkthroughTranscript
/// inspect the regenerated walkthrough-transcript.json, restore the Skip, and commit the JSON.
/// </summary>
public sealed class WalkthroughCaptureTests : IAsyncLifetime
{
    private const int WindowInitDelaySeconds = 5;
    private const string ThemeComboAutomationId = "ThemePicker";

    private Process? mSampleProcess;
    private SessionManager? mSession;
    private McpTools? mTools;
    private readonly WalkthroughTranscript mTranscript = new();

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
        var probe = new ProcessProbe();
        var injector = new InjectorService(probe, NullLogger<InjectorService>.Instance);
        mTools = new McpTools(mSession, injector);
    }

    public async ValueTask DisposeAsync()
    {
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
    }

    private async Task<int> FindFirstIdAsync(int rootId, ElementPredicateDto predicate, CancellationToken ct)
    {
        JsonElement found = await mTools!.FindElements(rootId, predicate, ct);
        JsonElement matches = found.GetProperty("matches");
        Assert.True(matches.GetArrayLength() > 0, "No element matched predicate.");
        return matches[0].GetProperty("id").GetInt32();
    }

    [Fact(Skip = "Manual capture; run by hand to refresh walkthrough.md. See class XML doc for the command.")]
    public async Task CaptureWalkthroughTranscript()
    {
        Assert.NotNull(mTools);
        Assert.NotNull(mSampleProcess);
        CancellationToken ct = TestContext.Current.CancellationToken;

        // --- Discovery & lifecycle ---
        JsonElement procs = await mTools!.ListWpfProcesses(ct);
        mTranscript.Add("Discovery", "listWpfProcesses", new { }, procs);

        var attachReq = new { pid = mSampleProcess!.Id };
        JsonElement attach = await mTools.Attach(mSampleProcess.Id, ct);
        mTranscript.Add("Discovery", "attach", attachReq, attach);

        // --- Orientation ---
        JsonElement roots = await mTools.ListVisualRoots(ct);
        mTranscript.Add("Orientation", "listVisualRoots", new { }, roots);
        int rootId = roots.GetProperty("roots")[0].GetProperty("rootElementId").GetInt32();

        JsonElement desc = await mTools.DescribeElement(rootId, ct);
        mTranscript.Add("Orientation", "describeElement", new { id = rootId }, desc);

        // --- Tree navigation ---
        int detailPaneId = await FindFirstIdAsync(rootId, new ElementPredicateDto { Name = "DetailPane" }, ct);

        JsonElement kidsVisual = await mTools.GetChildren(detailPaneId, "visual", ct);
        mTranscript.Add("Navigation", "getChildren", new { id = detailPaneId, tree = "visual" }, kidsVisual);

        JsonElement kidsLogical = await mTools.GetChildren(detailPaneId, "logical", ct);
        mTranscript.Add("Navigation", "getChildren", new { id = detailPaneId, tree = "logical" }, kidsLogical);

        int firstVisualChild = kidsVisual.GetProperty("children")[0].GetProperty("id").GetInt32();
        JsonElement parent = await mTools.GetParent(firstVisualChild, "visual", ct);
        mTranscript.Add("Navigation", "getParent", new { id = firstVisualChild, tree = "visual" }, parent);

        int saveButtonId = await FindFirstIdAsync(rootId, new ElementPredicateDto { Name = "SaveButton" }, ct);
        JsonElement saveKids = await mTools.GetChildren(saveButtonId, "visual", ct);
        int templatedChild = saveKids.GetProperty("children")[0].GetProperty("id").GetInt32();
        JsonElement templatedParent = await mTools.GetTemplatedParent(templatedChild, ct);
        mTranscript.Add("Navigation", "getTemplatedParent", new { id = templatedChild }, templatedParent);

        await mTranscript.WriteAsync();
    }
}
