# SnoopMCP Installer — PR 1: ClientIntegration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `SnoopMCP.ClientIntegration` library that idempotently registers/unregisters the SnoopMCP HTTP MCP server (`http://127.0.0.1:6300/mcp`) in Claude Code (`~/.claude.json`) and VS Code (`%APPDATA%\Code\User\mcp.json`), preserving all other config content.

**Architecture:** One abstract base (`JsonMcpServerWriter`) holds all the JSON read/mutate/atomic-write logic; two thin sealed subclasses supply only the config path and the servers-container key. Both clients use the identical entry shape `{ "type": "http", "url": "…" }` — Claude Code nests it under `mcpServers`, VS Code under `servers` (verified against current Microsoft Learn MCP docs, 2026-05). Writers are synchronous (small local-file edits) and edit the live JSON DOM via `System.Text.Json.Nodes` so untouched keys survive verbatim.

**Tech Stack:** .NET 10 (`net10.0`), `System.Text.Json` / `System.Text.Json.Nodes` (in-box, no NuGet), xunit.v3.

**Spec:** `docs/superpowers/specs/2026-05-29-snoopmcp-installer-design.md` (§4).

**Test approach note (refinement of spec §4):** the spec described SaddleRAG-style `input.json`/`expected-after-register.json` fixture files with exact-string equality. This plan uses **inline scenario inputs + semantic assertions** (parse the written file; assert the `snoopmcp` entry is correct AND sibling servers/keys are preserved AND status reflects state). This is the same scenario coverage, but robust to JSON formatting/key-order and far more TDD-friendly than whole-file string snapshots. The `tests/` tree already suppresses STY0008 (magic strings), so inline JSON literals are idiomatic here.

---

## Analyzer rules that apply (src/ is strict; warnings = errors)

- One public type per file (STR0008). Each type below is its own file.
- Single return / no early return / no `continue` / no if-else-if chains (use switch or the variable pattern). Max 3 nesting levels.
- Validate public method args at entry (STR0010): `ArgumentNullException.ThrowIfNull` / `ArgumentException.ThrowIfNullOrEmpty`.
- Field prefixes: `m` private instance, `sm` private static readonly. No magic strings → consts. No null-forgiving `!` (STY0002) — use `is`/`as ?? throw`.
- Nullable enabled: non-nullable strings never initialized to `null` (use `string.Empty`).
- This library does **no** logging, so CA1848 (`[LoggerMessage]`) does not arise; methods are sync, so CA2007 (`ConfigureAwait`) does not arise.

---

## File Structure

- `src/SnoopMCP.ClientIntegration/SnoopMCP.ClientIntegration.csproj` — net10.0 class library.
- `src/SnoopMCP.ClientIntegration/McpEndpoint.cs` — the server identity (`snoopmcp` / `http` / url) with a `Default`.
- `src/SnoopMCP.ClientIntegration/RegisterResult.cs`, `UnregisterResult.cs`, `StatusResult.cs` — small result records.
- `src/SnoopMCP.ClientIntegration/IClientWriter.cs` — the writer interface.
- `src/SnoopMCP.ClientIntegration/JsonMcpServerWriter.cs` — abstract base; all JSON logic.
- `src/SnoopMCP.ClientIntegration/ClaudeCodeWriter.cs` — `~/.claude.json`, key `mcpServers`.
- `src/SnoopMCP.ClientIntegration/VsCodeMcpWriter.cs` — `%APPDATA%\Code\User\mcp.json`, key `servers`.
- `tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj` — net10.0 test project.
- `tests/SnoopMCP.ClientIntegration.Tests/McpEndpointTests.cs`
- `tests/SnoopMCP.ClientIntegration.Tests/ClaudeCodeWriterTests.cs`
- `tests/SnoopMCP.ClientIntegration.Tests/VsCodeMcpWriterTests.cs`

---

### Task 1: Scaffold the project and shared types

**Files:**
- Create: `src/SnoopMCP.ClientIntegration/SnoopMCP.ClientIntegration.csproj`
- Create: `src/SnoopMCP.ClientIntegration/McpEndpoint.cs`, `RegisterResult.cs`, `UnregisterResult.cs`, `StatusResult.cs`, `IClientWriter.cs`
- Create: `tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj`, `McpEndpointTests.cs`
- Modify: `SnoopMCP.sln` (add both projects)

