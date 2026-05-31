# SnoopMCP

An MCP server that injects a payload DLL into a running WPF process so an LLM
client can diagnose styling, binding, and dependency-property resolution
problems live. **v1 is read-only.**

The host speaks MCP over **Streamable HTTP** (Kestrel, `http://127.0.0.1:6300`,
endpoint `/mcp`) to the LLM client, and length-prefixed JSON over a named pipe to
the payload injected into the target. Cross-process injection reuses
[snoopwpf](https://github.com/snoopwpf/snoopwpf)'s injector (pinned as a git
submodule); SnoopMCP ships its own payload with LLM-shaped read-only tools.

## How it works

```
LLM client ──HTTP /mcp──▶ SnoopMCP.Host.exe
                              │  attach(pid): subprocess the Snoop launcher
                              ▼
                          Snoop.InjectorLauncher.x64.exe
                              │  CreateRemoteThread + LoadLibrary
                              ▼
                          target WPF process
                          SnoopMCP.Payload.dll  ◀── injected
                              │  named pipe (length-prefixed JSON)
                              ▲
                          host ◀── tool calls marshalled onto the UI Dispatcher
```

## Quickstart

### Prerequisites

- Windows 10/11 (x64)
- **.NET 10 SDK**
- **Visual Studio 2022/2026 with the "Desktop development with C++" workload**
  (`VC.Tools.x86.x64` + Windows SDK). The build compiles Snoop's *native* injector
  (`Snoop.GenericInjector.vcxproj`) via the full MSBuild.exe — `dotnet build` alone
  cannot build the native component.
- A WPF app to attach to, running on **.NET 10+ (x64)**.
- The host must run at the **same elevation** as the target. If the target runs as
  Administrator, run the host as Administrator too.

### Clone with submodules

This repo references the snoopwpf fork as a git submodule under `external/snoopwpf/`:

```text
git clone --recurse-submodules https://github.com/JackalopeTechnologies/SnoopMCP.git
```

If you already cloned without `--recurse-submodules`:

```text
git submodule update --init --recursive
```

### Build

```text
dotnet build SnoopMCP.sln -c Release
```

This also builds Snoop's native injector + managed launcher (x64) from the
submodule and stages them under the host output. The host is at:

```text
src\SnoopMCP.Host\bin\Release\net10.0-windows\SnoopMCP.Host.exe
```

Two folders are staged next to it and **must travel with the exe** if you copy it:

- `injector\` — `Snoop.InjectorLauncher.x64.exe` + `Snoop.GenericInjector.x64.dll` (the injection tooling)
- `payload\` — `SnoopMCP.Payload.dll` + its dependency closure (the assembly injected into the target)

### Run the host

SnoopMCP is an HTTP MCP server, not a stdio subprocess. Start it directly:

```text
src\SnoopMCP.Host\bin\Release\net10.0-windows\SnoopMCP.Host.exe
```

It binds Kestrel to `http://127.0.0.1:6300` (localhost only) and serves the MCP
endpoint at `/mcp`.

### Attach via the MCP client

Point your MCP client at the HTTP endpoint:

```json
{
  "mcpServers": {
    "snoopmcp": {
      "type": "http",
      "url": "http://127.0.0.1:6300/mcp"
    }
  }
}
```

Then ask the LLM:

> Attach to the WPF process with PID 12345, list its visual roots, then describe
> the Save button.

The LLM will call:

1. `attach(pid: 12345)` — injects the payload, opens the session, returns process info
2. `listVisualRoots()` — returns `{ roots: [...] }`
3. `findElements(rootId, predicate: { type: "Button", name: "Save" })`
4. `describeElement(matchedId)`

## Install (MSI)

