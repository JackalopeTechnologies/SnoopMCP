# SnoopMCP — Continuation Prompt (v1.1 productization)

Paste everything below the `---` line into a fresh Claude session.

---

I'm continuing work on **SnoopMCP**, an MCP server that injects a payload DLL into running .NET 10 WPF processes so an LLM can diagnose styling, binding, and resolution problems live. **v1 (read-only) is complete and proven end-to-end** — real injection into a live WPF app works. I'm now doing the **v1.1 productization** arc: finish process discovery, write a walkthrough, build a WiX installer, and add tray apps.

## Repo

- Path: `E:\GitHub\SnoopMCP`
- GitHub: `https://github.com/JackalopeTechnologies/SnoopMCP` (private, org `JackalopeTechnologies`, you are logged in via `gh` as `WyoDoug`)
- Default branch `master`, **branch-protected**: PRs required, no force-push, a `build` status check is required, conversation-resolution + admins enforced. Identity auto-wired via `~/.gitconfig` includeIf for `E:/GitHub/` → `douglas@jackalopetechnologies.com`. Do not change it.

## Read these first

- **v1 plan (done):** `docs\superpowers\plans\2026-05-27-snoopmcp-v1-readonly.md` — the 34 tasks that built v1.
- **v1.1 plan (active):** `docs\superpowers\plans\2026-05-29-snoopmcp-v1.1-productization.md` — **this is your roadmap.** Workstreams A1 (finish listWpfProcesses), A2 (walkthrough), A3 (installer), B (per-product tray apps).
- **Spec:** `docs\superpowers\specs\2026-05-27-snoopmcp-investigation-design.md` — architecture + Phase 2 candidates.
- **README:** `README.md` (repo root) — current user-facing state.

## Current state (verify with `git -C E:/GitHub/SnoopMCP log --oneline -5`)

- master at the v1.1-docs merge (was `5ccd372` "Task 33: README" when v1 finished; a docs PR for the v1.1 plan + this prompt lands on top).
- **161 tests green** (5 protocol + 149 payload + 6 host + 1 integration). Build is zero-warning.
- **Injection is PROVEN**: the integration test (`tests/SnoopMCP.IntegrationTests/EndToEndTests.cs`) spawns SampleWpfApp, attaches via the real injector, and drives all 19 tools.
- **WIP parked on branch `task-34-list-processes`** (commit `d6fa02e`): `listWpfProcesses` is ~60% done — DTOs (`WpfProcessDto`, `ListWpfProcessesResponse`) + `ProcessEnumerator` are written; the `McpTools` wrapper, tests, build, and README row remain. **Resume that branch — do not start fresh.** Exact remaining steps are in the v1.1 plan, Workstream A1.

## Next concrete actions (in order)

