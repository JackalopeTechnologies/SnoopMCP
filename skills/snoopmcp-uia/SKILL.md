---
name: snoopmcp-uia
description: Use when driving or automating a running WPF application hands-free — clicking, selecting, setting values, navigating, waiting, or screenshotting — via SnoopMCP's UI Automation tools. Complements snoopmcp-first (inspection); this skill is for INTERACTION and background capture, never synthesized mouse/keyboard input.
---

# SnoopMCP — driving a live WPF app (UI Automation)

SnoopMCP can drive a running WPF app through UI Automation without touching the mouse or stealing
focus, and capture the window even when it is occluded. Driving is two-tier:

1. **UIA tier (no attach needed).** Keyed by PID.
   - `getUiaTree(pid, fromElement?, depth)` — discover elements (AutomationId, Name, ControlType, HelpText, bounds, patterns).
   - `findUiaElement(pid, by, value)` — `by` ∈ automationId | name | helpText | controlType (this is the stability order — prefer automationId; treat a VM-type-name Name as a smell).
   - `captureWindow(pid)` — returns the window as a PNG image; works while occluded; a minimized window returns CaptureUnavailable.
   - `waitForUia(pid, by, value, timeoutMs)` — wait for an element instead of sleeping.
   - `invokeUia(element, pattern?)` — click/select/toggle/expand. MUTATES.
   - `setUiaValue(element, value)` — set a text/numeric field. MUTATES.
2. **Payload tier (requires `attach`).** For controls UIA can't drive and for ground-truth checks.
   Element ids are the Snoop element ids from `attach`/`listVisualRoots`/`findElements`, not UIA-tier
   `element` references.
   - `getAutomationPeerInfo(id)` — bridge a Snoop element id to its UIA identity (AutomationId, Name, ClassName, ControlType); correlates the two tiers by `AutomationId`. Read-only.
   - `waitForValue(id, dependencyProperty?, dataContextPath?, expected, timeoutMs)` — poll a DP or DataContext path until it equals `expected` — ground-truth check (VM/DP state, not pixels). Read-only.
   - `peerInvoke(id, pattern, dispatch?)` — drive the real WPF `AutomationPeer` pattern (Invoke | Toggle | SelectionItem | ExpandCollapse) in-process. MUTATES.
   - `executeCommand(id, path?, parameter?, dispatch?)` — execute the `ICommand` bound to an element (or at a DataContext `path`), CanExecute-gated. MUTATES.

## Rules

- Never ask SnoopMCP to synthesize mouse/keyboard input — there is no such tool by design. If a control
  is `NotDrivable`, tell the user and ask them to act, or try the payload tier.
- Mutating tools (`invokeUia`, `setUiaValue`, `peerInvoke`, `executeCommand`) require the host
  **interaction gate**. If you get `InteractionDisabled`, ask the user to enable "Allow app interaction
  (driving)" in the SnoopMCP tray.
- `peerInvoke`/`executeCommand` accept an optional `dispatch="post"` for actions that open a modal
  dialog and would otherwise block the wait indefinitely — it fires the action and returns immediately
  (`Dispatched: true`) without observing the outcome, i.e. `ActionDispatched`; verify the effect
  separately with `waitForValue`/`captureWindow`.
- Without `dispatch="post"` (the default is `dispatch="wait"`), a mutating call that times out on the
  dispatcher returns `ActionPending` — the action may already have applied before the wait gave up;
  verify with `waitForValue`/`captureWindow` before assuming failure or retrying.
- Driving/capturing an elevated (admin) app requires the SnoopMCP host to run elevated — enabling
  autostart registers an elevated logon task (one UAC). This is admin-only.
- Element handles are short-lived; pass back the `element` reference you received. If it is stale,
  SnoopMCP re-resolves it by locator, or returns `UiaElementStale`/`UiaAmbiguousLocator` — re-find in that case.
- Endpoint: http://127.0.0.1:6300/mcp. Start the SnoopMCP host (tray app) first.
