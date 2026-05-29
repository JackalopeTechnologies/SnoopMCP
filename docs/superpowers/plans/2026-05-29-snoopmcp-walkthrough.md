# SnoopMCP v1.1 Walkthrough — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce `docs/walkthrough.md` — a category-organised, scene-plus-transcript demonstration of all 20 v1.1 read-only tools against the real `SampleWpfApp`, with every response captured from a live attached session.

**Architecture:** A skip-by-default xUnit fact (`CaptureWalkthroughTranscript`) spawns `SampleWpfApp`, attaches via the existing `McpTools` path, drives every tool in document order against deliberately-chosen elements, and writes a labelled `walkthrough-transcript.json`. The doc author then cherry-picks trimmed excerpts from that committed JSON into `docs/walkthrough.md`. The ComboBox popup scene opens the dropdown via in-box UI Automation (the only non-MCP capture step).

**Tech Stack:** .NET 10 (`net10.0-windows`), xunit.v3 3.2.2, `System.Windows.Automation` (UIA, via `UseWPF`), the existing `SnoopMCP.Host`/`SnoopMCP.Protocol` assemblies, `WireSerializer.JsonOptions` for transcript formatting.

**Spec:** `docs/superpowers/specs/2026-05-29-snoopmcp-walkthrough-design.md`

---

## SampleWpfApp fidelity check (DONE — no XAML changes needed)

The sample already exposes every fixture the scene script needs. Reconciliation result, with **two corrections** to the spec's loose wording baked into the targets below:

| Scene / tool | Target element | How the capture finds it | What it demonstrates |
|---|---|---|---|
| `listWpfProcesses` | — (host-side) | direct call | `SampleWpfApp` appears, `attachable=true` |
| `attach` | the sample PID | from the lifetime | session metadata (processName, runtime/framework version, bitness) |
| `listVisualRoots` (window-only) | — | direct call | single `Window` root |
| `describeElement` | window root | `roots[0].rootElementId` | type, bounds, path |
| `getChildren` (visual) | `DetailPane` | `findElements(root, {Name:"DetailPane"})` | visual tree dives into the `ContentPresenter`'s realised template content |
| `getChildren` (logical) | `DetailPane` | same id | logical tree shows the bare content node — the visual/logical divergence |
| `getParent` | a `DetailPane` visual child | from the visual `getChildren` | climbs back up |
| `getTemplatedParent` | a templated child of `SaveButton` (`PART_Border`/`PART_Content`) | find `SaveButton` → `getChildren` visual → first child | climbs out of `CustomButtonTemplate` to `SaveButton` |
| `findElements` (by name) | `SaveButton` | `{Name:"SaveButton"}` | exact single match |
| `findElements` (by text) | seeded customer rows | `{TextContains:"Springfield"}` | substring text search hits |
| `hitTest` | a point over the button row | `root`, x/y near bottom-right | deepest visual at a point |
| `resolvePath` | window root's own path | echo `describeElement.path` back | path string round-trips to an element |
| `describeDataContext` | `BrokenBinding` TextBlock | find within `DetailPane` by `{Name:"BrokenBinding"}` | **Customer** CLR shape (Name/Email/Address) — the template TextBlocks inherit the Customer DataContext, NOT the ContentControl's MainViewModel |
| `readDataContextPath` (reachable) | window root | `"SelectedCustomer.Name"` | `pathReachable=true`, value `Customer 0000` |
| `readDataContextPath` (broken) | `BrokenBinding` TextBlock | `"Address.Country.Name"` | `pathReachable=false`, stops at `Country` (Address exists, Country does not) |
| `listDependencyProperties` | `SaveButton` | SaveButton id | the DP catalogue |
| `getDependencyProperty` | `DeleteButton`, `"Background"` | DeleteButton id | `winningSource` is a style setter → Crimson, with the precedence trace |
| `resolveStyle` | **`DeleteButton`** | DeleteButton id | `DangerButtonStyle` + the 3-level `BasedOn` chain **Danger→Primary→Base**, setters, and the inherited `IsMouseOver`/`IsEnabled` **style** triggers |
| `resolveTemplate` | **`SaveButton`** | SaveButton id | `CustomButtonTemplate`, named parts `PART_Border`/`PART_Content`, and the `IsPressed` **template** trigger |
| `inspectBinding` | `BrokenBinding`, `"Text"` | BrokenBinding id | binding path `Address.Country.Name`, error state |
| `listBindings` | `DetailPane`, `includeDescendants=true` | DetailPane id | every binding under the pane, including the broken one |
| `exportXaml` | `DetailPane` | DetailPane id | live XAML snapshot + `byteCount` |
| UIA open `ThemeCombo` | `ThemeCombo` (`AutomationId="ThemePicker"`) | `ExpandCollapsePattern` | (sets up the popup scene) |
| `listVisualRoots` (with popup) | — | direct call | a second root of kind `Popup` with `openedBy` back-reference |
| `getChildren` | the popup root | `popup.rootElementId` | walking the popup's content |
| `detach` | — | direct call | session closed |

