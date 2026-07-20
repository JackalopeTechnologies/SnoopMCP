---
name: snoopmcp-first
description: Use when inspecting, diagnosing, or debugging a running WPF application's live visual tree, dependency properties, data bindings, styles, templates, or DataContext via SnoopMCP — a control renders blank, wrong-sized, or wrong-styled; a binding silently fails; the DataContext isn't what you expect; you need a template part or a property's effective value. For clicking, typing, waiting, or screenshotting the app, use snoopmcp-uia instead.
---

# SnoopMCP — live WPF inspection

SnoopMCP injects a payload into a running .NET (x64) WPF process and answers questions about its
live state: the visual and logical trees, dependency-property values with their precedence, bindings,
styles, templates, and DataContext. Every tool in this skill is read-only and ungated — call them
freely while diagnosing.

Driving the app (click, set a value, wait, capture the window) is a separate, mutating tool family
behind the host's interaction gate, which is **off by default** — the user enables it from the
SnoopMCP tray menu ("Allow app interaction (driving)"). See the **snoopmcp-uia** skill.

## Workflow

1. `listWpfProcesses()` — pick the target by pid, window title, and bitness. Host-side; no session yet.
2. `attach(pid)` — required before every tool below. Its response already carries the live visual
   roots; use those ids rather than guessing, or reusing ids from an earlier session. Only one session
   exists at a time: `attach` fails outright while another is open, so `detach()` first.
3. Inspect (below).
4. `detach()` when done.

## Quick reference

| To find out | Call |
|---|---|
| What windows/popups exist | `listVisualRoots()` |
| Which element is *that* one | `findElements(rootId, predicate)` when you know a name/type; `hitTest(rootId, x, y)` when you only have a location; `resolvePath(rootId, path)` to re-resolve a path string from an earlier `describeElement` |
| What a node is | `describeElement(id)` |
| Tree shape | `getChildren(id, tree)`, `getParent(id, tree)`, `getTemplatedParent(id)` |
| Why a property holds this value | `getDependencyProperty(id, name)` — value **and** precedence trace; `listDependencyProperties(id)` |
| Why a binding shows nothing | `inspectBinding(id, propName)` for one binding; `listBindings(id, includeDescendants)` to audit a subtree |
| What the data actually is | `describeDataContext(id)`, `readDataContextPath(id, path)` |
| Why it looks like that | `resolveStyle(id)`, `resolveTemplate(id)` |
| Full live snapshot | `exportXaml(id)` |

Property names are the plain DP name — `Text`, `Visibility`, `Background`. `listDependencyProperties`
gives the exact spellings available on an element.

`findElements` predicate fields, all optional and AND-combined: `type` (case-sensitive substring of
the full type name), `name` (exact `x:Name`), `automationId` (exact), `textContains` (case-insensitive,
against capped visible text), `propertyEquals` (stringified DP comparison), and the nested predicates
`hasAncestor`, `hasDescendant`, `inTemplateOf`.

## Common mistakes

- **Carrying element ids across sessions.** Ids are per-session. If the target exits, the session goes
  with it (`SessionLost`) — `attach` the new pid and re-resolve. Within a live session, an id whose
  element has been garbage-collected returns `ElementExpired` — re-resolve from
  `listVisualRoots`/`findElements`.
- **Blaming the binding first.** For a wrong value, read the precedence trace from
  `getDependencyProperty` — a local value, style trigger, or animation may be beating the binding.
- **Trusting `exportXaml` for binding shape.** It serializes evaluated values, not `{Binding}` markup;
  use `listBindings` when the question is about the binding itself.
- **Over-trusting predicates.** `textContains` searches only ~200 chars of visible text, and
  `propertyEquals` does not handle attached properties.
- **Waiting on `recentTraceLines`.** `inspectBinding` always returns it empty today.

## Environment

- One target at a time; x64 WPF on .NET 10+; the host must run at the **same elevation** as the target
  (`AccessDenied` otherwise).
- Start the SnoopMCP host (tray app) first. Endpoint: http://127.0.0.1:6300/mcp.
- Calls run on the target's dispatcher with a 5s timeout. `DispatcherTimeout` means the app's UI thread
  was busy or blocked, not that the call was wrong: let the app settle and retry the same call once. If
  it repeats, the UI thread is genuinely stuck — that is the bug to chase, not the tool call.
