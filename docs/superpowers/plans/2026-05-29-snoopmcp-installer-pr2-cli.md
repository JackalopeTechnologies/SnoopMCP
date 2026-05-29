# SnoopMCP Installer — PR 2: CLI + host `/health` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `SnoopMCP.Cli` management exe (`register-clients` / `unregister-clients` / `install-autostart` / `uninstall-autostart` / `status` / `start` / `stop`) that the installer and the user drive, plus a minimal `GET /health` on the host for readiness probing.

**Architecture:** The CLI wraps PR 1's `SnoopMCP.ClientIntegration` writers for client registration, manages a per-user logon scheduled task via `schtasks.exe`, supervises the host process, and probes `http://127.0.0.1:6300/health`. Leaf logic (schtasks argument construction, the writer-aggregation exit codes, the health probe) is unit-tested behind seams; the thin shell-out/process bits are exercised manually. The host gains a one-line `/health` endpoint returning `{ status, version, attached }`.

**Tech Stack:** .NET 10 (`net10.0`), `System.Diagnostics.Process` (schtasks + host process), `System.Net.Http` (health probe), PR 1's `SnoopMCP.ClientIntegration`, xunit.v3.

**Spec:** `docs/superpowers/specs/2026-05-29-snoopmcp-installer-design.md` (§5, §6).

**Refinement of spec §5 (transparent):** the spec said "a `System.CommandLine` console exe." This plan uses a **minimal hand-rolled verb dispatch** (a `switch` on `args[0]` + tiny flag helpers) instead. Rationale: 7 simple verbs with a couple of flags each do not justify pulling in the churning `System.CommandLine` beta API; a hand-rolled dispatcher is dependency-free, robust, and keeps the testable logic in plain methods. The verb set, behavior, and exit codes are exactly as the spec specifies.

---

## Analyzer rules that apply (src/ strict; warnings = errors)
- One public type per file (STR0008). Single return / no early return / no `continue` / no if-else-if chains (use `switch`). Max 3 nesting. Max 120 chars/line.
- Validate public method args at entry (STR0010). `m`/`sm` field prefixes. No magic strings → consts (STY0008). No null-forgiving `!` (STY0002).
- `ConfigureAwait(false)` on every non-test await (CA2007). Typed `catch` (not bare `Exception`; CA1031) — give empty catch bodies an explanatory comment (no inline comments; comment on its own line).
- tests/ suppresses STY0008/NUM0002/STY0002/CA1707/STY0006/xUnit1051; NOT STR0008 / CA1816 (IDisposable test classes call `GC.SuppressFinalize(this)`) / xUnit2013 (use `Assert.Single`, not `Assert.Equal(1, x.Count)`).
- The CLI targets `net10.0` (no WPF). It calls only cross-platform BCL (`Process`, `HttpClient`, `Environment`) so no `[SupportedOSPlatform]`/CA1416 concern. `schtasks.exe` is launched as a process, not via a Windows-only API.

---

## File Structure
- `src/SnoopMCP.Cli/SnoopMCP.Cli.csproj` — net10.0 Exe, AssemblyName `SnoopMCP.Cli`, references `SnoopMCP.ClientIntegration`.
- `src/SnoopMCP.Cli/AutostartTask.cs` — builds + runs the `schtasks` create/delete/query for the logon task.
- `src/SnoopMCP.Cli/ClientRegistration.cs` — aggregates Register/Unregister/Status across an injected writer list; maps to exit codes.
- `src/SnoopMCP.Cli/HostHealthProbe.cs` — GET `/health`, returns reachable bool.
- `src/SnoopMCP.Cli/HostProcess.cs` — resolves the host exe path, starts/stops it.
- `src/SnoopMCP.Cli/Program.cs` — verb dispatch + flag parsing; wires real dependencies.
- `src/SnoopMCP.Host/HealthStatus.cs` — the `/health` payload record + factory.
- `src/SnoopMCP.Host/Program.cs` (modify) — map `GET /health`.
- `tests/SnoopMCP.Cli.Tests/` — `AutostartTaskTests`, `ClientRegistrationTests`, `HostHealthProbeTests`.
- `tests/SnoopMCP.Host.Tests/HealthStatusTests.cs` — the factory test.

---

### Task 1: Scaffold `SnoopMCP.Cli` + verb dispatch skeleton

**Files:**
- Create: `src/SnoopMCP.Cli/SnoopMCP.Cli.csproj`, `src/SnoopMCP.Cli/Program.cs`
- Create: `tests/SnoopMCP.Cli.Tests/SnoopMCP.Cli.Tests.csproj`, `tests/SnoopMCP.Cli.Tests/UsageTests.cs`
- Modify: `SnoopMCP.sln`