**Corrections vs spec wording:** (1) the spec said `resolveTemplate` shows an "IsMouseOver trigger" — the `CustomButtonTemplate` actually carries an **`IsPressed`** trigger ([MainWindow.xaml:23](../../samples/SampleWpfApp/MainWindow.xaml)); the `IsMouseOver` trigger lives on `PrimaryButtonStyle` and is surfaced by `resolveStyle`. (2) The 3-level `BasedOn` chain is on `DangerButtonStyle` (the `DeleteButton`); the `SaveButton`'s `PrimaryButtonStyle` is only 2 levels, so `resolveStyle` targets the `DeleteButton`.

---

## File Structure

- **Create** `tests/SnoopMCP.IntegrationTests/WalkthroughRecord.cs` — the immutable `{scene, tool, request, response}` record. Its own file because STR0008 (one type per file) is not suppressed under `tests/`.
- **Create** `tests/SnoopMCP.IntegrationTests/WalkthroughTranscript.cs` — buffers `WalkthroughRecord`s and writes the JSON file. One responsibility: capture serialisation.
- **Create** `tests/SnoopMCP.IntegrationTests/ComboBoxAutomation.cs` — a tiny static helper that opens/closes a ComboBox by AutomationId via UIA. One responsibility: the single non-MCP interaction.
- **Create** `tests/SnoopMCP.IntegrationTests/WalkthroughCaptureTests.cs` — the skip-by-default fact + its sample-process lifetime. Drives the scene script.
- **Create** `tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json` — the committed capture artifact (written by running the fact).
- **Create** `docs/walkthrough.md` — the deliverable doc.
- **Modify** `tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj` — add `<UseWPF>true</UseWPF>` for `System.Windows.Automation`, and copy the transcript JSON to output so the fact can find/overwrite it next to the test (it writes to source path via `[CallerFilePath]`).

---

### Task 1: Capture infrastructure + first scenes

**Files:**
- Modify: `tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj`
- Create: `tests/SnoopMCP.IntegrationTests/WalkthroughRecord.cs`
- Create: `tests/SnoopMCP.IntegrationTests/WalkthroughTranscript.cs`
- Create: `tests/SnoopMCP.IntegrationTests/ComboBoxAutomation.cs`
- Create: `tests/SnoopMCP.IntegrationTests/WalkthroughCaptureTests.cs`

- [ ] **Step 1: Enable UIA in the test project**

In `SnoopMCP.IntegrationTests.csproj`, add `<UseWPF>true</UseWPF>` to the existing first `<PropertyGroup>` (right after `<RootNamespace>`). This pulls the Windows Desktop framework refs (including `UIAutomationClient`/`UIAutomationTypes`) so `System.Windows.Automation` resolves.

