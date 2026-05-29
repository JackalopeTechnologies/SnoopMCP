# SnoopMCP v1.1 — Installer Design (A3)

**Date:** 2026-05-29
**Status:** Approved; ready for the implementation-planning pass.
**Decision sought:** (already granted) — per-user install, logon task launches the host directly, staged PRs, elevated-target debugging deferred.

---

## 1. Goal

Make SnoopMCP self-service end to end. A per-user WiX MSI installs the host (plus its staged `injector/` and `payload/` folders) and a management CLI, registers the SnoopMCP HTTP MCP server into VS Code (Copilot) and Claude Code, creates a per-user logon scheduled task that launches the host in the interactive session, and packages `SampleWpfApp` so a new user immediately has a target to try the walkthrough against.

The artifact a user double-clicks should leave them with: SnoopMCP autostarting at logon, both LLM clients already pointed at `http://127.0.0.1:6300/mcp`, and a Start-menu shortcut to the sample app.

---

## 2. Locked decisions (granted in brainstorming — do not relitigate)

1. **Per-user install.** `Scope="perUser"`, install root `%LocalAppData%\SnoopMCP`. No UAC. The whole installer runs as the user, so client-config writes and logon-task registration need no impersonation.
2. **Logon task launches the host directly.** The per-user logon scheduled task runs `SnoopMCP.Host.exe`. The tray app (Workstream B) is a separate arc that will later re-point the task at the tray; A3 does not build it.
3. **Staged delivery — three PRs:** (1) `SnoopMCP.ClientIntegration`, (2) `SnoopMCP.Cli` + host `/health`, (3) `SnoopMCP.Installer` + sample packaging.
4. **Elevated-target debugging deferred.** v1.1 installs a non-elevated per-user host that attaches to same-elevation (non-admin) targets. The limitation is documented; the "restart elevated" path belongs to the tray work.

These carry over from the v1.1 plan and remain in force: **per-user logon autostart, not a service**; **HTTP transport** at `http://127.0.0.1:6300/mcp`; **VS Code (Copilot) + Claude Code** as the registered clients; **x64 only**.

---

## 3. Reference: how SaddleRAG does it (what we mirror, and the deltas)

SaddleRAG is the proven template. The implementation mirrors it with three swaps: **service → logon task**, **per-machine → per-user**, and **drop the skills/permissions apparatus**.

| Concern | SaddleRAG | SnoopMCP (this design) |
|---|---|---|
| WiX | v5 standalone `Package.wxs`, built with the `wix` global tool, no `.wixproj`, `-d Version=` CLI injection | Same |
| Scope | `perMachine`, ProgramFiles64 | `perUser`, `%LocalAppData%\SnoopMCP` |
| Run model | `ServiceInstall`/`ServiceControl` (LocalSystem, auto) | Per-user logon scheduled task launching `SnoopMCP.Host.exe` |
| Harvest | `<Files Include="$(var.PublishDir)\**" />` (host + CLI publish together) | Same, plus `SampleWpfApp` under `samples\` |
| Client registration | `SaddleRAG.Cli register-clients` via `WixQuietExec` CA, `Impersonate="yes"` (needed because perMachine) | `SnoopMCP.Cli register-clients` via `WixQuietExec` CA, **no impersonation** (perUser already runs as the user) |
| Writers | `IClientWriter` async, atomic temp-write + `File.Move`, UTF-8 no BOM, `ForCurrentUser()` + injectable paths; also writes skill files + permissions | `IClientWriter` **sync**, same atomic/encoding/factory pattern, **server entry only** (no skills, no permissions) |
| Readiness gate | CA polls `http://localhost:6100/health` (`Return="check"`, 300 s) | CA polls `http://127.0.0.1:6300/health` |
| Host hosting | `UseWindowsService` (conditional) | none — plain console host (already the case) |
| Tests | fixture-driven (`input.json` + `expected-after-register.json`), exact-JSON equality, temp dirs | Same pattern |

---

## 4. Component 1 — `SnoopMCP.ClientIntegration` (PR 1)

A new `net10.0` class library. Registers/unregisters the SnoopMCP HTTP MCP server in each supported client's config, idempotently, preserving everything else in the file.

