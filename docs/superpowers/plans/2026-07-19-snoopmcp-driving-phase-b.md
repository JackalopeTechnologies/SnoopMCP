# SnoopMCP Driving — Phase B Implementation Plan (Payload Fallback + Ground Truth)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add in-process driving that out-of-process UIA cannot do — invoking the real `AutomationPeer`, executing the bound `ICommand`, reading view-model ground truth — plus mutation-safe dispatcher semantics, exposed as gated MCP tools over the existing pipe.

**Architecture:** New wire tool names + request/response records in `SnoopMCP.Protocol`, new `Interaction/` action classes and `IToolHandler`s in `SnoopMCP.Payload` (registered in `PayloadEntryPoint.Run`), a mutation-safe path added to `DispatcherMarshal`, and relay `[McpServerTool]` methods in the host's `McpTools` that gate the mutating ones through the Phase A `InteractionGate`. Requires `attach` (goes over the pipe).

**Tech Stack:** .NET 10 (`net10.0-windows`, x64), WPF (`System.Windows`, `System.Windows.Automation.Peers`, `System.Windows.Input.ICommand`), the SnoopMCP pipe protocol, xUnit v3 + Xunit.StaFact (WPF objects need an STA/dispatcher in tests).

## Global Constraints

- **Standards:** Jackalope. Zero-warning build (`-p:TreatWarningsAsErrors=true`). `m`/`sm` field prefixes; file-scoped namespaces; 4-line MIT header.
- **Depends on Phase A:** the `InteractionGate` (`src/SnoopMCP.Server/Automation/InteractionGate.cs`) and the Phase A error codes. Do Phase A first.
- **Payload dependency rule:** `SnoopMCP.Payload` carries **no** version-sensitive third-party assemblies — framework-only references. New `Interaction/` code uses only WPF/BCL types. (See `payload-no-thirdparty-deps`.)
- **Wire evolution:** `ErrorCode` and `ToolNames` are append-only; new capability is new tool names + new argument/response records, never envelope changes. 16 MiB max frame; camelCase; omit-null.
- **Dispatcher discipline:** every WPF touch marshals through `DispatcherMarshal`; the pipe thread never touches WPF directly. Mutating calls must never reuse the read-only abort-on-timeout path (timeout must be distinguishable from not-applied).
- **Gate:** mutating tools (`peerInvoke`, `executeCommand`) check the host `InteractionGate`; read-only tools (`getAutomationPeerInfo`, `waitForValue`) do not.
- **Commits:** message via file + `git commit -F`; no AI attribution; branch `feat/69-interaction-driving`.
- **Spec:** `docs/superpowers/specs/2026-07-19-snoopmcp-interaction-driving-design.md` §4.2, §5 (Phase B rows), §9 (Phase B codes).

---

## Codex Review — Binding Corrections (READ FIRST — these OVERRIDE task text where they conflict)

A read-only Codex review (gpt-5.6-sol, high effort) found the issues below. Prerequisite: **Phase A Task 0** (the `SampleWpfApp` probe fixture — `RunProbe` button + `RunProbeCommand` setting `ProbeStatus="done"`, `ProbeText` TextBox) must exist; the Phase B E2E (Task 10) depends on it. Its buttons currently have no commands, so the original Task 10 could not pass.

### C5 (P1) — `getAutomationPeerInfo`: add runtime-id; scope the bridge honestly
Spec §4.2 wants AutomationId, Name, runtime-id, and "both directions". Corrections (Tasks 2, 6):
- Add `RuntimeId` to the response:
```csharp
public sealed record GetAutomationPeerInfoResponse(
    string AutomationId, string Name, string ClassName, string ControlType, IReadOnlyList<int> RuntimeId);
```
- `PeerInfoReader.Read` returns `RuntimeId: peer.GetRuntimeId() ?? Array.Empty<int>()`.
- **Scope decision (revises spec §4.2 — reflect it there):** v1 ships the **forward** bridge (Snoop element id → UIA identity). Cross-tier correlation between this payload tier and the Phase A host-UIA tier should key on **`AutomationId`** (both tiers expose it and it matches), NOT runtime-id — the WPF peer's raw `GetRuntimeId()` and the UIA *client's* full runtime id use different encodings and are not directly comparable. A UIA→Snoop-id **reverse-lookup tool** is deferred to v-next; if required now, add a new payload tool that walks the visual tree, creates peers, and matches by `AutomationId`. Update the spec §4.2 bullet to say "forward mapping in v1; reverse lookup deferred."

### C6 (P1) — `ValueWaiter` must be async, and the test must not block the dispatcher
The original `Wait` uses `Thread.Sleep`; the original test calls it synchronously on the `[WpfFact]` dispatcher thread while the value-flip is queued with `BeginInvoke` — the flip can never run, so it times out. Make the waiter async and fix the test (Task 7):
```csharp
// ValueWaiter.cs — async signature; Task.Delay instead of Thread.Sleep:
public async Task<WaitForValueResponse> WaitAsync(
    DependencyObject element, string? dependencyProperty, string? dataContextPath,
    string expected, int timeoutMs, DispatcherMarshal marshal, CancellationToken cancellationToken)
{
    // ...validation as before...
    DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
    string? actual = null;
    bool matched = false;
    while (!matched && DateTimeOffset.UtcNow < deadline)
    {
        cancellationToken.ThrowIfCancellationRequested();
        actual = marshal.Invoke(() => ReadValue(element, dependencyProperty, dataContextPath), cancellationToken);
        matched = string.Equals(actual, expected, StringComparison.Ordinal);
        if (!matched) { await Task.Delay(PollIntervalMs, cancellationToken).ConfigureAwait(false); }
    }
    return new WaitForValueResponse(matched, actual);
}
```
`WaitForValueToolHandler.ExecuteAsync` awaits `WaitAsync`. Corrected test:
```csharp
[WpfFact]
public async Task WaitAsync_DataContextPath_MatchesAfterChange()
{
    var vm = new TestVm { Status = "pending" };
    var element = new TextBlock { DataContext = vm };
    var dispatcher = Dispatcher.CurrentDispatcher;
    var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromSeconds(2));
    _ = dispatcher.BeginInvoke(async () => { await Task.Delay(150); vm.Status = "done"; });
    WaitForValueResponse r = await new ValueWaiter().WaitAsync(element, null, "Status", "done", 2000, marshal, default);
    Assert.True(r.Matched);
    Assert.Equal("done", r.ActualValue);
}
```
Rationale: `[WpfFact]` async runs the body on the dispatcher with a message pump; `await Task.Delay` yields so the queued flip runs. `Thread.Sleep` would block the pump — that was the deadlock. (In production the handler runs on the pipe thread, so the reads marshal to the UI thread and the delay yields the pipe thread — correct either way, but async is required for the test and is the right model.)

