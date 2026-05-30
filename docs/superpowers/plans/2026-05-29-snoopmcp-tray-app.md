# SnoopMCP Tray App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn `SnoopMCP.Host` into a single-process WPF system-tray app that self-hosts the existing Kestrel/MCP server in-process, with no console window, started/stopped/exited from the tray.

**Architecture:** Extract the web/MCP/tools/injection code into a new `SnoopMCP.Server` class library (`ServerHost.Build` returns a configured-but-unstarted `WebApplication`). `SnoopMCP.Host` becomes a thin WPF `WinExe` shell: an `App` that enforces single-instance and owns lifetime, a `ServerController` that drives `StartAsync`/`StopAsync` on a fresh `WebApplication`, and a hand-rolled Win32 `TrayIcon` (Shell_NotifyIcon via a hidden top-level `HwndSource`). State→UI mapping lives in a pure, unit-tested `ServerStateInfo`.

**Tech Stack:** .NET 10 (`net10.0-windows`, x64), WPF (`UseWPF`), ASP.NET Core Kestrel via `FrameworkReference Microsoft.AspNetCore.App`, ModelContextProtocol.AspNetCore 1.3.0, Win32 P/Invoke (`shell32`/`user32`), xUnit v3.

---

## Conventions (read once)

- **Commits:** this repo commits via a message file, never inline `-m`, and never adds AI attribution or `Co-Authored-By`. For each commit step: write the shown message to a temp file and run `git -C E:\GitHub\SnoopMCP commit -F <tempfile>`. Work happens on branch `tray-app` (already created).
- **Build:** `dotnet build E:\GitHub\SnoopMCP\SnoopMCP.sln -c Debug` — must be **zero-warning** (`TreatWarningsAsErrors=true` repo-wide). No quotes around `-p:` flags. Never use `cd`; never use `2>&1`.
- **Test:** `dotnet test E:\GitHub\SnoopMCP\SnoopMCP.sln -c Debug`.
- **Analyzer rules on `src/`** (CodeStructure.Analyzers + latest-recommended, errors): single-return / variable pattern (no early returns), `switch` over if/else chains, no `continue`, **named constants for every string/number literal** (no magic values), Allman braces, `m`/`sm`/`ps` field prefixes, comments on their own line. `tests/` relaxes naming + magic-literal rules. All code blocks below are written to satisfy these — keep them that way.

---

## File Structure

**New project `src/SnoopMCP.Server/`** (class library; owns everything headless):
- `SnoopMCP.Server.csproj` — `Microsoft.NET.Sdk`, `net10.0-windows`, x64, `FrameworkReference Microsoft.AspNetCore.App`, `PackageReference ModelContextProtocol.AspNetCore`, `ProjectReference SnoopMCP.Protocol`. `RootNamespace=SnoopMCP.Host`, `AssemblyName=SnoopMCP.Server`.
- Moved from `SnoopMCP.Host` (namespaces unchanged → `SnoopMCP.Host(.Injection/.Tools)`): `Tools/McpTools.cs`, `SessionManager.cs`, `HealthStatus.cs`, `ThisAssembly.cs`, `IInjectorService.cs`, `NullInjectorService.cs`, `ProcessProbeResult.cs`, `PipeClient.cs`, `Injection/{InjectorService,ProcessProbe,ProcessEnumerator,InjectorServiceLog}.cs`.
- New: `ServerHost.cs` (build the `WebApplication`), `ServerState.cs` (enum), `ServerStateInfo.cs` (pure state→UI mapping).

**`src/SnoopMCP.Host/`** (WPF `WinExe` shell; owns process + UI):
- `SnoopMCP.Host.csproj` — switched to `Microsoft.NET.Sdk` + `WinExe` + `UseWPF` + `FrameworkReference Microsoft.AspNetCore.App` + `ApplicationIcon` + `Content` icon; references `SnoopMCP.Server` and build-order-only `Injection`/`Payload`; keeps the injector/payload staging targets.
- `Program.cs` — **deleted** in Task 3 (WPF generates the entry point).
- New: `App.xaml` / `App.xaml.cs`, `ServerController.cs`, `TrayIcon.cs`.
- Existing `Images/icon.ico` — used as exe icon and tray icon.

**Tests:**
- `tests/SnoopMCP.Host.Tests/` — reference swapped Host→Server; gains `ServerStateInfoTests.cs`.
- `tests/SnoopMCP.IntegrationTests/` — reference swapped Host→Server (keeps build-order Injection/Payload/Sample + copy targets).

---

## Task 1: Extract `SnoopMCP.Server` library (green refactor, no behavior change)

**Files:**
- Create: `src/SnoopMCP.Server/SnoopMCP.Server.csproj`, `src/SnoopMCP.Server/ServerHost.cs`
- Move (git mv): the 12 files listed above into `src/SnoopMCP.Server/`
- Modify: `src/SnoopMCP.Host/Program.cs`, `src/SnoopMCP.Host/SnoopMCP.Host.csproj`, `SnoopMCP.sln`, `tests/SnoopMCP.Host.Tests/SnoopMCP.Host.Tests.csproj`, `tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj`

- [ ] **Step 1: Create the Server project file**