- [ ] **Step 1: Library/exe csproj** `src/SnoopMCP.Cli/SnoopMCP.Cli.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <RootNamespace>SnoopMCP.Cli</RootNamespace>
        <AssemblyName>SnoopMCP.Cli</AssemblyName>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\SnoopMCP.ClientIntegration\SnoopMCP.ClientIntegration.csproj" />
    </ItemGroup>
</Project>
```

- [ ] **Step 2: Program with verb dispatch** `src/SnoopMCP.Cli/Program.cs`:

```csharp
// Program.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli;

/// <summary>
/// Management CLI for SnoopMCP: registers the MCP server in LLM clients, manages the per-user logon
/// autostart task, and supervises the host process. Verbs are dispatched from <see cref="Main"/>;
/// each returns a process exit code (0 = success, 2 = partial failure, 64 = usage error).
/// </summary>
public static class Program
{
    private const string VerbRegisterClients = "register-clients";
    private const string VerbUnregisterClients = "unregister-clients";
    private const string VerbInstallAutostart = "install-autostart";
    private const string VerbUninstallAutostart = "uninstall-autostart";
    private const string VerbStatus = "status";
    private const string VerbStart = "start";
    private const string VerbStop = "stop";
    private const int ExitUsage = 64;

    /// <summary>Parses the verb and dispatches; returns the process exit code.</summary>
    /// <param name="args">Command-line arguments; <c>args[0]</c> is the verb.</param>
    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string verb = args.Length > 0 ? args[0] : string.Empty;
        Task<int> dispatched = verb switch
        {
            VerbRegisterClients => Task.FromResult(ExitUsage),
            VerbUnregisterClients => Task.FromResult(ExitUsage),
            VerbInstallAutostart => Task.FromResult(ExitUsage),
            VerbUninstallAutostart => Task.FromResult(ExitUsage),
            VerbStatus => Task.FromResult(ExitUsage),
            VerbStart => Task.FromResult(ExitUsage),
            VerbStop => Task.FromResult(ExitUsage),
            _ => Task.FromResult(PrintUsage())
        };
        return await dispatched.ConfigureAwait(false);
    }

    private static int PrintUsage()
    {
        Console.Error.WriteLine(
            "Usage: SnoopMCP.Cli <register-clients|unregister-clients|install-autostart|"
            + "uninstall-autostart|status|start|stop> [--vscode] [--claude-code]");
        return ExitUsage;
    }
}
```

(Each verb returns `ExitUsage` as a placeholder *exit code* for now — they are wired to real handlers in Tasks 2-4. This is a working skeleton, not a stub-with-TODO: an unknown verb prints usage; known verbs compile and run.)

- [ ] **Step 3: Test csproj** `tests/SnoopMCP.Cli.Tests/SnoopMCP.Cli.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <RootNamespace>SnoopMCP.Cli.Tests</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="xunit.v3" Version="3.2.2" />
        <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.6.0" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\src\SnoopMCP.Cli\SnoopMCP.Cli.csproj" />
    </ItemGroup>
</Project>
```

- [ ] **Step 4: Usage test** `tests/SnoopMCP.Cli.Tests/UsageTests.cs`:

```csharp
// UsageTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli.Tests;

using SnoopMCP.Cli;
using Xunit;

public sealed class UsageTests
{
    [Fact]
    public async Task Main_WithUnknownVerb_ReturnsUsageExitCode()
    {
        int code = await Program.Main(["bogus-verb"]);

        Assert.Equal(64, code);
    }

    [Fact]
    public async Task Main_WithNoArgs_ReturnsUsageExitCode()
    {
        int code = await Program.Main([]);

        Assert.Equal(64, code);
    }
}
```

- [ ] **Step 5: Add both projects to the solution**

`dotnet sln E:/GitHub/SnoopMCP/SnoopMCP.sln add E:/GitHub/SnoopMCP/src/SnoopMCP.Cli/SnoopMCP.Cli.csproj`
then
`dotnet sln E:/GitHub/SnoopMCP/SnoopMCP.sln add E:/GitHub/SnoopMCP/tests/SnoopMCP.Cli.Tests/SnoopMCP.Cli.Tests.csproj`

- [ ] **Step 6: Build + test**

Build: `dotnet build E:/GitHub/SnoopMCP/src/SnoopMCP.Cli/SnoopMCP.Cli.csproj -c Debug -p:TreatWarningsAsErrors=true` → `0 Warning(s) 0 Error(s)`.
Test: `dotnet test E:/GitHub/SnoopMCP/tests/SnoopMCP.Cli.Tests/SnoopMCP.Cli.Tests.csproj -c Debug --nologo` → 2 passed.

