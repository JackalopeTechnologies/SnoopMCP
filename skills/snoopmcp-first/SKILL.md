---
name: snoopmcp-first
description: Use when inspecting, diagnosing, or debugging a running WPF application's visual tree, data bindings, styles, templates, or DataContext via SnoopMCP. SnoopMCP attaches to a live .NET WPF process and exposes read-only inspection tools over MCP.
---

# SnoopMCP — live WPF inspection

SnoopMCP is a read-only MCP server that injects into a running .NET (x64) WPF process and lets you
inspect its live visual tree, dependency properties, bindings, styles, templates, and DataContext. It
does not modify the target app.

## Workflow

1. `listWpfProcesses` — find the target by PID and window title.
2. `attach` with the PID — required before any inspection tool.
3. Inspect: `listVisualRoots`, `describeElement`, `getChildren`/`getParent`, `findElements`, `hitTest`,
   `resolvePath`, `describeDataContext`, `readDataContextPath`, `listDependencyProperties`,
   `getDependencyProperty`, `resolveStyle`, `resolveTemplate`, `inspectBinding`, `listBindings`,
   `exportXaml`.
4. `detach` when done.

## Notes

- One target at a time; x64 WPF on .NET 10+ only.
- All tools are read-only — safe to call freely while diagnosing.
- Endpoint: http://127.0.0.1:6300/mcp. Start the SnoopMCP host (tray app) first.