- [ ] **Step 1: Create the library csproj**

`src/SnoopMCP.ClientIntegration/SnoopMCP.ClientIntegration.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <RootNamespace>SnoopMCP.ClientIntegration</RootNamespace>
    </PropertyGroup>
</Project>
```

(Analyzer + nullable + TreatWarningsAsErrors are inherited from the repo-root `Directory.Build.props`.)

- [ ] **Step 2: Create the shared types**

`McpEndpoint.cs`:

```csharp
// McpEndpoint.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Identity of the SnoopMCP MCP server as written into a client's config: the entry name, the
/// transport type, and the HTTP URL. <see cref="Default"/> is the canonical SnoopMCP endpoint.
/// </summary>
/// <param name="Name">The config entry key (e.g. <c>snoopmcp</c>).</param>
/// <param name="Type">The MCP transport type (e.g. <c>http</c>).</param>
/// <param name="Url">The Streamable-HTTP endpoint URL.</param>
public sealed record McpEndpoint(string Name, string Type, string Url)
{
    /// <summary>The canonical SnoopMCP HTTP endpoint registered into every supported client.</summary>
    public static McpEndpoint Default { get; } = new("snoopmcp", "http", "http://127.0.0.1:6300/mcp");
}
```

`RegisterResult.cs`:

```csharp
// RegisterResult.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>Outcome of registering the SnoopMCP server in a client's config.</summary>
/// <param name="Success">True when the config now contains the SnoopMCP entry.</param>
/// <param name="Message">Human-readable detail for logs / status output.</param>
public sealed record RegisterResult(bool Success, string Message);
```

`UnregisterResult.cs`:

```csharp
// UnregisterResult.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>Outcome of removing the SnoopMCP server from a client's config.</summary>
/// <param name="Success">True when the SnoopMCP entry is absent afterward (including no-op).</param>
/// <param name="Message">Human-readable detail for logs / status output.</param>
public sealed record UnregisterResult(bool Success, string Message);
```

`StatusResult.cs`:

```csharp
// StatusResult.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>Whether a client's config currently registers the SnoopMCP server.</summary>
/// <param name="IsRegistered">True when the SnoopMCP entry is present with the expected URL.</param>
/// <param name="Message">Human-readable detail for status output.</param>
public sealed record StatusResult(bool IsRegistered, string Message);
```

`IClientWriter.cs`:

```csharp
// IClientWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Registers, removes, and reports the SnoopMCP MCP server entry in one LLM client's configuration
/// file, idempotently and without disturbing the client's other settings.
/// </summary>
public interface IClientWriter
{
    /// <summary>Display name of the client (for logs / status), e.g. <c>Claude Code</c>.</summary>
    string ClientName { get; }

    /// <summary>Adds or updates the SnoopMCP entry; preserves all other content.</summary>
    RegisterResult Register(McpEndpoint endpoint);

    /// <summary>Removes the SnoopMCP entry if present; a missing file or entry is a successful no-op.</summary>
    UnregisterResult Unregister();

    /// <summary>Reports whether the SnoopMCP entry is currently present with the expected URL.</summary>
    StatusResult GetStatus();
}
```

- [ ] **Step 3: Create the test csproj**

`tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <RootNamespace>SnoopMCP.ClientIntegration.Tests</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="xunit.v3" Version="3.2.2" />
        <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.6.0" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\src\SnoopMCP.ClientIntegration\SnoopMCP.ClientIntegration.csproj" />
    </ItemGroup>
</Project>
```

- [ ] **Step 4: Write the McpEndpoint test**

`tests/SnoopMCP.ClientIntegration.Tests/McpEndpointTests.cs`:

```csharp
// McpEndpointTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration.Tests;

using SnoopMCP.ClientIntegration;
using Xunit;

public sealed class McpEndpointTests
{
    [Fact]
    public void Default_IsTheSnoopMcpHttpEndpoint()
    {
        McpEndpoint endpoint = McpEndpoint.Default;

        Assert.Equal("snoopmcp", endpoint.Name);
        Assert.Equal("http", endpoint.Type);
        Assert.Equal("http://127.0.0.1:6300/mcp", endpoint.Url);
    }
}
```

