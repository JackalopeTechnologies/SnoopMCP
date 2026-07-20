// EndToEndTests.cs
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
/// End-to-end gate: spawns the real SampleWpfApp, attaches via the host's injector + session, and
/// drives every read-only MCP tool against the live process. Asserts response shape (not per-tool
/// semantics, which the payload unit tests cover). This is the only test that exercises the full
/// inject -> pipe -> dispatcher -> tool path against a real .NET 10 WPF target.
/// </summary>
public sealed class EndToEndTests : IAsyncLifetime
{
    private const int WindowInitDelaySeconds = 5;

    private Process? mSampleProcess;
    private SessionManager? mSession;
    private McpTools? mTools;
    private JsonElement mAttach;

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
        string gatePath = Path.Combine(Path.GetTempPath(), "gate-" + Guid.NewGuid().ToString("N") + ".json");
        mTools = new McpTools(mSession, injector, new InteractionGate(gatePath));

        mAttach = await mTools.Attach(mSampleProcess.Id, TestContext.Current.CancellationToken);
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
    public async Task EveryReadOnlyTool_ReturnsShapedResponse()
    {
        Assert.NotNull(mTools);
        CancellationToken ct = TestContext.Current.CancellationToken;

        JsonElement roots = await mTools!.ListVisualRoots(ct);
        JsonElement rootArr = roots.GetProperty("roots");
        Assert.True(rootArr.GetArrayLength() > 0);
        int rootElementId = rootArr[0].GetProperty("rootElementId").GetInt32();

        JsonElement desc = await mTools.DescribeElement(rootElementId, ct);
        Assert.True(desc.TryGetProperty("type", out _));
        Assert.True(desc.TryGetProperty("bounds", out _));
        Assert.True(desc.TryGetProperty("path", out _));

        JsonElement kids = await mTools.GetChildren(rootElementId, "visual", ct);
        JsonElement children = kids.GetProperty("children");
        Assert.True(children.GetArrayLength() > 0);
        int childId = children[0].GetProperty("id").GetInt32();

        // getParent on a child returns a non-null parent (the root is omitted from JSON when null
        // because the wire serializer drops null properties — so test against a child, not the root).
        JsonElement parent = await mTools.GetParent(childId, "visual", ct);
        Assert.True(parent.TryGetProperty("parent", out _));

        // The root has no templated parent / no hit / nothing above it; these legitimately return an
        // empty object once the null member is dropped. Assert the call round-tripped a JSON object.
        JsonElement templated = await mTools.GetTemplatedParent(rootElementId, ct);
        Assert.Equal(JsonValueKind.Object, templated.ValueKind);

        JsonElement found = await mTools.FindElements(
            rootElementId,
            new Protocol.Tools.ElementPredicateDto { Type = "Button" },
            ct);
        Assert.True(found.TryGetProperty("matches", out _));

        JsonElement hit = await mTools.HitTest(rootElementId, 50, 50, ct);
        Assert.Equal(JsonValueKind.Object, hit.ValueKind);

        JsonElement path = desc.GetProperty("path");
        JsonElement resolved = await mTools.ResolvePath(rootElementId, path.GetString() ?? "/Window", ct);
        Assert.Equal(JsonValueKind.Object, resolved.ValueKind);

        JsonElement dc = await mTools.DescribeDataContext(rootElementId, ct);
        Assert.True(dc.TryGetProperty("dataContext", out _));

        JsonElement dcRead = await mTools.ReadDataContextPath(rootElementId, "SelectedCustomer.Name", ct);
        Assert.True(dcRead.TryGetProperty("pathReachable", out _));

        JsonElement dps = await mTools.ListDependencyProperties(rootElementId, ct);
        Assert.True(dps.GetProperty("properties").GetArrayLength() > 0);

        JsonElement widthDp = await mTools.GetDependencyProperty(rootElementId, "Width", ct);
        Assert.True(widthDp.TryGetProperty("winningSource", out _));

        JsonElement style = await mTools.ResolveStyle(rootElementId, ct);
        Assert.True(style.TryGetProperty("setters", out _));

        JsonElement tpl = await mTools.ResolveTemplate(rootElementId, ct);
        Assert.True(tpl.TryGetProperty("namedParts", out _));

        JsonElement binding = await mTools.InspectBinding(rootElementId, "DataContext", ct);
        Assert.True(binding.TryGetProperty("state", out _));

        JsonElement listed = await mTools.ListBindings(rootElementId, true, ct);
        Assert.True(listed.TryGetProperty("bindings", out _));

        JsonElement xaml = await mTools.ExportXaml(rootElementId, ct);
        Assert.True(xaml.TryGetProperty("xaml", out _));
        Assert.True(xaml.GetProperty("byteCount").GetInt32() > 0);
    }

    [Fact]
    public async Task ListWpfProcesses_ReturnsSampleWpfApp_AsAttachable()
    {
        Assert.NotNull(mTools);
        Assert.NotNull(mSampleProcess);
        CancellationToken ct = TestContext.Current.CancellationToken;

        JsonElement result = await McpTools.ListWpfProcesses(ct);

        JsonElement processes = result.GetProperty("processes");
        Assert.Equal(JsonValueKind.Array, processes.ValueKind);

        bool found = false;
        bool attachable = false;
        foreach (JsonElement proc in processes.EnumerateArray())
        {
            string name = proc.GetProperty("processName").GetString() ?? string.Empty;
            bool isSample = string.Equals(name, "SampleWpfApp", StringComparison.Ordinal);
            if (isSample)
            {
                found = true;
                attachable = proc.GetProperty("attachable").GetBoolean();
            }
        }

        Assert.True(found, "SampleWpfApp not found in listWpfProcesses output.");
        Assert.True(attachable, "SampleWpfApp was discovered but reported as non-attachable.");
    }

    [Fact]
    public async Task Attach_SeedsAndReturnsRoots_AndExpiredIdsReportAnActionableHint()
    {
        Assert.NotNull(mTools);
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Attach now hands back the live visual roots and seeds the payload registry with them.
        Assert.True(mAttach.TryGetProperty("visualRoots", out JsonElement roots));
        Assert.Equal(JsonValueKind.Array, roots.ValueKind);
        Assert.True(roots.GetArrayLength() > 0);
        int rootElementId = roots[0].GetProperty("rootElementId").GetInt32();

        // Because attach seeded the registry, the root id resolves immediately - no list_visual_roots
        // call is made in this test before describing it.
        JsonElement desc = await mTools!.DescribeElement(rootElementId, ct);
        Assert.True(desc.TryGetProperty("type", out _));

        // A never-registered id surfaces the enriched, self-correcting ElementExpired message.
        McpException ex = await Assert.ThrowsAsync<McpException>(
            () => mTools.FindElements(999_999, new Protocol.Tools.ElementPredicateDto { Type = "Button" }, ct));
        Assert.Contains(nameof(ErrorCode.ElementExpired), ex.Message);
        Assert.Contains("list_visual_roots", ex.Message);
    }
}
