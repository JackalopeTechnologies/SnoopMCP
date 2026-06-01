// WalkthroughCaptureTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

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
        var injector = new InjectorService(NullLogger<InjectorService>.Instance);
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

    private static JsonElement FirstRootOfKind(JsonElement listVisualRootsResponse, string kind)
    {
        JsonElement result = default;
        foreach (JsonElement root in listVisualRootsResponse.GetProperty("roots").EnumerateArray())
        {
            bool isKind = string.Equals(root.GetProperty("kind").GetString(), kind, StringComparison.Ordinal);
            if (isKind && result.ValueKind != JsonValueKind.Object)
            {
                result = root.Clone();
            }
        }
        return result;
    }

    [Fact(Skip = "Manual capture; run by hand to refresh walkthrough.md. See class XML doc for the command.")]
    public async Task CaptureWalkthroughTranscript()
    {
        Assert.NotNull(mTools);
        Assert.NotNull(mSampleProcess);
        CancellationToken ct = TestContext.Current.CancellationToken;

        // --- Discovery & lifecycle ---
        JsonElement procs = await McpTools.ListWpfProcesses(ct);
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

        // --- Search & spatial ---
        JsonElement byName = await mTools.FindElements(rootId, new ElementPredicateDto { Name = "SaveButton" }, ct);
        mTranscript.Add("Search", "findElements", new { rootId, predicate = new { name = "SaveButton" } }, byName);

        JsonElement byText = await mTools.FindElements(
            rootId, new ElementPredicateDto { TextContains = "Springfield" }, ct);
        mTranscript.Add(
            "Search", "findElements", new { rootId, predicate = new { textContains = "Springfield" } }, byText);

        JsonElement hit = await mTools.HitTest(rootId, 50, 50, ct);
        mTranscript.Add("Search", "hitTest", new { rootId, x = 50, y = 50 }, hit);

        string rootPath = desc.GetProperty("path").GetString() ?? "/Window";
        JsonElement resolved = await mTools.ResolvePath(rootId, rootPath, ct);
        mTranscript.Add("Search", "resolvePath", new { rootId, pathString = rootPath }, resolved);

        // --- DataContext ---
        int brokenBindingId = await FindFirstIdAsync(rootId, new ElementPredicateDto { Name = "BrokenBinding" }, ct);

        JsonElement dcShape = await mTools.DescribeDataContext(brokenBindingId, ct);
        mTranscript.Add("DataContext", "describeDataContext", new { id = brokenBindingId }, dcShape);

        JsonElement dcOk = await mTools.ReadDataContextPath(rootId, "SelectedCustomer.Name", ct);
        mTranscript.Add(
            "DataContext", "readDataContextPath", new { id = rootId, path = "SelectedCustomer.Name" }, dcOk);

        JsonElement dcBroken = await mTools.ReadDataContextPath(brokenBindingId, "Address.Country.Name", ct);
        mTranscript.Add(
            "DataContext", "readDataContextPath",
            new { id = brokenBindingId, path = "Address.Country.Name" }, dcBroken);

        // --- Dependency properties ---
        int deleteButtonId = await FindFirstIdAsync(rootId, new ElementPredicateDto { Name = "DeleteButton" }, ct);

        JsonElement dps = await mTools.ListDependencyProperties(saveButtonId, ct);
        mTranscript.Add("DependencyProperties", "listDependencyProperties", new { id = saveButtonId }, dps);

        JsonElement bg = await mTools.GetDependencyProperty(deleteButtonId, "Background", ct);
        mTranscript.Add(
            "DependencyProperties", "getDependencyProperty",
            new { id = deleteButtonId, propertyName = "Background" }, bg);

        // --- Styles & templates ---
        JsonElement style = await mTools.ResolveStyle(deleteButtonId, ct);
        mTranscript.Add("StylesTemplates", "resolveStyle", new { id = deleteButtonId }, style);

        JsonElement template = await mTools.ResolveTemplate(saveButtonId, ct);
        mTranscript.Add("StylesTemplates", "resolveTemplate", new { id = saveButtonId }, template);

        // --- Bindings ---
        JsonElement inspect = await mTools.InspectBinding(brokenBindingId, "Text", ct);
        mTranscript.Add("Bindings", "inspectBinding", new { id = brokenBindingId, propertyName = "Text" }, inspect);

        JsonElement listed = await mTools.ListBindings(detailPaneId, true, ct);
        mTranscript.Add("Bindings", "listBindings", new { id = detailPaneId, includeDescendants = true }, listed);

        // --- Snapshot ---
        JsonElement xaml = await mTools.ExportXaml(detailPaneId, ct);
        mTranscript.Add("Snapshot", "exportXaml", new { id = detailPaneId }, xaml);

        // --- Popup (UIA opens the dropdown; the only non-MCP step) ---
        bool opened = ComboBoxAutomation.TrySetDropDownOpen(ThemeComboAutomationId, open: true);
        if (opened)
        {
            await Task.Delay(500);
            JsonElement rootsWithPopup = await mTools.ListVisualRoots(ct);
            mTranscript.Add("Popup", "listVisualRoots", new { note = "ThemeCombo dropdown open" }, rootsWithPopup);

            JsonElement popupRoot = FirstRootOfKind(rootsWithPopup, "Popup");
            if (popupRoot.ValueKind == JsonValueKind.Object)
            {
                int popupRootId = popupRoot.GetProperty("rootElementId").GetInt32();
                JsonElement popupKids = await mTools.GetChildren(popupRootId, "visual", ct);
                mTranscript.Add("Popup", "getChildren", new { id = popupRootId, tree = "visual" }, popupKids);
            }

            ComboBoxAutomation.TrySetDropDownOpen(ThemeComboAutomationId, open: false);
        }

        // --- Wrap-up ---
        JsonElement detach = await mTools.Detach(ct);
        mTranscript.Add("Wrapup", "detach", new { }, detach);

        await mTranscript.WriteAsync();
    }
}