- [ ] **Step 5: Add both projects to the solution**

Run (one command per call):
`dotnet sln E:/GitHub/SnoopMCP/SnoopMCP.sln add E:/GitHub/SnoopMCP/src/SnoopMCP.ClientIntegration/SnoopMCP.ClientIntegration.csproj`
then
`dotnet sln E:/GitHub/SnoopMCP/SnoopMCP.sln add E:/GitHub/SnoopMCP/tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj`

- [ ] **Step 6: Build + run the test**

Build: `dotnet build E:/GitHub/SnoopMCP/src/SnoopMCP.ClientIntegration/SnoopMCP.ClientIntegration.csproj -c Debug -p:TreatWarningsAsErrors=true`
Expected: `0 Warning(s) 0 Error(s)`.
Test: `dotnet test E:/GitHub/SnoopMCP/tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj -c Debug --nologo`
Expected: 1 passed.

- [ ] **Step 7: Commit**

Message file content:
```
A3 PR1: scaffold SnoopMCP.ClientIntegration + shared types

New net10.0 library with McpEndpoint (Default = snoopmcp/http/127.0.0.1:6300/mcp),
RegisterResult/UnregisterResult/StatusResult, and the IClientWriter interface.
Adds the library + its test project to the solution.
```
Stage: the new src files, the new test files, and `SnoopMCP.sln`. Commit with `-F`.

---

### Task 2: `JsonMcpServerWriter` base + `ClaudeCodeWriter` (TDD)

The base class carries every behavior; `ClaudeCodeWriter` is the first concrete client and is where the base logic is fully tested.

**Files:**
- Create: `src/SnoopMCP.ClientIntegration/JsonMcpServerWriter.cs`, `src/SnoopMCP.ClientIntegration/ClaudeCodeWriter.cs`
- Create: `tests/SnoopMCP.ClientIntegration.Tests/ClaudeCodeWriterTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/SnoopMCP.ClientIntegration.Tests/ClaudeCodeWriterTests.cs`:

```csharp
// ClaudeCodeWriterTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using SnoopMCP.ClientIntegration;
using Xunit;

public sealed class ClaudeCodeWriterTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;

    public ClaudeCodeWriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-cc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, ".claude.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private JsonObject ReadConfig()
    {
        string text = File.ReadAllText(mConfigPath);
        return JsonNode.Parse(text) as JsonObject ?? throw new JsonException("not an object");
    }

    [Fact]
    public void Register_OnMissingFile_CreatesConfigWithHttpEntry()
    {
        var writer = new ClaudeCodeWriter(mConfigPath);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        JsonObject root = ReadConfig();
        JsonNode entry = root["mcpServers"]!["snoopmcp"]!;
        Assert.Equal("http", (string?)entry["type"]);
        Assert.Equal("http://127.0.0.1:6300/mcp", (string?)entry["url"]);
    }

    [Fact]
    public void Register_PreservesOtherServersAndKeys()
    {
        File.WriteAllText(mConfigPath,
            "{\"mcpServers\":{\"other\":{\"type\":\"http\",\"url\":\"http://x\"}},\"numStartups\":7}");
        var writer = new ClaudeCodeWriter(mConfigPath);

        writer.Register(McpEndpoint.Default);

        JsonObject root = ReadConfig();
        Assert.Equal("http://x", (string?)root["mcpServers"]!["other"]!["url"]);
        Assert.Equal(7, (int?)root["numStartups"]);
        Assert.Equal("http://127.0.0.1:6300/mcp", (string?)root["mcpServers"]!["snoopmcp"]!["url"]);
    }

    [Fact]
    public void Register_OnFileWithNoServersSection_AddsTheSection()
    {
        File.WriteAllText(mConfigPath, "{\"numStartups\":3}");
        var writer = new ClaudeCodeWriter(mConfigPath);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        JsonObject root = ReadConfig();
        Assert.Equal(3, (int?)root["numStartups"]);
        Assert.Equal("http://127.0.0.1:6300/mcp", (string?)root["mcpServers"]!["snoopmcp"]!["url"]);
    }

    [Fact]
    public void Register_WhenAlreadyPresent_IsIdempotent()
    {
        var writer = new ClaudeCodeWriter(mConfigPath);
        writer.Register(McpEndpoint.Default);

        RegisterResult second = writer.Register(McpEndpoint.Default);

        Assert.True(second.Success);
        JsonObject root = ReadConfig();
        Assert.Equal(1, root["mcpServers"]!.AsObject().Count);
    }

    [Fact]
    public void Register_OnMalformedJson_FailsWithoutThrowing()
    {
        File.WriteAllText(mConfigPath, "{ this is not json ");
        var writer = new ClaudeCodeWriter(mConfigPath);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.False(result.Success);
    }

    [Fact]
    public void Unregister_RemovesOnlySnoopMcp_PreservingOthers()
    {
        File.WriteAllText(mConfigPath,
            "{\"mcpServers\":{\"snoopmcp\":{\"type\":\"http\",\"url\":\"http://127.0.0.1:6300/mcp\"}," +
            "\"other\":{\"type\":\"http\",\"url\":\"http://x\"}}}");
        var writer = new ClaudeCodeWriter(mConfigPath);

        UnregisterResult result = writer.Unregister();

        Assert.True(result.Success, result.Message);
        JsonObject root = ReadConfig();
        Assert.False(root["mcpServers"]!.AsObject().ContainsKey("snoopmcp"));
        Assert.True(root["mcpServers"]!.AsObject().ContainsKey("other"));
    }

    [Fact]
    public void Unregister_OnMissingFile_IsSuccessfulNoOp()
    {
        var writer = new ClaudeCodeWriter(mConfigPath);

        UnregisterResult result = writer.Unregister();

        Assert.True(result.Success);
        Assert.False(File.Exists(mConfigPath));
    }

    [Fact]
    public void GetStatus_ReflectsRegistration()
    {
        var writer = new ClaudeCodeWriter(mConfigPath);
        Assert.False(writer.GetStatus().IsRegistered);

        writer.Register(McpEndpoint.Default);

        Assert.True(writer.GetStatus().IsRegistered);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test E:/GitHub/SnoopMCP/tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj -c Debug --nologo`
Expected: compile failure / fail — `ClaudeCodeWriter` and `JsonMcpServerWriter` do not exist yet.

- [ ] **Step 3: Implement the base class**

`src/SnoopMCP.ClientIntegration/JsonMcpServerWriter.cs`:

```csharp
// JsonMcpServerWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Shared logic for registering the SnoopMCP server in a JSON-config MCP client. Subclasses supply
/// the config file path and the name of the object that holds server entries (Claude Code uses
/// <c>mcpServers</c>; VS Code uses <c>servers</c>). The SnoopMCP entry is added/updated/removed in
/// place via a mutable JSON DOM, so every other key in the file survives unchanged. Writes are
/// atomic (temp file then move).
/// </summary>
public abstract class JsonMcpServerWriter : IClientWriter
{
    private const string TypeKey = "type";
    private const string UrlKey = "url";
    private const string TempSuffix = ".tmp";

    private static readonly JsonSerializerOptions smWriteOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding smNoBomUtf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string mConfigPath;
    private readonly string mServersKey;

    /// <summary>Initialises the writer.</summary>
    /// <param name="configPath">Absolute path to the client's JSON config file.</param>
    /// <param name="serversKey">Name of the object holding server entries (e.g. <c>mcpServers</c>).</param>
    protected JsonMcpServerWriter(string configPath, string serversKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(configPath);
        ArgumentException.ThrowIfNullOrEmpty(serversKey);
        mConfigPath = configPath;
        mServersKey = serversKey;
    }

    /// <inheritdoc />
    public abstract string ClientName { get; }

    /// <inheritdoc />
    public RegisterResult Register(McpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        RegisterResult result;
        try
        {
            JsonObject root = LoadOrCreateRoot();
            JsonObject servers = GetOrAddObject(root, mServersKey);
            servers[endpoint.Name] = new JsonObject
            {
                [TypeKey] = endpoint.Type,
                [UrlKey] = endpoint.Url
            };
            WriteAtomic(root);
            result = new RegisterResult(true, $"Registered '{endpoint.Name}' in {ClientName}.");
        }
        catch (JsonException ex)
        {
            result = new RegisterResult(false, $"{ClientName} config is not valid JSON: {ex.Message}");
        }
        return result;
    }

    /// <inheritdoc />
    public UnregisterResult Unregister()
    {
        UnregisterResult result;
        if (!File.Exists(mConfigPath))
        {
            result = new UnregisterResult(true, $"{ClientName}: no config file; nothing to remove.");
        }
        else
        {
            try
            {
                JsonObject root = LoadRoot();
                bool removed = root[mServersKey] is JsonObject servers
                    && servers.Remove(McpEndpoint.Default.Name);
                if (removed)
                {
                    WriteAtomic(root);
                }
                string detail = removed
                    ? $"Removed SnoopMCP from {ClientName}."
                    : $"{ClientName}: SnoopMCP entry was not present.";
                result = new UnregisterResult(true, detail);
            }
            catch (JsonException ex)
            {
                result = new UnregisterResult(false, $"{ClientName} config is not valid JSON: {ex.Message}");
            }
        }
        return result;
    }

    /// <inheritdoc />
    public StatusResult GetStatus()
    {
        StatusResult result;
        bool present = false;
        if (File.Exists(mConfigPath))
        {
            try
            {
                JsonObject root = LoadRoot();
                present = root[mServersKey] is JsonObject servers
                    && servers[McpEndpoint.Default.Name] is JsonObject entry
                    && string.Equals((string?)entry[UrlKey], McpEndpoint.Default.Url, StringComparison.Ordinal);
            }
            catch (JsonException)
            {
                present = false;
            }
        }
        result = new StatusResult(present, present
            ? $"{ClientName}: SnoopMCP is registered."
            : $"{ClientName}: SnoopMCP is not registered.");
        return result;
    }

    private JsonObject LoadOrCreateRoot()
    {
        JsonObject root = File.Exists(mConfigPath) ? LoadRoot() : new JsonObject();
        return root;
    }

    private JsonObject LoadRoot()
    {
        string text = File.ReadAllText(mConfigPath);
        JsonNode? parsed = string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text);
        JsonObject root = parsed as JsonObject
            ?? throw new JsonException("Root JSON value is not an object.");
        return root;
    }

    private static JsonObject GetOrAddObject(JsonObject parent, string key)
    {
        JsonObject child;
        if (parent[key] is JsonObject existing)
        {
            child = existing;
        }
        else
        {
            child = new JsonObject();
            parent[key] = child;
        }
        return child;
    }

    private void WriteAtomic(JsonObject root)
    {
        string? dir = Path.GetDirectoryName(mConfigPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        string tmp = mConfigPath + TempSuffix;
        string json = root.ToJsonString(smWriteOptions);
        File.WriteAllText(tmp, json, smNoBomUtf8);
        File.Move(tmp, mConfigPath, overwrite: true);
    }
}
```

- [ ] **Step 4: Implement `ClaudeCodeWriter`**

`src/SnoopMCP.ClientIntegration/ClaudeCodeWriter.cs`:

```csharp
// ClaudeCodeWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Registers SnoopMCP in Claude Code's <c>~/.claude.json</c>, under the <c>mcpServers</c> object.
/// </summary>
public sealed class ClaudeCodeWriter : JsonMcpServerWriter
{
    private const string ServersKey = "mcpServers";
    private const string ConfigFileName = ".claude.json";

    /// <summary>Initialises the writer against an explicit config path (used by tests).</summary>
    /// <param name="configPath">Absolute path to <c>.claude.json</c>.</param>
    public ClaudeCodeWriter(string configPath) : base(configPath, ServersKey)
    {
    }

    /// <inheritdoc />
    public override string ClientName => "Claude Code";

    /// <summary>Creates a writer targeting the current user's <c>~/.claude.json</c>.</summary>
    public static ClaudeCodeWriter ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new ClaudeCodeWriter(Path.Combine(profile, ConfigFileName));
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test E:/GitHub/SnoopMCP/tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj -c Debug --nologo`
Expected: all pass (8 in this file + 1 from Task 1).