### C7 (P2) — Wire the fire-and-forget path to a real tool (dispatch mode)
`Post`/`ActionDispatched` are defined but unused, yet `executeCommand` can open a modal (which would block `InvokeMutating` to `ActionPending`). Give the two mutating tools a `dispatch` mode:
- Requests gain an optional mode: `PeerInvokeRequest(int Id, string Pattern, string? Dispatch)`, `ExecuteCommandRequest(int Id, string? Path, string? Parameter, string? Dispatch)` — `Dispatch` ∈ `null|"wait"` (default) or `"post"`.
- Responses gain a `Dispatched` flag: `PeerInvokeResponse(bool Ok, bool Dispatched)`, `ExecuteCommandResponse(bool Executed, bool CanExecute, bool Dispatched)`.
- Handler: `"post"` → `mMarshal.Post(() => driver.Invoke(...))` then return `Dispatched=true, Ok=false`; `"wait"` → `InvokeMutating(...)` as before, `Dispatched=false`.
- Host relay (`McpTools`) adds the optional `dispatch` string param and forwards it.
- **`ActionDispatched` (code 20) is a reserved code, NOT an error thrown here** — successful fire-and-forget is conveyed by the `Dispatched` response field (a dispatched action is not a failure). Keep the code appended (append-only) for potential future error use; do not throw it. Update Phase B Task 1 doc comment to say "reserved".

### C8 (P2) — Execute bound `RoutedCommand`s through the source/target route
`CommandInvoker` calls `ICommand.CanExecute/Execute(parameter)` directly and discards `ICommandSource.CommandTarget`. WPF `RoutedCommand`s resolve against a target via routed events; passing no target falls back to `Keyboard.FocusedElement`, which is wrong under hands-free/no-focus operation. Correct `CommandInvoker.Execute` (Task 5):
```csharp
// after resolving (ICommand command, object? boundParameter) and commandParameter:
IInputElement? target = (element as ICommandSource)?.CommandTarget ?? element as IInputElement;
if (command is RoutedCommand routed)
{
    if (!routed.CanExecute(commandParameter, target))
    {
        throw new SnoopMcpException(ErrorCode.CommandNotExecutable, "The routed command's CanExecute returned false.");
    }
    routed.Execute(commandParameter, target);
}
else
{
    if (!command.CanExecute(commandParameter))
    {
        throw new SnoopMcpException(ErrorCode.CommandNotExecutable, "The command's CanExecute returned false.");
    }
    command.Execute(commandParameter);
}
return new ExecuteCommandResponse(Executed: true, CanExecute: true, Dispatched: false);
```
Add a test binding an `RoutedCommand` with a `CommandBinding` on a target element and asserting execution routes to that target (not the focused element). `Resolve(...)` must preserve `CommandTarget` (return it alongside the command) so it flows to `Execute`.

### C9 (P2) — Task 10 E2E: use the Phase A Task 0 fixture and assert real effects
Rewrite Task 10 to: attach to `SampleWpfApp`; enable the gate; `executeCommand` on the `RunProbe` button's id (its `RunProbeCommand`); `waitForValue` on `ProbeStatus == "done"`; assert `Matched`. Then flip the gate off and assert `executeCommand` yields `InteractionDisabled`. The sample must carry the Task 0 fixture first.

---

## File Structure

**Create:**
- `src/SnoopMCP.Protocol/Tools/GetAutomationPeerInfoRequest.cs`, `GetAutomationPeerInfoResponse.cs`
- `src/SnoopMCP.Protocol/Tools/PeerInvokeRequest.cs`, `PeerInvokeResponse.cs`
- `src/SnoopMCP.Protocol/Tools/ExecuteCommandRequest.cs`, `ExecuteCommandResponse.cs`
- `src/SnoopMCP.Protocol/Tools/WaitForValueRequest.cs`, `WaitForValueResponse.cs`
- `src/SnoopMCP.Payload/Interaction/AutomationPeerDriver.cs`
- `src/SnoopMCP.Payload/Interaction/CommandInvoker.cs`
- `src/SnoopMCP.Payload/Interaction/PeerInfoReader.cs`
- `src/SnoopMCP.Payload/Interaction/ValueWaiter.cs`
- `src/SnoopMCP.Payload/Tools/PeerInvokeToolHandler.cs`, `ExecuteCommandToolHandler.cs`, `GetAutomationPeerInfoToolHandler.cs`, `WaitForValueToolHandler.cs`
- Tests: `tests/SnoopMCP.Payload.Tests/AutomationPeerDriverTests.cs`, `CommandInvokerTests.cs`, `PeerInfoReaderTests.cs`, `ValueWaiterTests.cs`, `DispatcherMarshalMutatingTests.cs`

**Modify:**
- `src/SnoopMCP.Protocol/Errors/ErrorCode.cs` — append `ActionPending=19`, `ActionDispatched=20`, `CommandNotExecutable=21`.
- `src/SnoopMCP.Protocol/ToolNames.cs` — add four constants.
- `src/SnoopMCP.Payload/DispatcherMarshal.cs` — add `InvokeMutating` and `Post`.
- `src/SnoopMCP.Payload/PayloadEntryPoint.cs` — construct the four handlers + `Interaction` action classes and register them.
- `src/SnoopMCP.Server/Tools/McpTools.cs` — inject `InteractionGate`; add four relay tools (gate the two mutating).
- `src/SnoopMCP.Server/ServerHost.cs` — `McpTools` already resolved via DI; ensure `InteractionGate` (registered in Phase A) is injectable into `McpTools`.