### Types
- `McpEndpoint` — the server identity the writers stamp in: name `snoopmcp`, `type` `http`, `url` `http://127.0.0.1:6300/mcp`. A single source of truth (`McpEndpoint.Default`).
- `RegisterResult` / `UnregisterResult` / `StatusResult` — small result records (`Success`, `Message`, and for status an `IsRegistered` flag).
- `IClientWriter`:
  ```csharp
  public interface IClientWriter
  {
      string ClientName { get; }
      RegisterResult Register(McpEndpoint endpoint);
      UnregisterResult Unregister();
      StatusResult GetStatus();
  }
  ```
  Synchronous — these are small local-file reads/writes; SaddleRAG's async buys nothing here. Each writer: parse-or-empty the JSON document, mutate only its own `snoopmcp` entry, write atomically (`*.tmp` then `File.Move(overwrite:true)`), UTF-8 without BOM. A `ForCurrentUser()` static factory resolves the real path via environment variables; the constructor takes an explicit path for tests.

### `ClaudeCodeWriter`
- Path: `%USERPROFILE%\.claude.json`.
- Register: ensure `mcpServers` object exists; set `mcpServers.snoopmcp = { "type": "http", "url": "http://127.0.0.1:6300/mcp" }`. Overwrite that one key only; never touch other servers or top-level keys.
- Unregister: remove `mcpServers.snoopmcp` if present; leave the rest. No-op (success) if the file or key is absent.
- Status: registered iff `mcpServers.snoopmcp.url` equals the endpoint URL.

### `VsCodeMcpWriter`
- Path: `%APPDATA%\Code\User\mcp.json`.
- Register: ensure the top-level `servers` object exists; set `servers.snoopmcp = { "type": "http", "url": "http://127.0.0.1:6300/mcp" }`. (The modern VS Code MCP shape is `mcp.json` with a `servers` map; **the exact key/shape must be re-verified against current VS Code MCP docs in the plan** — VS Code's MCP config has changed across releases. If current VS Code expects the entry under `settings.json` `"mcp"."servers"` instead, the plan adjusts the target accordingly. The writer's behavior — idempotent add of one `snoopmcp` http entry, preserve others — is fixed; only the file/key location is pending verification.)
- Unregister: remove `servers.snoopmcp`; leave the rest.
- Status: registered iff the entry exists with the right URL.
- **No** settings mutations, plugin directory, autostart flags, or skills locations (those are SaddleRAG-plugin concerns SnoopMCP does not need).

### Tests (`SnoopMCP.ClientIntegration.Tests` — a new test project, matching the repo's per-project test convention)
Mirror SaddleRAG: fixture-driven, one folder per client with `input.json` + `expected-after-register.json` for scenarios **empty**, **no-mcp-section**, **other-servers-present**, **existing-snoopmcp**, **malformed-json**. Tests construct the writer with an injected temp path, run register, assert exact JSON equality after newline/trailing-whitespace normalization. Separate tests assert: unregister removes only `snoopmcp` and preserves a sibling server, unregister on a missing file/key is a success no-op, malformed input fails without corrupting the file, status reflects registration state.

---

## 5. Component 2 — `SnoopMCP.Cli` (PR 2)

A `System.CommandLine` console exe (`SnoopMCP.Cli.exe`), published next to the host so the installer can invoke it from the install directory. Verbs:

| Verb | Behavior | Options |
|---|---|---|
| `register-clients` | Calls the writers' `Register`. Exit 0 = all ok, non-zero = some failed. | `--vscode` / `--claude-code` (bool, default both true), `--quiet`, `--log-file` |
| `unregister-clients` | Calls the writers' `Unregister`. | same client flags, `--quiet`, `--log-file` |
| `install-autostart` | Creates the per-user logon task launching the host. | `--quiet`, `--log-file` |
| `uninstall-autostart` | Removes the logon task. | `--quiet`, `--log-file` |
| `status` | Probes `http://127.0.0.1:6300/health`; reports host reachable?, logon task present?, each client registered?. | `--json` |
| `start` | Launches `SnoopMCP.Host.exe` if not already reachable. | `--quiet` |
| `stop` | Stops the running host process. | `--quiet` |

