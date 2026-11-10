---
name: snoopmcp-first
description: Use when inspecting, diagnosing, or debugging a running WPF application's live visual tree, dependency properties, data bindings, styles, templates, or DataContext via SnoopMCP — a control renders blank, wrong-sized, or wrong-styled; a binding silently fails; the DataContext isn't what you expect; you need a template part or a property's effective value. For clicking, typing, waiting, or screenshotting the app, use snoopmcp-uia instead.
---

# SnoopMCP — live WPF inspection

SnoopMCP injects a payload into a running .NET (x64) WPF process and answers questions about its
live state: the visual and logical trees, dependency-property values with their precedence, bindings,
styles, templates, and DataContext. Use Snoop for these WPF internals; general app tools remain
useful for navigation, other application types, and screenshots. A screenshot verifies appearance;
it cannot establish a binding's source, a property's precedence, or the live DataContext.

Inspection, waiting, and screenshots are read-only and ungated. Clicking and setting values mutate
the target and require the host's interaction gate. Never enable it automatically or bypass it with
another tool. See the **snoopmcp-uia** skill.

## Workflow

1. Read the application's instance/launch rules. Enumerate existing processes with
   `listWpfProcesses()` and identify the requested instance by pid, title, and bitness. Reuse it;
   do not launch a duplicate or restart it just to obtain tools.
2. Check `http://127.0.0.1:6300/health`. Its `attached` flag does not identify the session's owner.
   Attach only when no session is open, or reuse a session you opened in this task. Never detach
   someone else's session. If ownership is unknown or `attach` reports an open session, stop and
   coordinate with the user; do not recover by detaching or restarting the host.
3. `attach(pid)` opens the inspection session and returns live visual roots. Use those ids rather
   than guessing or reusing ids from an earlier session. Inspect with the tools below.
4. Use `captureWindow(pid)` or an appropriate screenshot tool to verify the actual appearance.
   Report what was inspected and what the screenshot shows separately from file/configuration checks.
5. `detach()` only the session you opened, when your inspection is finished.

## Connection diagnosis

Tool names here use the server's spelling; clients may expose snake_case names with a server prefix.
Search the client's available tools for SnoopMCP before concluding they are absent. A healthy `/health`
response proves the host is reachable, not that the client completed connection and tool discovery.
If tools are missing, check the configured `/mcp` URL, server enablement, tool filters, and client
connection logs. Refresh the client's connection or restart the client after configuration changes.
Do not change permissions, remove filters, or assume the client prefers another tool. When the host
is stopped, use the installed SnoopMCP launcher or `SnoopMCP.Cli start`; do not kill a running host.

## Quick reference

| To find out | Call |
|---|---|
| What windows/popups exist | `listVisualRoots()` |
| Which element is *that* one | `findElements(id, predicate)` when you know a name/type; `hitTest(id, x, y)` when you only have a location; `resolvePath(id, path)` to re-resolve a path string from an earlier `describeElement` |
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
`hasAncestor`, `hasDescendant`, `inTemplateOf`. For bulk text searches, also `leafOnly`, `maxResults`,
and `suppressPath` to keep large result sets bounded and readable.

## Common mistakes

- **Carrying element ids across sessions.** Ids are per-session. If the target exits, the session goes
  with it (`SessionLost`) — `attach` the new pid and re-resolve. Within a live session, an id whose
  element has been garbage-collected returns `ElementExpired` — re-resolve from
  `listVisualRoots`/`findElements`. Worse, a live-looking id can silently describe a different row: a
  virtualizing panel recycles the *same* container for a different data item as it scrolls — same id,
  no error, no `ElementExpired`. If you're holding ids across calls that might span a scroll or data
  change, re-`describeElement` and compare `dataContextHashCode` against the value you saw before; a
  changed value means this id now names different data.
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
  it repeats, investigate the busy or blocked UI thread; the timeout alone does not prove a deadlock.