Create `src/SnoopMCP.Server/SnoopMCP.Server.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0-windows</TargetFramework>
        <RootNamespace>SnoopMCP.Host</RootNamespace>
        <AssemblyName>SnoopMCP.Server</AssemblyName>
        <PlatformTarget>x64</PlatformTarget>
        <InvariantGlobalization>true</InvariantGlobalization>
    </PropertyGroup>

    <ItemGroup>
        <FrameworkReference Include="Microsoft.AspNetCore.App" />
    </ItemGroup>

    <!--
        This code came from a Microsoft.NET.Sdk.Web project, which supplied ASP.NET Core implicit
        global usings. Microsoft.NET.Sdk does not, so re-add them here to keep the moved files and
        ServerHost compiling unchanged (Kestrel config, Results, MapMcp, AddMcpServer, ILogger, etc.).
    -->
    <ItemGroup>
        <Using Include="Microsoft.AspNetCore.Builder" />
        <Using Include="Microsoft.AspNetCore.Hosting" />
        <Using Include="Microsoft.AspNetCore.Http" />
        <Using Include="Microsoft.AspNetCore.Routing" />
        <Using Include="Microsoft.Extensions.Configuration" />
        <Using Include="Microsoft.Extensions.DependencyInjection" />
        <Using Include="Microsoft.Extensions.Hosting" />
        <Using Include="Microsoft.Extensions.Logging" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.3.0" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\SnoopMCP.Protocol\SnoopMCP.Protocol.csproj" />
    </ItemGroup>
</Project>
```

> Build note (Step 8): with the global usings above plus each moved file's own explicit usings, the moved code reproduces its old Web-SDK using environment. If a moved file still reports a missing namespace (e.g. `System.Net.Http.Json`), add that one `using` to the file — expected to be rare.

- [ ] **Step 2: Move the headless code into the Server project**

Run (creates destination folders automatically; namespaces stay `SnoopMCP.Host`):

```
git -C E:\GitHub\SnoopMCP mv src/SnoopMCP.Host/Tools/McpTools.cs            src/SnoopMCP.Server/Tools/McpTools.cs
git -C E:\GitHub\SnoopMCP mv src/SnoopMCP.Host/SessionManager.cs            src/SnoopMCP.Server/SessionManager.cs
git -C E:\GitHub\SnoopMCP mv src/SnoopMCP.Host/HealthStatus.cs              src/SnoopMCP.Server/HealthStatus.cs
git -C E:\GitHub\SnoopMCP mv src/SnoopMCP.Host/ThisAssembly.cs              src/SnoopMCP.Server/ThisAssembly.cs
git -C E:\GitHub\SnoopMCP mv src/SnoopMCP.Host/IInjectorService.cs          src/SnoopMCP.Server/IInjectorService.cs
git -C E:\GitHub\SnoopMCP mv src/SnoopMCP.Host/NullInjectorService.cs       src/SnoopMCP.Server/NullInjectorService.cs
git -C E:\GitHub\SnoopMCP mv src/SnoopMCP.Host/ProcessProbeResult.cs        src/SnoopMCP.Server/ProcessProbeResult.cs
git -C E:\GitHub\SnoopMCP mv src/SnoopMCP.Host/PipeClient.cs                src/SnoopMCP.Server/PipeClient.cs
git -C E:\GitHub\SnoopMCP mv src/SnoopMCP.Host/Injection/InjectorService.cs    src/SnoopMCP.Server/Injection/InjectorService.cs
git -C E:\GitHub\SnoopMCP mv src/SnoopMCP.Host/Injection/ProcessProbe.cs       src/SnoopMCP.Server/Injection/ProcessProbe.cs
git -C E:\GitHub\SnoopMCP mv src/SnoopMCP.Host/Injection/ProcessEnumerator.cs  src/SnoopMCP.Server/Injection/ProcessEnumerator.cs
git -C E:\GitHub\SnoopMCP mv src/SnoopMCP.Host/Injection/InjectorServiceLog.cs src/SnoopMCP.Server/Injection/InjectorServiceLog.cs
```

- [ ] **Step 3: Add `ServerHost.Build` (the old `Program.Main` body, returning the app)**

Create `src/SnoopMCP.Server/ServerHost.cs`:

```csharp
// ServerHost.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using SnoopMCP.Host.Tools;

/// <summary>
/// Builds the SnoopMCP MCP server as a configured-but-not-started <see cref="WebApplication"/>
/// (Kestrel on http://127.0.0.1:6300, endpoints <c>/mcp</c> and <c>/health</c>). The WPF tray shell
/// owns the process and drives the returned app with StartAsync/StopAsync; tests build it directly.
/// </summary>
public static class ServerHost
{
    private const string McpEndpointPattern = "/mcp";
    private const string HealthEndpointPattern = "/health";
    private const string ServerName = "SnoopMCP — WPF live-inspection MCP server";
    private const int ListenPort = 6300;

    /// <summary>Builds the MCP Streamable-HTTP host on localhost:6300 without starting it.</summary>
    /// <param name="args">Process command-line arguments.</param>
    /// <returns>The configured, not-yet-started web application.</returns>
    public static WebApplication Build(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(ListenPort));

        builder.Services.AddSingleton<SessionManager>();
        builder.Services.AddSingleton<Injection.ProcessProbe>();
        builder.Services.AddSingleton<IInjectorService, Injection.InjectorService>();

        builder.Services
            .AddMcpServer(options => options.ServerInfo = new Implementation
            {
                Name = ServerName,
                Version = ThisAssembly.InformationalVersion
            })
            .WithHttpTransport(transport => transport.Stateless = true)
            .WithToolsFromAssembly(typeof(McpTools).Assembly);

        WebApplication app = builder.Build();
        app.MapMcp(McpEndpointPattern);
        app.MapGet(HealthEndpointPattern, (SessionManager session) =>
            Results.Ok(HealthStatus.Create(ThisAssembly.InformationalVersion, session.IsAttached)));

        return app;
    }
}
```

