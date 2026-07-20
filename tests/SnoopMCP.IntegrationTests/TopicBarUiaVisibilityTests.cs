// TopicBarUiaVisibilityTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.IntegrationTests;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Host;
using Host.Automation;
using Host.Injection;
using Host.Tools;
using Xunit;

/// <summary>
/// Proves issue #80 ("topic-bar items are invisible to UIA name search") against the real
/// <c>TopicBarControl</c> fixture: its custom <see cref="System.Windows.Automation.Peers.AutomationPeer"/>
/// reports no children, so <c>find_uia_element</c> cannot reach "Browse" no matter how it is scoped —
/// while the payload tier's visual-tree walk (<c>findElements</c>), which never consults automation
/// peers, finds it directly. This is the documented workaround, not a SnoopMCP defect: no UIA client
/// can see a subtree a parent peer refuses to report as children.
/// </summary>
public sealed class TopicBarUiaVisibilityTests : IAsyncLifetime
{
    private const int WindowInitDelaySeconds = 5;
    private const string TopicBarItemName = "Browse";

    private Process? mSampleProcess;
    private SessionManager? mSession;
    private McpTools? mTools;

    public async ValueTask InitializeAsync()
    {
        string samplePath = Path.Combine(AppContext.BaseDirectory, "SampleWpfApp.exe");
        Assert.True(File.Exists(samplePath), $"SampleWpfApp.exe not found at {samplePath}.");

        mSampleProcess = Process.Start(new ProcessStartInfo { FileName = samplePath, UseShellExecute = false })
            ?? throw new InvalidOperationException("Failed to start SampleWpfApp.");

        await Task.Delay(TimeSpan.FromSeconds(WindowInitDelaySeconds));

        mSession = new SessionManager(NullLogger<SessionManager>.Instance, NullLoggerFactory.Instance);
        var injector = new InjectorService(NullLogger<InjectorService>.Instance);
        string gatePath = Path.Combine(Path.GetTempPath(), "gate-" + Guid.NewGuid().ToString("N") + ".json");
        mTools = new McpTools(mSession, injector, new InteractionGate(gatePath));
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
    }

    [Fact]
    public async Task FindUiaElement_ByName_CannotSeeTopicBarItem_BecausePeerPrunesChildren()
    {
        Assert.NotNull(mSampleProcess);
        CancellationToken ct = TestContext.Current.CancellationToken;
        var driver = new UiaDriver(new ElementHandleCache());

        IReadOnlyList<UiaElementInfo> found =
            await driver.FindAsync(mSampleProcess!.Id, "name", TopicBarItemName, ct);

        Assert.Empty(found);
    }

    [Fact]
    public async Task FindElements_ByTextContains_FindsTopicBarItem_ViaVisualTreeNotPeers()
    {
        Assert.NotNull(mSampleProcess);
        Assert.NotNull(mTools);
        CancellationToken ct = TestContext.Current.CancellationToken;

        JsonElement attach = await mTools!.Attach(mSampleProcess!.Id, ct);
        JsonElement roots = attach.GetProperty("visualRoots");
        int rootElementId = roots[0].GetProperty("rootElementId").GetInt32();

        JsonElement found = await mTools.FindElements(
            rootElementId,
            new Protocol.Tools.ElementPredicateDto { TextContains = TopicBarItemName, LeafOnly = true },
            ct);

        JsonElement matches = found.GetProperty("matches");
        Assert.True(matches.GetArrayLength() > 0);
        bool any = false;
        foreach (JsonElement match in matches.EnumerateArray())
        {
            string text = match.GetProperty("visibleText").GetString() ?? string.Empty;
            any = any || text.Contains(TopicBarItemName, StringComparison.Ordinal);
        }
        Assert.True(any, "Expected a payload-tier match whose visible text contains 'Browse'.");
    }
}
