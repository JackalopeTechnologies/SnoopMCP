# SnoopMCP — Binding & WPF trace diagnostics (Phase 1)

Status: design approved (brainstorm), pending spec review
Date: 2026-05-30

## Summary

Add the first of the planned WPF-debugging tools to SnoopMCP, starting with the two
highest-value, binding-focused capabilities:

1. **Trace capture** — surface WPF's otherwise-silent trace failures (binding errors first,
   plus other `PresentationTraceSources`), *including initialization-time errors* that occur
   before a tool could normally attach.
2. **Deep binding diagnosis** — on-demand inspection of a single element's binding
   (`BindingExpression` status, validation, resolved source, converter, live values).

Trace capture is delivered by a new **lightweight client DLL** the target app references, which
records trace into an in-process ring buffer and exposes it over a per-process named pipe. The
endpoint is non-streaming, opt-in, and dormant unless enabled, so it is safe to ship and usable
in the field. Deep binding diagnosis reuses the existing Snoop-injected payload channel (it needs
a live element).

These also serve the longer-term goal of an agent-driven test bed: when an automated action's
expected result does not happen, "the binding had a path error / the value came from a style, not
your binding / the element is zero-size" is the assertion-and-explanation layer.

## Goals / non-goals

Goals:
- Capture binding + selected WPF trace errors from app startup onward, readable on demand.
- A bounded startup connect-window so a client can be hooked in live before the first render.
- On-demand deep diagnosis of a specific element/property binding.
- Field-usable: opt-in, dormant by default, zero data emitted unless a local client connects.

Non-goals (recorded for later, not built in Phase 1):
- The other six diagnostic tools (value-source, visibility/layout, datacontext-provenance,
  resource-resolution, change-detection, validation/commands) — later phases.
- Push/streaming of trace to the client — Phase 1 is pull-based (`sinceSeq`).
- **License-key authorization** — an encrypted key in the license file the connecting tool must
  present to be allowed to read app internals. Future hardening.
- **MEF on-demand load** — not shipping the diagnostics surface at all by default; loading it via
  MEF only when the tool is installed to investigate a problem. Future hardening.

## Components

1. **`SnoopMCP.Diagnostics`** (new, lightweight; depends only on WPF, not on the Snoop injection
   stack, so it can run at the very top of app startup, before any `Window`/binding initializes).
   Referenced by a target app; one call at the top of startup. Owns the trace listeners, the ring
   buffer, the named-pipe server, and the bounded startup wait.
2. **Host diagnostics client** (in `SnoopMCP.Server`) — connects to a target's diagnostics pipe and
   reads trace entries; surfaced as MCP tools.
3. **Payload addition** (existing injected `SnoopMCP.Payload`) — `diagnose_binding` over the
   existing pipe, since it needs a live `BindingExpression`.
4. **Protocol** (`SnoopMCP.Protocol`) — message types for the diagnostics pipe (get-trace, clear)
   and for `diagnose_binding` on the payload pipe.

The two channels are independent by design: trace capture + the startup wait work with no Snoop
injection; the visual-tree channel is unchanged.

## `SnoopMCP.Diagnostics` behavior

Entry point, called first thing in the target app's `App` constructor / `OnStartup` (before any
`Window`):

```csharp
SnoopMcpDiagnostics.Start();   // no-op unless SNOOPMCP_DIAG=1
```

When enabled (`SNOOPMCP_DIAG=1`), `Start()`:
1. Installs trace listeners on `PresentationTraceSources` at `Warning`+ for: DataBinding (primary),
   ResourceDictionary, Markup, DependencyProperty, NameScope. Capture is live from this instant.
2. Writes each trace event into a bounded ring buffer (default capacity 1000), each entry carrying
   a monotonically increasing sequence id.
3. Opens a named-pipe server `snoopmcp-diag-{pid}`, ACL'd to the current user SID only.
4. Waits up to `SNOOPMCP_DIAG_WAIT_MS` (default 15000) for a client to connect, then returns and
   lets startup continue — whether or not a client connected. A timeout is not an error; buffered
   init errors remain readable by a later connection. `SNOOPMCP_DIAG_WAIT_MS=0` => do not wait.
5. Is a complete no-op (no listeners, no pipe, no wait) when not enabled, so Release builds and
   normal runs are unaffected.

Each ring-buffer entry (the "rich/live references" capture — read structured fields from the trace
payload while the objects are still alive):
- `seq`, `timestampUtc`, `source` (e.g. DataBinding), `level` (Warning/Error), `message`
- best-effort structured fields where WPF exposes them: target element type, target
  `AutomationId`/`Name`, target property, binding path, source type. Absent fields are null.

## Read tools (host, over the diagnostics pipe)

- `get_binding_errors(sinceSeq?, max?)` — entries from the DataBinding source (the common case).
- `get_wpf_trace(sources?, sinceSeq?, max?)` — all/filtered entries across the captured sources.
- `clear_trace()` — reset the buffer.

`sinceSeq` lets a client poll for "what's new" without re-reading. Connecting to the pipe also
satisfies the startup wait.

## Deep binding diagnosis (payload channel)

- `diagnose_binding(elementId, property)` — using the element handles the existing tools already
  provide. Returns:
  - `BindingExpression.Status` (Unattached / Inactive / Active / Detached / PathError /
    UpdateSourceError / UpdateTargetError)
  - `HasError` + `Validation.GetErrors` for that property
  - resolved source object identity + type
  - Binding params: Path, Mode, UpdateSourceTrigger, Converter type, ConverterParameter,
    StringFormat, FallbackValue, TargetNullValue, ElementName/RelativeSource/Source
  - current target value vs. the value the path resolves to on the source
  - MultiBinding: report each child binding; converter is reported, not invoked (read-only).

## Security / field considerations

- The pipe is **ACL'd to the current user SID only** — no cross-user/session access.
- **Opt-in**: nothing is installed, opened, or paused unless `SNOOPMCP_DIAG=1`.
- **No data leaves the process unless a local client connects and requests it.** Trace content
  (binding paths/values, resource keys) can contain app data; this is documented.
- The bounded startup wait only applies when enabled; enabling diagnostics is a deliberate act
  (you are investigating), so a ≤15s pause is acceptable in that mode.

## Testing

Unit (no WPF / no pipe):
- Ring buffer: bounded capacity, eviction order, monotonic `seq`, `sinceSeq` filtering.
- Trace-event → entry mapping, including the structured-field extraction and null-when-absent.
- Pipe message (de)serialization for the new protocol messages.
- `diagnose_binding` request/response argument builders.

Integration:
- A tiny WPF harness app that deliberately raises a binding error at startup; with `SNOOPMCP_DIAG=1`,
  connect to the diagnostics pipe and assert `get_binding_errors` returns the seeded error with its
  structured fields. Assert a no-connect run still continues after the timeout and the error is
  present on a later connect.
- `diagnose_binding` against a known-bad binding on a live element returns `HasError` + a `PathError`
  status; against a healthy binding returns `Active` with matching target/source values.
- Pipe is not connectable by a different user (ACL) — best-effort, environment-permitting.

## Decisions resolved during brainstorm

- Driver/goal: agent-drives + capture-tests is the north star; this phase builds the diagnostic
  foundation first.
- Sequencing: binding-first (this phase), then the foundation tools, then temporal tools.
- Error structure: rich/live references (structured fields lifted from the trace payload).
- Startup wiring: cooperative client DLL (not launch-and-inject).
- Connect model: non-streaming pipe, opt-in, dormant when idle; bounded 15s startup connect-window
  (configurable), continue regardless on timeout.