- [ ] **Step 7: Commit** (message file E:/tmp/pr2-t1-msg.txt):

```
A3 PR2: scaffold SnoopMCP.Cli + verb dispatch skeleton

New net10.0 exe with a hand-rolled verb dispatcher (register-clients,
unregister-clients, install-autostart, uninstall-autostart, status, start,
stop) returning process exit codes; unknown verb / no args print usage and
return 64. Real handlers land in subsequent tasks. Adds the exe + its test
project to the solution.
```
Stage the 2 src files + 2 test files + sln. Commit with `-F`.

---

### Task 2: `AutostartTask` (schtasks) + `HostProcess` + wire autostart verbs

**Files:**
- Create: `src/SnoopMCP.Cli/AutostartTask.cs`, `src/SnoopMCP.Cli/HostProcess.cs`, `tests/SnoopMCP.Cli.Tests/AutostartTaskTests.cs`
- Modify: `src/SnoopMCP.Cli/Program.cs`

(`HostProcess` — the single source of the host-exe path, plus start/stop — is created here because `install-autostart` needs the host path; the `start`/`stop` verbs in Task 4 reuse it.)

- [ ] **Step 1: Write the failing tests** `tests/SnoopMCP.Cli.Tests/AutostartTaskTests.cs`:

```csharp
// AutostartTaskTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli.Tests;

using SnoopMCP.Cli;
using Xunit;

public sealed class AutostartTaskTests
{
    [Fact]
    public void BuildCreateArguments_IsOnLogonLimitedForcedWithHostPath()
    {
        IReadOnlyList<string> args = AutostartTask.BuildCreateArguments(@"C:\app\SnoopMCP.Host.exe");

        Assert.Equal(
            ["/Create", "/TN", "SnoopMCP Host", "/SC", "ONLOGON", "/RL", "LIMITED",
             "/TR", @"C:\app\SnoopMCP.Host.exe", "/F"],
            args);
    }

    [Fact]
    public void BuildDeleteArguments_TargetsTheTaskWithForce()
    {
        IReadOnlyList<string> args = AutostartTask.BuildDeleteArguments();

        Assert.Equal(["/Delete", "/TN", "SnoopMCP Host", "/F"], args);
    }

    [Fact]
    public void BuildQueryArguments_QueriesTheTask()
    {
        IReadOnlyList<string> args = AutostartTask.BuildQueryArguments();

        Assert.Equal(["/Query", "/TN", "SnoopMCP Host"], args);
    }

    [Fact]
    public void BuildCreateArguments_NullHostPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => AutostartTask.BuildCreateArguments(string.Empty));
    }
}
```

- [ ] **Step 2: Run, verify FAIL** (`AutostartTask` missing):
`dotnet test E:/GitHub/SnoopMCP/tests/SnoopMCP.Cli.Tests/SnoopMCP.Cli.Tests.csproj -c Debug --nologo`

- [ ] **Step 3: Implement `AutostartTask`** `src/SnoopMCP.Cli/AutostartTask.cs`:

```csharp
// AutostartTask.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli;

using System.Diagnostics;

/// <summary>
/// Manages the per-user logon scheduled task that launches the SnoopMCP host. Wraps
/// <c>schtasks.exe</c>: the argument builders are pure (and unit-tested); the create/remove/exists
/// methods shell out. The task runs at logon, non-elevated (<c>/RL LIMITED</c>), matching the
/// per-user run model.
/// </summary>
public static class AutostartTask
{
    private const string SchTasksExe = "schtasks.exe";
    private const string TaskName = "SnoopMCP Host";
    private const string CreateSwitch = "/Create";
    private const string DeleteSwitch = "/Delete";
    private const string QuerySwitch = "/Query";
    private const string TaskNameSwitch = "/TN";
    private const string ScheduleSwitch = "/SC";
    private const string OnLogon = "ONLOGON";
    private const string RunLevelSwitch = "/RL";
    private const string Limited = "LIMITED";
    private const string TaskRunSwitch = "/TR";
    private const string ForceSwitch = "/F";

    /// <summary>Builds the <c>schtasks</c> arguments that create the logon task for the host exe.</summary>
    /// <param name="hostExePath">Absolute path to <c>SnoopMCP.Host.exe</c>.</param>
    public static IReadOnlyList<string> BuildCreateArguments(string hostExePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(hostExePath);
        return
        [
            CreateSwitch, TaskNameSwitch, TaskName, ScheduleSwitch, OnLogon,
            RunLevelSwitch, Limited, TaskRunSwitch, hostExePath, ForceSwitch
        ];
    }

    /// <summary>Builds the <c>schtasks</c> arguments that delete the logon task.</summary>
    public static IReadOnlyList<string> BuildDeleteArguments()
    {
        return [DeleteSwitch, TaskNameSwitch, TaskName, ForceSwitch];
    }

    /// <summary>Builds the <c>schtasks</c> arguments that query the logon task.</summary>
    public static IReadOnlyList<string> BuildQueryArguments()
    {
        return [QuerySwitch, TaskNameSwitch, TaskName];
    }

    /// <summary>Creates (or replaces) the logon task. Returns true on success.</summary>
    /// <param name="hostExePath">Absolute path to <c>SnoopMCP.Host.exe</c>.</param>
    public static bool Create(string hostExePath)
    {
        return Run(BuildCreateArguments(hostExePath)) == 0;
    }

    /// <summary>Removes the logon task. Returns true on success or if it did not exist.</summary>
    public static bool Remove()
    {
        return Run(BuildDeleteArguments()) == 0;
    }

    /// <summary>Reports whether the logon task currently exists.</summary>
    public static bool Exists()
    {
        return Run(BuildQueryArguments()) == 0;
    }

    private static int Run(IReadOnlyList<string> arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = SchTasksExe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }
        int exit;
        using (Process? process = Process.Start(psi))
        {
            if (process is null)
            {
                exit = -1;
            }
            else
            {
                process.WaitForExit();
                exit = process.ExitCode;
            }
        }
        return exit;
    }
}
```