---

## Task 1: Append Phase B error codes

**Files:**
- Modify: `src/SnoopMCP.Protocol/Errors/ErrorCode.cs`
- Test: `tests/SnoopMCP.Protocol.Tests/ErrorCodeTests.cs`

**Interfaces:** Produces `ActionPending=19`, `ActionDispatched=20`, `CommandNotExecutable=21`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void PhaseBDrivingCodes_HaveStableNumbers()
{
    Assert.Equal(19, (int)ErrorCode.ActionPending);
    Assert.Equal(20, (int)ErrorCode.ActionDispatched);
    Assert.Equal(21, (int)ErrorCode.CommandNotExecutable);
}
```

- [ ] **Step 2: Run to verify it fails** — `dotnet test tests/SnoopMCP.Protocol.Tests`. Expected: FAIL.

- [ ] **Step 3: Append** (after `TargetUnresponsive = 18` from Phase A):

```csharp
    /// <summary>A mutating action timed out on the dispatcher; it may still have applied — verify.</summary>
    ActionPending = 19,

    /// <summary>A fire-and-forget action (e.g. one opening a dialog) was posted; observe for its effect.</summary>
    ActionDispatched = 20,

    /// <summary>The bound command's CanExecute returned false.</summary>
    CommandNotExecutable = 21
```

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit** — "feat: add Phase B driving error codes".

---

## Task 2: Wire tool names + request/response records

**Files:**
- Modify: `src/SnoopMCP.Protocol/ToolNames.cs`
- Create: the eight DTO files listed above.
- Test: `tests/SnoopMCP.Protocol.Tests/ToolNamesTests.cs` (create if absent)

**Interfaces:** Produces
- `ToolNames.GetAutomationPeerInfo="getAutomationPeerInfo"`, `PeerInvoke="peerInvoke"`, `ExecuteCommand="executeCommand"`, `WaitForValue="waitForValue"`.
- `GetAutomationPeerInfoRequest(int Id)`
- `GetAutomationPeerInfoResponse(string AutomationId, string Name, string ClassName, string ControlType)`
- `PeerInvokeRequest(int Id, string Pattern)`
- `PeerInvokeResponse(bool Ok)`
- `ExecuteCommandRequest(int Id, string? Path, string? Parameter)`
- `ExecuteCommandResponse(bool Executed, bool CanExecute)`
- `WaitForValueRequest(int Id, string? DependencyProperty, string? DataContextPath, string Expected, int TimeoutMs)`
- `WaitForValueResponse(bool Matched, string? ActualValue)`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void DrivingToolNames_AreStable()
{
    Assert.Equal("getAutomationPeerInfo", ToolNames.GetAutomationPeerInfo);
    Assert.Equal("peerInvoke", ToolNames.PeerInvoke);
    Assert.Equal("executeCommand", ToolNames.ExecuteCommand);
    Assert.Equal("waitForValue", ToolNames.WaitForValue);
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL.

- [ ] **Step 3a: Add the tool names** to `ToolNames.cs`:

```csharp
    /// <summary>Bridge a Snoop element id to its UIA identity (AutomationId/Name/ClassName/ControlType).</summary>
    public const string GetAutomationPeerInfo = "getAutomationPeerInfo";

    /// <summary>Drive an element's AutomationPeer pattern in-process (fallback for what UIA2 cannot do).</summary>
    public const string PeerInvoke = "peerInvoke";

    /// <summary>Execute the ICommand bound to an element (CanExecute-gated).</summary>
    public const string ExecuteCommand = "executeCommand";

    /// <summary>Poll a dependency property or DataContext path until it matches an expected value.</summary>
    public const string WaitForValue = "waitForValue";
```

- [ ] **Step 3b: Create the eight records** (positional `sealed record`s, MIT header, XML doc per param — follow `GetDependencyPropertyRequest`/`Response` convention). Example:

```csharp
// PeerInvokeRequest.cs
namespace SnoopMCP.Protocol.Tools;

/// <summary>Wire request for <c>peerInvoke</c>.</summary>
/// <param name="Id">Element id whose AutomationPeer is driven.</param>
/// <param name="Pattern">The peer pattern to invoke: Invoke | Toggle | SelectionItem | ExpandCollapse.</param>
public sealed record PeerInvokeRequest(int Id, string Pattern);
```

```csharp
// ExecuteCommandRequest.cs
namespace SnoopMCP.Protocol.Tools;

/// <summary>Wire request for <c>executeCommand</c>.</summary>
/// <param name="Id">Element id: an ICommandSource (e.g. Button) or the root for <paramref name="Path"/>.</param>
/// <param name="Path">Optional dotted DataContext path to an ICommand; when null, the element's own Command is used.</param>
/// <param name="Parameter">Optional command parameter (string); when null, the element's CommandParameter is used.</param>
public sealed record ExecuteCommandRequest(int Id, string? Path, string? Parameter);
```

```csharp
// WaitForValueRequest.cs
namespace SnoopMCP.Protocol.Tools;

/// <summary>Wire request for <c>waitForValue</c>. Exactly one of the two sources must be set.</summary>
/// <param name="Id">Element id whose DP or DataContext is polled.</param>
/// <param name="DependencyProperty">The DP registered name to poll, or null.</param>
/// <param name="DataContextPath">The dotted DataContext path to poll, or null.</param>
/// <param name="Expected">The expected value, compared via invariant-culture string equality.</param>
/// <param name="TimeoutMs">Maximum time to poll.</param>
public sealed record WaitForValueRequest(int Id, string? DependencyProperty, string? DataContextPath, string Expected, int TimeoutMs);
```

(Create the four `*Response` records and `GetAutomationPeerInfoRequest` similarly, matching the Interfaces block.)

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit** — "feat: add Phase B driving wire contract".

---

## Task 3: Mutation-safe DispatcherMarshal (InvokeMutating + Post)

**Files:**
- Modify: `src/SnoopMCP.Payload/DispatcherMarshal.cs`
- Test: `tests/SnoopMCP.Payload.Tests/DispatcherMarshalMutatingTests.cs`

**Interfaces:** Produces
- `T InvokeMutating<T>(Func<T> work, CancellationToken ct)` — like `Invoke` but on timeout does **not** `Abort()`; throws `SnoopMcpException(ActionPending)`.
- `void Post(Action work)` — fire-and-forget `InvokeAsync` (no wait); returns immediately.

- [ ] **Step 1: Write the failing test** (use a real dispatcher via `Xunit.StaFact`'s `[WpfFact]`)

```csharp
// DispatcherMarshalMutatingTests.cs (using Xunit; using System.Windows.Threading;)
[WpfFact]
public void InvokeMutating_ReturnsResult_WhenFast()
{
    var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));
    int r = marshal.InvokeMutating(() => 42, default);
    Assert.Equal(42, r);
}