```xml
        <RootNamespace>SnoopMCP.IntegrationTests</RootNamespace>
        <UseWPF>true</UseWPF>
```

- [ ] **Step 2: Write the transcript record + recorder**

Create `WalkthroughTranscript.cs`. Two types in one file is normally an analyzer violation (STR0008), but this is under `tests/` where the per-tree `Directory.Build.props` does not suppress STR0008 — so split is required. Put the recorder here and the record in its own file instead:

Create `tests/SnoopMCP.IntegrationTests/WalkthroughRecord.cs`:

```csharp
// WalkthroughRecord.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.IntegrationTests;

using System.Text.Json;

/// <summary>One captured tool call: the scene it belongs to, the tool name, the request, the response.</summary>
public sealed record WalkthroughRecord(string Scene, string Tool, object Request, JsonElement Response);
```

Create `tests/SnoopMCP.IntegrationTests/WalkthroughTranscript.cs`:

```csharp
// WalkthroughTranscript.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.IntegrationTests;

using System.Runtime.CompilerServices;
using System.Text.Json;
using SnoopMCP.Protocol.Wire;

/// <summary>
/// Buffers <see cref="WalkthroughRecord"/> entries during a capture run and writes them to
/// <c>walkthrough-transcript.json</c> beside this source file. The doc author cherry-picks excerpts
/// from the committed JSON into <c>docs/walkthrough.md</c>.
/// </summary>
public sealed class WalkthroughTranscript
{
    private const string TranscriptFileName = "walkthrough-transcript.json";

    private readonly List<WalkthroughRecord> mRecords = new();

    public void Add(string scene, string tool, object request, JsonElement response)
    {
        mRecords.Add(new WalkthroughRecord(scene, tool, request, response.Clone()));
    }

    /// <summary>Writes the buffered records to the JSON file beside this source file.</summary>
    public async Task WriteAsync([CallerFilePath] string callerPath = "")
    {
        string dir = Path.GetDirectoryName(callerPath) ?? AppContext.BaseDirectory;
        string target = Path.Combine(dir, TranscriptFileName);
        string json = JsonSerializer.Serialize(mRecords, WireSerializer.JsonOptions);
        await File.WriteAllTextAsync(target, json);
    }
}
```

- [ ] **Step 3: Write the UIA ComboBox helper**

Create `ComboBoxAutomation.cs`:

```csharp
// ComboBoxAutomation.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.IntegrationTests;

using System.Windows.Automation;

/// <summary>
/// Opens or closes a ComboBox by its AutomationId via UI Automation. This is the only capture step
/// that drives the sample app through a path other than the MCP tool surface; the walkthrough notes
/// that a human reader would simply click the dropdown open instead. Searches the desktop root by
/// AutomationId (unique for the sample's ThemePicker).
/// </summary>
public static class ComboBoxAutomation
{
    public static bool TrySetDropDownOpen(string automationId, bool open)
    {
        bool ok = false;
        Condition byId = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
        AutomationElement? combo = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, byId);
        if (combo is not null
            && combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out object patternObj)
            && patternObj is ExpandCollapsePattern pattern)
        {
            if (open)
            {
                pattern.Expand();
            }
            else
            {
                pattern.Collapse();
            }
            ok = true;
        }
        return ok;
    }
}
```

- [ ] **Step 4: Write the capture fact skeleton + lifecycle/orientation/navigation scenes**

Create `WalkthroughCaptureTests.cs`. Mirror `EndToEndTests`'s lifetime, but attach **inside the body** so `attach` is itself captured. The lifetime only owns the sample process.

```csharp
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
        Assert.True(matches.GetArrayLength() > 0, $"No element matched predicate.");
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
```

- [ ] **Step 5: Build**

Run: `dotnet build E:/GitHub/SnoopMCP/tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj -c Debug -p:TreatWarningsAsErrors=true`
Expected: `0 Warning(s) 0 Error(s)`. If `System.Windows.Automation` does not resolve, confirm `<UseWPF>true</UseWPF>` landed in the csproj.

