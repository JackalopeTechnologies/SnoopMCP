# SnoopMCP Interaction & Driving Layer — Design Spec

- **Date:** 2026-07-19
- **Status:** Approved design (brainstorm complete); pending spec review, then implementation plans
- **Roadmap anchor:** GitHub issue #47 (North Star — agent-driven WPF test bed), "Interaction layer" bullet (marked *Design spec needed*) and the observation/capture bullet. This spec fulfills that design-spec requirement.
- **Related specs:** `2026-05-27-snoopmcp-investigation-design.md` (v1 architecture, Phase-2 candidates), `2026-05-29-snoopmcp-installer-design.md` (per-user MSI), `2026-05-29-snoopmcp-tray-app-design.md`, `2026-05-30-snoopmcp-binding-diagnostics-design.md` (Phase 1).

---

## 1. Context & motivation

SnoopMCP v1.3.0 is a **read-only** WPF inspection MCP server: `SnoopMCP.Host.exe` (WPF tray app hosting Kestrel at `http://127.0.0.1:6300/mcp`) injects `SnoopMCP.Payload.dll` into a target WPF process and exposes 20 read-only tools over a named pipe, marshalled onto the target's Dispatcher.

This spec adds the ability for an AI agent to **drive** a live WPF app — click, select, set values, navigate, wait — and to **see** it via background window capture, so an agent can author and run end-to-end UX flows against a real app hands-off. The motivating scenario is a hands-free multi-step flow — load a document, drive a settings panel, produce output, verify the result — carried out entirely through UI Automation while the user keeps working in another window, without ever touching the physical mouse or stealing focus.

The core principle carried from that work: **never synthesize input and never steal focus.** Everything is driven through the accessibility/automation layer or through the injected payload; the window can be occluded (buried behind other windows) and both driving and capture still work.

### Two surfaces (why the design has two driving mechanisms)

| Need | Mechanism | Why |
|---|---|---|
| **Drive controls** hands-off | **UI Automation (UIA2)** via `System.Windows.Automation`, out-of-process in the host | Fires actions through the accessibility layer — no cursor movement, no foreground activation. Native client for WPF's `AutomationPeer`s. |
| **See the app** (incl. GPU charts) | **`PrintWindow` + `PW_RENDERFULLCONTENT`**, in the host | Renders window content to an off-screen bitmap even when background/occluded; captures GPU-composited content (e.g. a hardware-accelerated chart surface). |
| **Drive what UIA can't; verify ground truth** | **Injected payload** (`AutomationPeer` patterns, real `ICommand`, DP/VM polling) | UIA2 lacks `DoDefaultAction`; some WPF commands are bound to real clicks with no UIA action pattern. The payload runs *inside* the process and can invoke the real peer/command and read view-model state as ground truth. |

## 2. Goals / Non-goals

### Goals
- Host-side UIA driving of a running WPF app (discover, act, wait) keyed by process id — **no `attach()` required** for the UIA tier.
- Background window capture (`PrintWindow`) as an MCP image the agent can see.
- Injected-payload fallback driver (`AutomationPeer` patterns, `ICommand` execution) and ground-truth waits on DP / DataContext state, for controls UIA can't drive.
- A single **host-side safety switch** gating all mutating tools; read-only tools stay ungated.
- Install/startup that runs the host **elevated** (equal-or-higher integrity than the target) with at most one UAC acknowledgment, because injection, UIA actions, and `PrintWindow` capture across an integrity boundary all require it.
- A new agent **skill** (`snoopmcp-uia`) teaching the driving tool family, installed alongside `snoopmcp-first`.