[WpfFact]
public void InvokeMutating_Timeout_ThrowsActionPending_NotDispatcherTimeout()
{
    // A foreign-thread call whose work blocks past the timeout. Run the marshal from a worker thread
    // targeting this test's dispatcher, then pump briefly.
    var dispatcher = Dispatcher.CurrentDispatcher;
    var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromMilliseconds(100));
    Exception? caught = null;
    var worker = new Thread(() =>
    {
        try { marshal.InvokeMutating<object?>(() => { Thread.Sleep(1000); return null; }, default); }
        catch (Exception ex) { caught = ex; }
    });
    worker.Start();
    // pump the dispatcher so the queued work starts, then let the timeout fire
    var frame = new DispatcherFrame();
    Dispatcher.CurrentDispatcher.BeginInvoke(() => { Thread.Sleep(400); frame.Continue = false; });
    Dispatcher.PushFrame(frame);
    worker.Join(2000);
    var mcp = Assert.IsType<SnoopMCP.Protocol.Errors.SnoopMcpException>(caught);
    Assert.Equal(SnoopMCP.Protocol.Errors.ErrorCode.ActionPending, mcp.Code);
}
```

> This timing test is inherently interactive/dispatcher-bound; if it proves flaky in CI, mark it `[WpfFact(Skip=...)]` for CI and keep it as an interactive check. The essential invariant: **timeout of a mutating call yields `ActionPending`, and the operation is NOT aborted.**

- [ ] **Step 2: Run to verify it fails** — `dotnet test tests/SnoopMCP.Payload.Tests --filter DispatcherMarshalMutating`. Expected: FAIL (method missing).

- [ ] **Step 3: Implement** (add to `DispatcherMarshal`)

```csharp
// DispatcherMarshal.cs — add:
    /// <summary>
    /// Runs a MUTATING <paramref name="work"/> on the dispatcher. Identical to <see cref="Invoke{T}"/>
    /// except that on timeout the operation is NOT aborted (the mutation may already be applied) and
    /// the caller receives <see cref="ErrorCode.ActionPending"/> so it can verify rather than assume.
    /// </summary>
    public T InvokeMutating<T>(Func<T> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        cancellationToken.ThrowIfCancellationRequested();
        if (mDispatcher.HasShutdownStarted || mDispatcher.HasShutdownFinished)
        {
            throw new InvalidOperationException("Dispatcher has been shut down.");
        }

        T result;
        if (mDispatcher.CheckAccess())
        {
            result = work();
        }
        else
        {
            var operation = mDispatcher.InvokeAsync(work, DispatcherPriority.Normal, cancellationToken);
            bool completed = operation.Task.Wait(mTimeout, cancellationToken);
            if (completed)
            {
                result = operation.Task.GetAwaiter().GetResult();
            }
            else
            {
                // Deliberately do NOT Abort(): the mutation may already be mid-flight or applied.
                throw new SnoopMcpException(
                    ErrorCode.ActionPending,
                    $"Mutating action did not confirm within {mTimeout.TotalMilliseconds:F0}ms; it may have applied. Verify with waitForValue or captureWindow.");
            }
        }
        return result;
    }

    /// <summary>
    /// Posts a fire-and-forget action to the dispatcher and returns immediately. Used for actions that
    /// spin a nested message loop (e.g. opening a modal dialog), which would otherwise stall the serial
    /// pipe. The caller receives no completion; observe the effect separately.
    /// </summary>
    public void Post(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (mDispatcher.HasShutdownStarted || mDispatcher.HasShutdownFinished)
        {
            throw new InvalidOperationException("Dispatcher has been shut down.");
        }
        mDispatcher.InvokeAsync(work, DispatcherPriority.Normal);
    }