- [ ] **Step 6: Verify the capture runs (un-skip → run → inspect → re-skip)**

Temporarily delete the `Skip = "..."` argument (leave `[Fact]`). Run:

`dotnet test E:/GitHub/SnoopMCP/tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj -c Debug --filter FullyQualifiedName~CaptureWalkthroughTranscript`

Expected: 1 passed. Then Read `tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json` and confirm it contains records for scenes `Discovery`, `Orientation`, `Navigation` with tools `listWpfProcesses`, `attach`, `listVisualRoots`, `describeElement`, `getChildren` (×2), `getParent`, `getTemplatedParent`. Restore the `Skip` argument.

- [ ] **Step 7: Commit (code only — NOT the transcript yet)**

```bash
git -C E:/GitHub/SnoopMCP add tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj tests/SnoopMCP.IntegrationTests/WalkthroughRecord.cs tests/SnoopMCP.IntegrationTests/WalkthroughTranscript.cs tests/SnoopMCP.IntegrationTests/ComboBoxAutomation.cs tests/SnoopMCP.IntegrationTests/WalkthroughCaptureTests.cs
git -C E:/GitHub/SnoopMCP commit -F <msgfile>
```

Message: "A2: walkthrough capture infrastructure + lifecycle/orientation/navigation scenes". The transcript JSON is committed in Task 4 once all scenes are present.

---

### Task 2: Search, spatial, DataContext, and dependency-property scenes

**Files:**
- Modify: `tests/SnoopMCP.IntegrationTests/WalkthroughCaptureTests.cs`

- [ ] **Step 1: Insert the scenes before `await mTranscript.WriteAsync();`**

Add this block (it reuses `rootId`, `detailPaneId`, `saveButtonId` from Task 1):

```csharp
        // --- Search & spatial ---
        JsonElement byName = await mTools.FindElements(rootId, new ElementPredicateDto { Name = "SaveButton" }, ct);
        mTranscript.Add("Search", "findElements", new { rootId, predicate = new { name = "SaveButton" } }, byName);

        JsonElement byText = await mTools.FindElements(rootId, new ElementPredicateDto { TextContains = "Springfield" }, ct);
        mTranscript.Add("Search", "findElements", new { rootId, predicate = new { textContains = "Springfield" } }, byText);

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
        mTranscript.Add("DataContext", "readDataContextPath", new { id = rootId, path = "SelectedCustomer.Name" }, dcOk);

        JsonElement dcBroken = await mTools.ReadDataContextPath(brokenBindingId, "Address.Country.Name", ct);
        mTranscript.Add("DataContext", "readDataContextPath", new { id = brokenBindingId, path = "Address.Country.Name" }, dcBroken);

        // --- Dependency properties ---
        int deleteButtonId = await FindFirstIdAsync(rootId, new ElementPredicateDto { Name = "DeleteButton" }, ct);

        JsonElement dps = await mTools.ListDependencyProperties(saveButtonId, ct);
        mTranscript.Add("DependencyProperties", "listDependencyProperties", new { id = saveButtonId }, dps);

        JsonElement bg = await mTools.GetDependencyProperty(deleteButtonId, "Background", ct);
        mTranscript.Add("DependencyProperties", "getDependencyProperty", new { id = deleteButtonId, propertyName = "Background" }, bg);
```

- [ ] **Step 2: Build**

Run: `dotnet build E:/GitHub/SnoopMCP/tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj -c Debug -p:TreatWarningsAsErrors=true`
Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Verify (un-skip → run → inspect → re-skip)**

Same procedure as Task 1 Step 6. Confirm the transcript now also contains `Search`, `DataContext`, and `DependencyProperties` scenes. Spot-check that the `readDataContextPath` broken record shows `pathReachable: false` and that `getDependencyProperty` Background shows a style-setter winning source with a Crimson value. Restore the `Skip`.