Note the **critical** change vs the old code: `WithToolsFromAssembly(typeof(McpTools).Assembly)` (was argless). The entry assembly is no longer where the tools live, so the assembly must be named explicitly.

- [ ] **Step 4: Reduce `Program.cs` to call `ServerHost` (Host stays a console exe for now)**

Replace the entire contents of `src/SnoopMCP.Host/Program.cs`:

```csharp
// Program.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

/// <summary>Console entry point: builds the MCP host and runs it until shutdown.</summary>
public static class Program
{
    /// <summary>Builds and runs the MCP host.</summary>
    /// <param name="args">Process command-line arguments.</param>
    public static async Task Main(string[] args)
    {
        WebApplication app = ServerHost.Build(args);
        await app.RunAsync().ConfigureAwait(false);
    }
}
```

- [ ] **Step 5: Point `SnoopMCP.Host.csproj` at the Server library**

In `src/SnoopMCP.Host/SnoopMCP.Host.csproj`: remove the `ModelContextProtocol.AspNetCore` `PackageReference` (it moved to Server) and add a `ProjectReference` to Server. The `<ItemGroup>` of references becomes:

```xml
    <ItemGroup>
        <ProjectReference Include="..\SnoopMCP.Server\SnoopMCP.Server.csproj" />
        <ProjectReference Include="..\SnoopMCP.Protocol\SnoopMCP.Protocol.csproj" />
        <ProjectReference Include="..\SnoopMCP.Injection\SnoopMCP.Injection.csproj">
            <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
        </ProjectReference>
        <ProjectReference Include="..\SnoopMCP.Payload\SnoopMCP.Payload.csproj">
            <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
        </ProjectReference>
    </ItemGroup>
```

Delete the separate `<ItemGroup>` that held `<PackageReference Include="ModelContextProtocol.AspNetCore" ... />`. Leave the `<PropertyGroup>` and both `CopyInjectorBinaries`/`CopyPayloadBinaries` targets untouched.

- [ ] **Step 6: Add the Server project to the solution**

Run:

```
dotnet sln E:\GitHub\SnoopMCP\SnoopMCP.sln add src/SnoopMCP.Server/SnoopMCP.Server.csproj
```

- [ ] **Step 7: Swap test references Host → Server**

In `tests/SnoopMCP.Host.Tests/SnoopMCP.Host.Tests.csproj`, change the Host project reference line to Server:

```xml
        <ProjectReference Include="..\..\src\SnoopMCP.Server\SnoopMCP.Server.csproj" />
```

(Keep the `SnoopMCP.Protocol` reference.) In `tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj`, change the same line:

```xml
        <ProjectReference Include="..\..\src\SnoopMCP.Server\SnoopMCP.Server.csproj" />
```

(Keep the `ReferenceOutputAssembly=false` Injection/Payload/Sample references and all three copy targets unchanged.)

- [ ] **Step 8: Build and verify zero warnings**

Run: `dotnet build E:\GitHub\SnoopMCP\SnoopMCP.sln -c Debug`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. If `WithToolsFromAssembly` errors on the overload, confirm `typeof(McpTools).Assembly` is passed (not a string).

- [ ] **Step 9: Run the full suite (everything still green — pure refactor)**

Run: `dotnet test E:\GitHub\SnoopMCP\SnoopMCP.sln -c Debug`
Expected: all existing tests pass (Host.Tests, IntegrationTests, Protocol/Payload/Cli/ClientIntegration suites). No test count change.

- [ ] **Step 10: Commit**

Message (write to temp file, then `git -C E:\GitHub\SnoopMCP commit -F <tempfile>`):

```
Extract SnoopMCP.Server library from the host

Move the web/MCP/tools/injection code into a new SnoopMCP.Server class
library and add ServerHost.Build, which returns a configured-but-unstarted
WebApplication. The host's Program.Main now just builds and runs it. Pure
refactor; namespaces kept as SnoopMCP.* so tests only swap a project ref.
WithToolsFromAssembly now names the Server assembly explicitly.
```

```
git -C E:\GitHub\SnoopMCP add -A
git -C E:\GitHub\SnoopMCP commit -F <tempfile>
```

---

## Task 2: `ServerState` + `ServerStateInfo` (TDD, pure, in Server)

**Files:**
- Create: `src/SnoopMCP.Server/ServerState.cs`, `src/SnoopMCP.Server/ServerStateInfo.cs`
- Test: `tests/SnoopMCP.Host.Tests/ServerStateInfoTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/SnoopMCP.Host.Tests/ServerStateInfoTests.cs`:

```csharp
// ServerStateInfoTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host.Tests;

using SnoopMCP.Host;
using Xunit;

public class ServerStateInfoTests
{
    [Fact]
    public void CanStart_WhenStopped_IsTrue()
    {
        Assert.True(ServerStateInfo.CanStart(ServerState.Stopped));
    }

    [Fact]
    public void CanStart_WhenFaulted_IsTrue()
    {
        Assert.True(ServerStateInfo.CanStart(ServerState.Faulted));
    }

    [Fact]
    public void CanStart_WhenRunning_IsFalse()
    {
        Assert.False(ServerStateInfo.CanStart(ServerState.Running));
    }

    [Fact]
    public void CanStop_WhenRunning_IsTrue()
    {
        Assert.True(ServerStateInfo.CanStop(ServerState.Running));
    }

    [Fact]
    public void CanStop_WhenStopped_IsFalse()
    {
        Assert.False(ServerStateInfo.CanStop(ServerState.Stopped));
    }

    [Theory]
    [InlineData(ServerState.Stopped)]
    [InlineData(ServerState.Starting)]
    [InlineData(ServerState.Running)]
    [InlineData(ServerState.Faulted)]
    public void Tooltip_IsNeverEmpty(ServerState state)
    {
        Assert.False(string.IsNullOrWhiteSpace(ServerStateInfo.Tooltip(state)));
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test E:\GitHub\SnoopMCP\SnoopMCP.sln -c Debug --filter FullyQualifiedName~ServerStateInfoTests`
Expected: compile error / FAIL — `ServerState` and `ServerStateInfo` do not exist yet.

- [ ] **Step 3: Implement the enum**

Create `src/SnoopMCP.Server/ServerState.cs`:

```csharp
// ServerState.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

/// <summary>The lifecycle state of the in-process MCP server, as surfaced by the tray.</summary>
public enum ServerState
{
    /// <summary>No server instance is bound; the port is free.</summary>
    Stopped,

    /// <summary>A server instance is being built and bound.</summary>
    Starting,

    /// <summary>The server is bound and serving on its port.</summary>
    Running,

    /// <summary>The last start attempt failed (for example, the port was already in use).</summary>
    Faulted
}
```

- [ ] **Step 4: Implement the mapping**

Create `src/SnoopMCP.Server/ServerStateInfo.cs`:

```csharp
// ServerStateInfo.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

/// <summary>
/// Pure mapping from <see cref="ServerState"/> to the tray's tooltip text and the availability of
/// the Start/Stop actions. No WPF or Win32 dependency, so it is unit-testable in isolation.
/// </summary>
public static class ServerStateInfo
{
    private const string RunningTip = "SnoopMCP — running on http://127.0.0.1:6300";
    private const string StartingTip = "SnoopMCP — starting…";
    private const string StoppedTip = "SnoopMCP — stopped";
    private const string FaultedTip = "SnoopMCP — error (is port 6300 already in use?)";

    /// <summary>Gets the tooltip text describing the supplied state.</summary>
    /// <param name="state">The current server state.</param>
    /// <returns>A non-empty tooltip string.</returns>
    public static string Tooltip(ServerState state) => state switch
    {
        ServerState.Running => RunningTip,
        ServerState.Starting => StartingTip,
        ServerState.Faulted => FaultedTip,
        _ => StoppedTip
    };

    /// <summary>Gets a value indicating whether Start is available in the supplied state.</summary>
    /// <param name="state">The current server state.</param>
    /// <returns><c>true</c> when stopped or faulted.</returns>
    public static bool CanStart(ServerState state) => state is ServerState.Stopped or ServerState.Faulted;

    /// <summary>Gets a value indicating whether Stop is available in the supplied state.</summary>
    /// <param name="state">The current server state.</param>
    /// <returns><c>true</c> when running.</returns>
    public static bool CanStop(ServerState state) => state is ServerState.Running;
}
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test E:\GitHub\SnoopMCP\SnoopMCP.sln -c Debug --filter FullyQualifiedName~ServerStateInfoTests`
Expected: all 8 cases PASS.

- [ ] **Step 6: Commit**

Message:

```
Add ServerState + ServerStateInfo mapping

Pure, WPF-free state model for the tray: Stopped/Starting/Running/Faulted
plus tooltip text and Start/Stop availability. Unit-tested.
```

```
git -C E:\GitHub\SnoopMCP add -A
git -C E:\GitHub\SnoopMCP commit -F <tempfile>
```

---

## Task 3: Convert the host to a WPF tray app (windowless server + tray with Exit)