1. **Finish A1 (`listWpfProcesses`)** on branch `task-34-list-processes` per the v1.1 plan A1 steps → PR → merge.
2. **A2 walkthrough** — `docs/walkthrough.md` driving the sample app's authored bugs through the tools.
3. **A3 installer** — do a `superpowers:writing-plans` pass FIRST (read SaddleRAG's live `SaddleRAG.Installer/Package.wxs` + `SaddleRAG.ClientIntegration/Writers/*`), then execute. Per-user logon autostart (NOT a service), WiX, `SnoopMCP.ClientIntegration` writers (VS Code Copilot + Claude Code, HTTP entry → `http://127.0.0.1:6300/mcp`), `SnoopMCP.Cli` management exe, package SampleWpfApp.
4. **B SnoopMCP tray app** (`SnoopMCP.Tray`, this repo) — the autostart presence; runtime-rendered state icon; links to the (future) monitor UI. SaddleRAG/AeroCoder tray apps are parallel work in their own repos.

Work **one task per PR**; stop after each and let me review unless I say otherwise.

## Per-task PR workflow (branch protection is real — follow exactly)

ONE command per Bash call. No `&&`/`;`/`||`/pipes. No `cd` (use `git -C E:/GitHub/SnoopMCP`). Commits via `-F msgfile`, never `-m`. No AI attribution in commits/PRs. No hook-bypass flags.

1. `git -C E:/GitHub/SnoopMCP fetch origin` → `git -C E:/GitHub/SnoopMCP reset --hard origin/master` (sync).
2. `git -C E:/GitHub/SnoopMCP checkout -b <branch>`.
3. Implement. `dotnet build E:/GitHub/SnoopMCP/SnoopMCP.sln -c Debug` must be zero-warning; run relevant tests.
4. Commit (`-F` msgfile), `git -C E:/GitHub/SnoopMCP push -u origin <branch>`.
5. `git -C E:/GitHub/SnoopMCP rev-parse <branch>` → `gh api -X POST repos/JackalopeTechnologies/SnoopMCP/statuses/<sha> --field state=success --field context=build --field description="local build green"` (no CI yet — this satisfies the required `build` check).
6. `gh pr create --repo JackalopeTechnologies/SnoopMCP --base master --head <branch> --title "..." --body-file <file>`.
7. `gh pr merge --repo JackalopeTechnologies/SnoopMCP <n> --rebase --delete-branch`.
8. `git -C E:/GitHub/SnoopMCP checkout master` → fetch → `reset --hard origin/master`.

## Hard-won conventions (don't re-discover these)

- **Build needs Visual Studio + the C++ x64 workload.** `SnoopMCP.Injection` shells out to the full `MSBuild.exe` (located via vswhere) to build Snoop's **native** `Snoop.GenericInjector.vcxproj`; `dotnet build` alone can't. VS Professional 2026 is installed and works. MSBuild path: `C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe`.
- **CodeStructure.Analyzers 1.0.7 is strict (warnings = errors):** one type per file (STR0008); no magic strings → consts (STY0008); no magic numbers (NUM0002); no null-forgiving `!` (STY0002) — use combined guards like `if (!ok || x is null)`; `ILogger` calls via `[LoggerMessage]` source-gen (CA1848); `ConfigureAwait(false)` on every non-test await (CA2007); switch expressions not if/else chains; single return / no early return / no `continue`; max 3 nesting; field prefixes `m`/`ps`/`sm`. The only grandfathered `out` is `ElementRegistry.TryResolve`. `tests/Directory.Build.props` suppresses xUnit-conflicting rules (CA1707, STY0006, STY0008, NUM0002, STY0002, xUnit1051) under `tests/` only.
- **Transport: HTTP.** Host = ASP.NET Core/Kestrel on `http://127.0.0.1:6300/mcp` (Streamable HTTP, `ModelContextProtocol.AspNetCore` 1.3.0), NOT stdio. MCP client config: `{"type":"http","url":"http://127.0.0.1:6300/mcp"}`.
- **Injection mechanism:** host subprocesses `injector/Snoop.InjectorLauncher.x64.exe` with `--targetPID --assembly payload/SnoopMCP.Payload.dll --className SnoopMCP.Payload.PayloadEntryPoint --methodName Inject --settingsFile <pipeName>`. The pipe name rides in via `--settingsFile`. The payload's `Inject` is a thin bootstrap that installs an `AssemblyResolve` handler (probes the payload dir) before touching its deps. The host stages `injector/` and `payload/` next to its exe; test projects re-stage via copy targets.
- **Run model = per-user logon autostart, NOT a session-0 service.** SnoopMCP injects into the user's interactive-session apps; same-user-same-session is the proven path. A LocalSystem service (like SaddleRAG) is unproven/risky here — rejected. (SaddleRAG can do it only because it never leaves its own process.)
- **Tray apps:** three separate per-product apps, each in its own repo, autostarted. Icons are **rendered at runtime** with `System.Drawing` (letter badge tinted by state) — no hand-drawn `.ico` needed. SnoopMCP's tray app is in this repo and is the A3 autostart presence.
- **Versions:** .NET 10 (`net10.0` libs, `net10.0-windows` WPF/host), `ModelContextProtocol.AspNetCore` 1.3.0, `xunit.v3` 3.2.2, `xunit.runner.visualstudio` 3.1.5, `Microsoft.NET.Test.Sdk` 18.6.0, `Xunit.StaFact` 3.0.13, `Microsoft.Extensions.Logging.Abstractions` 10.0.7 (transitively required by MCP 1.3.0 — don't downgrade), `CodeStructure.Analyzers` 1.0.7. SDK pinned `10.0.0` rollForward latestFeature.
- **Snoop submodule:** `external/snoopwpf` pins fork `JackalopeTechnologies/snoopwpf` branch `snoopmcp` @ `81a5cd6` (== upstream `develop`, Ms-PL, no patches). `.gitmodules` has `ignore = dirty` so build artifacts don't dirty the parent. Clone needs `--recurse-submodules`.
- **SaddleRAG is the reference** for the installer + client registration: `E:/GitHub/SaddleRAG/SaddleRAG.Installer/Package.wxs`, `E:/GitHub/SaddleRAG/SaddleRAG.ClientIntegration/Writers/{VsCodeMcpWriter,ClaudeCodeWriter,ClaudeDesktopWriter}.cs` (+ their tests). Mirror the patterns; swap service→logon-task.

Begin by reading the v1.1 plan, then resume branch `task-34-list-processes` to finish `listWpfProcesses` (Workstream A1).