### Autostart task
Created/removed via `schtasks.exe` invoked as a child process (no extra NuGet dependency):
- Create: `schtasks /Create /TN "SnoopMCP Host" /SC ONLOGON /RL LIMITED /TR "\"<install>\SnoopMCP.Host.exe\"" /F` (LIMITED = non-elevated, matching the per-user model).
- Remove: `schtasks /Delete /TN "SnoopMCP Host" /F`.
- Presence check (`status`): `schtasks /Query /TN "SnoopMCP Host"` exit code.
The task name is a single shared constant. The host path is resolved relative to the CLI's own location (`AppContext.BaseDirectory`) so it works regardless of install root.

### Tests (`SnoopMCP.Cli.Tests` — new test project)
Unit-test the argument wiring and the parts that don't shell out: the `status` aggregation (with the health probe and task-presence behind small injectable seams so tests don't require a live host or real Task Scheduler), and the schtasks command-string construction (assert the exact arguments built for create/delete/query, without executing them). The writer logic itself is covered by PR 1's tests.

---

## 6. Component 3 — host `/health` (PR 2)

Add `GET /health` to `SnoopMCP.Host` returning `200` with `{ "status": "ok", "version": <informational version>, "attached": <bool> }`. `/mcp` is a POST/SSE endpoint and is awkward to liveness-probe; `/health` gives the CLI `status` and the installer's readiness gate a clean signal. This is the only host change in A3. No `--prewarm` (SnoopMCP has no warmup phase). A tiny host test asserts `/health` returns 200 with the expected shape.

---

## 7. Component 4 — `SnoopMCP.Installer` (PR 3)

WiX 5 standalone `Package.wxs`, built with the `wix` global tool (documented in the repo README / a build script), no `.wixproj`. Mirrors SaddleRAG minus the service.

- `Package` `Scope="perUser"`, a fresh `UpgradeCode` GUID, `MajorUpgrade` with a downgrade message, `MediaTemplate EmbedCab="yes"`, `Version="$(var.Version)"` injected via `-d Version=` from the build.
- Install root: `%LocalAppData%\SnoopMCP` (WiX `LocalAppDataFolder`). Binaries harvested with `<Files Include="$(var.PublishDir)\**" />` over a `dotnet publish` output that contains the host + its staged `injector/` and `payload/` + `SnoopMCP.Cli`. `SampleWpfApp` is published into a `samples\` subfolder and harvested too.
- **Start-menu shortcuts:** one for `SampleWpfApp` (so users have a target), and optionally one that opens `docs/walkthrough.md`'s rendered location or the README — minimally, the sample shortcut.
- **Close-the-running-app on every transaction:** `<util:CloseApplication Target="SnoopMCP.Host.exe" TerminateProcess="0" />` (auto-scheduled before `InstallValidate`) terminates a running host before any file work, so an **upgrade or uninstall does not fail on a locked `SnoopMCP.Host.exe`** (the logon task keeps it running between installs). The host is a windowless Kestrel process, so a hard terminate — not a `WM_CLOSE` — is required. When Workstream B ships the tray app, a second `CloseApplication` target for the tray exe is added here.
- **Custom actions** (`WixQuietExec`, run as the user; deferred CAs run as the installing user under perUser):
  - On install (`NOT Installed OR REINSTALL`, after `InstallFiles`): `SnoopMCP.Cli register-clients`, then `SnoopMCP.Cli install-autostart`.
  - On uninstall (`REMOVE = "ALL"`, sequenced before `RemoveFiles`): `SnoopMCP.Cli uninstall-autostart`, `unregister-clients`. (No `stop` CA — `CloseApplication` already terminated the host at transaction start.)
- **Launch on exit (opt-in):** the `WixUI_Minimal` ExitDialog shows a **"Launch SnoopMCP now"** checkbox (`WIXUI_EXITDIALOGOPTIONALCHECKBOX`, default checked). On Finish, if checked and `NOT Installed`, a `WixShellExec` custom action launches `[INSTALLFOLDER]SnoopMCP.Host.exe`. The host is **not** started silently mid-install; the user opts in. Either way the logon task autostarts it on the next sign-in. (When the tray app exists, this checkbox launches the tray instead.)

### Verification (manual — WiX is not unit-testable)
Documented checklist in the installer's README/notes: build the MSI; install; confirm (a) files under `%LocalAppData%\SnoopMCP` incl. `injector/`, `payload/`, CLI, and `samples\SampleWpfApp.exe`; (b) the `SnoopMCP Host` logon task exists (`schtasks /Query`); (c) `.claude.json` and VS Code `mcp.json` carry the `snoopmcp` http entry and nothing else was disturbed; (d) with **"Launch SnoopMCP now"** ticked on the finish page, `http://127.0.0.1:6300/health` returns 200 (otherwise the host starts at next sign-in via the logon task); (e) the sample Start-menu shortcut launches; (f) an **upgrade** (install a higher `-Version` over the running install) succeeds without a locked-file error — `CloseApplication` terminates the running host first; (g) uninstall removes the task, the client entries, and the files, leaving other MCP servers intact. The logic under the installer (writers, CLI, schtasks string-building, `/health`) is unit-tested in PRs 1–2, so manual verification is confined to the MSI packaging/sequencing itself.