This is the largest task: it flips the project to a WPF `WinExe`, deletes `Program.cs`, and adds the `App`, `ServerController`, and `TrayIcon`. The tray menu in this task has Start/Stop/Status/Exit wired from the start (Start/Stop are exercised in Task 4's verification, but they are implemented here so the menu is complete). Verification is manual — Win32/WPF glue is not unit-tested.

**Files:**
- Modify: `src/SnoopMCP.Host/SnoopMCP.Host.csproj`
- Delete: `src/SnoopMCP.Host/Program.cs`
- Create: `src/SnoopMCP.Host/App.xaml`, `src/SnoopMCP.Host/App.xaml.cs`, `src/SnoopMCP.Host/ServerController.cs`, `src/SnoopMCP.Host/TrayIcon.cs`

- [ ] **Step 1: Convert the project to a WPF WinExe**

Replace the entire contents of `src/SnoopMCP.Host/SnoopMCP.Host.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>WinExe</OutputType>
        <TargetFramework>net10.0-windows</TargetFramework>
        <RootNamespace>SnoopMCP.Host</RootNamespace>
        <AssemblyName>SnoopMCP.Host</AssemblyName>
        <PlatformTarget>x64</PlatformTarget>
        <UseWPF>true</UseWPF>
        <InvariantGlobalization>true</InvariantGlobalization>
        <ApplicationIcon>Images\icon.ico</ApplicationIcon>
        <InjectorStageDir>$(MSBuildThisFileDirectory)..\SnoopMCP.Injection\bin\$(Configuration)\net10.0-windows\injector\</InjectorStageDir>
        <PayloadOutputDir>$(MSBuildThisFileDirectory)..\SnoopMCP.Payload\bin\$(Configuration)\net10.0-windows\</PayloadOutputDir>
    </PropertyGroup>

    <ItemGroup>
        <FrameworkReference Include="Microsoft.AspNetCore.App" />
    </ItemGroup>

    <!-- UseWPF drops System.IO from the SDK implicit-usings set; restore it (matches IntegrationTests). -->
    <ItemGroup>
        <Using Include="System.IO" />
    </ItemGroup>

    <ItemGroup>
        <Content Include="Images\icon.ico">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </Content>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\SnoopMCP.Server\SnoopMCP.Server.csproj" />
        <ProjectReference Include="..\SnoopMCP.Injection\SnoopMCP.Injection.csproj">
            <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
        </ProjectReference>
        <ProjectReference Include="..\SnoopMCP.Payload\SnoopMCP.Payload.csproj">
            <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
        </ProjectReference>
    </ItemGroup>

    <Target Name="CopyInjectorBinaries" AfterTargets="Build">
        <ItemGroup>
            <InjectorBinary Include="$(InjectorStageDir)**\*.*" />
        </ItemGroup>
        <Error Condition="'@(InjectorBinary)' == ''"
               Text="No injector binaries found at $(InjectorStageDir). Build SnoopMCP.Injection first (it stages Snoop's launcher + native DLL)." />
        <Copy SourceFiles="@(InjectorBinary)"
              DestinationFiles="@(InjectorBinary->'$(OutDir)injector\%(RecursiveDir)%(Filename)%(Extension)')"
              SkipUnchangedFiles="true" />
    </Target>

    <Target Name="CopyPayloadBinaries" AfterTargets="Build">
        <ItemGroup>
            <PayloadBinary Include="$(PayloadOutputDir)*.dll;$(PayloadOutputDir)*.json" />
        </ItemGroup>
        <Error Condition="'@(PayloadBinary)' == ''"
               Text="No payload binaries found at $(PayloadOutputDir). Build SnoopMCP.Payload first." />
        <Copy SourceFiles="@(PayloadBinary)"
              DestinationFiles="@(PayloadBinary->'$(OutDir)payload\%(Filename)%(Extension)')"
              SkipUnchangedFiles="true" />
    </Target>
</Project>
```

- [ ] **Step 2: Delete the console entry point**

Run: `git -C E:\GitHub\SnoopMCP rm src/SnoopMCP.Host/Program.cs`
(WPF generates the `[STAThread] Main` from `App.xaml`; keeping `Program.Main` would be a duplicate entry point.)

- [ ] **Step 3: Add the WPF application definition**

Create `src/SnoopMCP.Host/App.xaml`:

```xml
<Application x:Class="SnoopMCP.Host.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown" />
```

- [ ] **Step 4: Add the `ServerController`**

Create `src/SnoopMCP.Host/ServerController.cs`:

```csharp
// ServerController.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Owns the in-process MCP <see cref="WebApplication"/> and its <see cref="ServerState"/>. Start
/// builds and binds a fresh instance; Stop unbinds and disposes it so the port frees without the
/// process exiting. A failed bind (for example, the port is taken) becomes <see cref="ServerState.Faulted"/>
/// rather than throwing, so the tray stays alive.
/// </summary>
public sealed class ServerController : IAsyncDisposable
{
    private readonly string[] mArgs;
    private WebApplication? mApp;

    /// <summary>Raised on the calling thread whenever <see cref="State"/> changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Initialises a new <see cref="ServerController"/>.</summary>
    /// <param name="args">Process arguments forwarded to each built server instance.</param>
    public ServerController(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        mArgs = args;
    }

    /// <summary>Gets the current server lifecycle state.</summary>
    public ServerState State { get; private set; } = ServerState.Stopped;

    /// <summary>Gets a value indicating whether a WPF target is currently attached.</summary>
    public bool IsAttached => mApp?.Services.GetService<SessionManager>()?.IsAttached == true;

    /// <summary>Builds and starts a fresh server instance when one is not already running.</summary>
    public async Task StartAsync()
    {
        if (ServerStateInfo.CanStart(State))
        {
            SetState(ServerState.Starting);
            WebApplication app = ServerHost.Build(mArgs);
            bool started = await TryStartAsync(app).ConfigureAwait(true);
            if (started)
            {
                mApp = app;
                SetState(ServerState.Running);
            }
            else
            {
                await app.DisposeAsync().ConfigureAwait(true);
                SetState(ServerState.Faulted);
            }
        }
    }

    /// <summary>Stops and disposes the running server, freeing the port.</summary>
    public async Task StopAsync()
    {
        if (ServerStateInfo.CanStop(State) && mApp is not null)
        {
            WebApplication app = mApp;
            mApp = null;
            await app.StopAsync().ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
            SetState(ServerState.Stopped);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (mApp is not null)
        {
            WebApplication app = mApp;
            mApp = null;
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<bool> TryStartAsync(WebApplication app)
    {
        bool ok = true;
        try
        {
            await app.StartAsync().ConfigureAwait(true);
        }
        catch (IOException)
        {
            ok = false;
        }
        return ok;
    }

    private void SetState(ServerState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

Notes: `catch (IOException)` covers the realistic failure (Kestrel surfaces a taken port as `IOException`). Other exceptions are intentionally not swallowed. `State`/`StartAsync`/`StopAsync` use the variable/if pattern (no early returns).

- [ ] **Step 5: Add the hand-rolled `TrayIcon`**

Create `src/SnoopMCP.Host/TrayIcon.cs`:

```csharp
// TrayIcon.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

/// <summary>
/// Notification-area icon built directly on Win32 <c>Shell_NotifyIcon</c>, hosted by a hidden
/// top-level <see cref="HwndSource"/> window. The window is top-level (not message-only) so it
/// receives the <c>TaskbarCreated</c> broadcast and can re-add the icon after an Explorer restart.
/// Right-click shows a WPF context menu with Start / Stop / Status / Exit.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const uint NimAdd = 0x0;
    private const uint NimModify = 0x1;
    private const uint NimDelete = 0x2;
    private const uint NifMessage = 0x1;
    private const uint NifIcon = 0x2;
    private const uint NifTip = 0x4;
    private const uint NifInfo = 0x10;
    private const uint WmTrayIcon = 0x8001;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmNull = 0x0000;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x10;
    private const uint LrDefaultSize = 0x40;
    private const uint TrayIconId = 1;
    private const long LoWordMask = 0xFFFF;
    private const int TipCapacity = 128;
    private const int InfoCapacity = 256;
    private const int InfoTitleCapacity = 64;
    private const string IconRelativePath = "Images\\icon.ico";
    private const string TaskbarCreatedMessage = "TaskbarCreated";
    private const string WindowName = "SnoopMcpTrayWindow";
    private const string StartHeader = "Start MCP";
    private const string StopHeader = "Stop MCP";
    private const string StatusHeader = "Status";
    private const string ExitHeader = "Exit";
    private const string BalloonTitle = "SnoopMCP";
    private const string StatusFormat = "{0}\nAttached: {1}";
    private const string AttachedYes = "yes";
    private const string AttachedNo = "no";

    private readonly ServerController mController;
    private readonly Action mExit;
    private readonly HwndSource mSource;
    private readonly ContextMenu mMenu;
    private readonly MenuItem mStartItem;
    private readonly MenuItem mStopItem;
    private readonly uint mTaskbarCreated;
    private IntPtr mIcon;
    private bool mAdded;

    /// <summary>Creates the tray icon and adds it to the notification area.</summary>
    /// <param name="controller">The server controller the menu drives.</param>
    /// <param name="exit">Callback that shuts the whole application down.</param>
    public TrayIcon(ServerController controller, Action exit)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(exit);
        mController = controller;
        mExit = exit;

        var parameters = new HwndSourceParameters(WindowName)
        {
            Width = 0,
            Height = 0
        };
        mSource = new HwndSource(parameters);
        mSource.AddHook(WndProc);
        mTaskbarCreated = RegisterWindowMessageW(TaskbarCreatedMessage);
        mIcon = LoadTrayIcon();

        mStartItem = new MenuItem { Header = StartHeader };
        mStartItem.Click += (_, _) => OnStart();
        mStopItem = new MenuItem { Header = StopHeader };
        mStopItem.Click += (_, _) => OnStop();
        var statusItem = new MenuItem { Header = StatusHeader };
        statusItem.Click += (_, _) => OnStatus();
        var exitItem = new MenuItem { Header = ExitHeader };
        exitItem.Click += (_, _) => mExit();

        mMenu = new ContextMenu();
        mMenu.Items.Add(mStartItem);
        mMenu.Items.Add(mStopItem);
        mMenu.Items.Add(new Separator());
        mMenu.Items.Add(statusItem);
        mMenu.Items.Add(new Separator());
        mMenu.Items.Add(exitItem);

        mController.StateChanged += (_, _) => OnStateChanged();
        AddOrModify(NimAdd);
        OnStateChanged();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (mAdded)
        {
            NOTIFYICONDATAW data = CreateData(NifMessage);
            Shell_NotifyIconW(NimDelete, ref data);
            mAdded = false;
        }
        if (mIcon != IntPtr.Zero)
        {
            DestroyIcon(mIcon);
            mIcon = IntPtr.Zero;
        }
        mSource.RemoveHook(WndProc);
        mSource.Dispose();
    }

    private IntPtr LoadTrayIcon()
    {
        string path = Path.Combine(AppContext.BaseDirectory, IconRelativePath);
        return LoadImageW(IntPtr.Zero, path, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize);
    }

    private void OnStart()
    {
        _ = mController.StartAsync();
    }

    private void OnStop()
    {
        _ = mController.StopAsync();
    }

    private void OnStatus()
    {
        string attached = mController.IsAttached ? AttachedYes : AttachedNo;
        string text = string.Format(
            CultureInfo.InvariantCulture, StatusFormat, ServerStateInfo.Tooltip(mController.State), attached);
        ShowBalloon(text);
    }

    private void OnStateChanged()
    {
        mStartItem.IsEnabled = ServerStateInfo.CanStart(mController.State);
        mStopItem.IsEnabled = ServerStateInfo.CanStop(mController.State);
        if (mAdded)
        {
            AddOrModify(NimModify);
        }
    }

    private void AddOrModify(uint message)
    {
        NOTIFYICONDATAW data = CreateData(NifMessage | NifIcon | NifTip);
        data.hIcon = mIcon;
        data.szTip = ServerStateInfo.Tooltip(mController.State);
        bool ok = Shell_NotifyIconW(message, ref data);
        if (ok && message == NimAdd)
        {
            mAdded = true;
        }
    }

    private void ShowBalloon(string text)
    {
        NOTIFYICONDATAW data = CreateData(NifInfo);
        data.szInfo = text;
        data.szInfoTitle = BalloonTitle;
        Shell_NotifyIconW(NimModify, ref data);
    }

    private NOTIFYICONDATAW CreateData(uint flags)
    {
        return new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = mSource.Handle,
            uID = TrayIconId,
            uFlags = flags,
            uCallbackMessage = WmTrayIcon,
            szTip = string.Empty,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        uint message = (uint)msg;
        if (message == WmTrayIcon)
        {
            uint mouse = (uint)(lParam.ToInt64() & LoWordMask);
            if (mouse is WmRButtonUp or WmLButtonUp)
            {
                ShowMenu();
                handled = true;
            }
        }
        else if (message == mTaskbarCreated)
        {
            AddOrModify(NimAdd);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ShowMenu()
    {
        SetForegroundWindow(mSource.Handle);
        mMenu.Placement = PlacementMode.MousePoint;
        mMenu.IsOpen = true;
        PostMessageW(mSource.Handle, WmNull, IntPtr.Zero, IntPtr.Zero);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = TipCapacity)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = InfoCapacity)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = InfoTitleCapacity)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImageW(IntPtr hinst, string lpszName, uint uType, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessageW(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
```

- [ ] **Step 6: Add the application code-behind**

Create `src/SnoopMCP.Host/App.xaml.cs`:

```csharp
// App.xaml.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using System.Windows;

/// <summary>
/// WPF application root for the SnoopMCP tray app. Enforces single-instance, starts the in-process
/// MCP server, shows the tray icon, and tears everything down on Exit or Windows session end.
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\SnoopMCP.Host";
    private Mutex? mInstanceMutex;
    private ServerController? mController;
    private TrayIcon? mTray;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        mInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
        }
        else
        {
            mController = new ServerController(e.Args);
            mTray = new TrayIcon(mController, Shutdown);
            SessionEnding += OnSessionEnding;
            _ = mController.StartAsync();
        }
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        Shutdown();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        mTray?.Dispose();
        if (mController is not null)
        {
            mController.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        mInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
```

`_ = mController.StartAsync()` is safe to fire-and-forget: `StartAsync` catches its own bind failure and sets `Faulted`.

- [ ] **Step 7: Build zero-warning**

Run: `dotnet build E:\GitHub\SnoopMCP\SnoopMCP.sln -c Debug`
Expected: `0 Warning(s) 0 Error(s)`. If a duplicate-entry-point error appears, confirm `Program.cs` was removed (Step 2). If `System.IO`/`Path` is unresolved, confirm the `<Using Include="System.IO" />` item is present.

- [ ] **Step 8: Run the suite (Server/Integration tests unaffected)**

Run: `dotnet test E:\GitHub\SnoopMCP\SnoopMCP.sln -c Debug`
Expected: all green (no test references the WPF exe).

- [ ] **Step 9: Manual verification — launch, observe, exit**

Hypothesis: the exe launches with no console window, the tray icon shows `icon.ico`, the server is reachable, and Exit tears it all down.

Run: `dotnet build E:\GitHub\SnoopMCP\SnoopMCP.sln -c Debug` then launch the built exe (non-blocking):

```
Start-Process E:\GitHub\SnoopMCP\src\SnoopMCP.Host\bin\Debug\net10.0-windows\SnoopMCP.Host.exe
```

Verify, in order:
1. **No console window** appears.
2. A tray icon (the `icon.ico`) is present in the notification area; hover tooltip reads "running on http://127.0.0.1:6300".
3. `Invoke-WebRequest http://127.0.0.1:6300/health -UseBasicParsing` returns HTTP 200 with `"status":"ok"`.
4. Right-click the icon → menu shows Start MCP (disabled), Stop MCP (enabled), Status, Exit.
5. Click **Status** → balloon shows the running URL and "Attached: no".
6. Click **Exit** → icon disappears; `Invoke-WebRequest http://127.0.0.1:6300/health` now fails (connection refused); no `SnoopMCP.Host` process remains (`Get-Process SnoopMCP.Host -ErrorAction SilentlyContinue` is empty).

If any check fails, stop and diagnose before committing.

- [ ] **Step 10: Commit**

Message:

```
Convert the host to a WPF system-tray app

SnoopMCP.Host is now a WPF WinExe that self-hosts the MCP server in-process
(no console window). App enforces single-instance and owns lifetime;
ServerController drives StartAsync/StopAsync on a fresh WebApplication;
TrayIcon is a hand-rolled Shell_NotifyIcon on a hidden top-level HwndSource
(no WinForms, no NuGet) using Images/icon.ico, with a Start/Stop/Status/Exit
menu. Exit and Windows session-end tear everything down.
```

```
git -C E:\GitHub\SnoopMCP add -A
git -C E:\GitHub\SnoopMCP commit -F <tempfile>
```

---

## Task 4: Verify Start/Stop lifecycle from the tray (manual)

The Start/Stop items were implemented in Task 3; this task verifies the runtime toggle and repeated-start (rebuild) behavior, which is the subtle part (a stopped `WebApplication` cannot be restarted, so Start builds a fresh one).

**Files:** none (verification + any fix that arises).

- [ ] **Step 1: Launch and confirm running**

```
Start-Process E:\GitHub\SnoopMCP\src\SnoopMCP.Host\bin\Debug\net10.0-windows\SnoopMCP.Host.exe
```
Confirm `Invoke-WebRequest http://127.0.0.1:6300/health -UseBasicParsing` → 200.

- [ ] **Step 2: Stop frees the port, tray survives**

Right-click → **Stop MCP**. Expected: tooltip becomes "stopped"; `Invoke-WebRequest http://127.0.0.1:6300/health` fails (connection refused); the tray icon is still present; Start MCP enabled, Stop MCP disabled.

- [ ] **Step 3: Start rebinds (fresh instance)**

Right-click → **Start MCP**. Expected: tooltip back to "running…"; `/health` → 200 again. Repeat Stop/Start once more to confirm repeated rebuild works without error.

- [ ] **Step 4: Port-conflict shows Faulted (not a crash)**

With the tray running and bound, start a second binder to force a conflict, then Stop+Start the tray while the port is held:
```
Start-Process E:\GitHub\SnoopMCP\src\SnoopMCP.Host\bin\Debug\net10.0-windows\SnoopMCP.Host.exe
```
The second instance is blocked by the single-instance mutex (no second icon). To exercise Faulted directly: Stop MCP, occupy 6300 with any listener, then Start MCP — expect the tooltip/menu to reflect **Faulted** and the app to remain alive (no crash). Release the port, Start again → Running.

- [ ] **Step 5: Exit and confirm teardown**

Exit from the tray. Confirm no `SnoopMCP.Host` process remains and the port is free. No commit unless a fix was needed (then commit it with a clear message).

---

## Task 5: Docs + runtime/publish note

**Files:**
- Modify: `README.md` (add a short "System tray" section), `docs/superpowers/specs/2026-05-29-snoopmcp-installer-design.md` (annotate the §10 tray items as delivered)

- [ ] **Step 1: README — document the tray**

Add a section to `README.md` stating: SnoopMCP runs as a WPF system-tray app (`SnoopMCP.Host.exe`); it self-hosts the MCP server on `http://127.0.0.1:6300`; the tray menu offers Start MCP / Stop MCP / Status / Exit; there is no console window; the logon autostart and the installer's launch checkbox launch this app unchanged. Note the CLI `stop` now stops the whole app, and that the app requires the .NET 10 Desktop Runtime (WPF) in addition to the ASP.NET Core runtime.

- [ ] **Step 2: Installer spec — mark §10 tray items delivered**

In `docs/superpowers/specs/2026-05-29-snoopmcp-installer-design.md` §10, annotate that the tray app now ships in-process under the existing `SnoopMCP.Host.exe` name, so the logon task, launch checkbox, and the single `util:CloseApplication Target="SnoopMCP.Host.exe"` need no change; graceful `WM_CLOSE` remains a future option.

- [ ] **Step 3: Verify publish picks up the new shape (runtime check)**

Run: `dotnet publish src/SnoopMCP.Host/SnoopMCP.Host.csproj -c Release`
Expected: the publish folder contains `SnoopMCP.Host.exe`, `SnoopMCP.Server.dll`, `Images/icon.ico`, and the `injector/` + `payload/` folders. Confirm the app launches from the publish folder and `/health` responds. (The MSI harvests this folder via its existing `<Files Include="$(PublishDir)\**" />`, so no WiX change is required; the only new runtime requirement is the .NET Desktop Runtime.)

- [ ] **Step 4: Build + full suite one more time**

Run: `dotnet build E:\GitHub\SnoopMCP\SnoopMCP.sln -c Debug` then `dotnet test E:\GitHub\SnoopMCP\SnoopMCP.sln -c Debug`
Expected: zero warnings, all green.

- [ ] **Step 5: Commit**

Message:

```
Document the SnoopMCP tray app

README gains a System-tray section (Start/Stop/Status/Exit, no console
window, Desktop Runtime requirement, CLI stop semantics). Installer spec
§10 annotated: tray ships in-process under SnoopMCP.Host.exe, no WiX change.
```

```
git -C E:\GitHub\SnoopMCP add -A
git -C E:\GitHub\SnoopMCP commit -F <tempfile>
```

---

## Done when

- `SnoopMCP.Host.exe` is a WPF tray app with no console window, using `Images/icon.ico`.
- The MCP server runs in-process on `http://127.0.0.1:6300` (`/mcp` + `/health`); all existing tests pass after the Host→Server reference swap; `ServerStateInfoTests` pass.
- Tray menu Start/Stop/Status/Exit behaves per Tasks 3–4 (Stop frees the port without exiting; Exit tears everything down and removes the icon; single instance; icon survives Explorer restart).
- Every solution build is zero-warning; `dotnet publish` yields a launchable folder the existing MSI can harvest unchanged.