- [ ] **Step 4: Commit (code only)**

```bash
git -C E:/GitHub/SnoopMCP add tests/SnoopMCP.IntegrationTests/WalkthroughCaptureTests.cs
git -C E:/GitHub/SnoopMCP commit -F <msgfile>
```

Message: "A2: search, spatial, DataContext, and dependency-property capture scenes".

---

### Task 3: Styles, templates, bindings, snapshot, popup, detach

**Files:**
- Modify: `tests/SnoopMCP.IntegrationTests/WalkthroughCaptureTests.cs`

- [ ] **Step 1: Insert the scenes before `await mTranscript.WriteAsync();`**

```csharp
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
```

- [ ] **Step 2: Add the `FirstRootOfKind` helper**

Add this private method to the class (next to `FindFirstIdAsync`):

```csharp
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
```

- [ ] **Step 3: Build**

Run: `dotnet build E:/GitHub/SnoopMCP/tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj -c Debug -p:TreatWarningsAsErrors=true`
Expected: `0 Warning(s) 0 Error(s)`. If the `kind` field name differs from the captured JSON, adjust to match `VisualRootDto` (it is `Kind` → serialised `kind`).

- [ ] **Step 4: Verify (un-skip → run → inspect → re-skip)**

Same procedure. Confirm scenes `StylesTemplates`, `Bindings`, `Snapshot`, `Wrapup` are present. Confirm `resolveStyle` on the DeleteButton shows the `Danger→Primary→Base` BasedOn chain; `resolveTemplate` on the SaveButton shows `PART_Border`/`PART_Content` and the `IsPressed` trigger; `inspectBinding` shows the `Address.Country.Name` path in an error state. If the UIA popup opened, confirm a `Popup` scene with a `Popup`-kind root carrying `openedBy`. (If UIA could not open the dropdown in the run environment, the `Popup` scene is simply absent — acceptable; note it and proceed.) Restore the `Skip`.

- [ ] **Step 5: Commit (code only)**

```bash
git -C E:/GitHub/SnoopMCP add tests/SnoopMCP.IntegrationTests/WalkthroughCaptureTests.cs
git -C E:/GitHub/SnoopMCP commit -F <msgfile>
```

Message: "A2: styles, templates, bindings, snapshot, popup, and detach capture scenes".

---

### Task 4: Run the full capture and commit the transcript

**Files:**
- Create (by running): `tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json`

- [ ] **Step 1: Full capture run**

Remove the `Skip` argument. Run:

`dotnet test E:/GitHub/SnoopMCP/tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj -c Debug --filter FullyQualifiedName~CaptureWalkthroughTranscript`

Expected: 1 passed.

- [ ] **Step 2: Inspect the complete transcript**

Read `tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json`. Confirm all scenes present and all 20 tools represented (`listWpfProcesses`, `attach`, `listVisualRoots` ×2, `describeElement`, `getChildren` ×3, `getParent`, `getTemplatedParent`, `findElements` ×2, `hitTest`, `resolvePath`, `describeDataContext`, `readDataContextPath` ×2, `listDependencyProperties`, `getDependencyProperty`, `resolveStyle`, `resolveTemplate`, `inspectBinding`, `listBindings`, `exportXaml`, `detach`).

- [ ] **Step 3: Restore the Skip**

Put the `Skip = "..."` argument back on the `[Fact]`. This is mandatory — the standard test suite must not run the capture.

- [ ] **Step 4: Confirm the standard suite skips it**

Run: `dotnet test E:/GitHub/SnoopMCP/tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj -c Debug --no-build`
Expected: 2 passed (the two existing `EndToEndTests` facts), 1 skipped.

- [ ] **Step 5: Commit the transcript + the restored Skip**