- [ ] **Step 6: Build zero-warning**

Run: `dotnet build E:/GitHub/SnoopMCP/src/SnoopMCP.ClientIntegration/SnoopMCP.ClientIntegration.csproj -c Debug -p:TreatWarningsAsErrors=true`
Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 7: Commit**

Message file content:
```
A3 PR1: JsonMcpServerWriter base + ClaudeCodeWriter

Abstract base holds all JSON read/mutate/atomic-write logic; edits the SnoopMCP
entry in a mutable DOM so other keys survive verbatim. ClaudeCodeWriter targets
~/.claude.json under mcpServers. Tests cover create-on-missing, preserve-others,
idempotent re-register, malformed-json failure, unregister-removes-only-snoopmcp,
unregister-no-op, and status.
```
Stage the two new src files + the new test file. Commit with `-F`.

---

### Task 3: `VsCodeMcpWriter` (TDD)

The base logic is already proven via Claude Code; this task adds the VS Code subclass and a focused test that it targets the right key (`servers`) and produces the http entry, plus a preserve-others check.

**Files:**
- Create: `src/SnoopMCP.ClientIntegration/VsCodeMcpWriter.cs`
- Create: `tests/SnoopMCP.ClientIntegration.Tests/VsCodeMcpWriterTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/SnoopMCP.ClientIntegration.Tests/VsCodeMcpWriterTests.cs`:

```csharp
// VsCodeMcpWriterTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using SnoopMCP.ClientIntegration;
using Xunit;

public sealed class VsCodeMcpWriterTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;

    public VsCodeMcpWriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-vsc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, "mcp.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private JsonObject ReadConfig()
    {
        string text = File.ReadAllText(mConfigPath);
        return JsonNode.Parse(text) as JsonObject ?? throw new JsonException("not an object");
    }

    [Fact]
    public void Register_UsesServersKey_WithHttpEntry()
    {
        var writer = new VsCodeMcpWriter(mConfigPath);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        JsonObject root = ReadConfig();
        JsonNode entry = root["servers"]!["snoopmcp"]!;
        Assert.Equal("http", (string?)entry["type"]);
        Assert.Equal("http://127.0.0.1:6300/mcp", (string?)entry["url"]);
    }

    [Fact]
    public void Register_PreservesExistingServersAndInputs()
    {
        File.WriteAllText(mConfigPath,
            "{\"servers\":{\"other\":{\"type\":\"http\",\"url\":\"http://x\"}},\"inputs\":[]}");
        var writer = new VsCodeMcpWriter(mConfigPath);

        writer.Register(McpEndpoint.Default);

        JsonObject root = ReadConfig();
        Assert.Equal("http://x", (string?)root["servers"]!["other"]!["url"]);
        Assert.NotNull(root["inputs"]);
        Assert.Equal("http://127.0.0.1:6300/mcp", (string?)root["servers"]!["snoopmcp"]!["url"]);
    }

    [Fact]
    public void Unregister_RemovesOnlySnoopMcp()
    {
        File.WriteAllText(mConfigPath,
            "{\"servers\":{\"snoopmcp\":{\"type\":\"http\",\"url\":\"http://127.0.0.1:6300/mcp\"}," +
            "\"other\":{\"type\":\"http\",\"url\":\"http://x\"}}}");
        var writer = new VsCodeMcpWriter(mConfigPath);

        writer.Unregister();

        JsonObject root = ReadConfig();
        Assert.False(root["servers"]!.AsObject().ContainsKey("snoopmcp"));
        Assert.True(root["servers"]!.AsObject().ContainsKey("other"));
    }

    [Fact]
    public void ClientName_IsVsCode()
    {
        Assert.Equal("VS Code", new VsCodeMcpWriter(mConfigPath).ClientName);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test E:/GitHub/SnoopMCP/tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj -c Debug --nologo`
Expected: fail — `VsCodeMcpWriter` does not exist.

- [ ] **Step 3: Implement `VsCodeMcpWriter`**