Download the latest `SnoopMCP-<version>.msi` from the
[**Releases page**](https://github.com/JackalopeTechnologies/SnoopMCP/releases/latest) and run it
(double-click, or `msiexec /i SnoopMCP-<version>.msi`).

The per-user MSI installs SnoopMCP without administrator rights to `%LocalAppData%\SnoopMCP`, creates
a logon autostart task (so the tray host starts at sign-in), and adds a "SnoopMCP Sample App"
Start-menu shortcut. It launches the host immediately on a fresh install (the logon task starts it on
later sign-ins). An upgrade or uninstall first terminates the running host (so the locked exe doesn't
block file replacement).

The finish page offers an opt-in **"Register SnoopMCP into detected AI agents now"** checkbox (checked
by default): when left ticked it registers SnoopMCP into the AI agents detected on the machine,
best-effort — a missing agent or a write error never fails the install. You can also register from the
tray **Install into ▸** menu or the CLI at any time (see [Using the tray app](#using-the-tray-app)).
Confirm overall state any time with `SnoopMCP.Cli status`.

**Build the installer yourself** (needs the .NET 10 SDK, the VS C++ x64 workload, and the
[WiX 5 toolset](https://wixtoolset.org) — `dotnet tool install --global wix`):

```text
pwsh -File SnoopMCP.Installer/build-installer.ps1 -Version 1.1.0
```

This produces `SnoopMCP.Installer/SnoopMCP.msi`. Releases are cut by pushing a `v*` tag
(e.g. `git tag v1.1.0 && git push origin v1.1.0`); CI builds the MSI and publishes it to the Releases
page automatically.

**Limitations (v1.1):** non-elevated targets only (the host attaches to
same-elevation WPF apps — see Known limitations); single user per machine (port
6300 is not multiplexed).

### Verifying an install (manual)

After running the MSI on a test machine:
1. `%LocalAppData%\SnoopMCP\` contains (among other runtime DLLs) `SnoopMCP.Host.exe`, `SnoopMCP.Cli.exe`, `injector\`, `payload\`, and `samples\SampleWpfApp.exe`.
2. `schtasks /Query /TN "SnoopMCP Host"` shows the logon task.
3. The host launches on a fresh install, so `http://127.0.0.1:6300/health` returns 200 (otherwise it starts at next sign-in via the logon task). Either way, `SnoopMCP.Cli status` reports the state.
4. The "SnoopMCP Sample App" Start-menu shortcut launches the sample.
5. Register an agent from the tray **Install into ▸** menu (or `SnoopMCP.Cli register-clients`), then confirm that agent's config carries a `snoopmcp` entry — and nothing else was disturbed.
6. An **upgrade** (install a higher `-Version` over the running install) succeeds without a locked-file error — the running host is terminated first.
7. Uninstall (Apps & features, or `msiexec /x`) removes the task, any client entries SnoopMCP added, and the files — leaving other MCP servers intact.

## Using the tray app

`SnoopMCP.Host` runs as a system-tray app. Left- or right-click the tray icon for the menu:

- **Start MCP** / **Stop MCP** — start or stop the MCP server (it starts automatically when the host launches).
- **Install into ▸** — register SnoopMCP with an AI agent. Pick a single agent (**Claude Code**, **Claude Desktop**, **VS Code**, **Codex**, **Copilot CLI**, **Cursor**, **Gemini CLI**, **Windsurf**, **Visual Studio 2022**) or **All detected agents**. A tray notification reports what was written.
- **Uninstall from ▸** — remove SnoopMCP from a single agent or all detected agents.
- **Status** — report SnoopMCP's registration state across every supported agent.
- **Exit** — quit the host.

Registering writes the agent's MCP configuration so it can find the SnoopMCP endpoint. Each agent uses
its own documented remote-server shape: streamable HTTP (`type: http`) for Claude Code, VS Code, Codex,
Copilot CLI, Cursor, and Visual Studio 2022; `httpUrl` for Gemini CLI; `serverUrl` for Windsurf; and an
`npx mcp-remote` stdio bridge for Claude Desktop (which has no native HTTP MCP transport — this
requires [Node.js](https://nodejs.org) on `PATH`). For Claude Code, registration also pre-approves
SnoopMCP's read-only tools (`permissions.allow`) and installs the `snoopmcp-first` skill. Restart the
target agent after registering so it picks up the new config.

The same registration is available from the command line via `SnoopMCP.Cli`:

```text
SnoopMCP.Cli register-clients     # all agents; or target specific ones:
SnoopMCP.Cli register-clients --claude-code --claude-desktop --vscode --codex --copilot-cli ^
                              --cursor --gemini-cli --windsurf --visual-studio
SnoopMCP.Cli register-clients --detected-only   # only agents that are installed
```

The CLI also supports `unregister-clients` and `status` (both accept the same agent flags and
`--detected-only`), plus `install-autostart`, `uninstall-autostart`, `start`, and `stop`.

## Tool surface

For a guided, end-to-end demonstration of every tool against the bundled sample
app — real tool calls and captured responses diagnosing real bugs — see
[`docs/walkthrough.md`](docs/walkthrough.md).

Twenty read-only tools, plus `attach`/`detach`:

| Tool | Use it for |
|---|---|
| `listWpfProcesses()` | Discover WPF processes the host can attach to (pid, name, window title, bitness). Host-side, pre-attach — no session required |
| `attach(pid)` | Open a session by process id |
| `detach()` | Close the current session |
| `listVisualRoots()` | Find windows, popups, tooltip layers |
| `describeElement(id)` | Per-node identity: type, name, bounds, path, binding-error flag |
| `getChildren(id, tree)` | Walk visual or logical tree, virtualization-aware |
| `getParent(id, tree)` | Climb upward |
| `getTemplatedParent(id)` | Climb out of a template |
| `findElements(rootId, predicate)` | Search by type, name, AutomationId, text, DP value, has-ancestor, has-descendant, in-template-of |
| `hitTest(rootId, x, y)` | Deepest visual at a point |
| `resolvePath(rootId, pathString)` | Path string back to element |
| `describeDataContext(id)` | CLR type shape of the DataContext |
| `readDataContextPath(id, path)` | Read a dotted path off the DataContext |
| `listDependencyProperties(id)` | All DPs available on an element |
| `getDependencyProperty(id, name)` | Current value + precedence trace |
| `resolveStyle(id)` | Applied style, BasedOn chain, setters, triggers |
| `resolveTemplate(id)` | Applied template, runtime tree, named parts |
| `inspectBinding(id, propName)` | BindingExpression state, source, path, mode, value (deep dive on one binding) |
| `listBindings(id, includeDescendants)` | Every binding on an element / under a subtree — wide audit |
| `exportXaml(id)` | `XamlWriter` snapshot of the element's live state (bindings appear as evaluated values; use `listBindings` for binding shape) |

## Error codes

| Code | Meaning |
|---|---|
| `AttachFailed` | Target not found, not WPF, or non-x64 |
| `PayloadLoadFailed` | Payload or its dependencies failed to load in the target |
| `DispatcherTimeout` | Per-call timeout (5s default) |
| `SessionLost` | Target exited or pipe closed |
| `AccessDenied` | Elevation mismatch |
| `ElementExpired` | Element id has been garbage collected |
| `InvalidArgument` | Bad tool argument |
| `PathParseError` | Malformed path string |

## Known v1 limitations

- **Read-only.** No property writes, no method invocation, no scripting.
- **One target at a time.** Phase 2 may add multi-target.
- **x64 only.** x86/ARM64 targets are rejected at probe time.
- **No persistent reattach.** Sessions die with the target process.
- **`textContains` searches capped visible text** (~200 chars per element).
- **`propertyEquals` does not support attached properties.**
- **`recentTraceLines` always empty** on `inspectBinding`. Phase 2 will wire up `PresentationTraceSources`.

See [`docs/superpowers/specs/2026-05-27-snoopmcp-investigation-design.md`](docs/superpowers/specs/2026-05-27-snoopmcp-investigation-design.md)
for the Phase 2 candidate list (writes, method invocation, scripting, web inspector UI).

## License

The injector built from the snoopwpf submodule under `external/snoopwpf/` is from
the snoopwpf project and retains its upstream **Ms-PL** license; see
[`src/SnoopMCP.Injection/THIRD_PARTY_NOTICES.md`](src/SnoopMCP.Injection/THIRD_PARTY_NOTICES.md).
Everything else is Copyright (c) 2026 Jackalope Technologies.