```bash
git -C E:/GitHub/SnoopMCP add tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json tests/SnoopMCP.IntegrationTests/WalkthroughCaptureTests.cs
git -C E:/GitHub/SnoopMCP commit -F <msgfile>
```

Message: "A2: capture walkthrough-transcript.json (full 20-tool run)".

---

### Task 5: Author `docs/walkthrough.md` from the transcript

**Files:**
- Create: `docs/walkthrough.md`

This task is prose, not code. Read `tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json` and render each scene. The values below come from that file — do not invent them.

- [ ] **Step 1: Write the opening**

Open with the scene-setter: a developer has a WPF app misbehaving, the SnoopMCP host is running on `http://127.0.0.1:6300/mcp`, the MCP client is connected, and `SampleWpfApp` is running as the target. State that every response shown is a real, lightly-trimmed capture; element ids will differ on the reader's machine; full untrimmed JSON lives in `tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json`.

- [ ] **Step 2: Write the nine category sections**

For each section, one framing paragraph, then a mini-scene per tool using this template (worked example shown for `resolveStyle`; fill real captured values for every tool):

````markdown
### Styles & templates

WPF styles compose through `BasedOn` chains and implicit type keys; the rendered look is the *winner* of that composition. `resolveStyle` flattens the chain so you can see every link.

**Doug:** "The Delete button is red — where's that coming from?"

Claude calls:

```json
{ "tool": "resolveStyle", "id": 42 }
```

Response (trimmed):

```json
{
  "styleKey": "DangerButtonStyle",
  "basedOnChain": ["DangerButtonStyle", "PrimaryButtonStyle", "BaseButtonStyle"],
  "setters": [ { "property": "Background", "value": "#FFDC143C" }, … ],
  "triggers": [ { "property": "IsMouseOver", "value": "True", … } ]
}
```

The `basedOnChain` shows Crimson comes from `DangerButtonStyle`, which inherits the hover/disabled triggers from `PrimaryButtonStyle` two links up.
````

Cover, in order: **Discovery & lifecycle** (`listWpfProcesses` with the "what's the PID?" framing, `attach`); **Orientation** (`listVisualRoots`, `describeElement`); **Tree navigation** (`getChildren` visual vs logical as a contrast, `getParent`, `getTemplatedParent`); **Search & spatial** (`findElements` by name + by text, `hitTest`, `resolvePath`); **DataContext** (`describeDataContext`, `readDataContextPath` reachable + broken); **Dependency properties** (`listDependencyProperties`, `getDependencyProperty`); **Styles & templates** (`resolveStyle`, `resolveTemplate`); **Bindings** (`inspectBinding`, `listBindings`); **Snapshot & wrap-up** (`exportXaml`, then `detach` in one line). If the `Popup` scene was captured, add it to **Orientation** as a "with the dropdown open" revisit showing the second root and `openedBy`; if it was not captured, write the popup paragraph as "open the dropdown yourself, then re-run `listVisualRoots`" without a captured block.

- [ ] **Step 3: Write the closing**

Recap the tour in two or three sentences, point at `samples/SampleWpfApp/` for readers who want to read the fixtures, and note the same flow works against the reader's own WPF app — `SampleWpfApp` is just a target with known bugs. Add a short "Refreshing this walkthrough" note pointing at the `CaptureWalkthroughTranscript` fact and its run command.

- [ ] **Step 4: Trim pass**

Re-read each captured block. Keep each under ~20 lines; replace deep sub-trees and long arrays with a clearly-marked `…` elision. Verify every field referenced in prose actually appears in the (possibly trimmed) block above it.

- [ ] **Step 5: Render check**

