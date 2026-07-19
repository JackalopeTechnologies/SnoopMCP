# SnoopMCP Driving — Phase A Implementation Plan (Host UIA + Capture + Gate + Elevation + Skill)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an AI agent drive and observe a live WPF app from the host process using out-of-process UI Automation (UIA2) and `PrintWindow` capture — gated by a host-side safety switch, running elevated, taught by a new skill — with zero changes to the injected payload or the pipe wire.

**Architecture:** A new `Automation/` subsystem in `SnoopMCP.Server` (assembly `SnoopMCP.Server`, namespace `SnoopMCP.Host`) exposes `IUiaDriver`, `IScreenCapture`, `InteractionGate`, and an `ElementHandleCache`, registered as DI singletons and surfaced through a second `[McpServerToolType]` class `UiaTools` that the existing `WithToolsFromAssembly` auto-discovers. Driving tools are session-free (keyed by `pid`, like `ListWpfProcesses`). Elevation is achieved by flipping the autostart scheduled task to `/RL HIGHEST` and routing manual start through it. A `snoopmcp-uia` skill ships via a multi-skill registry.

**Tech Stack:** .NET 10 (`net10.0-windows`, x64), ASP.NET Core Minimal Hosting + ModelContextProtocol SDK, `System.Windows.Automation` (UIA2, via `Microsoft.WindowsDesktop.App` framework reference), user32/gdi32 P/Invoke, xUnit v3.

## Global Constraints

- **Standards:** Jackalope (`E:\GitHub\*`). Zero-warning build; `dotnet build <sln> -p:TreatWarningsAsErrors=true` must be clean. Suppressions only when no code fix exists, narrowly scoped, documented.
- **Field naming:** instance `m`-prefix (`mFoo`), static `sm`-prefix (`smFoo`). File-scoped namespaces. 4-line MIT header on every new `.cs` (copy from any existing file, change the first `// <Filename>.cs` line).
- **No synthesized input, ever:** no `SendInput`/`mouse_event`/`keybd_event`/`SendKeys`, no `SetForegroundWindow`/`ShowWindow`. Driving is UIA patterns only; capture is `PrintWindow` only.
- **Managed UIA2 only:** `System.Windows.Automation`. No FlaUI, no raw UIA3 COM, no new NuGet package (framework reference only).
- **Payload/wire untouched:** the only `SnoopMCP.Protocol` change in Phase A is **append-only** additions to `ErrorCode`. Never renumber `ErrorCode` (numeric on the wire).
- **UIA DTOs live in `SnoopMCP.Server/Automation/`,** not `SnoopMCP.Protocol` — the payload does not know about UIA. (Refinement over spec §4.1 which named the subsystem but not the DTO home; keeps the Protocol change to `ErrorCode` only.)
- **Gate default OFF:** read-only tools ungated; mutating tools (`invokeUia`, `setUiaValue`) throw `InteractionDisabled` when the gate is off.
- **Commits:** message via file + `git commit -F`; no AI attribution; branch `feat/69-interaction-driving`. Task steps below show `git commit -m` for brevity — use a message file instead per repo rule.
- **Spec:** `docs/superpowers/specs/2026-07-19-snoopmcp-interaction-driving-design.md`. This plan implements §4.1, §5 (Phase A rows), §6, §7, §8, §9 (Phase A codes), §10.

---

## Codex Review — Binding Corrections (READ FIRST — these OVERRIDE task text where they conflict)

A read-only Codex review (gpt-5.6-sol, high effort) found the issues below. Each is binding; apply it to the named task.

### C1 (P1) — `captureWindow` MUST return a native MCP image, not base64-in-JSON
Spec §13 settled on image content ("the agent sees the window"). Change `UiaTools.CaptureWindow` (Task 8) to return an MCP image content result instead of `SerializeResult(...)`:
```csharp
[McpServerTool, Description("Capture a WPF window (by pid) as an image, even if occluded. Read-only.")]
public Task<CallToolResult> CaptureWindow(int pid, CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    try
    {
        CaptureResult r = mCapture.Capture(pid);
        return Task.FromResult(new CallToolResult
        {
            Content = [new ImageContentBlock { Data = r.Base64, MimeType = "image/png" }]
        });
    }
    catch (SnoopMcpException ex) { throw Promote(ex); }
}
```
Verify the exact SDK type/property names against the installed `ModelContextProtocol` 1.3.0 (`CallToolResult`, `ImageContentBlock` vs `ImageContent`, `Data`/`MimeType`); prefer a first-class helper if the SDK exposes one. `PrintWindowCapture` still returns `CaptureResult` (Task 7 unchanged). Only fall back to base64-in-JSON if the SDK image type genuinely cannot be returned — and note which was used in the commit.

### C2 (P1) — Elevation must cover the REAL launch paths, not just CLI `start`
The host auto-starts Kestrel in-process on launch (`App.xaml.cs` OnStartup → `ServerController.StartAsync`); tray Start/Stop only cycle Kestrel *within* the already-running host. So Task 12 (routing CLI `HostProcess.Start`) leaves double-click/MSI-finish launches at **Medium IL**. Task 13 does NOT surface the standard-user limit — that claim in Task 11's note is wrong. Add **Task 12b — host self-elevation on startup**:
- Create `src/SnoopMCP.Server/Automation/ElevationInfo.cs`:
```csharp
namespace SnoopMCP.Host.Automation;

using System.Runtime.InteropServices;
using System.Security.Principal;

/// <summary>Reports the host's integrity/elevation so startup can relaunch elevated when possible.</summary>
public static class ElevationInfo
{
    private enum TokenElevationType { Default = 1, Full = 2, Limited = 3 }

    /// <summary>True when the current process runs at high integrity (elevated).</summary>
    public static bool IsElevated()
    {
        using WindowsIdentity id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>True when the account is an administrator (elevated now, or an admin whose token is filtered).</summary>
    public static bool CanElevate()
    {
        if (IsElevated())
        {
            return true;
        }
        return QueryElevationType() == TokenElevationType.Limited; // filtered admin token → UAC can elevate
    }

    private static TokenElevationType QueryElevationType()
    {
        using WindowsIdentity id = WindowsIdentity.GetCurrent();
        int size = Marshal.SizeOf<int>();
        nint buffer = Marshal.AllocHGlobal(size);
        try
        {
            // TOKEN_INFORMATION_CLASS.TokenElevationType = 18
            return GetTokenInformation(id.Token, 18, buffer, size, out _)
                ? (TokenElevationType)Marshal.ReadInt32(buffer)
                : TokenElevationType.Default;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(nint tokenHandle, int tokenInformationClass, nint tokenInformation, int tokenInformationLength, out int returnLength);
}
```
- In `App.xaml.cs` OnStartup, BEFORE starting the server and after the single-instance mutex check: if `!ElevationInfo.IsElevated()` and `AutostartTask.Exists()` and `ElevationInfo.CanElevate()` → **release the mutex, `AutostartTask.RunNow()` (runs the elevated task), and `Shutdown()` this Medium instance** (the elevated instance re-acquires the mutex). If `!IsElevated()` and `!CanElevate()` → continue at Medium and expose a `bool CanDriveElevatedTargets => ElevationInfo.IsElevated()` flag the tray tooltip reflects ("driving elevated targets unavailable — not an administrator").
- Fix the Task 11 note: non-admin messaging lives in Task 12b + the tray tooltip, not Task 13.