- [ ] **Step 4: Implement `HostProcess`** (the single source of the host-exe path + start/stop). `src/SnoopMCP.Cli/HostProcess.cs`:

```csharp
// HostProcess.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli;

using System.Diagnostics;

/// <summary>
/// Starts and stops the SnoopMCP host process (<c>SnoopMCP.Host.exe</c>), which sits next to this
/// CLI in the install directory. <see cref="ExePath"/> is the one place the host-exe location is
/// resolved. Start is fire-and-forget (the host runs until stopped); Stop kills any running host
/// instances by process name.
/// </summary>
public static class HostProcess
{
    private const string HostExeName = "SnoopMCP.Host.exe";
    private const string HostProcessName = "SnoopMCP.Host";

    /// <summary>Absolute path to the host exe alongside this CLI.</summary>
    public static string ExePath()
    {
        return Path.Combine(AppContext.BaseDirectory, HostExeName);
    }

    /// <summary>Launches the host. Returns true if a process was started.</summary>
    public static bool Start()
    {
        var psi = new ProcessStartInfo
        {
            FileName = ExePath(),
            UseShellExecute = false
        };
        using Process? process = Process.Start(psi);
        return process is not null;
    }

    /// <summary>Stops any running host instances. Returns the count stopped.</summary>
    public static int Stop()
    {
        Process[] running = Process.GetProcessesByName(HostProcessName);
        int stopped = 0;
        foreach (Process process in running)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            process.Dispose();
            stopped++;
        }
        return stopped;
    }
}
```

- [ ] **Step 5: Wire the autostart verbs into `Program`.** Replace the two placeholder arms:
```csharp
            VerbInstallAutostart => Task.FromResult(InstallAutostart()),
            VerbUninstallAutostart => Task.FromResult(UninstallAutostart()),
```
and add these members (the host path comes from `HostProcess.ExePath()` — the one resolver):
```csharp
    private const int ExitOk = 0;
    private const int ExitFailure = 2;

    private static int InstallAutostart()
    {
        bool ok = AutostartTask.Create(HostProcess.ExePath());
        Console.WriteLine(ok ? "Autostart task created." : "Failed to create autostart task.");
        return ok ? ExitOk : ExitFailure;
    }

    private static int UninstallAutostart()
    {
        bool ok = AutostartTask.Remove();
        Console.WriteLine(ok ? "Autostart task removed." : "Failed to remove autostart task.");
        return ok ? ExitOk : ExitFailure;
    }
```

- [ ] **Step 6: Run tests, verify PASS** (4 AutostartTask + 2 usage = 6):
`dotnet test E:/GitHub/SnoopMCP/tests/SnoopMCP.Cli.Tests/SnoopMCP.Cli.Tests.csproj -c Debug --nologo`

- [ ] **Step 7: Build zero-warning**
`dotnet build E:/GitHub/SnoopMCP/src/SnoopMCP.Cli/SnoopMCP.Cli.csproj -c Debug -p:TreatWarningsAsErrors=true`