Run: `git -C E:/GitHub/SnoopMCP diff --stat` to confirm only `docs/walkthrough.md` is new. Visually confirm GitHub-flavoured markdown (fenced ```json blocks, tables, headings) is well-formed by reading the file top to bottom.

- [ ] **Step 6: Commit**

```bash
git -C E:/GitHub/SnoopMCP add docs/walkthrough.md
git -C E:/GitHub/SnoopMCP commit -F <msgfile>
```

Message: "A2: docs/walkthrough.md — guided tour of all 20 tools against SampleWpfApp".

---

### Task 6: Finalize — full build, suite, README pointer, PR

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add a README pointer to the walkthrough**

Under the `## Tool surface` heading's intro line, add a sentence linking the walkthrough:

```markdown
For a guided, end-to-end demonstration of every tool against the bundled sample app, see
[`docs/walkthrough.md`](docs/walkthrough.md).
```

- [ ] **Step 2: Full solution build (zero-warning gate)**

Run: `dotnet build E:/GitHub/SnoopMCP/SnoopMCP.sln -c Debug -p:TreatWarningsAsErrors=true`
Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Full standard test suite**

Run each project's tests (`--no-build`): Protocol, Host, Payload, IntegrationTests. Expected totals unchanged from master except IntegrationTests reports **1 skipped** (the capture fact): 5 protocol + 7 host + 149 payload + 2 integration passed, 1 skipped.

- [ ] **Step 4: Commit the README pointer**

```bash
git -C E:/GitHub/SnoopMCP add README.md
git -C E:/GitHub/SnoopMCP commit -F <msgfile>
```

Message: "A2: link walkthrough from README tool surface".

- [ ] **Step 5: Push, status, PR (per the repo's per-task PR workflow)**

```bash
git -C E:/GitHub/SnoopMCP push -u origin task-a2-walkthrough
git -C E:/GitHub/SnoopMCP rev-parse task-a2-walkthrough
gh api -X POST repos/JackalopeTechnologies/SnoopMCP/statuses/<sha> --field state=success --field context=build --field description="local build green; suite passes, capture fact skipped"
gh pr create --repo JackalopeTechnologies/SnoopMCP --base master --head task-a2-walkthrough --title "A2: walkthrough — guided tour of all 20 tools" --body-file <file>
```

Then STOP and let the user review before merging (per the standing one-task-per-PR rule).

---

## Notes for the executor

- **One Bash call per command. No `&&`/`;`/`|`. No `cd` — use `git -C E:/GitHub/SnoopMCP`. Commits via `-F <msgfile>`, never `-m`. No AI attribution. No hook-bypass flags.**
- **The capture fact is a generator, not a gate.** Its "verification" is always: un-skip → run filtered → inspect JSON → restore Skip. Never leave the Skip off in a commit (Task 4 Step 4 guards this).
- **If a captured field name differs** from what a scene's prose claims, the JSON wins — adjust the prose. The transcript is the source of truth.
- **`BrokenBinding` is inside the DetailPane template.** `findElements({Name:"BrokenBinding"})` relies on the finder matching `FrameworkElement.Name` on template-instantiated elements, which it should. If the Task 2 verify shows "No element matched" for `BrokenBinding`, fall back to locating it by walking `getChildren(detailPaneId, "visual")` recursively for the `Red`-foreground TextBlock, or add an `AutomationProperties.AutomationId` to the `BrokenBinding` TextBlock in `MainWindow.xaml` and match on `{AutomationId:...}`. Its text is empty (that's the bug), so `TextContains` will not find it.
- **UIA fragility:** if `ComboBoxAutomation.TrySetDropDownOpen` returns false at capture time, the popup scene is omitted and the doc uses the manual-open phrasing. Do not block the whole capture on it.
- **Analyzer reminders (under `tests/` the suppressed set is CA1707, STY0006, STY0008, NUM0002, STY0002, xUnit1051):** STR0008 (one type per file) is NOT suppressed — hence `WalkthroughRecord` and `WalkthroughTranscript` in separate files. CA2007 (ConfigureAwait) IS effectively required on non-test awaits, but test-method awaits are exempt under `tests/`; keep the `await mTranscript.WriteAsync()` and tool awaits plain as the existing `EndToEndTests` does.