### Non-goals (explicitly out of scope)
- **Synthesized mouse/keyboard input** (`SendInput`, `mouse_event`, `keybd_event`, `SendKeys`) — never, not even as a last-resort fallback. If neither UIA nor the payload can drive a control, the tool returns `NotDrivable` and the agent asks the human.
- **`SetForegroundWindow` / `ShowWindow`** for capture — capture is `PrintWindow`-only.
- **Non-WPF targets.** UIA could drive any Win32/WinForms/WinUI app; this layer is deliberately WPF-gated for product coherence (broadening later is a filter change, since UIA itself is app-agnostic).
- **Capture/replay test artifacts** (#47's later phase), arbitrary `invokeMethod` / scripting (separate design per the investigation spec), x86/ARM64 *attach* (x86 WPF apps are drivable via UIA but not attachable), multi-target sessions.
- **Any application-specific content** — page objects, locator maps, and test-suite conventions belong in downstream consumer repositories, not here. SnoopMCP stays a generic, public, MIT tool.
- **`uiAccess=true` signed-binary path** (see §7) — rejected for this model.

## 3. Decisions locked (brainstorm)

1. **Primary consumer:** AI agents via MCP. A test-suite client library is YAGNI for now; a suite can consume the same MCP server later.
2. **App scope:** WPF-only (reuse the existing WPF process gate). UIA is app-agnostic, so broadening is a later filter change.
3. **Safety gate:** a **host-side switch**, default off, one switch protecting all registered clients uniformly.
4. **Phasing:** host-first, two phases. **Phase A** = host UIA + capture + waits (no injected-payload or pipe-wire changes; the only shared-contract edit is append-only `ErrorCode` additions in `SnoopMCP.Protocol`). **Phase B** = payload fallback + ground-truth waits.
5. **UIA client tech:** managed **UIA2** (`System.Windows.Automation`) — framework-only, zero new dependencies, native for WPF peers. Its `DoDefaultAction` gap is exactly what Phase B's in-process fallback covers with the *real* command.
6. **Elevation model:** autostart scheduled task at **`/RL HIGHEST`** (silent elevated launch at logon; one UAC when autostart is enabled); **manual start routes through the task** (`schtasks /Run`) so a manually launched host elevates the same way, with a Medium-IL fallback when no task is registered. Admin-only (inherent Windows limit).
7. **Skills:** add a second skill `snoopmcp-uia` via a small multi-skill registry inside `SnoopSkill`; **Claude Code only** (skills are a Claude-Code mechanism).

## 4. Architecture

### 4.1 Phase A — host-only (no injected-payload or pipe-wire changes; only append-only `ErrorCode` additions in Protocol)

New subsystem `src/SnoopMCP.Server/Automation/` (sibling to `Injection/` and `Logging/` — the repo's established host-subsystem pattern). Interfaces registered as DI singletons in `ServerHost.Build` next to `SessionManager` / `IInjectorService` (`ServerHost.cs:50-51`):

- **`IUiaDriver` / `UiaDriver`** — wraps `System.Windows.Automation`. Responsibilities: enumerate/find UIA elements under a target `pid`, read element facts (`AutomationId`, `Name`, `ControlType`, `HelpText`, `BoundingRectangle`, supported patterns), and invoke action patterns (`Invoke`, `SelectionItem`, `Toggle`, `ExpandCollapse`, `Value`). All cross-process UIA calls run on worker threads behind a **bounded-timeout wrapper** (`Task` + `CancellationToken`, default 5 s) so an unresponsive target can never wedge a Kestrel request thread; finds use UIA's caching request API to batch cross-process property reads.
- **`IScreenCapture` / `PrintWindowCapture`** — `GetWindowRect` + `PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT=2)` P/Invoke (follows the existing `IsWow64Process` DllImport precedent in `ProcessEnumerator.cs`). Occluded target → full render (chrome + GPU chart). Minimized target → `CaptureUnavailable` error (no blank frame). Never computes screen coordinates or moves/raises the window.
- **`InteractionGate`** — the host-side switch. **Default off.** State persisted in a host user-settings store (small JSON under `%LocalAppData%\SnoopMCP`, or the host's existing settings mechanism if present). Tray menu toggle applies it live; a CLI flag / config value sets the startup default. `/health` gains an `interactionEnabled` field (`HealthStatus.Create`, `ServerHost.cs:80-81`); the tray icon/tooltip reflects it. Read-only tools ignore the gate; every mutating tool checks it and returns `InteractionDisabled` (message: how to enable it in the tray) when off.
- **`UiaTools`** — a second `[McpServerToolType]` class in the `SnoopMCP.Server` assembly. `WithToolsFromAssembly` (`ServerHost.cs:60`) discovers it with zero wiring. Tools are **session-free**, keyed by `pid` (the `ListWpfProcesses` precedent, `McpTools.cs:57-64`), so UIA driving works **without `attach()`**. A target must probe as WPF (`PresentationFramework.dll` loaded) but **not** x64 — bitness was purely an *injection* constraint, so x86 WPF apps become UIA-drivable (not attachable). This is a small, deliberate divergence from `attach()`'s x64 gate.

### 4.2 Phase B — payload fallback + ground truth

Rides the existing extension pattern (new `ToolNames` constant + request/response records in `SnoopMCP.Protocol`, new `IToolHandler` + action class in `SnoopMCP.Payload`, registration line in `PayloadEntryPoint.Run`, relay `[McpServerTool]` in `McpTools`). Requires `attach()` (goes over the pipe). A new `Interaction/` namespace in the payload parallels `Inspection/`:

- **`AutomationPeerDriver`** — `UIElementAutomationPeer.CreatePeerForElement` / `FrameworkElementAutomationPeer.FromElement`, then `peer.GetPattern(PatternInterface.X)` → `IInvokeProvider` / `IToggleProvider` / `IValueProvider` / `ISelectionItemProvider` / `IExpandCollapseProvider`. This drives the **real** peer in-process, covering UIA2's missing `DoDefaultAction` with the genuine WPF pattern rather than an a11y default-action guess.
- **`CommandInvoker`** — resolve the bound `ICommand` (from `ButtonBase.Command` DP or a DataContext path), gate on `CanExecute`, then `Execute`. This is the "trigger a command and observe the effect" tool from the investigation spec (line 310), and the action-side complement to the read-only command explanation planned in #56.
- **Ground-truth waits** — `waitForValue` polls a DP or DataContext path until it matches an expected value or times out. This is stronger verification than screenshot-diffing: it reads the view-model reality (an "the operation actually produced its result" style assertion), not pixels.
- **Peer/id bridge** — `getAutomationPeerInfo` maps a Snoop element id (rich: bindings, DPs, styles) to its UIA identity (`AutomationId`, `Name`, `ClassName`, `ControlType`). **v1 ships the forward direction only**; an agent correlates with the host UIA tier by `AutomationId`. (Runtime-id is not exposed — WPF's `AutomationPeer.GetRuntimeId()` is not publicly accessible — and a UIA→Snoop-id reverse-lookup tool is deferred to v-next.) This is the "Snoop knows the app deeply and can drive it" integration glue.

**Mutation-safe dispatcher semantics (critical).** `DispatcherMarshal`'s current timeout path `Abort()`s the operation and **ignores the return value** (`DispatcherMarshal.cs:94-97`) — read-safe only: on timeout, already-started work still completes after the caller saw `DispatcherTimeout`. That is a lie for a mutation. Mutating payload tools therefore use a distinct dispatch path:
- A mutating call that times out returns **`ActionPending`** (the action may have applied; the agent must verify via `waitForValue`/capture) — never a claim that nothing happened.
- Dialog-opening actions (which spin a nested message loop and would stall the strictly-serial pipe — `PipeServer.cs`, one request in flight per session) use a **fire-and-forget** dispatch (`Dispatcher.InvokeAsync` without `Wait`) that returns immediately with `ActionDispatched`, so subsequent pipe calls aren't blocked behind a modal.

### 4.3 Two driving tiers — agent workflow

1. `listWpfProcesses` → choose a target `pid`.
2. **UIA tier (no attach):** `getUiaTree` / `findUiaElement` to locate, `invokeUia` / `setUiaValue` to act, `captureWindow` to see, `waitForUia` to synchronize. Sufficient for the majority of flows.
3. **Payload tier (attach):** call `attach(pid)` only when UIA can't drive a control (`NotDrivable`) or when the agent needs binding/DP inspection or ground-truth `waitForValue`. Then `getAutomationPeerInfo` bridges the two locator spaces; `peerInvoke` / `executeCommand` drive; `waitForValue` verifies.

## 5. Tool surface

All tools camelCase, on the existing `snoopmcp` server (auto-approved by the whole-server `mcp__snoopmcp` grant — which is why the **host gate**, not client permissions, is the real control). "Gated" = blocked by `InteractionGate` when off.

### Phase A (host UIA)

| Tool | Kind | Signature (conceptual) | Notes |
|---|---|---|---|
| `getUiaTree` | observe | `(pid, fromElementRef?, depth?)` | Walk UIA tree; each node returns id/name/controlType/helpText/boundingRect + available patterns. The locator-discovery surface. |
| `findUiaElement` | observe | `(pid, by, value)` | `by` ∈ `automationId \| name \| helpText \| controlType`, in stability order. Returns an `elementRef` (may match multiple → returns the disambiguation set). |
| `captureWindow` | observe | `(pid)` → MCP **image** (PNG) | `PrintWindow`/`PW_RENDERFULLCONTENT`. Occluded OK; minimized → `CaptureUnavailable`. |
| `waitForUia` | observe | `(pid, by, value, timeoutMs)` | Poll until an element matches/appears. Replaces `Start-Sleep`. |
| `invokeUia` | **act (gated)** | `(elementRef, pattern?)` | Auto-selects action pattern (`Invoke`→`SelectionItem`→`Toggle`→`ExpandCollapse`) or explicit `pattern`. |
| `setUiaValue` | **act (gated)** | `(elementRef, value)` | `ValuePattern.SetValue`; `IsReadOnly` checked first → `ValueReadOnly`. |

### Phase B (payload; requires attach)

| Tool | Kind | Signature (conceptual) | Notes |
|---|---|---|---|
| `getAutomationPeerInfo` | observe | `(elementId)` | Snoop element id ↔ UIA identity bridge. |
| `peerInvoke` | **act (gated)** | `(elementId, pattern)` | Drives the real `AutomationPeer` pattern in-process (covers UIA2 `DoDefaultAction` gap). |
| `executeCommand` | **act (gated)** | `(elementId \| path, parameter?)` | Resolve `ICommand`, gate on `CanExecute` (→ `CommandNotExecutable`), execute. |
| `waitForValue` | observe | `(elementId \| path, expected, timeoutMs)` | Poll DP / DataContext path for ground-truth verification. |

Every mutating tool's MCP `Description` states plainly that it changes the target application.

### 5.1 Element referencing (`elementRef`)

UIA elements are not serializable across stateless tool calls. An `elementRef` is a JSON object:

```
{ "pid": <int>, "handle": "<pid>:<seq>", "by"?: "<locator kind>", "value"?: "<locator value>" }
```

- `handle` indexes a **short-TTL host-side element cache** (opaque, monotonic `seq`, never reused). Resolves instantly while warm.
- When the handle is stale (TTL expired / tree changed), the driver **silently re-resolves** via `by`+`value` (the durable locator it was found by). Only if *both* fail does the tool return `UiaElementStale`.
- This mirrors the payload's existing ephemeral-id + durable-path-string pattern (`ElementRegistry` + `PathStringEmitter`), so the semantics are already familiar. Locators are not guaranteed unique (the path-string grammar warns of this); a re-resolve that finds multiple candidates returns `UiaAmbiguousLocator` rather than guessing.

## 6. Safety model

- **The host `InteractionGate` is the control.** Because the MSI pre-approves the whole server (`mcp__snoopmcp`, `ClaudeCodeWriter.cs:29`) and most of the other 8 clients lack per-tool permissioning, the client layer cannot gate mutation — the host does. Default off; enable via tray toggle or CLI/config; state in `/health` and the tray icon.
- **Read-only stays ungated everywhere** (`getUiaTree`, `findUiaElement`, `captureWindow`, `waitForUia`, `getAutomationPeerInfo`, `waitForValue`).
- **Never synthesize input; never steal focus.** No `SendInput`/`mouse_event`/`keybd_event`, no `SetForegroundWindow`/`ShowWindow`. Un-drivable control → `NotDrivable`, agent asks the human.
- **Elevation is a safety boundary too:** driving mutates a live app; the admin-only elevated path (§7) means an unprivileged user simply cannot drive elevated targets — failing clearly rather than partially.

## 7. Elevation & install

### 7.1 Current state (verified)
- **Host manifest:** none. `SnoopMCP.Host.csproj` declares no `<ApplicationManifest>`; the built exe carries the .NET default `asInvoker` / `uiAccess=false` → **Medium IL**.
- **Autostart:** `schtasks /Create /SC ONLOGON /RL LIMITED` (`AutostartTask.cs:27,39`) → non-elevated at logon, but on the **interactive desktop** (session 1+ — good for UIA; the blocker is integrity, not session).
- **MSI:** `Package/@Scope="perUser"`, installs to `%LocalAppData%`, all custom actions `Impersonate="yes"` (`Package.wxs:9`) → **zero UAC today**, Medium-IL runtime.
- **No runtime elevation detection.** `ProcessProbe` only *infers* an elevation mismatch when `IsWow64Process` fails → `AccessDenied` (`ProcessProbe.cs:57-60`). "Same elevation" is documented (README:40-41), never automated.

### 7.2 Why it must change
Injection (`OpenProcess`/`CreateRemoteThread`), UIA **actions**, and `PrintWindow` capture are all blocked by UIPI when the host is at **lower** integrity than the target. Elevated targets (any app that runs as administrator) are unreachable from a Medium host. Running the host at **High IL** removes the integrity boundary for equal-or-lower targets and fixes injection, driving, and capture in one move.

### 7.3 Change set (Option A + manual-start routing)
1. **Autostart run level `/RL LIMITED` → `/RL HIGHEST`** (`AutostartTask.cs:27,39`; class doc `:13-14`; test `AutostartTaskTests.cs:19`). With the default `InteractiveToken` principal, a Highest logon task launches the host with the user's **full elevated token at every logon with no per-logon UAC**. Keep `InteractiveToken` + `ONLOGON` (do **not** switch to `SYSTEM`/S4U — that is session 0 and breaks UIA).
2. **One-time elevation to register the task.** Registering a Highest task requires an elevated caller, so `install-autostart` self-elevates once (`ProcessStartInfo{ UseShellExecute=true, Verb="runas" }`) → **exactly one UAC** at install/enable time. All other install steps stay non-elevated (per-user `%LocalAppData%` install unchanged).
3. **Manual start routes through the task.** Tray Start / CLI `start` run `schtasks /Run /TN "SnoopMCP Host"`, reusing the elevated Highest task so a manually launched host elevates identically with no extra UAC. **Fallback:** if no autostart task is registered, `start` launches normally (Medium IL) and the tray surfaces that driving elevated targets needs autostart enabled.
4. **No host-manifest change** — the Highest task supplies the elevated token; the host stays `asInvoker`. (Adding `requireAdministrator`/`highestAvailable` would force a UAC on every manual double-click; the task-routing above avoids that.)
5. **Admin-only reality:** `/RL HIGHEST` elevates only for Administrators-group members; a standard user still gets a limited token. The tray/installer **detects a non-admin user and states plainly** that elevated-target driving is unavailable, rather than failing cryptically.
6. **Map the missing error:** `ProcessProbe.cs:53` — `process.Handle` can throw an unmapped `Win32Exception` before `IsWow64Process`; map it to the friendly `AccessDenied` (elevation-mismatch message). Fixed as part of this work since it *is* the elevation error path.

### 7.4 Rejected alternatives
- **`uiAccess=true` signed exe in Program Files:** bypasses UIPI for input only — grants no `OpenProcess`/`CreateRemoteThread` into a High target, so it doesn't help injection; also incompatible with the per-user `%LocalAppData%` install and needs code-signing. Wrong tool.
- **Per-machine install:** bigger surface, still needs the Highest task anyway to make the *runtime* host elevated; explicitly rejected for v1.1 in the installer spec.

## 8. Skills

- **Current:** one skill, `snoopmcp-first`, stored as an embedded C# raw-string `SnoopSkill.SkillBody` (`SnoopSkill.cs:20-48`) — that constant, not the repo `skills/` file, is written to `~/.claude/skills/snoopmcp-first/SKILL.md` during `register-clients`, **Claude Code only** (`ClaudeCodeWriter.cs:91`). The repo copy `skills/snoopmcp-first/SKILL.md` is kept in sync by convention, with **no test guarding drift**.
- **New skill `snoopmcp-uia`:**
  1. Create `skills/snoopmcp-uia/SKILL.md` (human-canonical) — triggers on inspecting/driving a live WPF app via UIA; teaches the two-tier workflow (UIA first, attach+payload fallback), the locator-stability order (`AutomationId` > `HelpText` > `Name`, and that VM-type-name `Name`s are a smell), the no-synthesized-input rule, the host gate, and the elevation/admin-only note. Trigger phrasing must not collide with `snoopmcp-first`.
  2. Refactor `SnoopSkill` into a small **multi-skill registry**: `SkillDefinition(string Name, string Body)`, an `IReadOnlyList<SkillDefinition>` (both bodies as raw-string constants), and `Install`/`Remove` looping over all definitions (`SnoopSkill.cs:52-72`, `76-97`). `ClaudeCodeWriter` keeps calling one `Install`/`Remove` — no wiring change.
  3. Update the singular status strings (`ClaudeCodeWriter.cs:31-34`) to reflect multiple skills.
  4. **Close the drift gap:** add a test asserting each embedded body equals its repo `skills/<name>/SKILL.md`.
- **No installer/CLI wiring changes** — skills ride `ClaudeCodeWriter.Register`, reached by both `register-clients` and the tray. **Claude Code only** (unchanged).

## 9. Error codes

Append after `PathParseError = 11` in `src/SnoopMCP.Protocol/Errors/ErrorCode.cs` (numeric wire encoding → **append-only, never renumber**):

| Code | Meaning |
|---|---|
| `InteractionDisabled` | A mutating tool was called while the host gate is off (message: enable in tray). |
| `NotDrivable` | No UIA action pattern and no payload peer/command can drive this element. |
| `ValueReadOnly` | `setUiaValue` target's `ValuePattern.IsReadOnly` is true. |
| `CaptureUnavailable` | `captureWindow` target is minimized / has no printable content. |
| `UiaElementStale` | `elementRef` handle expired and re-resolution by locator failed. |
| `UiaAmbiguousLocator` | Locator re-resolution matched multiple elements; caller must disambiguate. |
| `ActionPending` | A mutating payload action timed out on the dispatcher; it may have applied — verify. |
| `ActionDispatched` | A fire-and-forget (dialog-opening) action was posted; observe for its effect. |
| `CommandNotExecutable` | `executeCommand` target's `ICommand.CanExecute` is false. |

`AccessDenied` (existing) continues to signal elevation mismatch, now with the `ProcessProbe.cs:53` path mapped to it (§7.3.6).

## 10. Testing strategy

- **Host UIA & capture:** unit-test the driver against `SampleWpfApp` (the repo's existing sample) — enumerate, find by each locator kind, invoke a button, set a text value, capture a window and assert non-blank PNG dimensions. The existing `ComboBoxAutomation.cs` walkthrough helper is precedent for driving the sample via UIA in a test.
- **Gate:** unit-test that mutating tools return `InteractionDisabled` when off and proceed when on; that read-only tools ignore the gate; that `/health` reports the flag.
- **Element cache/ref:** unit-test warm-hit, TTL-expiry re-resolve, ambiguous-locator, and stale paths.
- **Payload tier:** handler unit tests following the existing 4-step template; `executeCommand` `CanExecute` gating; `waitForValue` match/timeout; `ActionPending` on simulated dispatcher timeout.
- **Skills:** extend `SnoopSkillTests.cs` — second `SKILL.md` written; **embedded body == repo file** for each skill (drift guard).
- **Autostart:** update `AutostartTaskTests.cs` for `/RL HIGHEST`; unit-test the manual-start-routes-through-task decision and the no-task fallback.
- **Known constraints that limit automated verification (document, don't fight):**
  - **UIA needs an interactive desktop** — it cannot run in a session-0 CI agent (recorded in the walkthrough spec). The UIA/capture tests are interactive-session tests, not headless-CI tests.
  - **Elevated-target driving is admin-only and requires a real elevated host** — cannot be exercised by an unprivileged CI runner. Verified manually against an elevated target; the non-admin *detection/messaging* path is unit-testable.

## 11. Related fixes folded into this work
1. `ProcessProbe.cs:53` unmapped `Win32Exception` → map to `AccessDenied` (§7.3.6).
2. Skill embedded-vs-repo drift guard test (§8.4).
Both are logged here so they are addressed within this feature rather than dropped.

## 12. Delivery / phasing

- **Implementation Plan 1 — Phase A (host driving + capture + gate + elevation + skill):** `Automation/` subsystem, `UiaTools`, `InteractionGate`, `PrintWindow` capture, element cache, new error codes, the elevation change set (§7.3), the `snoopmcp-uia` skill + multi-skill registry (§8). Touches no injected-payload code and no pipe RPC envelope/handlers — the only shared-contract edit is append-only `ErrorCode` additions. This is the fast, high-value first release.
- **Implementation Plan 2 — Phase B (payload fallback + ground truth):** protocol tool names + DTOs, payload `Interaction/` action classes + handlers, mutation-safe dispatcher path (`ActionPending`/fire-and-forget), host relay tools, peer/id bridge. Depends on Plan 1's gate and error codes.

Each plan is authored separately via the writing-plans skill after this spec is approved.

## 13. Open questions (none blocking)
- **Capture result shape:** MCP native image content (recommended — the agent *sees* the chart) vs base64-in-JSON (fits the current single `SerializeResult` path). Chosen: **image content**, accepting a small result-path extension. Revisit only if the SDK's image support proves awkward.
- **Gate default for CI/dev:** off by default everywhere; a `--allow-interaction` host flag can default it on for a dev box. No further granularity in v1.