- [ ] **Step 8: Commit** (E:/tmp/pr2-t2-msg.txt):
```
A3 PR2: AutostartTask (schtasks logon task) + HostProcess + autostart verbs

Pure argument builders for schtasks Create/Delete/Query (ONLOGON, LIMITED, /F)
plus thin Create/Remove/Exists that shell out. HostProcess is the single source
of the host-exe path (+ start/stop, used by Task 4's verbs). install-autostart /
uninstall-autostart verbs wired to launch the host exe next to the CLI. Tests
assert the exact argument vectors.
```
Stage AutostartTask.cs, HostProcess.cs, AutostartTaskTests.cs, Program.cs. Commit `-F`.

---

### Task 3: `ClientRegistration` + wire client verbs

**Files:**
- Create: `src/SnoopMCP.Cli/ClientRegistration.cs`, `tests/SnoopMCP.Cli.Tests/ClientRegistrationTests.cs`
- Modify: `src/SnoopMCP.Cli/Program.cs`

- [ ] **Step 1: Write the failing tests** `tests/SnoopMCP.Cli.Tests/ClientRegistrationTests.cs`:

```csharp
// ClientRegistrationTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli.Tests;

using SnoopMCP.ClientIntegration;
using SnoopMCP.Cli;
using Xunit;

public sealed class ClientRegistrationTests : IDisposable
{
    private readonly string mDir;
    private readonly IReadOnlyList<IClientWriter> mWriters;

    public ClientRegistrationTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mWriters =
        [
            new ClaudeCodeWriter(Path.Combine(mDir, ".claude.json")),
            new VsCodeMcpWriter(Path.Combine(mDir, "mcp.json"))
        ];
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RegisterAll_RegistersEveryWriter_AndReturnsOk()
    {
        using var log = new StringWriter();

        int code = ClientRegistration.RegisterAll(mWriters, McpEndpoint.Default, log);

        Assert.Equal(0, code);
        Assert.All(mWriters, w => Assert.True(w.GetStatus().IsRegistered));
    }

    [Fact]
    public void UnregisterAll_RemovesFromEveryWriter()
    {
        using var log = new StringWriter();
        ClientRegistration.RegisterAll(mWriters, McpEndpoint.Default, log);

        int code = ClientRegistration.UnregisterAll(mWriters, log);

        Assert.Equal(0, code);
        Assert.All(mWriters, w => Assert.False(w.GetStatus().IsRegistered));
    }

    [Fact]
    public void Status_ReportsEachWriter()
    {
        using var log = new StringWriter();
        mWriters[0].Register(McpEndpoint.Default);

        int code = ClientRegistration.Status(mWriters, log);

        Assert.Equal(0, code);
        string output = log.ToString();
        Assert.Contains("Claude Code", output);
        Assert.Contains("VS Code", output);
    }
}
```

- [ ] **Step 2: Run, verify FAIL** (`ClientRegistration` missing).

- [ ] **Step 3: Implement `ClientRegistration`** `src/SnoopMCP.Cli/ClientRegistration.cs`:

```csharp
// ClientRegistration.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli;

using SnoopMCP.ClientIntegration;

/// <summary>
/// Drives a set of <see cref="IClientWriter"/> over register / unregister / status, logging each
/// outcome and collapsing the per-writer results into a process exit code (0 = all succeeded,
/// 2 = at least one failed).
/// </summary>
public static class ClientRegistration
{
    private const int ExitOk = 0;
    private const int ExitPartialFailure = 2;

    /// <summary>Registers the endpoint in every writer.</summary>
    public static int RegisterAll(IReadOnlyList<IClientWriter> writers, McpEndpoint endpoint, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(log);
        int failures = 0;
        foreach (IClientWriter writer in writers)
        {
            RegisterResult result = writer.Register(endpoint);
            log.WriteLine($"{writer.ClientName}: {result.Message}");
            failures += result.Success ? 0 : 1;
        }
        return failures == 0 ? ExitOk : ExitPartialFailure;
    }

    /// <summary>Removes the SnoopMCP entry from every writer.</summary>
    public static int UnregisterAll(IReadOnlyList<IClientWriter> writers, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(log);
        int failures = 0;
        foreach (IClientWriter writer in writers)
        {
            UnregisterResult result = writer.Unregister();
            log.WriteLine($"{writer.ClientName}: {result.Message}");
            failures += result.Success ? 0 : 1;
        }
        return failures == 0 ? ExitOk : ExitPartialFailure;
    }

    /// <summary>Logs each writer's registration status. Always returns success.</summary>
    public static int Status(IReadOnlyList<IClientWriter> writers, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(log);
        foreach (IClientWriter writer in writers)
        {
            StatusResult result = writer.GetStatus();
            log.WriteLine($"{writer.ClientName}: {result.Message}");
        }
        return ExitOk;
    }
}
```