`src/SnoopMCP.ClientIntegration/VsCodeMcpWriter.cs`:

```csharp
// VsCodeMcpWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Registers SnoopMCP in VS Code's user-profile MCP config (<c>%APPDATA%\Code\User\mcp.json</c>),
/// under the top-level <c>servers</c> object — the location opened by the VS Code
/// <c>MCP: Open User Configuration</c> command.
/// </summary>
public sealed class VsCodeMcpWriter : JsonMcpServerWriter
{
    private const string ServersKey = "servers";
    private const string CodeDirName = "Code";
    private const string UserDirName = "User";
    private const string ConfigFileName = "mcp.json";

    /// <summary>Initialises the writer against an explicit config path (used by tests).</summary>
    /// <param name="configPath">Absolute path to VS Code's user <c>mcp.json</c>.</param>
    public VsCodeMcpWriter(string configPath) : base(configPath, ServersKey)
    {
    }

    /// <inheritdoc />
    public override string ClientName => "VS Code";

    /// <summary>Creates a writer targeting the current user's <c>%APPDATA%\Code\User\mcp.json</c>.</summary>
    public static VsCodeMcpWriter ForCurrentUser()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new VsCodeMcpWriter(Path.Combine(appData, CodeDirName, UserDirName, ConfigFileName));
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test E:/GitHub/SnoopMCP/tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj -c Debug --nologo`
Expected: all pass (Task 1 + Task 2 + these 4).

- [ ] **Step 5: Commit**

Message file content:
```
A3 PR1: VsCodeMcpWriter

Targets %APPDATA%\Code\User\mcp.json under the top-level servers object (the
VS Code "MCP: Open User Configuration" file), reusing the JsonMcpServerWriter
base. Tests assert the servers key + http entry, preserve-others (incl. inputs),
unregister-removes-only-snoopmcp, and the client name.
```
Stage the new src file + the new test file. Commit with `-F`.

---

### Task 4: Finalize PR 1

**Files:** none (verification + PR).

- [ ] **Step 1: Full solution build (zero-warning gate)**

Run: `dotnet build E:/GitHub/SnoopMCP/SnoopMCP.sln -c Debug -p:TreatWarningsAsErrors=true`
Expected: `0 Warning(s) 0 Error(s)`. Confirms the new projects integrate and the existing ones still build.

- [ ] **Step 2: Run the full test suite**

Run the four established projects + the new one (`--no-build`): Protocol, Host, Payload, IntegrationTests (2 passed, 1 skipped), and ClientIntegration.Tests (13 passed). Expected overall: prior 163 passed / 1 skipped, plus 13 new passed.

- [ ] **Step 3: Push, status, PR**

```text
git -C E:/GitHub/SnoopMCP push -u origin <branch>
git -C E:/GitHub/SnoopMCP rev-parse <branch>
gh api -X POST repos/JackalopeTechnologies/SnoopMCP/statuses/<sha> --field state=success --field context=build --field description="local build green; client-integration tests pass"
gh pr create --repo JackalopeTechnologies/SnoopMCP --base master --head <branch> --title "A3 PR1: SnoopMCP.ClientIntegration (Claude Code + VS Code writers)" --body-file <file>
```
Then STOP for user review before merging.

---

## Notes for the executor

- **One Bash command per call. No `&&`/`;`/`|`. No `cd` — use `git -C E:/GitHub/SnoopMCP`. Commits via `-F <msgfile>` (write to `E:/tmp/...`), never `-m`. No AI attribution. No hook-bypass flags.**
- This PR also carries the A3 spec + this plan if they are not yet on `master` — when branching, branch off the `a3-installer-docs` content or ensure the docs land first. (Controller note: the docs PR merges before PR1, so PR1 branches from a master that already has the spec/plan.)
- `System.Text.Json.Nodes` casts like `root["x"] as JsonObject` are the sanctioned no-null-forgiving pattern; tests use `!` freely because STY0002 is suppressed under `tests/`.
- If the full-solution build surfaces an analyzer rule the sample code above trips (e.g., a switch-expression preference), fix it in keeping with the rule — the logic is what matters, not the exact phrasing.