### C3 (P1) — Host driving tools MUST be WPF-gated (spec non-goal: non-WPF targets)
`UiaDriver.FindWindow` matches any top-level window by PID and `PrintWindowCapture` accepts any process with a main window, so arbitrary Win32/WinForms apps are drivable. Add a WPF probe (reuse `ProcessEnumerator`'s `PresentationFramework.dll` module scan **without** the x64 gate) and call it at the top of every `UiaTools` method. Create `src/SnoopMCP.Server/Automation/WpfTargetGuard.cs`:
```csharp
namespace SnoopMCP.Host.Automation;

using System.Diagnostics;
using SnoopMCP.Protocol.Errors;

/// <summary>Guards the driving tools to WPF targets (PresentationFramework loaded); no x64 restriction.</summary>
public static class WpfTargetGuard
{
    private const string WpfModule = "PresentationFramework.dll";

    public static void EnsureWpf(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            foreach (ProcessModule module in process.Modules)
            {
                if (string.Equals(module.ModuleName, WpfModule, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new SnoopMcpException(ErrorCode.AttachFailed, $"Process {pid} is not an inspectable WPF target.", ex);
        }
        throw new SnoopMcpException(ErrorCode.AttachFailed, $"Process {pid} is not a WPF app ({WpfModule} not loaded).");
    }
}
```
Call `WpfTargetGuard.EnsureWpf(pid)` as the first line of `GetUiaTree`, `FindUiaElement`, `CaptureWindow`, `WaitForUia`, and (via the ref's `Pid`) `InvokeUia`/`SetUiaValue` in Task 8. Add a unit test asserting a non-WPF pid (e.g. a running `notepad`/`explorer` with no WPF) yields `AttachFailed`.

### C4 (P1) — Discovery MUST return the full durable `elementRef`, and resolution must not silently fall back
Per spec §5.1 the reference is `{pid, handle, by, value}` with locator re-resolution and PID validation. Corrections:
- **`UiaElementInfo` carries the full reference** (Task 3). Replace the `Handle` string with a `UiaElementRef Reference`:
```csharp
public sealed record UiaElementInfo(
    UiaElementRef Reference, string AutomationId, string Name, string ControlType,
    string HelpText, double X, double Y, double Width, double Height, IReadOnlyList<string> Patterns);
```
- **`Project(...)` builds the ref with a locator** (Task 5). When an element has an `AutomationId`, set `by="automationId", value=<id>`; else fall back to the `by`/`value` the find used (may be null for tree nodes). Mint the handle, then `new UiaElementRef(pid, handle, by, value)`.
- **`ElementHandleCache.TryGet` validates the PID** (Task 4): parse the `"pid:seq"` prefix and reject a handle whose pid ≠ the caller's expected pid. Add `bool TryGet(string handle, int expectedPid, out AutomationElement e, out string? by, out string? value)`; `ResolveAsync`/`ResolveRoot` pass `reference.Pid`.
- **`getUiaTree` takes a `UiaElementRef? fromElement`, not a raw handle** (Tasks 5, 8). `IUiaDriver.GetTreeAsync(int pid, UiaElementRef? fromElement, int depth, CancellationToken ct)`; `UiaTools.GetUiaTree(int pid, UiaElementRef? fromElement, int depth, …)`.
- **No silent root fallback** (Task 5): `ResolveRoot` resolves `fromElement` via `ResolveAsync` (handle→locator→`UiaElementStale`); only when `fromElement` is null does it use `FindWindow(pid)`. A stale subtree ref throws `UiaElementStale`, never re-roots to the window.

### C5 (P1→scoped) — `getAutomationPeerInfo` runtime-id + reverse-lookup scope (Phase B, cross-referenced here)
Handled in Phase B corrections: add `RuntimeId` to the response; explicitly scope the bridge to **forward + correlation** for v1 (the agent matches `RuntimeId` against this tier's UIA runtime-id itself) and defer a UIA→Snoop reverse-lookup *tool* with a design note. If you want true reverse lookup in v1, add it as a new Phase B task instead.

### C6 (P1) — `ValueWaiter` test deadlocks the dispatcher (Phase B) — see Phase B corrections.

### C9 (P2) — `SampleWpfApp` lacks the fixtures the tests require — add **Task 0 (do first)**
Codex verified `SampleWpfApp` buttons have no commands/handlers and its VM exposes no result state, so the invocation/`waitForValue` tests cannot pass. Before any UIA test, augment the sample:
- Add a `TextBox x:Name="ProbeText"` with `AutomationProperties.AutomationId="ProbeText"`.
- Add a `Button` with `AutomationProperties.AutomationId="RunProbe"` bound to a `RelayCommand` `RunProbeCommand` that sets a VM string property `ProbeStatus` to `"done"` and appends `ProbeText` to a `ProbeResult` property.
- Expose `ProbeStatus`/`ProbeResult` on the VM (INotifyPropertyChanged).
These give Phase A's set-value/find/invoke tests and Phase B's `executeCommand`+`waitForValue` E2E a real, assertable fixture. This is Task 0 — commit it before Task 1.

### C10 (P2) — Use a UIA `CacheRequest` for discovery (spec §4.1)
`Project` reads `element.Current.*` and `GetSupportedPatterns()` per node — many cross-process COM calls; a large tree can exhaust the 5 s wrapper. In `GetTreeAsync`/`FindAsync`, activate a cache:
```csharp
var request = new CacheRequest();
request.Add(AutomationElement.AutomationIdProperty);
request.Add(AutomationElement.NameProperty);
request.Add(AutomationElement.ControlTypeProperty);
request.Add(AutomationElement.HelpTextProperty);
request.Add(AutomationElement.BoundingRectangleProperty);
request.Add(AutomationElement.IsInvokePatternAvailableProperty);
request.Add(AutomationElement.IsTogglePatternAvailableProperty);
request.Add(AutomationElement.IsSelectionItemPatternAvailableProperty);
request.Add(AutomationElement.IsExpandCollapsePatternAvailableProperty);
request.Add(AutomationElement.IsValuePatternAvailableProperty);
using (request.Activate())
{
    // FindAll / walk here; read info.Cached.* and the Is*PatternAvailable cached bools to build Patterns
}
```
`Project` reads `element.Cached.AutomationId` etc.; derive `Patterns` from the cached `Is*PatternAvailable` booleans instead of `GetSupportedPatterns()`. Strengthen the discovery test (C-verify) to find by each locator kind and assert an invocation effect (using the Task 0 fixture) and capture non-blank dimensions — replacing the weak "non-empty tree" assertions.

### Strengthen verification (P2, #9)
Add explicit tests: find by `automationId`/`name`/`helpText`/`controlType` (Task 0 fixture); `invokeUia` the `RunProbe` button then assert `ProbeStatus == "done"` via a UIA re-read or Phase B `waitForValue`; `setUiaValue` on `ProbeText` then read back its `ValuePattern.Current.Value`.

---

## File Structure

**Create:**
- `src/SnoopMCP.Server/Automation/UiaElementInfo.cs` — element-facts record returned by discovery tools.
- `src/SnoopMCP.Server/Automation/UiaElementRef.cs` — opaque handle + durable locator, the cross-call element reference.
- `src/SnoopMCP.Server/Automation/UiaLocator.cs` — the `by` locator kinds + string↔`ControlType` mapping.
- `src/SnoopMCP.Server/Automation/ElementHandleCache.cs` — TTL cache mapping `handle` → `AutomationElement` + locator.
- `src/SnoopMCP.Server/Automation/IUiaDriver.cs`, `UiaDriver.cs` — the UIA driver.
- `src/SnoopMCP.Server/Automation/IScreenCapture.cs`, `PrintWindowCapture.cs` — background window capture.
- `src/SnoopMCP.Server/Automation/InteractionGate.cs` — the host-side safety switch (file-backed).
- `src/SnoopMCP.Server/Automation/CaptureResult.cs` — capture payload record.
- `src/SnoopMCP.Server/Tools/UiaTools.cs` — the MCP tool class.
- `skills/snoopmcp-uia/SKILL.md` — the new agent skill (human-canonical copy).
- `tests/SnoopMCP.Host.Tests/InteractionGateTests.cs`, `ElementHandleCacheTests.cs`, `HealthStatusTests.cs`, `ProcessProbeElevationTests.cs`.
- `tests/SnoopMCP.IntegrationTests/UiaDriverTests.cs`, `PrintWindowCaptureTests.cs`, `UiaToolsGateTests.cs` (interactive-session tests; drive `SampleWpfApp`).

**Modify:**
- `src/SnoopMCP.Protocol/Errors/ErrorCode.cs` — append Phase A codes.
- `src/SnoopMCP.Server/SnoopMCP.Server.csproj` — add `Microsoft.WindowsDesktop.App` framework reference.
- `src/SnoopMCP.Server/ServerHost.cs` — register `InteractionGate`, `IUiaDriver`, `IScreenCapture`; pass gate state to `/health`; accept an optional gate for tests.
- `src/SnoopMCP.Server/HealthStatus.cs` — add `InteractionEnabled`.
- `src/SnoopMCP.Server/Injection/ProcessProbe.cs` — map `process.Handle` `Win32Exception` → `AccessDenied`.
- `src/SnoopMCP.Cli/AutostartTask.cs` — `/RL LIMITED` → `/RL HIGHEST`; self-elevate task creation.
- `src/SnoopMCP.Cli/HostProcess.cs` — route `Start()` through the task with a Medium fallback.
- `src/SnoopMCP.ClientIntegration/SnoopSkill.cs` — multi-skill registry (adds `snoopmcp-uia`).
- `src/SnoopMCP.ClientIntegration/ClaudeCodeWriter.cs` — status strings reflect multiple skills.
- `tests/SnoopMCP.Cli.Tests/AutostartTaskTests.cs` — update to `/RL HIGHEST`.
- `tests/SnoopMCP.ClientIntegration.Tests/SnoopSkillTests.cs` — second skill + drift guard.

---

## Task 1: Append Phase A error codes

**Files:**
- Modify: `src/SnoopMCP.Protocol/Errors/ErrorCode.cs`
- Test: `tests/SnoopMCP.Protocol.Tests/ErrorCodeTests.cs` (create if absent)

**Interfaces:**
- Produces: `ErrorCode.InteractionDisabled=12`, `NotDrivable=13`, `ValueReadOnly=14`, `CaptureUnavailable=15`, `UiaElementStale=16`, `UiaAmbiguousLocator=17`, `TargetUnresponsive=18`.

> Note (refinement over spec §9): `TargetUnresponsive` is added for UIA calls that exceed the per-call timeout against a hung target — the host analogue of the payload's `DispatcherTimeout`.

- [ ] **Step 1: Write the failing test**

```csharp
// ErrorCodeTests.cs (namespace SnoopMCP.Protocol.Tests; using Protocol.Errors; using Xunit;)
[Fact]
public void PhaseADrivingCodes_HaveStableNumbers()
{
    Assert.Equal(12, (int)ErrorCode.InteractionDisabled);
    Assert.Equal(13, (int)ErrorCode.NotDrivable);
    Assert.Equal(14, (int)ErrorCode.ValueReadOnly);
    Assert.Equal(15, (int)ErrorCode.CaptureUnavailable);
    Assert.Equal(16, (int)ErrorCode.UiaElementStale);
    Assert.Equal(17, (int)ErrorCode.UiaAmbiguousLocator);
    Assert.Equal(18, (int)ErrorCode.TargetUnresponsive);
}
```

- [ ] **Step 2: Run to verify it fails** — `dotnet test tests/SnoopMCP.Protocol.Tests -p:TreatWarningsAsErrors=true`. Expected: FAIL (members do not exist).

- [ ] **Step 3: Append the codes** (after `PathParseError = 11`, before the closing brace):

```csharp
    /// <summary>The host interaction gate is disabled; the requested mutating action was refused.</summary>
    InteractionDisabled = 12,

    /// <summary>No UIA action pattern (and no payload fallback) can drive the target element.</summary>
    NotDrivable = 13,

    /// <summary>The target value element is read-only.</summary>
    ValueReadOnly = 14,

    /// <summary>The window cannot be captured (e.g. minimized, no printable content).</summary>
    CaptureUnavailable = 15,

    /// <summary>An element handle expired and could not be re-resolved by its locator.</summary>
    UiaElementStale = 16,

    /// <summary>A locator re-resolved to more than one element; the caller must disambiguate.</summary>
    UiaAmbiguousLocator = 17,

    /// <summary>A UI Automation call exceeded its timeout against an unresponsive target.</summary>
    TargetUnresponsive = 18
```

(Remember to add a trailing comma after `PathParseError = 11`.)

- [ ] **Step 4: Run to verify it passes** — same command. Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -F <msg>` ("feat: add Phase A driving error codes").

---

## Task 2: InteractionGate (host-side safety switch)

**Files:**
- Create: `src/SnoopMCP.Server/Automation/InteractionGate.cs`
- Test: `tests/SnoopMCP.Host.Tests/InteractionGateTests.cs`

**Interfaces:**
- Produces:
  - `InteractionGate(string statePath)`
  - `static InteractionGate ForCurrentUser()`
  - `static string DefaultStatePath()`
  - `bool IsEnabled { get; }` (reads the file each call; default `false`)
  - `void SetEnabled(bool enabled)` (atomic write)

- [ ] **Step 1: Write the failing test**

```csharp
// InteractionGateTests.cs (namespace SnoopMCP.Host.Tests; using SnoopMCP.Host.Automation; using Xunit;)
public sealed class InteractionGateTests : IDisposable
{
    private readonly string mPath = Path.Combine(Path.GetTempPath(), "gate-" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose() { if (File.Exists(mPath)) File.Delete(mPath); GC.SuppressFinalize(this); }

    [Fact]
    public void IsEnabled_DefaultsFalse_WhenNoFile()
    {
        var gate = new InteractionGate(mPath);
        Assert.False(gate.IsEnabled);
    }

    [Fact]
    public void SetEnabled_Persists_AndIsReadBySecondInstance()
    {
        new InteractionGate(mPath).SetEnabled(true);
        Assert.True(new InteractionGate(mPath).IsEnabled);
        new InteractionGate(mPath).SetEnabled(false);
        Assert.False(new InteractionGate(mPath).IsEnabled);
    }
}
```

- [ ] **Step 2: Run to verify it fails** — `dotnet test tests/SnoopMCP.Host.Tests -p:TreatWarningsAsErrors=true`. Expected: FAIL (type missing).

- [ ] **Step 3: Implement**

```csharp
// InteractionGate.cs
namespace SnoopMCP.Host.Automation;

using System.Text.Json;

/// <summary>
/// The host-side safety switch for mutating driving tools. Default OFF. State is a tiny JSON file
/// under %LOCALAPPDATA%\SnoopMCP so the tray (which toggles it) and the running server (which reads
/// it per call) coordinate without a shared instance. Read-only tools ignore this gate.
/// </summary>
public sealed class InteractionGate
{
    private const string AppDirName = "SnoopMCP";
    private const string StateFileName = "interaction-gate.json";
    private const string TempSuffix = ".tmp";

    private readonly string mStatePath;

    /// <summary>Creates a gate backed by an explicit state-file path (tests pass a temp path).</summary>
    public InteractionGate(string statePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(statePath);
        mStatePath = statePath;
    }

    /// <summary>Creates a gate backed by the per-user state file.</summary>
    public static InteractionGate ForCurrentUser() => new(DefaultStatePath());

    /// <summary>The per-user state-file path under %LOCALAPPDATA%\SnoopMCP.</summary>
    public static string DefaultStatePath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, AppDirName, StateFileName);
    }

    /// <summary>True when mutating driving tools are permitted. Reads the file each call; default false.</summary>
    public bool IsEnabled
    {
        get
        {
            bool enabled = false;
            try
            {
                if (File.Exists(mStatePath))
                {
                    string text = File.ReadAllText(mStatePath);
                    GateState? state = JsonSerializer.Deserialize<GateState>(text);
                    enabled = state?.Enabled ?? false;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                enabled = false;
            }
            return enabled;
        }
    }

    /// <summary>Enables or disables the gate, persisting atomically.</summary>
    public void SetEnabled(bool enabled)
    {
        string? dir = Path.GetDirectoryName(mStatePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        string tmp = mStatePath + TempSuffix;
        File.WriteAllText(tmp, JsonSerializer.Serialize(new GateState(enabled)));
        File.Move(tmp, mStatePath, overwrite: true);
    }

    private sealed record GateState(bool Enabled);
}
```

- [ ] **Step 4: Run to verify it passes** — same command. Expected: PASS.

- [ ] **Step 5: Commit** — "feat: add InteractionGate host safety switch".

---

## Task 3: UIA DTOs (UiaElementInfo, UiaElementRef, UiaLocator, CaptureResult)

**Files:**
- Create: `src/SnoopMCP.Server/Automation/UiaElementInfo.cs`, `UiaElementRef.cs`, `UiaLocator.cs`, `CaptureResult.cs`
- Test: `tests/SnoopMCP.Host.Tests/UiaLocatorTests.cs`

**Interfaces:**
- Produces:
  - `record UiaElementRef(int Pid, string Handle, string? By, string? Value)`
  - `record UiaElementInfo(string Handle, string AutomationId, string Name, string ControlType, string HelpText, double X, double Y, double Width, double Height, IReadOnlyList<string> Patterns)`
  - `record CaptureResult(string Format, int Width, int Height, string Base64)`
  - `UiaLocator.ToCondition(string by, string value) -> System.Windows.Automation.Condition` and `UiaLocator.KnownKinds` (the accepted `by` strings).

- [ ] **Step 1: Write the failing test**

```csharp
// UiaLocatorTests.cs (namespace SnoopMCP.Host.Tests; using SnoopMCP.Host.Automation; using System.Windows.Automation; using Xunit;)
[Fact]
public void ToCondition_AutomationId_BuildsPropertyCondition()
{
    Condition c = UiaLocator.ToCondition("automationId", "SaveButton");
    var pc = Assert.IsType<PropertyCondition>(c);
    Assert.Equal(AutomationElement.AutomationIdProperty, pc.Property);
    Assert.Equal("SaveButton", pc.Value);
}

[Fact]
public void ToCondition_ControlType_MapsName()
{
    Condition c = UiaLocator.ToCondition("controlType", "Button");
    var pc = Assert.IsType<PropertyCondition>(c);
    Assert.Equal(AutomationElement.ControlTypeProperty, pc.Property);
    Assert.Equal(ControlType.Button.Id, ((ControlType)pc.Value).Id);
}

[Fact]
public void ToCondition_UnknownBy_Throws()
{
    Assert.Throws<SnoopMCP.Protocol.Errors.SnoopMcpException>(() => UiaLocator.ToCondition("colour", "x"));
}
```

- [ ] **Step 2: Run to verify it fails** — `dotnet test tests/SnoopMCP.Host.Tests`. Expected: FAIL (types missing). (This task also first requires the `Microsoft.WindowsDesktop.App` reference — add it now as part of Step 3 so `System.Windows.Automation` resolves.)

- [ ] **Step 3a: Add the framework reference** to `src/SnoopMCP.Server/SnoopMCP.Server.csproj` inside the first `<ItemGroup>` (next to `Microsoft.AspNetCore.App`):

```xml
        <FrameworkReference Include="Microsoft.WindowsDesktop.App" />
```

- [ ] **Step 3b: Create the records**

```csharp
// UiaElementRef.cs
namespace SnoopMCP.Host.Automation;

/// <summary>
/// A cross-call reference to a UIA element. <see cref="Handle"/> indexes the host-side
/// <c>ElementHandleCache</c>; when it expires, <see cref="By"/>+<see cref="Value"/> re-resolve it.
/// </summary>
/// <param name="Pid">The target process id.</param>
/// <param name="Handle">Opaque cache handle ("&lt;pid&gt;:&lt;seq&gt;"), or empty for a locator-only ref.</param>
/// <param name="By">Durable locator kind used to re-resolve, or null.</param>
/// <param name="Value">Durable locator value, or null.</param>
public sealed record UiaElementRef(int Pid, string Handle, string? By, string? Value);
```

```csharp
// UiaElementInfo.cs
namespace SnoopMCP.Host.Automation;

/// <summary>Facts about a discovered UIA element, plus a fresh cache handle for driving it.</summary>
public sealed record UiaElementInfo(
    string Handle,
    string AutomationId,
    string Name,
    string ControlType,
    string HelpText,
    double X,
    double Y,
    double Width,
    double Height,
    IReadOnlyList<string> Patterns);
```

```csharp
// CaptureResult.cs
namespace SnoopMCP.Host.Automation;

/// <summary>A background window capture: a base64 PNG plus its pixel dimensions.</summary>
/// <param name="Format">Always "png".</param>
public sealed record CaptureResult(string Format, int Width, int Height, string Base64);
```

```csharp
// UiaLocator.cs
namespace SnoopMCP.Host.Automation;

using System.Windows.Automation;
using SnoopMCP.Protocol.Errors;

/// <summary>Maps a caller "by" locator kind + value to a UIA <see cref="Condition"/>.</summary>
public static class UiaLocator
{
    /// <summary>The accepted locator kinds, in stability order.</summary>
    public static IReadOnlyList<string> KnownKinds { get; } = ["automationId", "name", "helpText", "controlType"];

    /// <summary>Builds a UIA property condition for the given locator, or throws <see cref="SnoopMcpException"/>.</summary>
    public static Condition ToCondition(string by, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(by);
        ArgumentNullException.ThrowIfNull(value);
        return by switch
        {
            "automationId" => new PropertyCondition(AutomationElement.AutomationIdProperty, value),
            "name" => new PropertyCondition(AutomationElement.NameProperty, value),
            "helpText" => new PropertyCondition(AutomationElement.HelpTextProperty, value),
            "controlType" => new PropertyCondition(AutomationElement.ControlTypeProperty, ToControlType(value)),
            _ => throw new SnoopMcpException(
                ErrorCode.InvalidArgument,
                $"Unknown locator '{by}'. Use one of: {string.Join(", ", KnownKinds)}.")
        };
    }

    private static ControlType ToControlType(string name)
    {
        // ControlType.<Name> exposes a ProgrammaticName like "ControlType.Button"; match on the leaf.
        foreach (System.Reflection.FieldInfo field in typeof(ControlType).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (field.GetValue(null) is ControlType ct
                && string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return ct;
            }
        }
        throw new SnoopMcpException(ErrorCode.InvalidArgument, $"Unknown controlType '{name}'.");
    }
}
```

- [ ] **Step 4: Run to verify it passes** — `dotnet test tests/SnoopMCP.Host.Tests`. Expected: PASS. (Build also confirms the framework reference resolved `System.Windows.Automation`.)

- [ ] **Step 5: Commit** — "feat: add UIA driving DTOs and locator mapping".

---

## Task 4: ElementHandleCache (TTL handle → element)

**Files:**
- Create: `src/SnoopMCP.Server/Automation/ElementHandleCache.cs`
- Test: `tests/SnoopMCP.Host.Tests/ElementHandleCacheTests.cs`

**Interfaces:**
- Produces:
  - `ElementHandleCache(TimeSpan ttl, Func<DateTimeOffset> clock)` and `ElementHandleCache()` (default 60 s TTL, system clock)
  - `string Add(int pid, AutomationElement element, string? by, string? value)` → `"<pid>:<seq>"`
  - `bool TryGet(string handle, out AutomationElement element, out string? by, out string? value)` (false if unknown/expired)

> The cache stores `AutomationElement` (from `System.Windows.Automation`). Tests exercise TTL/eviction using the public surface; because `AutomationElement` cannot be constructed in a unit test, the cache is generic-free but the **TTL/sequence logic** is verified via an internal seam: expose `Add`/`TryGet` and inject the clock. Use `AutomationElement.RootElement` (always available, no target needed) as the stored element in tests.

- [ ] **Step 1: Write the failing test**

```csharp
// ElementHandleCacheTests.cs (namespace SnoopMCP.Host.Tests; using SnoopMCP.Host.Automation; using System.Windows.Automation; using Xunit;)
public sealed class ElementHandleCacheTests
{
    [Fact]
    public void Add_ThenTryGet_ReturnsElementAndLocator()
    {
        var now = DateTimeOffset.UnixEpoch;
        var cache = new ElementHandleCache(TimeSpan.FromSeconds(60), () => now);
        AutomationElement root = AutomationElement.RootElement;

        string handle = cache.Add(1234, root, "automationId", "X");

        Assert.StartsWith("1234:", handle);
        Assert.True(cache.TryGet(handle, out AutomationElement got, out string? by, out string? value));
        Assert.Same(root, got);
        Assert.Equal("automationId", by);
        Assert.Equal("X", value);
    }

    [Fact]
    public void TryGet_AfterTtl_ReturnsFalse()
    {
        var now = DateTimeOffset.UnixEpoch;
        var cache = new ElementHandleCache(TimeSpan.FromSeconds(60), () => now);
        string handle = cache.Add(1, AutomationElement.RootElement, null, null);

        now = now.AddSeconds(61);

        Assert.False(cache.TryGet(handle, out _, out _, out _));
    }

    [Fact]
    public void TryGet_UnknownHandle_ReturnsFalse()
    {
        var cache = new ElementHandleCache();
        Assert.False(cache.TryGet("9:9", out _, out _, out _));
    }
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (type missing).

- [ ] **Step 3: Implement**

```csharp
// ElementHandleCache.cs
namespace SnoopMCP.Host.Automation;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Automation;

/// <summary>
/// Short-TTL cache mapping opaque handles ("&lt;pid&gt;:&lt;seq&gt;") to live <see cref="AutomationElement"/>s
/// plus the durable locator each was found by. Handles are monotonic and never reused. Expired or
/// unknown handles miss, letting the caller re-resolve by locator.
/// </summary>
public sealed class ElementHandleCache
{
    private static readonly TimeSpan smDefaultTtl = TimeSpan.FromSeconds(60);

    private readonly TimeSpan mTtl;
    private readonly Func<DateTimeOffset> mClock;
    private readonly ConcurrentDictionary<string, Entry> mEntries = new(StringComparer.Ordinal);
    private long mSeq;

    /// <summary>Creates a cache with the default 60-second TTL and system clock.</summary>
    public ElementHandleCache() : this(smDefaultTtl, () => DateTimeOffset.UtcNow) { }

    /// <summary>Creates a cache with an explicit TTL and clock (tests inject both).</summary>
    public ElementHandleCache(TimeSpan ttl, Func<DateTimeOffset> clock)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be positive.");
        }
        ArgumentNullException.ThrowIfNull(clock);
        mTtl = ttl;
        mClock = clock;
    }

    /// <summary>Caches an element and returns a fresh handle.</summary>
    public string Add(int pid, AutomationElement element, string? by, string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        long seq = Interlocked.Increment(ref mSeq);
        string handle = $"{pid}:{seq}";
        mEntries[handle] = new Entry(element, by, value, mClock());
        return handle;
    }

    /// <summary>Resolves a handle to its element and locator; false when unknown or expired.</summary>
    public bool TryGet(
        string handle,
        [MaybeNullWhen(false)] out AutomationElement element,
        out string? by,
        out string? value)
    {
        element = null;
        by = null;
        value = null;
        bool ok = false;
        if (!string.IsNullOrEmpty(handle) && mEntries.TryGetValue(handle, out Entry? entry))
        {
            bool fresh = mClock() - entry.Stamp <= mTtl;
            if (fresh)
            {
                element = entry.Element;
                by = entry.By;
                value = entry.Value;
                ok = true;
            }
            else
            {
                mEntries.TryRemove(handle, out _);
            }
        }
        return ok;
    }

    private sealed record Entry(AutomationElement Element, string? By, string? Value, DateTimeOffset Stamp);
}
```

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit** — "feat: add UIA element handle cache".

---

## Task 5: IUiaDriver / UiaDriver — discovery (find window, tree, find element)

**Files:**
- Create: `src/SnoopMCP.Server/Automation/IUiaDriver.cs`, `src/SnoopMCP.Server/Automation/UiaDriver.cs`
- Test: `tests/SnoopMCP.IntegrationTests/UiaDriverTests.cs` (interactive — launches `SampleWpfApp`)

**Interfaces:**
- Consumes: `ElementHandleCache` (Task 4), `UiaElementInfo`/`UiaElementRef`/`UiaLocator` (Task 3), `ErrorCode` (Task 1).
- Produces (`IUiaDriver`):
  - `Task<IReadOnlyList<UiaElementInfo>> GetTreeAsync(int pid, string? fromHandle, int depth, CancellationToken ct)`
  - `Task<IReadOnlyList<UiaElementInfo>> FindAsync(int pid, string by, string value, CancellationToken ct)`
  - `Task<UiaElementInfo> WaitForAsync(int pid, string by, string value, int timeoutMs, CancellationToken ct)`
  - `Task<AutomationElement> ResolveAsync(UiaElementRef reference, CancellationToken ct)` (used by Task 6)
  - `UiaElementInfo Describe(int pid, AutomationElement element)` (handle-minting projection; internal reuse)

> **Threading/timeout:** every UIA call runs via `RunUia(...)` which executes on the thread pool and applies a per-call timeout (`smCallTimeout`, 5 s), throwing `SnoopMcpException(TargetUnresponsive, …)` on timeout. UIA client calls are COM cross-process; a hung target must never wedge Kestrel.

- [ ] **Step 1: Write the failing test** (interactive; requires a desktop session)

```csharp
// UiaDriverTests.cs (namespace SnoopMCP.IntegrationTests; using SnoopMCP.Host.Automation; using System.Diagnostics; using Xunit;)
public sealed class UiaDriverTests : IDisposable
{
    private readonly Process mApp;

    public UiaDriverTests()
    {
        // SampleWpfApp is copied next to the integration-test output (see the project's existing
        // ComboBoxAutomation harness for the launch precedent). Adjust the relative path if needed.
        string exe = Path.Combine(AppContext.BaseDirectory, "samples", "SampleWpfApp.exe");
        mApp = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false });
        mApp.WaitForInputIdle(5000);
        Thread.Sleep(1000); // let the visual tree settle
    }

    public void Dispose()
    {
        if (!mApp.HasExited) { mApp.Kill(entireProcessTree: true); mApp.WaitForExit(); }
        mApp.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetTree_ReturnsElements_WithPatterns()
    {
        var driver = new UiaDriver(new ElementHandleCache());
        IReadOnlyList<UiaElementInfo> tree = await driver.GetTreeAsync(mApp.Id, null, 3, default);
        Assert.NotEmpty(tree);
        Assert.All(tree, e => Assert.False(string.IsNullOrEmpty(e.Handle)));
    }
}
```

> If `SampleWpfApp` exposes no stable `AutomationId`, this test asserts only non-emptiness. A second test (`Find_ByControlType_ReturnsButtons`) should query `by:"controlType", value:"Button"` once a button is confirmed present in `SampleWpfApp`'s window; keep the assertion to `>=1`.

- [ ] **Step 2: Run to verify it fails** — `dotnet test tests/SnoopMCP.IntegrationTests --filter UiaDriverTests`. Expected: FAIL (type missing).

- [ ] **Step 3: Implement the interface and driver**

```csharp
// IUiaDriver.cs
namespace SnoopMCP.Host.Automation;

using System.Windows.Automation;

/// <summary>Out-of-process UI Automation driver for a target WPF window, keyed by process id.</summary>
public interface IUiaDriver
{
    /// <summary>Walks the UIA subtree under a window (or a cached element) to a bounded depth.</summary>
    Task<IReadOnlyList<UiaElementInfo>> GetTreeAsync(int pid, string? fromHandle, int depth, CancellationToken ct);

    /// <summary>Finds elements under the target window matching a single locator.</summary>
    Task<IReadOnlyList<UiaElementInfo>> FindAsync(int pid, string by, string value, CancellationToken ct);

    /// <summary>Polls until one element matches the locator or the timeout elapses.</summary>
    Task<UiaElementInfo> WaitForAsync(int pid, string by, string value, int timeoutMs, CancellationToken ct);

    /// <summary>Resolves a cross-call reference to a live element (handle first, then locator).</summary>
    Task<AutomationElement> ResolveAsync(UiaElementRef reference, CancellationToken ct);
}
```

```csharp
// UiaDriver.cs
namespace SnoopMCP.Host.Automation;

using System.Windows;
using System.Windows.Automation;
using SnoopMCP.Protocol.Errors;

/// <summary>
/// UIA2 driver over <see cref="System.Windows.Automation"/>. Finds the target window by process id and
/// projects elements to <see cref="UiaElementInfo"/> (minting handles into the shared cache). All UIA
/// calls run on the thread pool under a per-call timeout so a hung target cannot wedge the server.
/// </summary>
public sealed class UiaDriver : IUiaDriver
{
    private static readonly TimeSpan smCallTimeout = TimeSpan.FromSeconds(5);
    private const int WaitPollMs = 200;

    private readonly ElementHandleCache mCache;

    /// <summary>Creates a driver over the shared handle cache.</summary>
    public UiaDriver(ElementHandleCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        mCache = cache;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UiaElementInfo>> GetTreeAsync(int pid, string? fromHandle, int depth, CancellationToken ct)
    {
        if (depth < 1)
        {
            throw new SnoopMcpException(ErrorCode.InvalidArgument, "depth must be >= 1.");
        }
        return RunUia(() =>
        {
            AutomationElement root = ResolveRoot(pid, fromHandle);
            var results = new List<UiaElementInfo>();
            Walk(pid, root, depth, results);
            return (IReadOnlyList<UiaElementInfo>)results;
        }, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UiaElementInfo>> FindAsync(int pid, string by, string value, CancellationToken ct)
    {
        Condition condition = UiaLocator.ToCondition(by, value);
        return RunUia(() =>
        {
            AutomationElement window = FindWindow(pid);
            AutomationElementCollection found = window.FindAll(TreeScope.Descendants | TreeScope.Element, condition);
            var results = new List<UiaElementInfo>(found.Count);
            foreach (AutomationElement element in found)
            {
                results.Add(Project(pid, element, by, value));
            }
            return (IReadOnlyList<UiaElementInfo>)results;
        }, ct);
    }

    /// <inheritdoc />
    public async Task<UiaElementInfo> WaitForAsync(int pid, string by, string value, int timeoutMs, CancellationToken ct)
    {
        if (timeoutMs < 0)
        {
            throw new SnoopMcpException(ErrorCode.InvalidArgument, "timeoutMs must be >= 0.");
        }
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            IReadOnlyList<UiaElementInfo> hits = await FindAsync(pid, by, value, ct).ConfigureAwait(false);
            if (hits.Count > 0)
            {
                return hits[0];
            }
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new SnoopMcpException(
                    ErrorCode.TargetUnresponsive,
                    $"No element matched {by}='{value}' within {timeoutMs}ms.");
            }
            await Task.Delay(WaitPollMs, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task<AutomationElement> ResolveAsync(UiaElementRef reference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return RunUia(() =>
        {
            if (!string.IsNullOrEmpty(reference.Handle)
                && mCache.TryGet(reference.Handle, out AutomationElement cached, out _, out _))
            {
                return cached;
            }
            if (reference.By is { } by && reference.Value is { } value)
            {
                AutomationElement window = FindWindow(reference.Pid);
                AutomationElementCollection matches =
                    window.FindAll(TreeScope.Descendants | TreeScope.Element, UiaLocator.ToCondition(by, value));
                if (matches.Count > 1)
                {
                    throw new SnoopMcpException(
                        ErrorCode.UiaAmbiguousLocator,
                        $"Locator {by}='{value}' matched {matches.Count} elements.");
                }
                if (matches.Count == 1)
                {
                    return matches[0];
                }
            }
            throw new SnoopMcpException(ErrorCode.UiaElementStale, "Element handle expired and could not be re-resolved.");
        }, ct);
    }

    private AutomationElement ResolveRoot(int pid, string? fromHandle)
    {
        AutomationElement root;
        if (!string.IsNullOrEmpty(fromHandle) && mCache.TryGet(fromHandle, out AutomationElement cached, out _, out _))
        {
            root = cached;
        }
        else
        {
            root = FindWindow(pid);
        }
        return root;
    }

    private static AutomationElement FindWindow(int pid)
    {
        AutomationElement? window = AutomationElement.RootElement.FindFirst(
            TreeScope.Children,
            new PropertyCondition(AutomationElement.ProcessIdProperty, pid));
        return window
            ?? throw new SnoopMcpException(ErrorCode.AttachFailed, $"No top-level window for process {pid}.");
    }

    private void Walk(int pid, AutomationElement element, int remainingDepth, List<UiaElementInfo> sink)
    {
        AutomationElementCollection children = element.FindAll(TreeScope.Children, Condition.TrueCondition);
        foreach (AutomationElement child in children)
        {
            sink.Add(Project(pid, child, by: null, value: null));
            if (remainingDepth > 1)
            {
                Walk(pid, child, remainingDepth - 1, sink);
            }
        }
    }

    private UiaElementInfo Project(int pid, AutomationElement element, string? by, string? value)
    {
        AutomationElement.AutomationElementInformation info = element.Current;
        string handle = mCache.Add(pid, element, by, value);
        var patterns = new List<string>();
        foreach (AutomationPattern pattern in element.GetSupportedPatterns())
        {
            patterns.Add(PatternShortName(pattern));
        }
        Rect r = info.BoundingRectangle;
        return new UiaElementInfo(
            handle,
            info.AutomationId ?? string.Empty,
            info.Name ?? string.Empty,
            info.ControlType?.ProgrammaticName ?? string.Empty,
            info.HelpText ?? string.Empty,
            r.X, r.Y, r.Width, r.Height,
            patterns);
    }

    private static async Task<T> RunUia<T>(Func<T> work, CancellationToken ct)
    {
        try
        {
            return await Task.Run(work, ct).WaitAsync(smCallTimeout, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new SnoopMcpException(ErrorCode.TargetUnresponsive, "UI Automation call timed out.");
        }
        catch (ElementNotAvailableException ex)
        {
            // The element vanished between find and use (window closed, tree changed).
            throw new SnoopMcpException(ErrorCode.UiaElementStale, "UIA element is no longer available.", ex);
        }
        catch (ElementNotEnabledException ex)
        {
            throw new SnoopMcpException(ErrorCode.NotDrivable, "UIA element is not enabled.", ex);
        }
    }

    private static string PatternShortName(AutomationPattern pattern)
    {
        // Map the common action/value patterns to clean agent-facing names.
        if (pattern == InvokePattern.Pattern) { return "Invoke"; }
        if (pattern == TogglePattern.Pattern) { return "Toggle"; }
        if (pattern == SelectionItemPattern.Pattern) { return "SelectionItem"; }
        if (pattern == ExpandCollapsePattern.Pattern) { return "ExpandCollapse"; }
        if (pattern == ValuePattern.Pattern) { return "Value"; }
        return pattern.ProgrammaticName;
    }
}
```

> Do NOT write `Automation.PatternName(...)` unqualified inside namespace `SnoopMCP.Host.Automation` — `Automation` would bind to the enclosing namespace, not `System.Windows.Automation.Automation`. The explicit `PatternShortName` map above avoids the ambiguity and yields predictable names.

- [ ] **Step 4: Run to verify it passes** — `dotnet test tests/SnoopMCP.IntegrationTests --filter UiaDriverTests`. Expected: PASS (interactive session).

- [ ] **Step 5: Commit** — "feat: add UIA discovery driver".

---

## Task 6: UiaDriver actions — invoke & set value

**Files:**
- Modify: `src/SnoopMCP.Server/Automation/IUiaDriver.cs`, `src/SnoopMCP.Server/Automation/UiaDriver.cs`
- Test: `tests/SnoopMCP.IntegrationTests/UiaDriverTests.cs`

**Interfaces:**
- Produces (added to `IUiaDriver`):
  - `Task InvokeAsync(UiaElementRef reference, string? pattern, CancellationToken ct)`
  - `Task SetValueAsync(UiaElementRef reference, string value, CancellationToken ct)`

- [ ] **Step 1: Write the failing test** (drive a button/textbox in `SampleWpfApp`; assert an observable effect, e.g. a textbox's value after `SetValueAsync`)

```csharp
[Fact]
public async Task SetValue_OnEditableText_UpdatesValue()
{
    var cache = new ElementHandleCache();
    var driver = new UiaDriver(cache);
    // Find any editable text element; SampleWpfApp must contain a TextBox.
    IReadOnlyList<UiaElementInfo> edits = await driver.FindAsync(mApp.Id, "controlType", "Edit", default);
    Assert.NotEmpty(edits);
    var reference = new UiaElementRef(mApp.Id, edits[0].Handle, "controlType", "Edit");

    await driver.SetValueAsync(reference, "hello-uia", default);

    IReadOnlyList<UiaElementInfo> again = await driver.FindAsync(mApp.Id, "controlType", "Edit", default);
    Assert.Equal("hello-uia", again[0].Name); // WPF TextBox surfaces Value via Name/ValuePattern; adjust to read ValuePattern if needed
}
```

> If `SampleWpfApp` has no `TextBox`, add one (a single `<TextBox x:Name="Probe"/>`) as part of this task — it is the fixture these tests need. Prefer reading back via `ValuePattern.Current.Value` in the assertion for precision.

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (methods missing).

- [ ] **Step 3: Implement** (add to `IUiaDriver`, then to `UiaDriver`)

```csharp
// IUiaDriver.cs — add:
    /// <summary>Invokes the element's action pattern (auto-selected, or the named one).</summary>
    Task InvokeAsync(UiaElementRef reference, string? pattern, CancellationToken ct);

    /// <summary>Sets the element's value via ValuePattern; throws if read-only.</summary>
    Task SetValueAsync(UiaElementRef reference, string value, CancellationToken ct);
```

```csharp
// UiaDriver.cs — add:
    /// <inheritdoc />
    public async Task InvokeAsync(UiaElementRef reference, string? pattern, CancellationToken ct)
    {
        AutomationElement element = await ResolveAsync(reference, ct).ConfigureAwait(false);
        await RunUia<object?>(() =>
        {
            Act(element, pattern);
            return null;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetValueAsync(UiaElementRef reference, string value, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(value);
        AutomationElement element = await ResolveAsync(reference, ct).ConfigureAwait(false);
        await RunUia<object?>(() =>
        {
            if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out object raw))
            {
                throw new SnoopMcpException(ErrorCode.NotDrivable, "Element does not support ValuePattern.");
            }
            var vp = (ValuePattern)raw;
            if (vp.Current.IsReadOnly)
            {
                throw new SnoopMcpException(ErrorCode.ValueReadOnly, "Element value is read-only.");
            }
            vp.SetValue(value);
            return null;
        }, ct).ConfigureAwait(false);
    }

    private static void Act(AutomationElement element, string? pattern)
    {
        // Explicit pattern wins; otherwise auto-select in priority order.
        if (string.Equals(pattern, "Invoke", StringComparison.OrdinalIgnoreCase) || pattern is null)
        {
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out object invoke))
            {
                ((InvokePattern)invoke).Invoke();
                return;
            }
        }
        if (string.Equals(pattern, "SelectionItem", StringComparison.OrdinalIgnoreCase) || pattern is null)
        {
            if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object select))
            {
                ((SelectionItemPattern)select).Select();
                return;
            }
        }
        if (string.Equals(pattern, "Toggle", StringComparison.OrdinalIgnoreCase) || pattern is null)
        {
            if (element.TryGetCurrentPattern(TogglePattern.Pattern, out object toggle))
            {
                ((TogglePattern)toggle).Toggle();
                return;
            }
        }
        if (string.Equals(pattern, "ExpandCollapse", StringComparison.OrdinalIgnoreCase) || pattern is null)
        {
            if (element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out object expand))
            {
                ((ExpandCollapsePattern)expand).Expand();
                return;
            }
        }
        throw new SnoopMcpException(
            ErrorCode.NotDrivable,
            pattern is null
                ? "Element exposes no Invoke/SelectionItem/Toggle/ExpandCollapse pattern."
                : $"Element does not support the '{pattern}' pattern.");
    }
```

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit** — "feat: add UIA invoke and set-value actions".

---

## Task 7: IScreenCapture / PrintWindowCapture

**Files:**
- Create: `src/SnoopMCP.Server/Automation/IScreenCapture.cs`, `src/SnoopMCP.Server/Automation/PrintWindowCapture.cs`
- Test: `tests/SnoopMCP.IntegrationTests/PrintWindowCaptureTests.cs`

**Interfaces:**
- Produces:
  - `interface IScreenCapture { CaptureResult Capture(int pid); }`
  - `PrintWindowCapture` implements it; occluded window → full render; minimized/no-content → `SnoopMcpException(CaptureUnavailable)`.

- [ ] **Step 1: Write the failing test**

```csharp
// PrintWindowCaptureTests.cs (namespace SnoopMCP.IntegrationTests; using SnoopMCP.Host.Automation; using Xunit;)
[Fact]
public async Task Capture_ReturnsNonEmptyPng_ForVisibleWindow()
{
    // reuse the SampleWpfApp launch fixture pattern from UiaDriverTests
    var capture = new PrintWindowCapture();
    CaptureResult result = capture.Capture(mApp.Id);
    Assert.Equal("png", result.Format);
    Assert.True(result.Width > 0 && result.Height > 0);
    byte[] bytes = Convert.FromBase64String(result.Base64);
    Assert.True(bytes.Length > 100);
    // PNG signature
    Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes[..4]);
    await Task.CompletedTask;
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (type missing).

- [ ] **Step 3: Implement**

```csharp
// IScreenCapture.cs
namespace SnoopMCP.Host.Automation;

/// <summary>Captures a target window's rendered content to a PNG, even when occluded.</summary>
public interface IScreenCapture
{
    /// <summary>Captures the main window of the target process.</summary>
    CaptureResult Capture(int pid);
}
```

```csharp
// PrintWindowCapture.cs
namespace SnoopMCP.Host.Automation;

using System.Diagnostics;
using System.Drawing;             // System.Drawing.Common (see csproj note)
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using SnoopMCP.Protocol.Errors;

/// <summary>
/// Background window capture via <c>PrintWindow(PW_RENDERFULLCONTENT)</c>: renders the window's own
/// content (including GPU-composited surfaces) to an off-screen bitmap without raising or focusing it.
/// Occluded windows capture correctly; minimized windows cannot and surface <see cref="ErrorCode.CaptureUnavailable"/>.
/// </summary>
public sealed class PrintWindowCapture : IScreenCapture
{
    private const uint PwRenderFullContent = 0x00000002;
    private const string PngFormat = "png";

    /// <inheritdoc />
    public CaptureResult Capture(int pid)
    {
        nint hwnd = MainWindowHandle(pid);
        if (IsIconic(hwnd))
        {
            throw new SnoopMcpException(ErrorCode.CaptureUnavailable, "Window is minimized; cannot capture content.");
        }
        if (!GetWindowRect(hwnd, out Rect rect))
        {
            throw new SnoopMcpException(ErrorCode.CaptureUnavailable, "Could not read window bounds.");
        }
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            throw new SnoopMcpException(ErrorCode.CaptureUnavailable, "Window has no drawable area.");
        }

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            nint hdc = g.GetHdc();
            try
            {
                bool ok = PrintWindow(hwnd, hdc, PwRenderFullContent);
                if (!ok)
                {
                    throw new SnoopMcpException(ErrorCode.CaptureUnavailable, "PrintWindow failed.");
                }
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return new CaptureResult(PngFormat, width, height, Convert.ToBase64String(ms.ToArray()));
    }

    private static nint MainWindowHandle(int pid)
    {
        using Process process = Process.GetProcessById(pid);
        nint hwnd = process.MainWindowHandle;
        if (hwnd == nint.Zero)
        {
            throw new SnoopMcpException(ErrorCode.CaptureUnavailable, $"Process {pid} has no main window.");
        }
        return hwnd;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(nint hWnd, nint hdcBlt, uint nFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);
}
```

> **csproj note (decided — use `System.Drawing.Common`):** the code above needs `System.Drawing` (`Graphics.GetHdc`, `Bitmap`, `ImageFormat.Png`), which on modern .NET is the NuGet package `System.Drawing.Common` (Windows-only). Add to `SnoopMCP.Server.csproj`:
> ```xml
>         <PackageReference Include="System.Drawing.Common" Version="10.0.0" />
> ```
> (Pin to the installed .NET 10 servicing version.) This is the one new package Phase A introduces — a Microsoft first-party, Windows-only package that does **not** enter the injected payload, so the "no version-sensitive third-party payload deps" rule (which governs the payload, not the host) is unaffected. Do **not** mix in the WIC/`PngBitmapEncoder` path; the code above is the single sanctioned implementation for this plan.

- [ ] **Step 4: Run to verify it passes** — `dotnet test tests/SnoopMCP.IntegrationTests --filter PrintWindowCaptureTests`. Expected: PASS.

- [ ] **Step 5: Commit** — "feat: add PrintWindow background capture".

---

## Task 8: UiaTools MCP class (wire tools + gate enforcement)

**Files:**
- Create: `src/SnoopMCP.Server/Tools/UiaTools.cs`
- Test: `tests/SnoopMCP.IntegrationTests/UiaToolsGateTests.cs`

**Interfaces:**
- Consumes: `IUiaDriver`, `IScreenCapture`, `InteractionGate`, `ElementHandleCache` (DI); `SerializeResult`/`Promote` mirror `McpTools`.
- Produces MCP tools (wire names): `getUiaTree`, `findUiaElement`, `captureWindow`, `waitForUia`, `invokeUia`, `setUiaValue`.

- [ ] **Step 1: Write the failing test** (gate enforcement is unit-testable without a live window by calling the tool method directly)

```csharp
// UiaToolsGateTests.cs
[Fact]
public async Task InvokeUia_WhenGateOff_ThrowsInteractionDisabled()
{
    string gatePath = Path.Combine(Path.GetTempPath(), "gate-" + Guid.NewGuid().ToString("N") + ".json");
    var gate = new InteractionGate(gatePath); // default off
    var tools = new UiaTools(new UiaDriver(new ElementHandleCache()), new PrintWindowCapture(), gate);

    var reference = new UiaElementRef(1234, "1234:1", "automationId", "X");
    McpException ex = await Assert.ThrowsAsync<McpException>(() => tools.InvokeUia(reference, null, default));
    Assert.Contains("InteractionDisabled", ex.Message);
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (type missing).

- [ ] **Step 3: Implement** (mirror `McpTools`' `SerializeResult`/`Promote`; gate the two mutating tools)

```csharp
// UiaTools.cs
namespace SnoopMCP.Host.Tools;

using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Automation;
using Protocol.Errors;
using Protocol.Wire;

/// <summary>
/// MCP tools that drive and observe a live WPF window via out-of-process UI Automation and
/// PrintWindow capture. Session-free (keyed by pid; no attach required). Read-only tools are
/// ungated; mutating tools (<c>invokeUia</c>, <c>setUiaValue</c>) require the host
/// <see cref="InteractionGate"/> to be enabled.
/// </summary>
[McpServerToolType]
public sealed class UiaTools
{
    private readonly IUiaDriver mDriver;
    private readonly IScreenCapture mCapture;
    private readonly InteractionGate mGate;

    /// <summary>Initialises the tool surface.</summary>
    public UiaTools(IUiaDriver driver, IScreenCapture capture, InteractionGate gate)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(gate);
        mDriver = driver;
        mCapture = capture;
        mGate = gate;
    }

    [McpServerTool, Description("Walk the UIA tree of a WPF process (by pid) to a bounded depth. Read-only.")]
    public async Task<JsonElement> GetUiaTree(int pid, string? fromHandle, int depth, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<UiaElementInfo> tree = await mDriver.GetTreeAsync(pid, fromHandle, depth, cancellationToken).ConfigureAwait(false);
            return SerializeResult(new { elements = tree });
        }
        catch (SnoopMcpException ex) { throw Promote(ex); }
    }

    [McpServerTool, Description("Find UIA elements under a WPF process by locator (automationId|name|helpText|controlType). Read-only.")]
    public async Task<JsonElement> FindUiaElement(int pid, string by, string value, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<UiaElementInfo> hits = await mDriver.FindAsync(pid, by, value, cancellationToken).ConfigureAwait(false);
            return SerializeResult(new { elements = hits });
        }
        catch (SnoopMcpException ex) { throw Promote(ex); }
    }

    [McpServerTool, Description("Capture a WPF window (by pid) to a base64 PNG, even if occluded. Read-only.")]
    public Task<JsonElement> CaptureWindow(int pid, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            CaptureResult result = mCapture.Capture(pid);
            return Task.FromResult(SerializeResult(result));
        }
        catch (SnoopMcpException ex) { throw Promote(ex); }
    }

    [McpServerTool, Description("Poll until a UIA element matches the locator or the timeout elapses. Read-only.")]
    public async Task<JsonElement> WaitForUia(int pid, string by, string value, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            UiaElementInfo hit = await mDriver.WaitForAsync(pid, by, value, timeoutMs, cancellationToken).ConfigureAwait(false);
            return SerializeResult(hit);
        }
        catch (SnoopMcpException ex) { throw Promote(ex); }
    }

    [McpServerTool, Description("MUTATES the target: invoke an element's action pattern (Invoke/SelectionItem/Toggle/ExpandCollapse). Requires the host interaction gate to be enabled.")]
    public async Task<JsonElement> InvokeUia(UiaElementRef element, string? pattern, CancellationToken cancellationToken)
    {
        RequireGate();
        try
        {
            await mDriver.InvokeAsync(element, pattern, cancellationToken).ConfigureAwait(false);
            return SerializeResult(new { ok = true });
        }
        catch (SnoopMcpException ex) { throw Promote(ex); }
    }

    [McpServerTool, Description("MUTATES the target: set an element's value via ValuePattern. Requires the host interaction gate to be enabled.")]
    public async Task<JsonElement> SetUiaValue(UiaElementRef element, string value, CancellationToken cancellationToken)
    {
        RequireGate();
        try
        {
            await mDriver.SetValueAsync(element, value, cancellationToken).ConfigureAwait(false);
            return SerializeResult(new { ok = true });
        }
        catch (SnoopMcpException ex) { throw Promote(ex); }
    }

    private void RequireGate()
    {
        if (!mGate.IsEnabled)
        {
            throw Promote(new SnoopMcpException(
                ErrorCode.InteractionDisabled,
                "Driving is disabled. Ask the user to enable interaction in the SnoopMCP tray menu."));
        }
    }

    private static McpException Promote(SnoopMcpException ex) => new($"[{ex.Code}] {ex.Message}", ex);

    private static JsonElement SerializeResult(object payload)
    {
        string json = JsonSerializer.Serialize(payload, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
```

- [ ] **Step 4: Run to verify it passes** — Expected: PASS (gate test). Add a follow-up interactive test `GetUiaTree_ReturnsElements` mirroring Task 5 through the tool method with the gate irrelevant (read-only).

- [ ] **Step 5: Commit** — "feat: add UiaTools MCP surface with gate enforcement".

---

## Task 9: Register in DI + surface gate in /health

**Files:**
- Modify: `src/SnoopMCP.Server/ServerHost.cs`, `src/SnoopMCP.Server/HealthStatus.cs`
- Test: `tests/SnoopMCP.Host.Tests/HealthStatusTests.cs`

**Interfaces:**
- Produces: `HealthStatus.Create(version, attached, interactionEnabled)`; DI singletons `ElementHandleCache`, `InteractionGate`, `IUiaDriver`, `IScreenCapture`; `ServerHost.Build(args, port, logPath, InteractionGate? gate = null)`.

- [ ] **Step 1: Write the failing test**

```csharp
// HealthStatusTests.cs
[Fact]
public void Create_CarriesInteractionEnabled()
{
    HealthStatus s = HealthStatus.Create("1.4.0", attached: false, interactionEnabled: true);
    Assert.True(s.InteractionEnabled);
    Assert.Equal("ok", s.Status);
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (arity).

- [ ] **Step 3: Implement**

```csharp
// HealthStatus.cs — replace the record + Create:
public sealed record HealthStatus(string Status, string Version, bool Attached, bool InteractionEnabled)
{
    private const string OkStatus = "ok";

    /// <summary>Creates an <c>ok</c> health status.</summary>
    public static HealthStatus Create(string version, bool attached, bool interactionEnabled)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        return new HealthStatus(OkStatus, version, attached, interactionEnabled);
    }
}
```

```csharp
// ServerHost.cs — Build signature and registrations:
public static WebApplication Build(string[] args, int port = ListenPort, string? logPath = null, InteractionGate? gate = null)
{
    // ... existing setup ...
    InteractionGate interactionGate = gate ?? InteractionGate.ForCurrentUser();
    builder.Services.AddSingleton(interactionGate);
    builder.Services.AddSingleton<ElementHandleCache>();
    builder.Services.AddSingleton<IUiaDriver, UiaDriver>();
    builder.Services.AddSingleton<IScreenCapture, PrintWindowCapture>();
    // ... existing AddSingleton<SessionManager>() and AddSingleton<IInjectorService, ...>() ...
    // ... existing AddMcpServer(...).WithToolsFromAssembly(...) (UiaTools auto-discovered) ...
}
```

Update the `/health` endpoint:

```csharp
app.MapGet(HealthEndpointPattern, (SessionManager session, InteractionGate gate) =>
    Results.Ok(HealthStatus.Create(ThisAssembly.InformationalVersion, session.IsAttached, gate.IsEnabled)));
```

Add `using Automation;` to `ServerHost.cs`.

- [ ] **Step 4: Run to verify it passes** — build the solution `-p:TreatWarningsAsErrors=true`, run `tests/SnoopMCP.Host.Tests`. Existing `/health` callers/tests that used `Create(version, attached)` must be updated to the 3-arg form — search `HealthStatus.Create(` and fix. Expected: PASS.

- [ ] **Step 5: Commit** — "feat: register UIA services and surface interaction gate in health".

---

## Task 10: ProcessProbe elevation-error fix

**Files:**
- Modify: `src/SnoopMCP.Server/Injection/ProcessProbe.cs`
- Test: `tests/SnoopMCP.Host.Tests/ProcessProbeElevationTests.cs`

**Interfaces:** unchanged public surface; `DetermineBitness` now maps a `Win32Exception` from `process.Handle` to `SnoopMcpException(AccessDenied)`.

- [ ] **Step 1: Write the failing test** (probe the current process elevated-vs-not is environment-dependent; instead test that probing a **System/protected** pid path surfaces `AccessDenied` rather than an unmapped `Win32Exception`. A deterministic unit test: extract the handle read into a testable seam, OR assert that `Probe(pid)` on a known-inaccessible pid throws `SnoopMcpException` with `AccessDenied`, never `Win32Exception`.)

```csharp
// ProcessProbeElevationTests.cs
[Fact]
public void Probe_InaccessibleProcess_ThrowsAccessDeniedNotWin32()
{
    // PID 4 is the Windows "System" process; OpenProcess for a handle is denied to a medium-IL host.
    SnoopMcpException ex = Assert.Throws<SnoopMcpException>(() => ProcessProbe.Probe(4));
    Assert.Equal(ErrorCode.AccessDenied, ex.Code);
}
```

> If PID 4 raises `AttachFailed` earlier (process-not-found on some SKUs), pick another reliably-inaccessible pid, or refactor `DetermineBitness` to accept an injected handle-getter and unit-test the `Win32Exception` branch directly. The **invariant under test**: no raw `Win32Exception` escapes `Probe`.

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (unmapped `Win32Exception`).

- [ ] **Step 3: Implement** — wrap the handle read:

```csharp
// ProcessProbe.cs — DetermineBitness:
private static string DetermineBitness(Process process)
{
    IntPtr handle;
    try
    {
        handle = process.Handle;
    }
    catch (System.ComponentModel.Win32Exception ex)
    {
        throw new SnoopMcpException(
            ErrorCode.AccessDenied,
            "Could not open the target process — usually means an elevation mismatch (target Admin, host not). "
            + "Run the SnoopMCP host elevated (enable autostart, which registers an elevated logon task).",
            ex);
    }
    bool ok = IsWow64Process(handle, out bool isWow64);
    if (!ok)
    {
        throw new SnoopMcpException(
            ErrorCode.AccessDenied,
            "Could not query process bitness — usually means an elevation mismatch (target Admin, host not).");
    }
    bool osIs64 = Environment.Is64BitOperatingSystem;
    string bitness = (osIs64 && !isWow64) ? X64 : X86;
    return bitness;
}
```

Add `using System.ComponentModel;` if not present (it is not currently imported in `ProcessProbe.cs`).

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit** — "fix: map process-open denial to AccessDenied in ProcessProbe".

---

## Task 11: Elevated autostart task (/RL HIGHEST + self-elevating create)

**Files:**
- Modify: `src/SnoopMCP.Cli/AutostartTask.cs`
- Test: `tests/SnoopMCP.Cli.Tests/AutostartTaskTests.cs`

**Interfaces:** `BuildCreateArguments` now emits `/RL HIGHEST`. `Create` self-elevates the `schtasks` call.

- [ ] **Step 1: Update the failing test** (change the expectation to `HIGHEST`)

```csharp
// AutostartTaskTests.cs — BuildCreateArguments_IsOnLogonLimitedForcedWithHostPath renamed:
[Fact]
public void BuildCreateArguments_IsOnLogonHighestForcedWithHostPath()
{
    IReadOnlyList<string> args = AutostartTask.BuildCreateArguments(@"C:\app\SnoopMCP.Host.exe");
    Assert.Equal(
        ["/Create", "/TN", "SnoopMCP Host", "/SC", "ONLOGON", "/RL", "HIGHEST",
         "/TR", @"C:\app\SnoopMCP.Host.exe", "/F"],
        args);
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (`LIMITED` vs `HIGHEST`).

- [ ] **Step 3: Implement** — rename the constant and value; make `Create` self-elevate:

```csharp
// AutostartTask.cs — replace the run-level constant:
    private const string Highest = "HIGHEST";
// ... and in BuildCreateArguments, use Highest instead of Limited.
// Update the class-doc comment: "The task runs at logon, elevated (/RL HIGHEST) for administrators."
```

```csharp
// AutostartTask.cs — Create must run schtasks elevated (registering a HIGHEST task needs an elevated caller):
    /// <summary>
    /// Creates (or replaces) the elevated logon task. Registering a /RL HIGHEST task requires an
    /// elevated caller, so this relaunches schtasks with the "runas" verb, producing one UAC prompt.
    /// Returns true on success.
    /// </summary>
    public static bool Create(string hostExePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostExePath);
        return RunElevated(BuildCreateArguments(hostExePath));
    }

    private static bool RunElevated(IReadOnlyList<string> arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = SchTasksExe,
            UseShellExecute = true,   // required for the "runas" verb (UAC)
            Verb = "runas",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (string arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }
        int exit;
        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                exit = -1;
            }
            else
            {
                process.WaitForExit();
                exit = process.ExitCode;
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User declined the UAC prompt (ERROR_CANCELLED) or elevation is unavailable.
            exit = -1;
        }
        return exit == 0;
    }
```

> `Remove()`/`Exists()` keep the non-elevated `Run(...)` path — deleting/querying a task the current user created does not require elevation. `BuildDeleteArguments`/`BuildQueryArguments` are unchanged.
> **Standard-user caveat (spec §7.3.5):** on a non-admin account `/RL HIGHEST` yields only a limited token. **Task 12b** (host self-elevation, per correction C2) detects this and surfaces it via the tray tooltip — NOT Task 13, which is only the interaction toggle. Here, the `runas` prompt simply fails/decline-handles gracefully.

- [ ] **Step 4: Run to verify it passes** — `dotnet test tests/SnoopMCP.Cli.Tests`. Expected: PASS (argument-builder tests; the elevation path is not unit-run — it needs interactive UAC).

- [ ] **Step 5: Commit** — "feat: register autostart as an elevated (/RL HIGHEST) logon task".

---

## Task 12: Route manual start through the task (elevated), with Medium fallback

**Files:**
- Modify: `src/SnoopMCP.Cli/HostProcess.cs`
- Test: `tests/SnoopMCP.Cli.Tests/HostProcessTests.cs` (create)

**Interfaces:** `HostProcess.Start()` runs the registered task via `schtasks /Run` when it exists (elevated), else falls back to a direct (Medium) launch. Add `AutostartTask.BuildRunArguments()` and `AutostartTask.Run(...)` reuse.

- [ ] **Step 1: Write the failing test** (unit-test the new argument builder)

```csharp
// AutostartTaskTests.cs — add:
[Fact]
public void BuildRunArguments_RunsTheTask()
{
    Assert.Equal(["/Run", "/TN", "SnoopMCP Host"], AutostartTask.BuildRunArguments());
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (method missing).

- [ ] **Step 3: Implement**

```csharp
// AutostartTask.cs — add:
    private const string RunSwitch = "/Run";

    /// <summary>Builds the <c>schtasks</c> arguments that run the logon task now (inherits its RunLevel).</summary>
    public static IReadOnlyList<string> BuildRunArguments()
    {
        return [RunSwitch, TaskNameSwitch, TaskName];
    }

    /// <summary>Runs the registered task immediately (elevated if it is a HIGHEST task). Returns true on success.</summary>
    public static bool RunNow()
    {
        return Run(BuildRunArguments()) == 0;
    }
```

```csharp
// HostProcess.cs — Start():
    /// <summary>
    /// Launches the host. Prefers running the registered elevated logon task (so a manual start is
    /// elevated just like autostart); falls back to a direct launch when the task is absent.
    /// Returns true if a launch was initiated.
    /// </summary>
    public static bool Start()
    {
        bool started;
        if (AutostartTask.Exists())
        {
            started = AutostartTask.RunNow();
        }
        else
        {
            var psi = new ProcessStartInfo { FileName = ExePath(), UseShellExecute = false };
            using var process = Process.Start(psi);
            started = process is not null;
        }
        return started;
    }
```

> `AutostartTask.Run(...)` is currently `private`; keep it private and expose `RunNow()` as the public seam (matches the existing `Create`/`Remove`/`Exists` shape).

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit** — "feat: route manual host start through the elevated task".

---

## Task 13: Tray toggle for the interaction gate

**Files:**
- Modify: `src/SnoopMCP.Host/TrayViewModel.cs`, `src/SnoopMCP.Host/App.xaml` (menu item), `src/SnoopMCP.Host/App.xaml.cs` (pass a gate)
- Test: `tests/SnoopMCP.Host.Tests/TrayViewModelGateTests.cs`

**Interfaces:** `TrayViewModel` gains `bool InteractionEnabled { get; }` and `ICommand ToggleInteractionCommand`, constructed with an `InteractionGate`.

- [ ] **Step 1: Write the failing test**

```csharp
// TrayViewModelGateTests.cs
[Fact]
public void ToggleInteraction_FlipsGate_AndRaisesPropertyChanged()
{
    string path = Path.Combine(Path.GetTempPath(), "gate-" + Guid.NewGuid().ToString("N") + ".json");
    var gate = new InteractionGate(path);
    var controller = new ServerController([]);
    var vm = new TrayViewModel(controller, () => { }, (_, _) => { }, (_, _) => { }, gate);
    bool raised = false;
    vm.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(TrayViewModel.InteractionEnabled);

    Assert.False(vm.InteractionEnabled);
    vm.ToggleInteractionCommand.Execute(null);

    Assert.True(vm.InteractionEnabled);
    Assert.True(gate.IsEnabled);
    Assert.True(raised);
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (ctor arity / members).

- [ ] **Step 3: Implement** — add the gate to the ctor and the command:

```csharp
// TrayViewModel.cs — add field, ctor param, property, command:
    private readonly InteractionGate mGate;
    private readonly RelayCommand mToggleInteractionCommand;

    // ctor: add `InteractionGate gate` parameter, ArgumentNullException.ThrowIfNull(gate); mGate = gate;
    // and: mToggleInteractionCommand = new RelayCommand(ToggleInteraction);

    /// <summary>Gets whether mutating driving tools are currently permitted.</summary>
    public bool InteractionEnabled => mGate.IsEnabled;

    /// <summary>Gets the command that flips the interaction gate.</summary>
    public ICommand ToggleInteractionCommand => mToggleInteractionCommand;

    private void ToggleInteraction()
    {
        mGate.SetEnabled(!mGate.IsEnabled);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InteractionEnabled)));
    }
```

Wire the menu item in `App.xaml` (the tray `ContextMenu`): add a checkable `MenuItem` bound to `ToggleInteractionCommand` with `IsChecked="{Binding InteractionEnabled, Mode=OneWay}"` and header `"Allow app interaction (driving)"`. In `App.xaml.cs`, construct the `TrayViewModel` with `InteractionGate.ForCurrentUser()`.

> **Update every existing `new TrayViewModel(...)` call site** to pass the new 5th `InteractionGate` argument — at minimum `App.xaml.cs`, plus any `TrayViewModel` unit tests. Search `new TrayViewModel(` before building. The Host project will not compile until all call sites match the new arity.

- [ ] **Step 4: Run to verify it passes** — Expected: PASS. Build the Host project to confirm XAML compiles.

- [ ] **Step 5: Commit** — "feat: add tray toggle for the interaction gate".

---

## Task 14: snoopmcp-uia skill + multi-skill registry + drift guard

**Files:**
- Create: `skills/snoopmcp-uia/SKILL.md`
- Modify: `src/SnoopMCP.ClientIntegration/SnoopSkill.cs`, `src/SnoopMCP.ClientIntegration/ClaudeCodeWriter.cs`
- Test: `tests/SnoopMCP.ClientIntegration.Tests/SnoopSkillTests.cs`

**Interfaces:** `SnoopSkill.Install(skillsDir)`/`Remove(skillsDir)` now install/remove **all** registered skills; `SnoopSkill.Definitions` exposes `(Name, Body)` pairs.

- [ ] **Step 1: Write the failing tests**

```csharp
// SnoopSkillTests.cs — add:
[Fact]
public void Install_WritesBothSkills()
{
    string skillsDir = Path.Combine(mDir, "skills");
    Assert.True(SnoopSkill.Install(skillsDir));
    Assert.True(File.Exists(Path.Combine(skillsDir, "snoopmcp-first", "SKILL.md")));
    Assert.True(File.Exists(Path.Combine(skillsDir, "snoopmcp-uia", "SKILL.md")));
}

[Fact]
public void EmbeddedBodies_MatchRepoFiles()
{
    // Repo root is four levels up from the test bin; adjust via a walk to the solution file if needed.
    string repoRoot = FindRepoRoot();
    foreach ((string name, string body) in SnoopSkill.Definitions)
    {
        string repoFile = Path.Combine(repoRoot, "skills", name, "SKILL.md");
        Assert.True(File.Exists(repoFile), $"Missing repo skill file: {repoFile}");
        Assert.Equal(
            File.ReadAllText(repoFile).ReplaceLineEndings("\n").TrimEnd(),
            body.ReplaceLineEndings("\n").TrimEnd());
    }
}
```

> Provide `FindRepoRoot()` as a small helper walking up until it finds `SnoopMCP.sln`. This closes the embedded-vs-repo drift gap the spec calls out (§8.4).

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (second skill absent; `Definitions` missing).

- [ ] **Step 3a: Author the skill file** `skills/snoopmcp-uia/SKILL.md`:

```markdown
---
name: snoopmcp-uia
description: Use when driving or automating a running WPF application hands-free — clicking, selecting, setting values, navigating, waiting, or screenshotting — via SnoopMCP's UI Automation tools. Complements snoopmcp-first (inspection); this skill is for INTERACTION and background capture, never synthesized mouse/keyboard input.
---

# SnoopMCP — driving a live WPF app (UI Automation)

SnoopMCP can drive a running WPF app through UI Automation without touching the mouse or stealing
focus, and capture the window even when it is occluded. Driving is two-tier:

1. **UIA tier (no attach needed).** Keyed by PID.
   - `getUiaTree(pid, fromHandle?, depth)` — discover elements (AutomationId, Name, ControlType, HelpText, bounds, patterns).
   - `findUiaElement(pid, by, value)` — `by` ∈ automationId | name | helpText | controlType (this is the stability order — prefer automationId; treat a VM-type-name Name as a smell).
   - `captureWindow(pid)` — base64 PNG; works while occluded; a minimized window returns CaptureUnavailable.
   - `waitForUia(pid, by, value, timeoutMs)` — wait for an element instead of sleeping.
   - `invokeUia(element, pattern?)` — click/select/toggle/expand. MUTATES.
   - `setUiaValue(element, value)` — set a text/numeric field. MUTATES.
2. **Payload tier (requires `attach`).** For controls UIA can't drive and for ground-truth checks — see the driving tools added under attach (peerInvoke, executeCommand, waitForValue).

## Rules

- Never ask SnoopMCP to synthesize mouse/keyboard input — there is no such tool by design. If a control
  is `NotDrivable`, tell the user and ask them to act, or try the payload tier.
- Mutating tools (`invokeUia`, `setUiaValue`) require the host **interaction gate**. If you get
  `InteractionDisabled`, ask the user to enable "Allow app interaction (driving)" in the SnoopMCP tray.
- Driving/capturing an elevated (admin) app requires the SnoopMCP host to run elevated — enabling
  autostart registers an elevated logon task (one UAC). This is admin-only.
- Element handles are short-lived; pass back the `element` reference you received. If it is stale,
  SnoopMCP re-resolves it by locator, or returns `UiaElementStale`/`UiaAmbiguousLocator` — re-find in that case.
- Endpoint: http://127.0.0.1:6300/mcp. Start the SnoopMCP host (tray app) first.
```

- [ ] **Step 3b: Refactor `SnoopSkill`** to a registry (embed the same body as the repo file, verbatim):

```csharp
// SnoopSkill.cs — replace the single-body model with a registry:
public static class SnoopSkill
{
    public const string FirstSkillName = "snoopmcp-first";
    public const string UiaSkillName = "snoopmcp-uia";
    private const string SkillFileName = "SKILL.md";

    private const string FirstSkillBody = """<existing snoopmcp-first body, unchanged>""";
    private const string UiaSkillBody = """<paste the exact snoopmcp-uia/SKILL.md content>""";

    /// <summary>The skills installed for Claude Code, as (directory-name, body) pairs.</summary>
    public static IReadOnlyList<(string Name, string Body)> Definitions { get; } =
    [
        (FirstSkillName, FirstSkillBody),
        (UiaSkillName, UiaSkillBody)
    ];

    public static bool Install(string skillsDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        bool ok = true;
        foreach ((string name, string body) in Definitions)
        {
            try
            {
                string dir = Path.Combine(skillsDir, name);
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, SkillFileName), body);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ok = false;
            }
        }
        return ok;
    }

    public static bool Remove(string skillsDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        bool ok = true;
        foreach ((string name, _) in Definitions)
        {
            string dir = Path.Combine(skillsDir, name);
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, recursive: true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ok = false; }
            }
        }
        return ok;
    }
}
```

> **Drift rule:** `FirstSkillBody`/`UiaSkillBody` must be byte-identical (modulo trailing newline) to the repo `skills/<name>/SKILL.md`. The `EmbeddedBodies_MatchRepoFiles` test enforces it. When editing a skill, edit the repo file and the constant together.

- [ ] **Step 3c: Update `ClaudeCodeWriter` status strings** (`ClaudeCodeWriter.cs:31-34`) to read "installed the SnoopMCP skills" (plural) instead of "the snoopmcp-first skill".

- [ ] **Step 4: Run to verify it passes** — `dotnet test tests/SnoopMCP.ClientIntegration.Tests`. Expected: PASS (including the drift guard).

- [ ] **Step 5: Commit** — "feat: add snoopmcp-uia skill via multi-skill registry".

---

## Final integration step

- [ ] Build the whole solution with `-p:TreatWarningsAsErrors=true`; run every non-interactive test project. Expected: clean build, green tests.
- [ ] Update `README.md`: note the driving tools, the interaction gate (default off, tray toggle), and the elevated-autostart/admin-only requirement. Revise "v1 is read-only" to scope it to inspection. (Keep this in one commit: "docs: document driving tools, interaction gate, and elevated autostart".)
- [ ] Run the interactive `SnoopMCP.IntegrationTests` UIA/capture tests once on an interactive desktop and record the result.

## Self-Review Notes (author)
- **Spec coverage:** §4.1 (subsystem, session-free, x86 note) → Tasks 5–9; §5 Phase A tool rows → Task 8; §5.1 elementRef → Tasks 3–4; §6 gate/no-input → Tasks 2, 8, 13; §7 elevation → Tasks 10–12; §8 skill → Task 14; §9 Phase A codes → Task 1; §10 tests → per-task + final step.
- **Deviations (intentional, noted):** UIA DTOs live in `SnoopMCP.Server` not `Protocol`; added `TargetUnresponsive` code; capture returns base64-in-JSON (image-content deferred); `System.Drawing.Common` vs WIC decision left to the implementer with a default.
- **x86 driving:** `UiaTools` never calls `ProcessProbe.EnsureX64`, so x86 WPF apps are drivable via UIA as the spec intends; only `attach` remains x64-gated.