- [ ] **Step 4: Wire the client verbs into `Program`.** Replace the two placeholder arms:
```csharp
            VerbRegisterClients => Task.FromResult(RegisterClients(args)),
            VerbUnregisterClients => Task.FromResult(UnregisterClients(args)),
```
and add these members (writer selection honors optional `--vscode` / `--claude-code`; if neither is given, both are used):
```csharp
    private const string FlagVsCode = "--vscode";
    private const string FlagClaudeCode = "--claude-code";

    private static IReadOnlyList<IClientWriter> SelectWriters(string[] args)
    {
        bool wantVsCode = HasFlag(args, FlagVsCode);
        bool wantClaude = HasFlag(args, FlagClaudeCode);
        bool both = !wantVsCode && !wantClaude;
        var writers = new List<IClientWriter>();
        if (wantClaude || both)
        {
            writers.Add(ClaudeCodeWriter.ForCurrentUser());
        }
        if (wantVsCode || both)
        {
            writers.Add(VsCodeMcpWriter.ForCurrentUser());
        }
        return writers;
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return Array.Exists(args, a => string.Equals(a, flag, StringComparison.Ordinal));
    }

    private static int RegisterClients(string[] args)
    {
        return ClientRegistration.RegisterAll(SelectWriters(args), McpEndpoint.Default, Console.Out);
    }

    private static int UnregisterClients(string[] args)
    {
        return ClientRegistration.UnregisterAll(SelectWriters(args), Console.Out);
    }
```
Add `using SnoopMCP.ClientIntegration;` to `Program.cs`'s usings.

- [ ] **Step 5: Run tests, verify PASS** (3 + 4 + 2 = 9).

- [ ] **Step 6: Build zero-warning.**

- [ ] **Step 7: Commit** (E:/tmp/pr2-t3-msg.txt):
```
A3 PR2: ClientRegistration + register/unregister verbs

Aggregates the ClientIntegration writers over register/unregister/status,
logging each outcome and collapsing to exit 0 (all ok) or 2 (any failed).
register-clients / unregister-clients verbs wired with optional --vscode /
--claude-code selection (both when neither given). Tests run real writers
against temp config files.
```
Stage ClientRegistration.cs, ClientRegistrationTests.cs, Program.cs. Commit `-F`.

---

### Task 4: `HostHealthProbe` + wire status/start/stop

**Files:**
- Create: `src/SnoopMCP.Cli/HostHealthProbe.cs`, `tests/SnoopMCP.Cli.Tests/HostHealthProbeTests.cs`
- Modify: `src/SnoopMCP.Cli/Program.cs`

(`HostProcess` already exists from Task 2; the `start`/`stop` verbs reuse it.)

- [ ] **Step 1: Write the failing tests** `tests/SnoopMCP.Cli.Tests/HostHealthProbeTests.cs`:

```csharp
// HostHealthProbeTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli.Tests;

using System.Net;
using SnoopMCP.Cli;
using Xunit;

public sealed class HostHealthProbeTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> mResponder;

        public StubHandler(Func<HttpResponseMessage> responder)
        {
            mResponder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(mResponder());
        }
    }

    [Fact]
    public async Task IsHealthyAsync_On200_ReturnsTrue()
    {
        using var client = new HttpClient(new StubHandler(() => new HttpResponseMessage(HttpStatusCode.OK)));

        bool healthy = await HostHealthProbe.IsHealthyAsync(client, "http://127.0.0.1:6300/health", default);

        Assert.True(healthy);
    }

    [Fact]
    public async Task IsHealthyAsync_On500_ReturnsFalse()
    {
        using var client = new HttpClient(
            new StubHandler(() => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        bool healthy = await HostHealthProbe.IsHealthyAsync(client, "http://127.0.0.1:6300/health", default);

        Assert.False(healthy);
    }

    [Fact]
    public async Task IsHealthyAsync_OnConnectionRefused_ReturnsFalse()
    {
        using var client = new HttpClient(
            new StubHandler(() => throw new HttpRequestException("refused")));

        bool healthy = await HostHealthProbe.IsHealthyAsync(client, "http://127.0.0.1:6300/health", default);

        Assert.False(healthy);
    }
}
```

- [ ] **Step 2: Run, verify FAIL** (`HostHealthProbe` missing).

- [ ] **Step 3: Implement `HostHealthProbe`** `src/SnoopMCP.Cli/HostHealthProbe.cs`:

```csharp
// HostHealthProbe.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli;

/// <summary>
/// Probes the host's <c>/health</c> endpoint over HTTP. A 2xx response means the host is up; a
/// connection failure or timeout means it is not (reported as unhealthy, never thrown).
/// </summary>
public static class HostHealthProbe
{
    /// <summary>The host's health URL on localhost.</summary>
    public const string HealthUrl = "http://127.0.0.1:6300/health";

    /// <summary>Returns true when the host answers <paramref name="healthUrl"/> with a success status.</summary>
    public static async Task<bool> IsHealthyAsync(HttpClient client, string healthUrl, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrEmpty(healthUrl);
        bool healthy = false;
        try
        {
            HttpResponseMessage response = await client.GetAsync(healthUrl, ct).ConfigureAwait(false);
            healthy = response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            // Host not listening → unhealthy, not an error.
        }
        catch (TaskCanceledException)
        {
            // Probe timed out → unhealthy, not an error.
        }
        return healthy;
    }
}
```

- [ ] **Step 4: Wire status/start/stop into `Program`.** Replace the three placeholder arms:
```csharp
            VerbStatus => StatusAsync(),
            VerbStart => Task.FromResult(Start()),
            VerbStop => Task.FromResult(Stop()),
```
and add the members (the `status` verb aggregates clients + autostart presence + host reachability; it sweeps both writers):
```csharp
    private static async Task<int> StatusAsync()
    {
        ClientRegistration.Status(SelectWriters([]), Console.Out);
        Console.WriteLine(AutostartTask.Exists() ? "Autostart task: present." : "Autostart task: absent.");
        using var client = new HttpClient();
        bool healthy = await HostHealthProbe
            .IsHealthyAsync(client, HostHealthProbe.HealthUrl, default)
            .ConfigureAwait(false);
        Console.WriteLine(healthy ? "Host: reachable on :6300." : "Host: not reachable.");
        return ExitOk;
    }

    private static int Start()
    {
        bool started = HostProcess.Start();
        Console.WriteLine(started ? "Host started." : "Host failed to start.");
        return started ? ExitOk : ExitFailure;
    }

    private static int Stop()
    {
        int stopped = HostProcess.Stop();
        Console.WriteLine($"Stopped {stopped} host process(es).");
        return ExitOk;
    }
```
(`SelectWriters([])` returns both writers for the status sweep. `status` takes no flags in v1.1 — a machine-readable `--json` mode is out of scope; the CLI tolerates and ignores any unrecognized flag the installer may pass, e.g. `--quiet`/`--log-file`.)

- [ ] **Step 5: Run tests, verify PASS** (3 probe + prior 9 = 12).

- [ ] **Step 6: Build zero-warning.**

- [ ] **Step 7: Commit** (E:/tmp/pr2-t4-msg.txt):
```
A3 PR2: HostHealthProbe + status/start/stop verbs

HostHealthProbe GETs /health (success=up; connection failure/timeout=down,
never throws), tested with a stub HttpMessageHandler. status reports clients +
autostart presence + host reachability; start/stop supervise the host via
HostProcess (added in the previous task).
```
Stage HostHealthProbe.cs, HostHealthProbeTests.cs, Program.cs. Commit `-F`.

---

### Task 5: Host `GET /health`

**Files:**
- Create: `src/SnoopMCP.Host/HealthStatus.cs`, `tests/SnoopMCP.Host.Tests/HealthStatusTests.cs`
- Modify: `src/SnoopMCP.Host/Program.cs`

- [ ] **Step 1: Write the failing test** `tests/SnoopMCP.Host.Tests/HealthStatusTests.cs`:

```csharp
// HealthStatusTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host.Tests;

using SnoopMCP.Host;
using Xunit;

public sealed class HealthStatusTests
{
    [Fact]
    public void Create_SetsOkStatus_AndPassesThroughVersionAndAttached()
    {
        HealthStatus health = HealthStatus.Create("1.2.3", attached: true);

        Assert.Equal("ok", health.Status);
        Assert.Equal("1.2.3", health.Version);
        Assert.True(health.Attached);
    }

    [Fact]
    public void Create_NullVersion_Throws()
    {
        Assert.Throws<ArgumentException>(() => HealthStatus.Create(string.Empty, attached: false));
    }
}
```

- [ ] **Step 2: Run, verify FAIL** (`HealthStatus` missing):
`dotnet test E:/GitHub/SnoopMCP/tests/SnoopMCP.Host.Tests/SnoopMCP.Host.Tests.csproj -c Debug --nologo`

- [ ] **Step 3: Implement `HealthStatus`** `src/SnoopMCP.Host/HealthStatus.cs`:

```csharp
// HealthStatus.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

/// <summary>
/// The <c>/health</c> response: a liveness marker, the host's informational version, and whether a
/// target is currently attached. Serialised to JSON by the endpoint.
/// </summary>
/// <param name="Status">Always <c>ok</c> when the host is serving requests.</param>
/// <param name="Version">The host's informational version.</param>
/// <param name="Attached">True when a WPF target session is open.</param>
public sealed record HealthStatus(string Status, string Version, bool Attached)
{
    private const string OkStatus = "ok";

    /// <summary>Creates an <c>ok</c> health status for the given version and attach state.</summary>
    public static HealthStatus Create(string version, bool attached)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        return new HealthStatus(OkStatus, version, attached);
    }
}
```