```

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit** — "feat: add mutation-safe dispatcher invoke and fire-and-forget post".

---

## Task 4: AutomationPeerDriver + PeerInvokeToolHandler

**Files:**
- Create: `src/SnoopMCP.Payload/Interaction/AutomationPeerDriver.cs`, `src/SnoopMCP.Payload/Tools/PeerInvokeToolHandler.cs`
- Test: `tests/SnoopMCP.Payload.Tests/AutomationPeerDriverTests.cs`

**Interfaces:**
- Consumes: `ElementRegistry`, `DispatcherMarshal`, `ErrorCode`, `PeerInvokeRequest`/`Response`.
- Produces: `AutomationPeerDriver.Invoke(DependencyObject element, string pattern)` (runs on the dispatcher thread; caller marshals) and the handler under `ToolNames.PeerInvoke`.

- [ ] **Step 1: Write the failing test** (`[WpfFact]`: build a `Button`, register it, invoke via the peer, assert a click handler fired)

```csharp
// AutomationPeerDriverTests.cs
[WpfFact]
public void Invoke_Button_FiresClick()
{
    bool clicked = false;
    var button = new System.Windows.Controls.Button();
    button.Click += (_, _) => clicked = true;
    // A peer needs the element in a rendered tree for some patterns; for InvokePattern a loose Button works.
    var driver = new AutomationPeerDriver();

    driver.Invoke(button, "Invoke");

    Assert.True(clicked);
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (type missing).

- [ ] **Step 3: Implement**

```csharp
// AutomationPeerDriver.cs
namespace SnoopMCP.Payload.Interaction;

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Protocol.Errors;

/// <summary>
/// Drives a WPF element's real <see cref="AutomationPeer"/> in-process — the fallback for actions
/// out-of-process UIA2 cannot perform (it has no DoDefaultAction). All calls must run on the UI thread.
/// </summary>
public sealed class AutomationPeerDriver
{
    /// <summary>Invokes the named pattern on the element's automation peer. Runs on the UI thread.</summary>
    public void Invoke(DependencyObject element, string pattern)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        AutomationPeer? peer = CreatePeer(element);
        if (peer is null)
        {
            throw new SnoopMcpException(ErrorCode.NotDrivable, "Element has no AutomationPeer.");
        }

        switch (pattern)
        {
            case "Invoke":
                Get<IInvokeProvider>(peer, PatternInterface.Invoke).Invoke();
                break;
            case "Toggle":
                Get<IToggleProvider>(peer, PatternInterface.Toggle).Toggle();
                break;
            case "SelectionItem":
                Get<ISelectionItemProvider>(peer, PatternInterface.SelectionItem).Select();
                break;
            case "ExpandCollapse":
                Get<IExpandCollapseProvider>(peer, PatternInterface.ExpandCollapse).Expand();
                break;
            default:
                throw new SnoopMcpException(ErrorCode.InvalidArgument, $"Unknown peer pattern '{pattern}'.");
        }
    }

    private static AutomationPeer? CreatePeer(DependencyObject element)
    {
        return element switch
        {
            UIElement ui => UIElementAutomationPeer.CreatePeerForElement(ui),
            ContentElement ce => ContentElementAutomationPeer.CreatePeerForElement(ce),
            _ => null
        };
    }

    private static T Get<T>(AutomationPeer peer, PatternInterface pattern) where T : class
    {
        return peer.GetPattern(pattern) as T
            ?? throw new SnoopMcpException(ErrorCode.NotDrivable, $"Element does not support the {pattern} pattern.");
    }
}
```

```csharp
// PeerInvokeToolHandler.cs — follow the 4-step handler template (see GetDependencyPropertyToolHandler):
//   deserialize PeerInvokeRequest -> TryResolve id -> mMarshal.InvokeMutating(() => { driver.Invoke(el, req.Pattern); return new PeerInvokeResponse(true); }) -> serialize.
// ToolName => ToolNames.PeerInvoke. Uses InvokeMutating (mutating) so a timeout yields ActionPending.
```

Full handler:

```csharp
// PeerInvokeToolHandler.cs
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using Interaction;
using Protocol;
using Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using Protocol.Wire;

/// <summary>Wire handler for <c>peerInvoke</c>: drives the element's AutomationPeer pattern in-process.</summary>
public sealed class PeerInvokeToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly AutomationPeerDriver mDriver;
    private readonly DispatcherMarshal mMarshal;

    public PeerInvokeToolHandler(ElementRegistry registry, AutomationPeerDriver driver, DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mDriver = driver;
        mMarshal = marshal;
    }

    public string ToolName => ToolNames.PeerInvoke;

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        PeerInvokeRequest request = arguments.Deserialize<PeerInvokeRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");
        if (!mRegistry.TryResolve(request.Id, out DependencyObject? element) || element is null)
        {
            throw new SnoopMcpException(ErrorCode.ElementExpired, $"Element id {request.Id} is not alive.");
        }

        PeerInvokeResponse response = mMarshal.InvokeMutating(() =>
        {
            mDriver.Invoke(element, request.Pattern);
            return new PeerInvokeResponse(true);
        }, cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit** — "feat: add AutomationPeer driver and peerInvoke handler".

---

## Task 5: CommandInvoker + ExecuteCommandToolHandler

**Files:**
- Create: `src/SnoopMCP.Payload/Interaction/CommandInvoker.cs`, `src/SnoopMCP.Payload/Tools/ExecuteCommandToolHandler.cs`
- Test: `tests/SnoopMCP.Payload.Tests/CommandInvokerTests.cs`

**Interfaces:**
- Produces: `CommandInvoker.Execute(DependencyObject element, string? path, string? parameter) -> ExecuteCommandResponse` (UI thread). Resolves the `ICommand` from an `ICommandSource` (element's own `Command`) or from a dotted DataContext path; gates on `CanExecute`.

- [ ] **Step 1: Write the failing test**

```csharp
// CommandInvokerTests.cs
[WpfFact]
public void Execute_ButtonWithCommand_RunsWhenCanExecute()
{
    bool ran = false;
    var cmd = new RelayTestCommand(_ => ran = true, _ => true);
    var button = new System.Windows.Controls.Button { Command = cmd };
    var invoker = new CommandInvoker();

    ExecuteCommandResponse r = invoker.Execute(button, path: null, parameter: null);

    Assert.True(r.CanExecute);
    Assert.True(r.Executed);
    Assert.True(ran);
}

[WpfFact]
public void Execute_CanExecuteFalse_DoesNotRun_AndReportsBlocked()
{
    var cmd = new RelayTestCommand(_ => throw new Xunit.Sdk.XunitException("should not run"), _ => false);
    var button = new System.Windows.Controls.Button { Command = cmd };
    var invoker = new CommandInvoker();

    var ex = Assert.Throws<SnoopMCP.Protocol.Errors.SnoopMcpException>(() => invoker.Execute(button, null, null));
    Assert.Equal(SnoopMCP.Protocol.Errors.ErrorCode.CommandNotExecutable, ex.Code);
}
```

> Provide a tiny `RelayTestCommand : ICommand` in the test file.

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
// CommandInvoker.cs
namespace SnoopMCP.Payload.Interaction;

using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Protocol.Errors;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Resolves and executes the <see cref="ICommand"/> bound to an element — from the element's own
/// <c>Command</c> (any <see cref="ICommandSource"/>) or from a dotted DataContext path — gating on
/// <see cref="ICommand.CanExecute"/>. Runs on the UI thread.
/// </summary>
public sealed class CommandInvoker
{
    /// <summary>Executes the resolved command, or throws with a structured code.</summary>
    public ExecuteCommandResponse Execute(DependencyObject element, string? path, string? parameter)
    {
        ArgumentNullException.ThrowIfNull(element);
        (ICommand command, object? boundParameter) = Resolve(element, path);
        object? commandParameter = parameter ?? boundParameter;

        bool canExecute = command.CanExecute(commandParameter);
        if (!canExecute)
        {
            throw new SnoopMcpException(ErrorCode.CommandNotExecutable, "The command's CanExecute returned false.");
        }
        command.Execute(commandParameter);
        return new ExecuteCommandResponse(Executed: true, CanExecute: true);
    }

    private static (ICommand Command, object? Parameter) Resolve(DependencyObject element, string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            if (element is ICommandSource source && source.Command is { } cmd)
            {
                return (cmd, source.CommandParameter);
            }
            throw new SnoopMcpException(ErrorCode.NotDrivable, "Element exposes no Command; supply a DataContext path.");
        }

        object? context = (element as FrameworkElement)?.DataContext
            ?? throw new SnoopMcpException(ErrorCode.NotDrivable, "Element has no DataContext to resolve the command path.");
        object? resolved = WalkPath(context, path);
        return resolved is ICommand pathCommand
            ? (pathCommand, null)
            : throw new SnoopMcpException(ErrorCode.NotDrivable, $"DataContext path '{path}' is not an ICommand.");
    }

    private static object? WalkPath(object root, string path)
    {
        object? current = root;
        foreach (string segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is null)
            {
                break;
            }
            PropertyInfo? property = current.GetType().GetProperty(
                segment, BindingFlags.Public | BindingFlags.Instance);
            current = property?.GetValue(current)
                ?? throw new SnoopMcpException(
                    ErrorCode.BindingPathError,
                    $"Path segment '{segment}' not found on {current.GetType().Name}.");
        }
        return current;
    }
}
```

```csharp
// ExecuteCommandToolHandler.cs — handler template: deserialize ExecuteCommandRequest, TryResolve id,
// mMarshal.InvokeMutating(() => mInvoker.Execute(el, req.Path, req.Parameter)), serialize the response.
// ToolName => ToolNames.ExecuteCommand.
```

(Write the handler fully, mirroring `PeerInvokeToolHandler`, substituting `CommandInvoker` and `ExecuteCommandRequest/Response`.)

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit** — "feat: add ICommand invoker and executeCommand handler".

---

## Task 6: PeerInfoReader + GetAutomationPeerInfoToolHandler (id ↔ UIA bridge)

**Files:**
- Create: `src/SnoopMCP.Payload/Interaction/PeerInfoReader.cs`, `src/SnoopMCP.Payload/Tools/GetAutomationPeerInfoToolHandler.cs`
- Test: `tests/SnoopMCP.Payload.Tests/PeerInfoReaderTests.cs`

**Interfaces:** Produces `PeerInfoReader.Read(DependencyObject) -> GetAutomationPeerInfoResponse` (UI thread) using the element's `AutomationPeer` (`GetAutomationId`, `GetName`, `GetClassName`, `GetAutomationControlType`). Read-only tool (ungated).

- [ ] **Step 1: Write the failing test**

```csharp
[WpfFact]
public void Read_ButtonWithAutomationId_ReturnsIt()
{
    var button = new System.Windows.Controls.Button();
    System.Windows.Automation.AutomationProperties.SetAutomationId(button, "SaveBtn");
    var reader = new PeerInfoReader();

    GetAutomationPeerInfoResponse info = reader.Read(button);

    Assert.Equal("SaveBtn", info.AutomationId);
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
// PeerInfoReader.cs
namespace SnoopMCP.Payload.Interaction;

using System.Windows;
using System.Windows.Automation.Peers;
using Protocol.Errors;
using SnoopMCP.Protocol.Tools;

/// <summary>Reads an element's automation identity via its <see cref="AutomationPeer"/>, so callers can
/// cross between Snoop element ids and the out-of-process UIA locator space. Runs on the UI thread.</summary>
public sealed class PeerInfoReader
{
    /// <summary>Projects the element's automation peer identity.</summary>
    public GetAutomationPeerInfoResponse Read(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        AutomationPeer peer = element switch
        {
            UIElement ui => UIElementAutomationPeer.CreatePeerForElement(ui),
            ContentElement ce => ContentElementAutomationPeer.CreatePeerForElement(ce),
            _ => throw new SnoopMcpException(ErrorCode.NotDrivable, "Element has no AutomationPeer.")
        } ?? throw new SnoopMcpException(ErrorCode.NotDrivable, "Element has no AutomationPeer.");

        return new GetAutomationPeerInfoResponse(
            AutomationId: peer.GetAutomationId() ?? string.Empty,
            Name: peer.GetName() ?? string.Empty,
            ClassName: peer.GetClassName() ?? string.Empty,
            ControlType: peer.GetAutomationControlType().ToString());
    }
}
```

(Write `GetAutomationPeerInfoToolHandler` via the template: `mMarshal.Invoke(...)` — read-only, so the ordinary `Invoke` is correct. `ToolName => ToolNames.GetAutomationPeerInfo`.)

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit** — "feat: add automation-peer info bridge".

---

## Task 7: ValueWaiter + WaitForValueToolHandler (ground truth)

**Files:**
- Create: `src/SnoopMCP.Payload/Interaction/ValueWaiter.cs`, `src/SnoopMCP.Payload/Tools/WaitForValueToolHandler.cs`
- Test: `tests/SnoopMCP.Payload.Tests/ValueWaiterTests.cs`

**Interfaces:** Produces `ValueWaiter.Wait(DependencyObject element, string? dependencyProperty, string? dataContextPath, string expected, int timeoutMs, DispatcherMarshal marshal, CancellationToken ct) -> WaitForValueResponse`. Polls on the UI thread via `marshal.Invoke`, comparing invariant-culture string equality.

> Design: polling loop lives OFF the UI thread (in the handler's async body), each read marshalled with `marshal.Invoke`. Exactly one of `dependencyProperty`/`dataContextPath` must be set. Reuse the DP-read approach from `DependencyPropertyInspector` and the path walk from `CommandInvoker.WalkPath`.

- [ ] **Step 1: Write the failing test**

```csharp
[WpfFact]
public void Wait_DataContextPath_MatchesAfterChange()
{
    var vm = new TestVm { Status = "pending" };
    var element = new System.Windows.Controls.TextBlock { DataContext = vm };
    var marshal = new DispatcherMarshal(System.Windows.Threading.Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));
    var waiter = new ValueWaiter();

    // flip the value shortly after starting the wait
    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(() => vm.Status = "done");

    WaitForValueResponse r = waiter.Wait(element, null, "Status", "done", 2000, marshal, default);

    Assert.True(r.Matched);
    Assert.Equal("done", r.ActualValue);
}
```

> `TestVm` is a small `INotifyPropertyChanged` with a `Status` string. Provide it in the test file.

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL.

- [ ] **Step 3: Implement** (poll loop off-thread; read on-thread)

```csharp
// ValueWaiter.cs
namespace SnoopMCP.Payload.Interaction;

using System.Globalization;
using System.Reflection;
using System.Windows;
using Protocol.Errors;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Polls a dependency property or a dotted DataContext path until it matches an expected value or the
/// timeout elapses. This is ground-truth verification — it reads view-model / DP reality, not pixels.
/// </summary>
public sealed class ValueWaiter
{
    private const int PollIntervalMs = 100;

    /// <summary>Waits for the target value. Reads are marshalled onto the UI thread each poll.</summary>
    public WaitForValueResponse Wait(
        DependencyObject element,
        string? dependencyProperty,
        string? dataContextPath,
        string expected,
        int timeoutMs,
        DispatcherMarshal marshal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(marshal);
        ArgumentNullException.ThrowIfNull(expected);
        bool oneSource = string.IsNullOrEmpty(dependencyProperty) ^ string.IsNullOrEmpty(dataContextPath);
        if (!oneSource)
        {
            throw new SnoopMcpException(ErrorCode.InvalidArgument, "Set exactly one of dependencyProperty or dataContextPath.");
        }

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        string? actual = null;
        bool matched = false;
        while (!matched && DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            actual = marshal.Invoke(() => ReadValue(element, dependencyProperty, dataContextPath), cancellationToken);
            matched = string.Equals(actual, expected, StringComparison.Ordinal);
            if (!matched)
            {
                Thread.Sleep(PollIntervalMs);
            }
        }
        return new WaitForValueResponse(matched, actual);
    }

    private static string? ReadValue(DependencyObject element, string? dp, string? path)
    {
        object? value;
        if (!string.IsNullOrEmpty(dp))
        {
            DependencyProperty? property = FindDependencyProperty(element, dp)
                ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, $"DP '{dp}' not found on {element.GetType().Name}.");
            value = element.GetValue(property);
        }
        else
        {
            object? context = (element as FrameworkElement)?.DataContext;
            value = context is null ? null : WalkPath(context, path!);
        }
        return value?.ToString();
    }

    private static DependencyProperty? FindDependencyProperty(DependencyObject element, string name)
    {
        // DependencyPropertyDescriptor / reflection over the registered DPs; mirror DependencyPropertyInspector.ResolveDp.
        System.ComponentModel.PropertyDescriptor? descriptor =
            System.ComponentModel.TypeDescriptor.GetProperties(element)[name];
        System.Windows.DependencyPropertyDescriptor? dpd = descriptor is null
            ? null
            : System.Windows.DependencyPropertyDescriptor.FromProperty(descriptor);
        return dpd?.DependencyProperty;
    }

    private static object? WalkPath(object root, string path)
    {
        object? current = root;
        foreach (string segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current is null)
            {
                break;
            }
            PropertyInfo? property = current.GetType().GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
            current = property?.GetValue(current);
        }
        return current;
    }
}
```

> **DRY note:** `WalkPath` also appears in `CommandInvoker`. Extract it into a shared `Interaction/DataContextPath.cs` static helper and call it from both; do this in whichever of Task 5 / Task 7 lands second (add a small refactor step + keep both tests green).

(Write `WaitForValueToolHandler` via the template; it deserializes `WaitForValueRequest`, resolves the id, and calls `mWaiter.Wait(...)` passing the handler's `DispatcherMarshal`. Read-only tool — ungated. `ToolName => ToolNames.WaitForValue`. Note the handler itself is `async`/polling; keep the poll off the pipe thread as written.)

- [ ] **Step 4: Run to verify it passes** — Expected: PASS.

- [ ] **Step 5: Commit** — "feat: add ground-truth value waiter and waitForValue handler".

---

## Task 8: Register the four handlers in the payload

**Files:**
- Modify: `src/SnoopMCP.Payload/PayloadEntryPoint.cs`
- Test: covered by an integration round-trip (Task 10) and the existing `PipeServerEchoTests` pattern.

**Interfaces:** none new; wires the handlers into the `ToolRegistry`.

- [ ] **Step 1: Add construction + registration** in `PayloadEntryPoint.Run` (alongside the existing inspector construction and `toolRegistry.Register(...)` calls):

```csharp
            // Interaction (Phase B) — driving fallback + ground truth.
            var peerDriver = new Interaction.AutomationPeerDriver();
            var commandInvoker = new Interaction.CommandInvoker();
            var peerInfoReader = new Interaction.PeerInfoReader();
            var valueWaiter = new Interaction.ValueWaiter();

            toolRegistry.Register(new GetAutomationPeerInfoToolHandler(registry, peerInfoReader, marshal));
            toolRegistry.Register(new PeerInvokeToolHandler(registry, peerDriver, marshal));
            toolRegistry.Register(new ExecuteCommandToolHandler(registry, commandInvoker, marshal));
            toolRegistry.Register(new WaitForValueToolHandler(registry, valueWaiter, marshal));
```

Add `using Interaction;` if the handlers reference it, or fully-qualify as above. (Namespace imports at file top: the handlers live in `SnoopMCP.Payload.Tools`, already imported.)

- [ ] **Step 2: Build** — `dotnet build src/SnoopMCP.Payload -p:TreatWarningsAsErrors=true`. Expected: clean.

- [ ] **Step 3: Commit** — "feat: register Phase B interaction handlers in the payload".

---

## Task 9: Host relay tools in McpTools (gate the mutating two)

**Files:**
- Modify: `src/SnoopMCP.Server/Tools/McpTools.cs`
- Test: `tests/SnoopMCP.Host.Tests/McpToolsGateTests.cs`

**Interfaces:** `McpTools` ctor gains `InteractionGate gate`; adds four `[McpServerTool]` methods. `peerInvoke`/`executeCommand` call `RequireGate()` before `Dispatch`.

- [ ] **Step 1: Write the failing test** (gate off → `peerInvoke` throws before dispatch)

```csharp
// McpToolsGateTests.cs
[Fact]
public async Task PeerInvoke_WhenGateOff_ThrowsInteractionDisabled_BeforeDispatch()
{
    string gatePath = Path.Combine(Path.GetTempPath(), "gate-" + Guid.NewGuid().ToString("N") + ".json");
    var session = new SessionManager(NullLogger<SessionManager>.Instance, NullLoggerFactory.Instance); // not attached
    var injector = new InjectorService(NullLogger<InjectorService>.Instance);
    var tools = new McpTools(session, injector, new InteractionGate(gatePath));

    McpException ex = await Assert.ThrowsAsync<McpException>(() => tools.PeerInvoke(1, "Invoke", default));
    Assert.Contains("InteractionDisabled", ex.Message);
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (ctor arity / method missing).

- [ ] **Step 3: Implement** — add the gate field + ctor param + `RequireGate`, and the four relays:

```csharp
// McpTools.cs — ctor: add InteractionGate gate; store mGate. Add using Automation;

    private void RequireGate()
    {
        if (!mGate.IsEnabled)
        {
            throw Promote(new SnoopMcpException(
                ErrorCode.InteractionDisabled,
                "Driving is disabled. Ask the user to enable interaction in the SnoopMCP tray menu."));
        }
    }

    [McpServerTool, Description("Bridge a Snoop element id to its UIA identity (AutomationId/Name/ClassName/ControlType). Read-only.")]
    public Task<JsonElement> GetAutomationPeerInfo(int id, CancellationToken cancellationToken) =>
        Dispatch(ToolNames.GetAutomationPeerInfo, new GetAutomationPeerInfoRequest(id), cancellationToken);

    [McpServerTool, Description("Poll a DP or DataContext path until it equals an expected value (ground-truth check). Read-only.")]
    public Task<JsonElement> WaitForValue(int id, string? dependencyProperty, string? dataContextPath, string expected, int timeoutMs, CancellationToken cancellationToken) =>
        Dispatch(ToolNames.WaitForValue, new WaitForValueRequest(id, dependencyProperty, dataContextPath, expected, timeoutMs), cancellationToken);

    [McpServerTool, Description("MUTATES the target: drive an element's AutomationPeer pattern in-process. Requires the interaction gate.")]
    public Task<JsonElement> PeerInvoke(int id, string pattern, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        RequireGate();
        return Dispatch(ToolNames.PeerInvoke, new PeerInvokeRequest(id, pattern), cancellationToken);
    }

    [McpServerTool, Description("MUTATES the target: execute the ICommand bound to an element (CanExecute-gated). Requires the interaction gate.")]
    public Task<JsonElement> ExecuteCommand(int id, string? path, string? parameter, CancellationToken cancellationToken)
    {
        RequireGate();
        return Dispatch(ToolNames.ExecuteCommand, new ExecuteCommandRequest(id, path, parameter), cancellationToken);
    }
```

> `Promote` and `Dispatch` already exist in `McpTools`. `RequireGate` throws an already-promoted `McpException` — adjust `Promote` visibility is fine (already `private static`). Because `RequireGate` needs to throw `McpException` (not `SnoopMcpException`) to reach the client verbatim, call `throw Promote(new SnoopMcpException(...))` as shown.

- [ ] **Step 4: Run to verify it passes** — Expected: PASS. Update `ServerHost.Build` DI so `McpTools` gets the `InteractionGate` (already registered as a singleton in Phase A Task 9 — the MCP SDK resolves `McpTools` from the container, so no explicit change is needed beyond the ctor signature).

- [ ] **Step 5: Commit** — "feat: add gated Phase B relay tools to McpTools".

---

## Task 10: End-to-end round-trip test

**Files:**
- Create/extend: `tests/SnoopMCP.IntegrationTests/DrivingE2ETests.cs`

**Interfaces:** none; validates the full path against `SampleWpfApp`.

- [ ] **Step 1: Write the test** — attach to `SampleWpfApp`, enable the gate (construct an `InteractionGate` at a temp path and point the server at it via `ServerHost.Build(..., gate)`), `peerInvoke` a button by id, `waitForValue` on the resulting VM/DP state, assert `Matched`. Also assert `peerInvoke` with the gate **off** yields `InteractionDisabled`.

- [ ] **Step 2: Run** — interactive session. Expected: PASS.

- [ ] **Step 3: Commit** — "test: end-to-end payload driving round-trip".

---

## Final integration step

- [ ] Build the whole solution `-p:TreatWarningsAsErrors=true`; run all non-interactive tests. Expected: clean, green.
- [ ] Update `skills/snoopmcp-uia/SKILL.md` and its embedded copy to describe the now-live payload-tier tools (`peerInvoke`, `executeCommand`, `getAutomationPeerInfo`, `waitForValue`) — and re-run the drift-guard test. One commit: "docs: teach payload-tier driving tools in the snoopmcp-uia skill".
- [ ] Update `README.md` tool table with the four Phase B tools and the two-tier driving model.

## Self-Review Notes (author)
- **Spec coverage:** §4.2 (AutomationPeerDriver, CommandInvoker, waitForValue ground truth, peer/id bridge, mutation-safe dispatcher) → Tasks 3–7; §5 Phase B tool rows → Tasks 2, 9; §9 Phase B codes → Task 1.
- **Mutation safety:** `InvokeMutating` never `Abort()`s and yields `ActionPending` on timeout (Task 3); `Post` provides fire-and-forget for dialog-openers (available for a future dialog-opening tool — no such tool ships in Phase B, so `ActionDispatched` is defined but only used when such a tool is added).
- **DRY:** `WalkPath` is shared between `CommandInvoker` and `ValueWaiter` via `Interaction/DataContextPath.cs` (noted in Task 7).
- **Gate:** enforced host-side in `McpTools` (Task 9) — the payload handlers themselves are not gate-aware (the host is the trust boundary), consistent with Phase A.
