# SnoopMCP — Tray App Design (Workstream B)

**Date:** 2026-05-29
**Status:** Approved in brainstorming; ready for the implementation-planning pass.
**Decision sought:** (already granted) — merged single-process WPF tray app hosting Kestrel in-process; hand-rolled `Shell_NotifyIcon` (no WinForms, no NuGet); supplied `icon.ico` for exe + tray; menu Start/Stop/Status/Exit with no "Open UI" until the monitor UI ships; exe name kept as `SnoopMCP.Host.exe`.

---

## 1. Goal

Turn SnoopMCP into a self-contained Windows **system-tray application**. Today `SnoopMCP.Host` is a console-subsystem ASP.NET Core exe, so a console window appears whenever it runs (at logon via the autostart task, or via the installer's launch checkbox). The tray app removes that window, gives the user a persistent presence in the notification area, and lets them start/stop the MCP server and exit the whole thing from a tray menu.

The MCP server is **not** split into a separate process. The tray app **is** the host: one process, one exe (`SnoopMCP.Host.exe`), hosting Kestrel/MCP in-process. "Start/Stop MCP" toggles the in-process listener; "Exit" tears everything down.

---

## 2. Locked decisions (from this brainstorm — do not relitigate)

1. **Merged single process.** One WPF `WinExe` self-hosts the existing Kestrel/MCP server in-process. No child process, no IPC, no separate `SnoopMCP.Tray.exe`.
2. **WPF, not WinForms.** UI framework is WPF. The tray icon is hand-rolled on Win32 `Shell_NotifyIcon` via a WPF `HwndSource` message window — **no `System.Windows.Forms` dependency and no third-party NuGet** (e.g. no Hardcodet/H.NotifyIcon).
3. **Supplied icon.** `src/SnoopMCP.Host/Images/icon.ico` (added 2026-05-29) is used for both the exe icon (`<ApplicationIcon>`) and the tray `HICON`. This replaces the previously-planned runtime-rendered "S" state badge; server state is surfaced through the tooltip and menu text instead.
4. **Menu:** Start MCP / Stop MCP / Status / Exit. **No "Open UI" item** until the monitor UI exists (then it is added to launch the browser).
5. **Exe name unchanged** — `SnoopMCP.Host.exe`. The logon task, the installer's "Launch SnoopMCP now" checkbox, and `util:CloseApplication` keep targeting it with no rename churn.
6. **Project split** for testability: a new `SnoopMCP.Server` class library owns the web/MCP/tools/injection wiring; `SnoopMCP.Host` becomes the thin WPF shell.

Carried from the v1.1 plan and still in force: **HTTP transport** at `http://127.0.0.1:6300/mcp`; `/health` retained; **per-user logon autostart, not a service**; **x64 only**; subprocess injector + payload `AssemblyResolve` bootstrap, with `injector/` + `payload/` staged next to the exe.

---

## 3. Architecture

```
SnoopMCP.Host.exe  (WPF WinExe, STA, ShutdownMode=OnExplicitShutdown)
│
├── App (OnStartup): single-instance mutex → build + StartAsync the server → create tray icon
│
├── Tray shell (Win32)            ├── In-process server (SnoopMCP.Server)
│   • hidden top-level HwndSource │   • WebApplication (Kestrel, 127.0.0.1:6300)
│   • Shell_NotifyIcon (v4)       │   • MapMcp("/mcp"), MapGet("/health")
│   • WPF ContextMenu             │   • SessionManager, InjectorService, McpTools (20 tools)
│   • state → tooltip / enablement│   • host↔payload named pipe (PipeClient) unchanged
│
└── App (OnExit / SessionEnding): StopAsync → NIM_DELETE → DestroyIcon → Shutdown
```

WPF owns the STA main thread and its Dispatcher. The `WebApplication` is built once and driven with `StartAsync()` / `StopAsync()` (never the blocking `RunAsync()`), so the UI thread stays responsive and the listener can be toggled without exiting the process. All existing server internals (the 20 read-only tools, attach/detach, the named-pipe payload channel) are unchanged — they just live in a library now and run inside the tray process.

---

## 4. Project restructure

### New: `src/SnoopMCP.Server/SnoopMCP.Server.csproj`
`Microsoft.NET.Sdk`, `net10.0-windows`, x64, `<FrameworkReference Include="Microsoft.AspNetCore.App"/>`, `PackageReference ModelContextProtocol.AspNetCore`. References `SnoopMCP.Protocol`. Contains the files moved from `SnoopMCP.Host`:

- `Tools/McpTools.cs`, `SessionManager.cs`, `HealthStatus.cs`, `ThisAssembly.cs`
- `IInjectorService.cs`, `NullInjectorService.cs`, `ProcessProbeResult.cs`, `PipeClient.cs`
- `Injection/` — `InjectorService.cs`, `ProcessProbe.cs`, `ProcessEnumerator.cs`, `InjectorServiceLog.cs`

Add one new type:

```csharp
public static class ServerHost
{
    // Builds the Kestrel/MCP WebApplication (localhost:6300, /mcp + /health) WITHOUT starting it.
    public static WebApplication Build(string[] args);
}
```

`ServerHost.Build` contains exactly today's `Program.Main` body (Kestrel `ListenLocalhost(6300)`, the `AddSingleton` registrations, `AddMcpServer(...).WithHttpTransport(stateless).WithToolsFromAssembly(...)`, `MapMcp`, `MapGet /health`) but returns the built `WebApplication` instead of `RunAsync()`-ing it.

> **Important:** `WithToolsFromAssembly()` currently scans the entry assembly. After the move, the entry assembly is the WPF Host, so this must become `WithToolsFromAssembly(typeof(McpTools).Assembly)` to scan `SnoopMCP.Server`.

### Changed: `src/SnoopMCP.Host/SnoopMCP.Host.csproj`
- SDK `Microsoft.NET.Sdk.Web` → **`Microsoft.NET.Sdk`**; add `<OutputType>WinExe</OutputType>`, `<UseWPF>true</UseWPF>`, `<FrameworkReference Include="Microsoft.AspNetCore.App"/>`. The plan's first build verifies this dual-shared-framework combination (`Microsoft.WindowsDesktop.App` via `UseWPF` alongside `Microsoft.AspNetCore.App`) on net10 — the documented way to self-host Kestrel inside a WPF app; `WebApplication.CreateBuilder` comes from the AspNetCore shared framework.
- Keep `net10.0-windows`, `PlatformTarget=x64`.
- `<ApplicationIcon>Images\icon.ico</ApplicationIcon>` (exe icon) and `<Content Include="Images\icon.ico"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>` (so the tray can `LoadImage` it at runtime).
- **Reference** `..\SnoopMCP.Server\SnoopMCP.Server.csproj`.
- **Keep** the build-order-only `ReferenceOutputAssembly=false` references to `SnoopMCP.Injection` and `SnoopMCP.Payload`, and **keep** the `CopyInjectorBinaries` / `CopyPayloadBinaries` AfterTargets — the injector/ and payload/ folders must continue to stage next to the **exe** (the injector subprocess and payload are resolved relative to `AppContext.BaseDirectory`, which is the exe's directory).
- Remove `Program.cs`.

### Changed: `tests/SnoopMCP.Host.Tests`
Retarget the reference from `SnoopMCP.Host` to `SnoopMCP.Server` (the types under test — `SessionManager`, `ProcessEnumerator`, `HealthStatus`, `PipeClient` — move there). `IntegrationTests` likewise build the server via `ServerHost.Build` instead of the old `Program`. No behavioral change expected; this is a reference + namespace retarget. (Consider renaming the test project to `SnoopMCP.Server.Tests` in the plan; optional.)

---

## 5. WPF application shell (`SnoopMCP.Host`)

- `App.xaml` (`ApplicationDefinition`, no `StartupUri`) + `App.xaml.cs`. `ShutdownMode = OnExplicitShutdown` — there is no main window; the tray controls process lifetime. The generated `[STAThread] Main` is the entry point (no hand-written `Main`).
- **`OnStartup`:**
  1. **Single instance** — `new Mutex(initiallyOwned: true, @"Local\SnoopMCP.Host", out bool createdNew)`. If `!createdNew`, the app exits immediately (autostart + a manual launch must not double-bind 6300).
  2. **Start server** — `mApp = ServerHost.Build(e.Args); await mApp.StartAsync()`. Started on the Dispatcher with a continuation that sets state to `Running` on success or `Error` on failure (e.g. port already bound).
  3. **Tray** — create the hidden message window + add the notify icon (§6).
- **Shutdown path** (Exit command, and `Application.SessionEnding` for logoff/shutdown): `mApp.StopAsync(timeout)` → `Shell_NotifyIcon(NIM_DELETE)` → `DestroyIcon` → dispose `HwndSource` → release mutex → `Application.Current.Shutdown()`.

---

## 6. Tray icon (hand-rolled Win32)

No WinForms. A hidden, never-shown **top-level** window provides the HWND that receives the tray callbacks and the taskbar-recreate broadcast:

- **HWND source:** a WPF `HwndSource` with no parent (top-level, 0×0, not shown) and a message hook. *(Not `HWND_MESSAGE` message-only — message-only windows do not receive the `WM_TASKBARCREATED` broadcast.)*
- **Notify icon:** `NOTIFYICONDATA` with `uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP`, `uCallbackMessage = WM_TRAYICON` (`WM_APP + 1`), `hIcon` from the supplied `.ico`, `szTip` = state text. `Shell_NotifyIcon(NIM_ADD)` then `NIM_SETVERSION` to `NOTIFYICON_VERSION_4` (modern message semantics). State changes update via `NIM_MODIFY`.
- **Icon load (no System.Drawing):** `LoadImage(IntPtr.Zero, "<base>\Images\icon.ico", IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE)` → `HICON`, where `<base>` is `AppContext.BaseDirectory`. `DestroyIcon` on shutdown.
- **Callbacks (hook on `WM_TRAYICON`):** right-click / context menu → show the WPF `ContextMenu`; with no owner window we call `SetForegroundWindow(hwnd)` before opening and `PostMessage(hwnd, WM_NULL, 0, 0)` after, the standard fix so the menu dismisses on outside-click.
- **Explorer restart:** register `WM_TASKBARCREATED = RegisterWindowMessage("TaskbarCreated")` and re-issue `NIM_ADD` when it arrives, so the icon survives an Explorer crash/restart.
- **Menu (WPF `ContextMenu`, MVVM `ICommand`s):**
  - **Start MCP** — enabled when `Stopped`/`Error`; runs `StartAsync`.
  - **Stop MCP** — enabled when `Running`; runs `StopAsync` (frees :6300; process stays alive).
  - **Status** — shows a balloon (`NIM_MODIFY` + `NIF_INFO`) with URL, state, and attached PID (read in-proc from `SessionManager.IsAttached`).
  - **Exit** — full shutdown (§5).

---

## 7. Server lifecycle & state model

A small **plain, WPF-free** presenter holds the state machine so it is unit-testable:

```csharp
public enum ServerState { Stopped, Starting, Running, Error }
```

- **Start:** `Stopped`/`Error` → `Starting` → (`StartAsync` ok) `Running` | (bind/throw) `Error`.
- **Stop:** `Running` → `StopAsync` → `Stopped`.
- **Error** (e.g. port 6300 already in use): caught, state `Error`, balloon shown, tray stays up so the user can retry Start or Exit. The single-instance mutex prevents a second tray; a stale external listener on 6300 is the realistic error case.
- The presenter maps `ServerState` → tooltip string and per-command `CanExecute`. Because the server is in-process, state is read directly (no HTTP self-poll); `/health` remains for external callers (CLI `status`, installer readiness gate).

---

## 8. Installer / CLI impact

The exe name is unchanged, so the bulk of A3 keeps working as-is:

- **Logon task** (`schtasks ... /TR SnoopMCP.Host.exe`): unchanged. Now launches a `WinExe` → **no console flash at logon** (a side benefit).
- **Launch-on-exit checkbox** (`WixShellExec [INSTALLFOLDER]SnoopMCP.Host.exe`): unchanged.
- **`util:CloseApplication Target="SnoopMCP.Host.exe"`**: keep `TerminateProcess` (hard terminate) as the shipping behavior for upgrades/uninstall — unchanged from today. *(Now that the process has a hidden top-level window, a graceful `WM_CLOSE` path is possible later; out of scope here.)*
- **CLI semantics shift (document):** `SnoopMCP.Cli stop` (via `HostProcess.Stop()`) now stops the **whole app** (kills the tray + server), not just a listener; `start` launches the tray app. The CLI's own behavior is unchanged; only the meaning of "the host process" widens. `HostProcess.Start/Stop/ExePath` need no code change (same exe path).

No installer code changes are required for the tray app to ship; the follow-ups above are documentation/notes. A separate later task may switch `CloseApplication` to graceful shutdown.

---

## 9. Testing

- **Server library (retargeted existing tests):** `SessionManagerTests`, `ProcessEnumeratorTests`, `HealthStatusTests`, `PipeClientTests`, and the integration suite build via `ServerHost.Build` and exercise `/health` + the tools exactly as before. Goal: green with only reference/namespace edits.
- **New unit tests (tray logic, WPF-free):** `ServerState` transitions for Start/Stop/Error; presenter mapping of state → tooltip text and command `CanExecute`. The Win32 glue (`Shell_NotifyIcon`, `HwndSource`, `LoadImage`) stays thin and is covered by manual verification.
- **Manual verification checklist:** tray icon shows the supplied `.ico`; right-click menu works; **Start/Stop** toggles reachability of `http://127.0.0.1:6300/health` while the icon stays present; **Status** balloon reports state + attached PID; **Exit** removes the icon and frees the port; launching a second instance no-ops (single instance); logon autostart shows the tray with **no console window**; killing `explorer.exe` and letting it restart re-adds the icon.

---

## 10. Out of scope (recorded so we don't drift)

- **Monitor UI** and the **"Open UI"** menu item — added when the Blazor/web monitor ships.
- **State-colored / overlay tray icon** — replaced by the static supplied `.ico`; revisit if desired.
- **Graceful `WM_CLOSE` for `CloseApplication`** — keep hard terminate for now.
- **Elevated-target restart** — still deferred (documented v1.1 limitation).
- **SaddleRAG / AeroCoder tray apps** — their own repos. This implementation should keep the state model + Win32 tray glue cohesive enough to extract into a shared skeleton later, but no extraction now.

---

## 11. Acceptance criteria

1. `SnoopMCP.Host.exe` runs as a WPF tray app with **no console window**; the supplied `icon.ico` is the exe icon and the tray icon.
2. The server runs in-process on `http://127.0.0.1:6300` (`/mcp` + `/health`); all 20 tools and attach/detach behave exactly as before.
3. Tray menu **Start MCP / Stop MCP / Status / Exit** behaves per §6–§7; Stop frees the port without exiting; Exit tears everything down and removes the icon.
4. Single instance enforced; icon survives an Explorer restart.
5. `SnoopMCP.Server` library + thin `SnoopMCP.Host` shell; the existing server/integration tests pass after retargeting; new tray-logic unit tests pass.
6. Solution builds zero-warning; logon autostart launches the tray cleanly; the installer (unchanged) still installs/upgrades/uninstalls correctly.
7. Implementation follows the Penske C# standards (single-return / variable pattern, `switch` over if/else chains, no `continue`, regions for grouped members, `m`/`sm`/`ps` field prefixes, lambda logging, Allman braces).