- [ ] **Step 4: Map the endpoint in the host `Program`.** In `src/SnoopMCP.Host/Program.cs`, add the const and the `MapGet` after the existing `app.MapMcp(...)`:

Add to the constants block:
```csharp
    private const string HealthEndpointPattern = "/health";
```
Add after `app.MapMcp(McpEndpointPattern);`:
```csharp
        app.MapGet(HealthEndpointPattern, (SessionManager session) =>
            Results.Ok(HealthStatus.Create(ThisAssembly.InformationalVersion, session.IsAttached)));
```
(`SessionManager` is already a registered singleton; `Results` is in `Microsoft.AspNetCore.Http`, available via the Web SDK implicit usings — if the build flags it, add `using Microsoft.AspNetCore.Http;`.)

- [ ] **Step 5: Run the host test, verify PASS** (2 new HealthStatus tests + the 7 existing Host tests = 9):
`dotnet test E:/GitHub/SnoopMCP/tests/SnoopMCP.Host.Tests/SnoopMCP.Host.Tests.csproj -c Debug --nologo`

- [ ] **Step 6: Build the host zero-warning**
`dotnet build E:/GitHub/SnoopMCP/src/SnoopMCP.Host/SnoopMCP.Host.csproj -c Debug -p:TreatWarningsAsErrors=true`

- [ ] **Step 7: Commit** (E:/tmp/pr2-t5-msg.txt):
```
A3 PR2: host GET /health endpoint

Adds a minimal /health returning { status:"ok", version, attached } for the CLI
status probe and the installer readiness gate (/mcp is POST/SSE and awkward to
liveness-probe). HealthStatus record + factory unit-tested; the endpoint maps it
from the SessionManager singleton.
```
Stage HealthStatus.cs, HealthStatusTests.cs, Program.cs. Commit `-F`.

---

### Task 6: Finalize PR 2

**Files:** none (verification + PR).

- [ ] **Step 1: Full solution build (zero-warning)**
`dotnet build E:/GitHub/SnoopMCP/SnoopMCP.sln -c Debug -p:TreatWarningsAsErrors=true` → `0 Warning(s) 0 Error(s)`.

- [ ] **Step 2: Full test suite.** Run each project `--no-build`: Protocol (5), Host (now 9), Payload (149), IntegrationTests (2 passed / 1 skipped), ClientIntegration (16), Cli (12). Expected: **193 passed, 1 skipped**.

- [ ] **Step 3: Smoke-check the CLI usage path** (no host needed):
`dotnet run --project E:/GitHub/SnoopMCP/src/SnoopMCP.Cli/SnoopMCP.Cli.csproj -c Debug -- status`
Expected: prints client status lines, "Autostart task: absent." (unless previously installed), and "Host: not reachable." (unless the host is running); exit 0.

- [ ] **Step 4: Push, status, PR**
```text
git -C E:/GitHub/SnoopMCP push -u origin <branch>
git -C E:/GitHub/SnoopMCP rev-parse <branch>
gh api -X POST repos/JackalopeTechnologies/SnoopMCP/statuses/<sha> --field state=success --field context=build --field description="local build green; 193 pass, 1 skipped"
gh pr create --repo JackalopeTechnologies/SnoopMCP --base master --head <branch> --title "A3 PR2: SnoopMCP.Cli + host /health" --body-file <file>
```
Then STOP for user review before merging.

---

## Notes for the executor
- **One Bash command per call. No `&&`/`;`/`|`. No `cd` — use `git -C E:/GitHub/SnoopMCP`. Commits via `-F <msgfile>` (write to `E:/tmp/...`), never `-m`. No AI attribution. No hook-bypass flags.**
- Collection-expression syntax (`[...]`) for `IReadOnlyList<string>` returns and test `Assert.Equal([...], actual)` is C# 12+ and fine on net10.0.
- The schtasks/Process/HttpClient shell-outs are not unit-tested by design — the pure builders (`AutostartTask.Build*Arguments`), the aggregation exit codes (`ClientRegistration`), and the probe (`HostHealthProbe` with a stub handler) are. The shell-out wrappers are exercised by the Task 6 smoke check and PR 3's installer verification.
- If an analyzer rule trips on provided code (e.g., an unused `FlagJson`/`args`, or a missing `using`), fix minimally in keeping with the rule — the logic is what matters.
- Branch from a master that already has PR 1 (the writers) merged.