---

## 8. Build & CI considerations

- The host build already needs Visual Studio + the C++ x64 workload (the native injector). The installer build additionally needs the WiX 5 toolset (`dotnet tool install --global wix`, plus `WixToolset.Util.wixext`). Document both as installer-build prerequisites.
- No CI is wired for SnoopMCP yet (the required `build` status check is satisfied by manual `gh api` status posts). The installer's MSI build and manual verification stay local for v1.1; document the `wix build` command line (mirroring SaddleRAG's) in the installer notes.

---

## 9. PR staging

1. **PR 1 — `SnoopMCP.ClientIntegration`**: `McpEndpoint`, results, `IClientWriter`, `ClaudeCodeWriter`, `VsCodeMcpWriter`, + fixture tests. Self-contained; green on its own.
2. **PR 2 — `SnoopMCP.Cli` + host `/health`**: the CLI verbs wrapping the writers + schtasks autostart + status/start/stop, and the host `/health` endpoint + its test.
3. **PR 3 — `SnoopMCP.Installer`**: WiX `Package.wxs`, the publish wiring that stages host + injector + payload + CLI + sample, custom actions, Start-menu shortcut, README install section, and the manual-verification checklist.

Each PR follows the established per-task workflow (sync master → branch → zero-warning build + tests → commit via message file → push → `gh api` build status → PR → review stop).

---

## 10. Out of scope (recorded so we don't drift)

- **Tray app** (`SnoopMCP.Tray`) — Workstream B. The logon task + the installer's launch-on-exit checkbox launch the host directly until then. When the tray ships, Workstream B must also: (a) give the tray menu **Launch MCP** / **Stop MCP** actions that start/stop the host (the v1.1 plan's Start/Stop menu items); (b) add the tray exe as a second `util:CloseApplication` target in the installer so an upgrade/uninstall terminates the tray too; (c) re-point the logon task + the launch-on-exit checkbox at the tray exe instead of the host.
- **Elevated-target debugging** — documented limitation; folds into the tray's "restart elevated".
- **Per-machine install** — rejected for v1.1.
- **Concurrent multi-user port 6300 collision** — acceptable for v1.1; noted in the README.
- **Claude Desktop / Copilot CLI registration** — only VS Code (Copilot) + Claude Code are in scope. (Claude Desktop would need the `npx mcp-remote` bridge, as in SaddleRAG; not now.)
- **CI for the installer** — local build + manual verification for v1.1.

---

## 11. Acceptance criteria

A3 is complete when, across the three PRs:
1. `SnoopMCP.ClientIntegration` registers/unregisters the `snoopmcp` http entry in Claude Code and VS Code idempotently, preserving other content, with fixture tests green.
2. `SnoopMCP.Cli` exposes `register-clients` / `unregister-clients` / `install-autostart` / `uninstall-autostart` / `status` / `start` / `stop`, with unit tests for the non-shell logic, and the host exposes `/health`.
3. `SnoopMCP.Installer` produces a per-user MSI that, on a test machine, installs the host + injector + payload + CLI + sample, registers both clients, creates the logon task, starts the host, and gates on `/health`; uninstall reverses all of it; the manual checklist passes.
4. Every solution build stays zero-warning; all unit/integration suites stay green.
5. The README gains an "Install" section pointing at the MSI and noting the documented limitations (non-elevated targets, single-user port).
