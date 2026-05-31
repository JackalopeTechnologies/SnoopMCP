# Self-Contained "Install SnoopMCP into <AI agent>" Experience — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give SnoopMCP a complete, self-contained registration experience — across its system tray, CLI, and MSI installer — that installs/uninstalls/reports the `snoopmcp` HTTP MCP server (`http://127.0.0.1:6300/mcp`) into 9 AI coding agents, raises the Claude Code integration to the standard (timeout in ms + `permissions.allow` + a `snoopmcp-first` skill), and only touches agents that are actually present.

**Architecture:** Everything stays in this repo. `SnoopMCP.ClientIntegration` keeps its `JsonMcpServerWriter` base + `McpClient` enum + factory; the base is generalized (per-writer URL field name, optional `type`, overridable entry/match) so the 4 new JSON writers (Cursor, Gemini CLI, Windsurf, Visual Studio 2022) are thin subclasses, and existing writers are corrected (Copilot `http`+`tools`, Codex `[features]` flag). A new `bool IsDetected()` on `IClientWriter` gates "All"/installer runs to present agents only. `ClaudeCodeWriter` is extended to also write `~/.claude/settings.json` `permissions.allow` and install the `snoopmcp-first` skill. The tray (`SnoopMCP.Host`) gains XAML-declared **Install into ▸ / Uninstall from ▸ / Status** submenus driven by `TrayViewModel` commands; the CLI gains 4 client flags; the MSI gains an opt-in "register detected agents now" finish-page checkbox.

**Tech Stack:** .NET 10, C# (repo coding standards), `System.Text.Json` nodes, Tomlyn 0.17 (Codex TOML), WPF + H.NotifyIcon.Wpf (tray, XAML-first), WiX 5 (installer), xUnit v3 (tests, real-FS temp-dir pattern).

---

## Verified agent config formats (source of truth for this plan)

| # | Agent | Config file (Windows) | Top key | Entry written | Detect dir (exists ⇒ present) |
|---|---|---|---|---|---|
| 1 | Claude Code | `%USERPROFILE%\.claude.json` | `mcpServers` | `{type:"http", url, timeout:<ms>}` + settings `permissions.allow` + skill | `%USERPROFILE%\.claude.json` or `%USERPROFILE%\.claude` |
| 2 | Claude Desktop | `%APPDATA%\Claude\claude_desktop_config.json` | `mcpServers` | `{command:"npx", args:["-y","mcp-remote@latest",url,"--allow-http"]}` (no native http) | `%APPDATA%\Claude` |
| 3 | VS Code | `%APPDATA%\Code\User\mcp.json` | `servers` | `{type:"http", url}` | `%APPDATA%\Code` |
| 4 | Copilot CLI | `%USERPROFILE%\.copilot\mcp-config.json` (or `$COPILOT_HOME`) | `mcpServers` | `{type:"http", url, tools:["*"]}` (was `sse` — corrected) | `%USERPROFILE%\.copilot` |
| 5 | Codex | `%USERPROFILE%\.codex\config.toml` (or `$CODEX_HOME`) | `[mcp_servers.snoopmcp]` | `url=...` + top-level `[features] experimental_use_rmcp_client = true` | `%USERPROFILE%\.codex` |
| 6 | Cursor | `%USERPROFILE%\.cursor\mcp.json` | `mcpServers` | `{url}` (no type) | `%USERPROFILE%\.cursor` |
| 7 | Gemini CLI | `%USERPROFILE%\.gemini\settings.json` | `mcpServers` | `{httpUrl}` | `%USERPROFILE%\.gemini` |
| 8 | Windsurf | `%USERPROFILE%\.codeium\windsurf\mcp_config.json` | `mcpServers` | `{serverUrl}` | `%USERPROFILE%\.codeium\windsurf` |
| 9 | Visual Studio 2022 | `%USERPROFILE%\.mcp.json` | `servers` | `{url}` (no type) | `%LOCALAPPDATA%\Microsoft\VisualStudio` or `%USERPROFILE%\.mcp.json` |

**Claude Code timeout:** milliseconds, computed `Math.Max(DefaultTimeoutSeconds, 10) * 1000`; `DefaultTimeoutSeconds = 120` ⇒ `120000`.
**Claude Code permissions file:** `%USERPROFILE%\.claude\settings.json`, `permissions.allow` array gets `"mcp__snoopmcp"` (whole-server pre-approval).
**Skill install dir:** `%USERPROFILE%\.claude\skills\snoopmcp-first\SKILL.md`.

---

## File structure (created / modified)

**`src/SnoopMCP.ClientIntegration/` (core):**
- Modify `IClientWriter.cs` — add `bool IsDetected();`
- Modify `JsonMcpServerWriter.cs` — add `urlKey` + `emitType` ctor params + `detectionPath`; make `Register`/`Unregister`/`GetStatus` `virtual`; add `protected virtual JsonObject BuildEntry(...)`, `protected virtual bool EntryMatches(JsonObject)`, `public override bool IsDetected()`.
- Modify `McpClient.cs` — add `Cursor, GeminiCli, Windsurf, VisualStudio2022`.
- Modify `ClientRegistration.cs` (lib) — 4 new factory cases; add `DetectedClients()` helper.
- Modify `ClaudeCodeWriter.cs` — timeout entry + permissions write + skill install + detection.
- Modify `CopilotCliWriter.cs` — `http` not `sse`; add `tools:["*"]`.
- Modify `CodexWriter.cs` — write `[features] experimental_use_rmcp_client = true`; detection.
- Modify `ClaudeDesktopWriter.cs`, `VsCodeMcpWriter.cs` — add detection only.
- Create `CursorWriter.cs`, `GeminiCliWriter.cs`, `WindsurfWriter.cs`, `VisualStudio2022Writer.cs`.
- Create `SnoopSkill.cs` — embeds the `snoopmcp-first` SKILL.md text + install/remove.
- Modify `SnoopMCP.ClientIntegration.csproj` — embed `snoopmcp-first/SKILL.md` (or inline const; this plan inlines a const to avoid embedded-resource plumbing).

**`skills/snoopmcp-first/SKILL.md`** (repo copy of the skill, source of the inlined const; also shipped in repo for reference).

**`src/SnoopMCP.Cli/`:**
- Modify `Program.cs` — 4 new client flags + usage text + map flags→`McpClient`.

**`src/SnoopMCP.Host/` (tray):**
- Modify `App.xaml` — Install into ▸ / Uninstall from ▸ / Status submenus.
- Modify `TrayViewModel.cs` — per-agent + All install/uninstall commands, Status command, results surfaced via tray balloon.
- Create `ClientMenuItem.cs` — `{ Header, McpClient }` row for menu data (optional; menu is static so may be plain XAML MenuItems).

**`SnoopMCP.Installer/`:**
- Modify `Package.wxs` — add finish-page "Register detected AI agents now" opt-in checkbox + deferred `register-clients` action.

**`tests/SnoopMCP.ClientIntegration.Tests/`:**
- Create `CursorWriterTests.cs`, `GeminiCliWriterTests.cs`, `WindsurfWriterTests.cs`, `VisualStudio2022WriterTests.cs`.
- Create `ClaudeCodePermissionsTests.cs`, `SnoopSkillTests.cs`.
- Modify `CopilotCliWriterTests.cs` (http+tools), `CodexWriterTests.cs` (features flag).
- Create `DetectionTests.cs` (IsDetected per writer).

**`tests/SnoopMCP.Cli.Tests/`:** add flag-mapping tests for the 4 new flags.

**`README.md`:** update tray + CLI + agent-list sections.

---

## Phase A — ClientIntegration core (foundation)

### Task A1: Add `IsDetected()` to the writer interface

**Files:** Modify `src/SnoopMCP.ClientIntegration/IClientWriter.cs`

- [ ] **Step 1: Extend the interface**

```csharp
/// <summary>
/// True when the target agent appears to be installed on this machine (its well-known config
/// directory or file exists). "All"/installer runs use this so absent agents are never touched;
/// an explicit single-agent register ignores it.
/// </summary>
bool IsDetected();
```

- [ ] **Step 2: Build (will fail — implementers missing)**

Run: `dotnet build src/SnoopMCP.ClientIntegration/SnoopMCP.ClientIntegration.csproj -p:TreatWarningsAsErrors=true`
Expected: FAIL — `ClaudeDesktopWriter`/`CodexWriter` don't implement `IsDetected`. (Fixed in A2–A4.)

### Task A2: Generalize `JsonMcpServerWriter`

**Files:** Modify `src/SnoopMCP.ClientIntegration/JsonMcpServerWriter.cs`

- [ ] **Step 1: Replace the class body** so entry shape is overridable, the URL key is configurable, `type` is optional, and detection is built in. Full new file:

```csharp
// JsonMcpServerWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Shared logic for registering the SnoopMCP server in a JSON-config MCP client. Subclasses supply the
/// config path, the servers-container key, the URL field name, and whether a <c>type</c> field is
/// emitted. The SnoopMCP entry is added/updated/removed in place via a mutable JSON DOM, so every other
/// key survives. Writes are atomic (temp file then move). Entry shape and status matching are
/// overridable for clients with a non-standard entry.
/// </summary>
public abstract class JsonMcpServerWriter : IClientWriter
{
    /// <summary>Conventional <c>type</c> field name.</summary>
    protected const string TypeKey = "type";

    private const string DefaultUrlKey = "url";
    private const string TempSuffix = ".tmp";

    private static readonly JsonSerializerOptions smWriteOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding smNoBomUtf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string mConfigPath;
    private readonly string mServersKey;
    private readonly string mUrlKey;
    private readonly bool mEmitType;
    private readonly string? mServerType;
    private readonly string mDetectionPath;

    /// <summary>Initialises the writer.</summary>
    /// <param name="configPath">Absolute path to the client's JSON config file.</param>
    /// <param name="serversKey">Name of the object holding server entries (e.g. <c>mcpServers</c>).</param>
    /// <param name="detectionPath">Directory/file whose existence means the agent is installed.</param>
    /// <param name="urlKey">Field name the URL is written under (default <c>url</c>).</param>
    /// <param name="emitType">When true, a <c>type</c> field is written.</param>
    /// <param name="serverType">Value for <c>type</c> when <paramref name="emitType"/> is true and this
    /// is non-null; otherwise the endpoint's own <see cref="McpEndpoint.Type"/> is used.</param>
    protected JsonMcpServerWriter(
        string configPath,
        string serversKey,
        string detectionPath,
        string urlKey = DefaultUrlKey,
        bool emitType = true,
        string? serverType = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(configPath);
        ArgumentException.ThrowIfNullOrEmpty(serversKey);
        ArgumentException.ThrowIfNullOrEmpty(detectionPath);
        ArgumentException.ThrowIfNullOrEmpty(urlKey);
        mConfigPath = configPath;
        mServersKey = serversKey;
        mDetectionPath = detectionPath;
        mUrlKey = urlKey;
        mEmitType = emitType;
        mServerType = serverType;
    }

    /// <inheritdoc />
    public abstract string ClientName { get; }

    /// <summary>The URL field name this client uses (e.g. <c>url</c>, <c>httpUrl</c>, <c>serverUrl</c>).</summary>
    protected string UrlKey => mUrlKey;

    /// <inheritdoc />
    public virtual bool IsDetected()
    {
        return Directory.Exists(mDetectionPath) || File.Exists(mDetectionPath) || File.Exists(mConfigPath);
    }

    /// <inheritdoc />
    public virtual RegisterResult Register(McpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        RegisterResult result;
        try
        {
            JsonObject root = LoadOrCreateRoot();
            JsonObject servers = GetOrAddObject(root, mServersKey);
            servers[endpoint.Name] = BuildEntry(endpoint);
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
    public virtual UnregisterResult Unregister()
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
    public virtual StatusResult GetStatus()
    {
        bool present = false;
        if (File.Exists(mConfigPath))
        {
            try
            {
                JsonObject root = LoadRoot();
                present = root[mServersKey] is JsonObject servers
                    && servers[McpEndpoint.Default.Name] is JsonObject entry
                    && EntryMatches(entry);
            }
            catch (JsonException)
            {
                // Malformed config is treated as "not registered".
            }
        }
        return new StatusResult(present, present
            ? $"{ClientName}: SnoopMCP is registered."
            : $"{ClientName}: SnoopMCP is not registered.");
    }

    /// <summary>Builds the JSON entry written under the server name. Override for a non-standard shape.</summary>
    protected virtual JsonObject BuildEntry(McpEndpoint endpoint)
    {
        var entry = new JsonObject();
        if (mEmitType)
        {
            entry[TypeKey] = mServerType ?? endpoint.Type;
        }
        entry[mUrlKey] = endpoint.Url;
        return entry;
    }

    /// <summary>Returns true when an existing entry is recognised as SnoopMCP's. Override if needed.</summary>
    protected virtual bool EntryMatches(JsonObject entry)
    {
        return string.Equals((string?) entry[mUrlKey], McpEndpoint.Default.Url, StringComparison.Ordinal);
    }

    private JsonObject LoadOrCreateRoot()
    {
        return File.Exists(mConfigPath) ? LoadRoot() : new JsonObject();
    }

    private JsonObject LoadRoot()
    {
        string text = File.ReadAllText(mConfigPath);
        JsonNode? parsed = string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text);
        return parsed as JsonObject ?? throw new JsonException("Root JSON value is not an object.");
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
        File.WriteAllText(tmp, root.ToJsonString(smWriteOptions), smNoBomUtf8);
        File.Move(tmp, mConfigPath, overwrite: true);
    }
}
```

- [ ] **Step 2: Update existing JSON subclass ctors** (they now must pass `detectionPath`). See A3 (ClaudeCode), A5 (Copilot). VS Code below:

In `VsCodeMcpWriter.cs`, change the ctor + `ForCurrentUser`:

```csharp
private const string DetectDirName = "Code";

public VsCodeMcpWriter(string configPath, string detectionPath)
    : base(configPath, ServersKey, detectionPath)
{
}

public static VsCodeMcpWriter ForCurrentUser()
{
    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    string config = Path.Combine(appData, CodeDirName, UserDirName, ConfigFileName);
    string detect = Path.Combine(appData, DetectDirName);
    return new VsCodeMcpWriter(config, detect);
}
```

- [ ] **Step 3: Build** (still fails until ClaudeDesktop/Codex implement `IsDetected` — A4/A6). Defer full build to A7.

### Task A3: Claude Code — timeout, permissions, skill, detection

**Files:** Modify `src/SnoopMCP.ClientIntegration/ClaudeCodeWriter.cs`; Create `src/SnoopMCP.ClientIntegration/SnoopSkill.cs`; Create `skills/snoopmcp-first/SKILL.md`; Test `tests/SnoopMCP.ClientIntegration.Tests/ClaudeCodePermissionsTests.cs`, `SnoopSkillTests.cs`

- [ ] **Step 1: Write the skill file** `skills/snoopmcp-first/SKILL.md`

```markdown
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
```

- [ ] **Step 2: Write the failing test** `SnoopSkillTests.cs`

```csharp
// SnoopSkillTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration.Tests;

/// <summary>Tests for <see cref="SnoopSkill"/> installing/removing the snoopmcp-first skill.</summary>
public sealed class SnoopSkillTests : IDisposable
{
    private readonly string mDir;

    public SnoopSkillTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-skill-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
    }

    [Fact]
    public void Install_WritesSkillMarkdown()
    {
        string skillsDir = Path.Combine(mDir, "skills");

        bool ok = SnoopSkill.Install(skillsDir);

        Assert.True(ok);
        string path = Path.Combine(skillsDir, "snoopmcp-first", "SKILL.md");
        Assert.True(File.Exists(path));
        string text = File.ReadAllText(path);
        Assert.Contains("name: snoopmcp-first", text);
        Assert.Contains("listWpfProcesses", text);
    }

    [Fact]
    public void Install_IsIdempotent()
    {
        string skillsDir = Path.Combine(mDir, "skills");
        SnoopSkill.Install(skillsDir);
        bool ok = SnoopSkill.Install(skillsDir);
        Assert.True(ok);
    }

    [Fact]
    public void Remove_DeletesSkillDir()
    {
        string skillsDir = Path.Combine(mDir, "skills");
        SnoopSkill.Install(skillsDir);

        bool ok = SnoopSkill.Remove(skillsDir);

        Assert.True(ok);
        Assert.False(Directory.Exists(Path.Combine(skillsDir, "snoopmcp-first")));
    }
}
```

- [ ] **Step 3: Run — expect FAIL** (`SnoopSkill` not defined)

Run: `dotnet test tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj --filter FullyQualifiedName~SnoopSkillTests`
Expected: FAIL (compile error: SnoopSkill missing).

- [ ] **Step 4: Implement `SnoopSkill.cs`** — the SKILL.md text is an inlined const (keeps the .csproj free of embedded-resource plumbing; keep this string in sync with `skills/snoopmcp-first/SKILL.md`).

```csharp
// SnoopSkill.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Installs (and removes) the minimal <c>snoopmcp-first</c> skill into a Claude Code skills directory
/// (<c>~/.claude/skills/snoopmcp-first/SKILL.md</c>). The skill body is kept in sync with
/// <c>skills/snoopmcp-first/SKILL.md</c> in the repo.
/// </summary>
public static class SnoopSkill
{
    /// <summary>The skill directory name (becomes the Claude Code skill command).</summary>
    public const string SkillName = "snoopmcp-first";

    private const string SkillFileName = "SKILL.md";

    private const string SkillBody =
        """
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
        """;

    /// <summary>Writes the skill under <paramref name="skillsDir"/>. Returns false on IO failure.</summary>
    public static bool Install(string skillsDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        bool ok;
        try
        {
            string dir = Path.Combine(skillsDir, SkillName);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, SkillFileName), SkillBody);
            ok = true;
        }
        catch (IOException)
        {
            ok = false;
        }
        catch (UnauthorizedAccessException)
        {
            ok = false;
        }
        return ok;
    }

    /// <summary>Removes the skill directory if present. Returns false on IO failure.</summary>
    public static bool Remove(string skillsDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        bool ok = true;
        string dir = Path.Combine(skillsDir, SkillName);
        if (Directory.Exists(dir))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                ok = false;
            }
            catch (UnauthorizedAccessException)
            {
                ok = false;
            }
        }
        return ok;
    }
}
```

- [ ] **Step 5: Run skill tests — expect PASS**

Run: `dotnet test tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj --filter FullyQualifiedName~SnoopSkillTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Write failing permissions test** `ClaudeCodePermissionsTests.cs`

```csharp
// ClaudeCodePermissionsTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration.Tests;

using System.Text.Json;

/// <summary>Tests Claude Code register also writes timeout (ms), permissions, and the skill.</summary>
public sealed class ClaudeCodePermissionsTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;
    private readonly string mSettingsPath;
    private readonly string mSkillsDir;

    public ClaudeCodePermissionsTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-ccperm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, ".claude.json");
        mSettingsPath = Path.Combine(mDir, ".claude", "settings.json");
        mSkillsDir = Path.Combine(mDir, ".claude", "skills");
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
    }

    [Fact]
    public void Register_WritesTimeoutInMilliseconds()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        writer.Register(McpEndpoint.Default);

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mConfigPath));
        JsonElement entry = doc.RootElement.GetProperty("mcpServers").GetProperty("snoopmcp");
        Assert.Equal("http", entry.GetProperty("type").GetString());
        Assert.Equal(120000, entry.GetProperty("timeout").GetInt32());
    }

    [Fact]
    public void Register_AddsWholeServerPermission()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        writer.Register(McpEndpoint.Default);

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mSettingsPath));
        JsonElement allow = doc.RootElement.GetProperty("permissions").GetProperty("allow");
        bool found = allow.EnumerateArray().Any(e => e.GetString() == "mcp__snoopmcp");
        Assert.True(found);
    }

    [Fact]
    public void Register_PermissionsAreIdempotent()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);
        writer.Register(McpEndpoint.Default);
        writer.Register(McpEndpoint.Default);

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mSettingsPath));
        JsonElement allow = doc.RootElement.GetProperty("permissions").GetProperty("allow");
        int count = allow.EnumerateArray().Count(e => e.GetString() == "mcp__snoopmcp");
        Assert.Equal(1, count);
    }

    [Fact]
    public void Register_InstallsSkill()
    {
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        writer.Register(McpEndpoint.Default);

        Assert.True(File.Exists(Path.Combine(mSkillsDir, "snoopmcp-first", "SKILL.md")));
    }

    [Fact]
    public void Register_PreservesExistingPermissions()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(mSettingsPath)!);
        File.WriteAllText(mSettingsPath, """{ "permissions": { "allow": ["Bash(ls:*)"] } }""");
        var writer = new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir);

        writer.Register(McpEndpoint.Default);

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mSettingsPath));
        JsonElement allow = doc.RootElement.GetProperty("permissions").GetProperty("allow");
        var values = allow.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("Bash(ls:*)", values);
        Assert.Contains("mcp__snoopmcp", values);
    }
}
```

- [ ] **Step 7: Run — expect FAIL** (ctor signature/behaviour missing)

Run: `dotnet test tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj --filter FullyQualifiedName~ClaudeCodePermissionsTests`
Expected: FAIL (compile error: 3-arg ctor missing).

- [ ] **Step 8: Rewrite `ClaudeCodeWriter.cs`** — full file:

```csharp
// ClaudeCodeWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Registers SnoopMCP in Claude Code. Writes three things: the <c>mcpServers</c> entry in
/// <c>~/.claude.json</c> (with a millisecond <c>timeout</c>), a whole-server <c>permissions.allow</c>
/// entry (<c>mcp__snoopmcp</c>) in <c>~/.claude/settings.json</c> so the read-only tools are
/// pre-approved, and the <c>snoopmcp-first</c> skill under <c>~/.claude/skills</c>.
/// </summary>
public sealed class ClaudeCodeWriter : JsonMcpServerWriter
{
    private const string ServersKey = "mcpServers";
    private const string ConfigFileName = ".claude.json";
    private const string ClaudeDirName = ".claude";
    private const string SettingsFileName = "settings.json";
    private const string SkillsDirName = "skills";
    private const string ClaudeCodeClientName = "Claude Code";
    private const string TimeoutKey = "timeout";
    private const string PermissionsKey = "permissions";
    private const string AllowKey = "allow";
    private const string WholeServerPermission = "mcp__snoopmcp";
    private const string TempSuffix = ".tmp";
    private const int DefaultTimeoutSeconds = 120;
    private const int MinTimeoutSeconds = 10;
    private const int MillisPerSecond = 1000;

    private static readonly JsonSerializerOptions smWriteOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding smNoBomUtf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string mSettingsPath;
    private readonly string mSkillsDir;

    /// <summary>Initialises the writer against explicit paths (used by tests).</summary>
    public ClaudeCodeWriter(string configPath, string settingsPath, string skillsDir)
        : base(configPath, ServersKey, ResolveDetectionPath(configPath))
    {
        ArgumentException.ThrowIfNullOrEmpty(settingsPath);
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        mSettingsPath = settingsPath;
        mSkillsDir = skillsDir;
    }

    /// <inheritdoc />
    public override string ClientName => ClaudeCodeClientName;

    /// <summary>Creates a writer targeting the current user's Claude Code config.</summary>
    public static ClaudeCodeWriter ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string config = Path.Combine(profile, ConfigFileName);
        string claudeDir = Path.Combine(profile, ClaudeDirName);
        string settings = Path.Combine(claudeDir, SettingsFileName);
        string skills = Path.Combine(claudeDir, SkillsDirName);
        return new ClaudeCodeWriter(config, settings, skills);
    }

    /// <inheritdoc />
    /// <remarks>Adds a millisecond <c>timeout</c> to the base entry.</remarks>
    protected override JsonObject BuildEntry(McpEndpoint endpoint)
    {
        JsonObject entry = base.BuildEntry(endpoint);
        entry[TimeoutKey] = Math.Max(DefaultTimeoutSeconds, MinTimeoutSeconds) * MillisPerSecond;
        return entry;
    }

    /// <inheritdoc />
    public override RegisterResult Register(McpEndpoint endpoint)
    {
        RegisterResult baseResult = base.Register(endpoint);
        RegisterResult result = baseResult;
        if (baseResult.Success)
        {
            bool perms = TryAddPermission();
            bool skill = SnoopSkill.Install(mSkillsDir);
            string extra = (perms, skill) switch
            {
                (true, true) => " Pre-approved tools and installed the snoopmcp-first skill.",
                (true, false) => " Pre-approved tools; skill install failed.",
                (false, true) => " Installed the snoopmcp-first skill; permission write failed.",
                (false, false) => " Permission and skill writes failed."
            };
            result = new RegisterResult(true, baseResult.Message + extra);
        }
        return result;
    }

    /// <inheritdoc />
    public override UnregisterResult Unregister()
    {
        UnregisterResult baseResult = base.Unregister();
        TryRemovePermission();
        SnoopSkill.Remove(mSkillsDir);
        return baseResult;
    }

    private static string ResolveDetectionPath(string configPath)
    {
        // The .claude.json file itself is the detection marker.
        return configPath;
    }

    private bool TryAddPermission()
    {
        bool ok;
        try
        {
            JsonObject root = LoadJsonObjectOrEmpty(mSettingsPath);
            JsonObject permissions = GetOrAddObject(root, PermissionsKey);
            JsonArray allow = permissions[AllowKey] as JsonArray ?? new JsonArray();
            permissions[AllowKey] = allow;
            bool present = allow.Any(n => string.Equals((string?) n, WholeServerPermission, StringComparison.Ordinal));
            if (!present)
            {
                allow.Add(WholeServerPermission);
            }
            WriteAtomic(mSettingsPath, root);
            ok = true;
        }
        catch (JsonException)
        {
            ok = false;
        }
        catch (IOException)
        {
            ok = false;
        }
        return ok;
    }

    private void TryRemovePermission()
    {
        if (File.Exists(mSettingsPath))
        {
            try
            {
                JsonObject root = LoadJsonObjectOrEmpty(mSettingsPath);
                if (root[PermissionsKey] is JsonObject permissions
                    && permissions[AllowKey] is JsonArray allow)
                {
                    JsonNode? match = allow.FirstOrDefault(n =>
                        string.Equals((string?) n, WholeServerPermission, StringComparison.Ordinal));
                    if (match is not null)
                    {
                        allow.Remove(match);
                        WriteAtomic(mSettingsPath, root);
                    }
                }
            }
            catch (JsonException)
            {
                // Leave a malformed settings file untouched.
            }
            catch (IOException)
            {
                // Best effort.
            }
        }
    }

    private static JsonObject LoadJsonObjectOrEmpty(string path)
    {
        JsonObject root = new();
        if (File.Exists(path))
        {
            string text = File.ReadAllText(path);
            if (!string.IsNullOrWhiteSpace(text))
            {
                root = JsonNode.Parse(text) as JsonObject
                    ?? throw new JsonException("Root JSON value is not an object.");
            }
        }
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

    private static void WriteAtomic(string path, JsonObject root)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        string tmp = path + TempSuffix;
        File.WriteAllText(tmp, root.ToJsonString(smWriteOptions), smNoBomUtf8);
        File.Move(tmp, path, overwrite: true);
    }
}
```

- [ ] **Step 9: Run permissions tests — expect PASS**

Run: `dotnet test tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj --filter FullyQualifiedName~ClaudeCodePermissionsTests`
Expected: PASS (5 tests).

- [ ] **Step 10: Update the existing `ClaudeCodeWriterTests`** — the old single-arg ctor is gone; its tests must construct with the 3-arg ctor. For each `new ClaudeCodeWriter(mConfigPath)` add settings + skills temp paths in the test ctor:

```csharp
private readonly string mSettingsPath;
private readonly string mSkillsDir;
// in ctor, after mConfigPath:
mSettingsPath = Path.Combine(mDir, ".claude", "settings.json");
mSkillsDir = Path.Combine(mDir, ".claude", "skills");
// every: new ClaudeCodeWriter(mConfigPath) -> new ClaudeCodeWriter(mConfigPath, mSettingsPath, mSkillsDir)
```

- [ ] **Step 11: Commit**

```bash
git add src/SnoopMCP.ClientIntegration/ClaudeCodeWriter.cs src/SnoopMCP.ClientIntegration/SnoopSkill.cs src/SnoopMCP.ClientIntegration/JsonMcpServerWriter.cs src/SnoopMCP.ClientIntegration/IClientWriter.cs src/SnoopMCP.ClientIntegration/VsCodeMcpWriter.cs skills/snoopmcp-first/SKILL.md tests/SnoopMCP.ClientIntegration.Tests/ClaudeCodePermissionsTests.cs tests/SnoopMCP.ClientIntegration.Tests/SnoopSkillTests.cs tests/SnoopMCP.ClientIntegration.Tests/ClaudeCodeWriterTests.cs
git commit -F commit-A3.txt
```

### Task A4: Claude Desktop & VS Code detection

**Files:** Modify `src/SnoopMCP.ClientIntegration/ClaudeDesktopWriter.cs`

- [ ] **Step 1: Add detection** — `ClaudeDesktopWriter` implements `IClientWriter` directly, so add:

```csharp
private readonly string mDetectionPath;

// ctor gains detectionPath:
public ClaudeDesktopWriter(string configPath, string detectionPath)
{
    ArgumentException.ThrowIfNullOrEmpty(configPath);
    ArgumentException.ThrowIfNullOrEmpty(detectionPath);
    mConfigPath = configPath;
    mDetectionPath = detectionPath;
}

public bool IsDetected() => Directory.Exists(mDetectionPath) || File.Exists(mConfigPath);

// ForCurrentUser:
public static ClaudeDesktopWriter ForCurrentUser()
{
    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    string dir = Path.Combine(appData, ClaudeDirName);
    return new ClaudeDesktopWriter(Path.Combine(dir, ConfigFileName), dir);
}
```

Update `ClaudeDesktopWriterTests` ctor calls to `new ClaudeDesktopWriter(path, Path.GetDirectoryName(path)!)`.

- [ ] **Step 2: Build** `dotnet build src/SnoopMCP.ClientIntegration/SnoopMCP.ClientIntegration.csproj -p:TreatWarningsAsErrors=true` — Codex still missing `IsDetected` (A6). Defer.

### Task A5: Copilot CLI — http + tools (correction)

**Files:** Modify `src/SnoopMCP.ClientIntegration/CopilotCliWriter.cs`; Modify `tests/.../CopilotCliWriterTests.cs`

- [ ] **Step 1: Update the failing test first** — change the type expectation to `http` and assert `tools`:

```csharp
[Fact]
public void Register_WritesHttpTypeAndToolsWildcard()
{
    var writer = new CopilotCliWriter(mConfigPath, Path.GetDirectoryName(mConfigPath)!);
    writer.Register(McpEndpoint.Default);
    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mConfigPath));
    JsonElement entry = doc.RootElement.GetProperty("mcpServers").GetProperty("snoopmcp");
    Assert.Equal("http", entry.GetProperty("type").GetString());
    Assert.Equal("*", entry.GetProperty("tools")[0].GetString());
}
```

(Replace any prior `"sse"` assertion in this file.)

- [ ] **Step 2: Run — expect FAIL**

Run: `dotnet test tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj --filter FullyQualifiedName~CopilotCliWriterTests`
Expected: FAIL.

- [ ] **Step 3: Rewrite `CopilotCliWriter.cs`** — `http` type, `tools:["*"]`, detection:

```csharp
// CopilotCliWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

using System.Text.Json.Nodes;

/// <summary>
/// Registers SnoopMCP in GitHub Copilot CLI's <c>~/.copilot/mcp-config.json</c>, under
/// <c>mcpServers</c>, as a streamable-HTTP server (<c>{ "type": "http", "url": ..., "tools": ["*"] }</c>).
/// Honours <c>COPILOT_HOME</c>.
/// </summary>
public sealed class CopilotCliWriter : JsonMcpServerWriter
{
    private const string ServersKey = "mcpServers";
    private const string HttpType = "http";
    private const string ToolsKey = "tools";
    private const string ToolsWildcard = "*";
    private const string CopilotHomeEnvVar = "COPILOT_HOME";
    private const string CopilotDirName = ".copilot";
    private const string ConfigFileName = "mcp-config.json";
    private const string CopilotCliClientName = "Copilot CLI";

    public CopilotCliWriter(string configPath, string detectionPath)
        : base(configPath, ServersKey, detectionPath, serverType: HttpType)
    {
    }

    public override string ClientName => CopilotCliClientName;

    public static CopilotCliWriter ForCurrentUser()
    {
        string home = Environment.GetEnvironmentVariable(CopilotHomeEnvVar) is { Length: > 0 } copilotHome
            ? copilotHome
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), CopilotDirName);
        return new CopilotCliWriter(Path.Combine(home, ConfigFileName), home);
    }

    protected override JsonObject BuildEntry(McpEndpoint endpoint)
    {
        JsonObject entry = base.BuildEntry(endpoint);
        entry[ToolsKey] = new JsonArray { ToolsWildcard };
        return entry;
    }
}
```

- [ ] **Step 4: Run — expect PASS** (same filter as Step 2).

### Task A6: Codex — features flag + detection (correction)

**Files:** Modify `src/SnoopMCP.ClientIntegration/CodexWriter.cs`; Modify `tests/.../CodexWriterTests.cs`

- [ ] **Step 1: Add failing test** for the features flag:

```csharp
[Fact]
public void Register_EnablesRmcpFeatureFlag()
{
    var writer = new CodexWriter(mConfigPath, Path.GetDirectoryName(mConfigPath)!);
    writer.Register(McpEndpoint.Default);
    TomlTable root = Toml.ToModel(File.ReadAllText(mConfigPath));
    var features = (TomlTable) root["features"];
    Assert.True((bool) features["experimental_use_rmcp_client"]);
}
```

- [ ] **Step 2: Run — expect FAIL** (`--filter FullyQualifiedName~CodexWriterTests`).

- [ ] **Step 3: Modify `CodexWriter`** — add detection ctor param + write the flag in `Register`:

```csharp
private const string FeaturesTableKey = "features";
private const string RmcpFlagKey = "experimental_use_rmcp_client";

private readonly string mDetectionPath;

public CodexWriter(string configPath, string detectionPath)
{
    ArgumentException.ThrowIfNullOrEmpty(configPath);
    ArgumentException.ThrowIfNullOrEmpty(detectionPath);
    mConfigPath = configPath;
    mDetectionPath = detectionPath;
}

public bool IsDetected() => Directory.Exists(mDetectionPath) || File.Exists(mConfigPath);

public static CodexWriter ForCurrentUser()
{
    string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    string dir = Path.Combine(profile, CodexDirName);
    return new CodexWriter(Path.Combine(dir, ConfigFileName), dir);
}
```

In `Register`, after adding the server table, also set the flag:

```csharp
TomlTable servers = GetOrAddTable(root, ServersTableKey);
servers[endpoint.Name] = new TomlTable { { UrlKey, endpoint.Url } };
TomlTable features = GetOrAddTable(root, FeaturesTableKey);
features[RmcpFlagKey] = true;
WriteAtomic(root);
```

(`Unregister` leaves the `[features]` flag in place — it is harmless and may be shared; only the server table entry is removed. Document this in the method remarks.)

- [ ] **Step 4: Run — expect PASS** (same filter).

### Task A7: The four new JSON writers

**Files:** Create `CursorWriter.cs`, `GeminiCliWriter.cs`, `WindsurfWriter.cs`, `VisualStudio2022Writer.cs`; Create matching test files.

Each writer is a thin subclass. Pattern (Cursor shown; the others differ only in constants/keys):

- [ ] **Step 1: Failing test** `CursorWriterTests.cs`

```csharp
// CursorWriterTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration.Tests;

using System.Text.Json;

public sealed class CursorWriterTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;

    public CursorWriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-cursor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, "mcp.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir)) Directory.Delete(mDir, recursive: true);
    }

    [Fact]
    public void Register_WritesBareUrlUnderMcpServers()
    {
        var writer = new CursorWriter(mConfigPath, mDir);
        writer.Register(McpEndpoint.Default);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mConfigPath));
        JsonElement entry = doc.RootElement.GetProperty("mcpServers").GetProperty("snoopmcp");
        Assert.Equal("http://127.0.0.1:6300/mcp", entry.GetProperty("url").GetString());
        Assert.False(entry.TryGetProperty("type", out _));
    }

    [Fact]
    public void Status_TrueAfterRegister()
    {
        var writer = new CursorWriter(mConfigPath, mDir);
        writer.Register(McpEndpoint.Default);
        Assert.True(writer.GetStatus().IsRegistered);
    }

    [Fact]
    public void Unregister_RemovesEntry()
    {
        var writer = new CursorWriter(mConfigPath, mDir);
        writer.Register(McpEndpoint.Default);
        writer.Unregister();
        Assert.False(writer.GetStatus().IsRegistered);
    }
}
```

- [ ] **Step 2: Run — expect FAIL**.

- [ ] **Step 3: Implement the four writers.**

`CursorWriter.cs`:

```csharp
// CursorWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>Registers SnoopMCP in Cursor's <c>~/.cursor/mcp.json</c> under <c>mcpServers</c> as a
/// streamable-HTTP server (bare <c>{ "url": ... }</c>, no <c>type</c>).</summary>
public sealed class CursorWriter : JsonMcpServerWriter
{
    private const string ServersKey = "mcpServers";
    private const string CursorDirName = ".cursor";
    private const string ConfigFileName = "mcp.json";
    private const string CursorClientName = "Cursor";

    public CursorWriter(string configPath, string detectionPath)
        : base(configPath, ServersKey, detectionPath, emitType: false)
    {
    }

    public override string ClientName => CursorClientName;

    public static CursorWriter ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dir = Path.Combine(profile, CursorDirName);
        return new CursorWriter(Path.Combine(dir, ConfigFileName), dir);
    }
}
```

`GeminiCliWriter.cs` — key `mcpServers`, url field `httpUrl`, dir `~/.gemini`, file `settings.json`:

```csharp
// GeminiCliWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>Registers SnoopMCP in Gemini CLI's <c>~/.gemini/settings.json</c> under <c>mcpServers</c>
/// with the streamable-HTTP <c>httpUrl</c> field.</summary>
public sealed class GeminiCliWriter : JsonMcpServerWriter
{
    private const string ServersKey = "mcpServers";
    private const string HttpUrlKey = "httpUrl";
    private const string GeminiDirName = ".gemini";
    private const string ConfigFileName = "settings.json";
    private const string GeminiClientName = "Gemini CLI";

    public GeminiCliWriter(string configPath, string detectionPath)
        : base(configPath, ServersKey, detectionPath, urlKey: HttpUrlKey, emitType: false)
    {
    }

    public override string ClientName => GeminiClientName;

    public static GeminiCliWriter ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dir = Path.Combine(profile, GeminiDirName);
        return new GeminiCliWriter(Path.Combine(dir, ConfigFileName), dir);
    }
}
```

`WindsurfWriter.cs` — key `mcpServers`, url field `serverUrl`, file `%USERPROFILE%\.codeium\windsurf\mcp_config.json`:

```csharp
// WindsurfWriter.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>Registers SnoopMCP in Windsurf's <c>~/.codeium/windsurf/mcp_config.json</c> under
/// <c>mcpServers</c> with the remote <c>serverUrl</c> field.</summary>
public sealed class WindsurfWriter : JsonMcpServerWriter
{
    private const string ServersKey = "mcpServers";
    private const string ServerUrlKey = "serverUrl";
    private const string CodeiumDirName = ".codeium";
    private const string WindsurfDirName = "windsurf";
    private const string ConfigFileName = "mcp_config.json";
    private const string WindsurfClientName = "Windsurf";

    public WindsurfWriter(string configPath, string detectionPath)
        : base(configPath, ServersKey, detectionPath, urlKey: ServerUrlKey, emitType: false)
    {
    }

    public override string ClientName => WindsurfClientName;

    public static WindsurfWriter ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dir = Path.Combine(profile, CodeiumDirName, WindsurfDirName);
        return new WindsurfWriter(Path.Combine(dir, ConfigFileName), dir);
    }
}
```

`VisualStudio2022Writer.cs` — key `servers`, url field `url`, no type, file `%USERPROFILE%\.mcp.json`; detection prefers the VS data dir:

```csharp
// VisualStudio2022Writer.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>Registers SnoopMCP in Visual Studio 2022's global <c>~/.mcp.json</c> under <c>servers</c>
/// as a streamable-HTTP server (bare <c>{ "url": ... }</c>).</summary>
public sealed class VisualStudio2022Writer : JsonMcpServerWriter
{
    private const string ServersKey = "servers";
    private const string ConfigFileName = ".mcp.json";
    private const string VsDataDirName = "Microsoft\\VisualStudio";
    private const string VsClientName = "Visual Studio 2022";

    public VisualStudio2022Writer(string configPath, string detectionPath)
        : base(configPath, ServersKey, detectionPath, emitType: false)
    {
    }

    public override string ClientName => VsClientName;

    public static VisualStudio2022Writer ForCurrentUser()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string config = Path.Combine(profile, ConfigFileName);
        string detect = Path.Combine(localAppData, VsDataDirName);
        return new VisualStudio2022Writer(config, detect);
    }
}
```

- [ ] **Step 4: Add the other three test files** (`GeminiCliWriterTests` asserts `httpUrl`; `WindsurfWriterTests` asserts `serverUrl`; `VisualStudio2022WriterTests` asserts `servers`/`url`, no type), mirroring `CursorWriterTests`.

- [ ] **Step 5: Run all new writer tests — expect PASS**

Run: `dotnet test tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj --filter "FullyQualifiedName~CursorWriterTests|FullyQualifiedName~GeminiCliWriterTests|FullyQualifiedName~WindsurfWriterTests|FullyQualifiedName~VisualStudio2022WriterTests"`
Expected: PASS.

### Task A8: Enum + factory + detection helper

**Files:** Modify `McpClient.cs`, `ClientRegistration.cs` (lib); Create `DetectionTests.cs`

- [ ] **Step 1: Extend the enum**

```csharp
/// <summary>Cursor (~/.cursor/mcp.json).</summary>
Cursor,
/// <summary>Gemini CLI (~/.gemini/settings.json).</summary>
GeminiCli,
/// <summary>Windsurf (~/.codeium/windsurf/mcp_config.json).</summary>
Windsurf,
/// <summary>Visual Studio 2022 (~/.mcp.json).</summary>
VisualStudio2022
```

- [ ] **Step 2: Extend the factory switch** in lib `ClientRegistration.CreateWriter`:

```csharp
McpClient.Cursor => CursorWriter.ForCurrentUser(),
McpClient.GeminiCli => GeminiCliWriter.ForCurrentUser(),
McpClient.Windsurf => WindsurfWriter.ForCurrentUser(),
McpClient.VisualStudio2022 => VisualStudio2022Writer.ForCurrentUser(),
```

- [ ] **Step 3: Add a detected-only helper** to lib `ClientRegistration`:

```csharp
/// <summary>The writers for every client whose agent is detected as installed on this machine.</summary>
public static IReadOnlyList<IClientWriter> DetectedWriters()
{
    return AllClients.Select(CreateWriter).Where(w => w.IsDetected()).ToArray();
}
```

- [ ] **Step 4: Failing detection test** `DetectionTests.cs` — verify a writer pointed at a non-existent dir reports not-detected, and one pointed at an existing dir reports detected:

```csharp
// DetectionTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration.Tests;

public sealed class DetectionTests : IDisposable
{
    private readonly string mDir;

    public DetectionTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-detect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir)) Directory.Delete(mDir, recursive: true);
    }

    [Fact]
    public void IsDetected_FalseWhenDirMissing()
    {
        string missing = Path.Combine(mDir, "nope");
        var writer = new CursorWriter(Path.Combine(missing, "mcp.json"), missing);
        Assert.False(writer.IsDetected());
    }

    [Fact]
    public void IsDetected_TrueWhenDirExists()
    {
        var writer = new CursorWriter(Path.Combine(mDir, "mcp.json"), mDir);
        Assert.True(writer.IsDetected());
    }
}
```

- [ ] **Step 5: Run + full ClientIntegration build/test — expect PASS**

Run: `dotnet test tests/SnoopMCP.ClientIntegration.Tests/SnoopMCP.ClientIntegration.Tests.csproj -p:TreatWarningsAsErrors=true`
Expected: PASS (all writers + detection + skill + permissions).

- [ ] **Step 6: Commit**

```bash
git add src/SnoopMCP.ClientIntegration tests/SnoopMCP.ClientIntegration.Tests
git commit -F commit-A.txt
```

---

## Phase B — CLI (9 agents, individually + all)

### Task B1: Add the four client flags

**Files:** Modify `src/SnoopMCP.Cli/Program.cs`; Test `tests/SnoopMCP.Cli.Tests/`

The verbs (`register-clients`/`unregister-clients`/`status`) already iterate `SelectWriters(args)` and need no change. Add flags and map them.

- [ ] **Step 1: Failing test** (if `SelectWriters` is private, expose an internal `SelectClients(string[])` returning `IReadOnlyList<McpClient>` and test that; mark assembly `InternalsVisibleTo` the test project if not already). Test that `--cursor` selects only Cursor, and no flags selects all 9.

- [ ] **Step 2: Add flag constants + selection** in `Program.cs`:

```csharp
private const string FlagCursor = "--cursor";
private const string FlagGemini = "--gemini-cli";
private const string FlagWindsurf = "--windsurf";
private const string FlagVisualStudio = "--visual-studio";
```

Refactor `SelectWriters` to build from `McpClient` values via the lib factory so the list stays single-sourced:

```csharp
private static List<IClientWriter> SelectWriters(string[] args)
{
    return SelectClients(args).Select(SnoopMCP.ClientIntegration.ClientRegistration.CreateWriter).ToList();
}

internal static IReadOnlyList<McpClient> SelectClients(string[] args)
{
    var map = new (string Flag, McpClient Client)[]
    {
        (FlagClaudeCode, McpClient.ClaudeCode),
        (FlagClaudeDesktop, McpClient.ClaudeDesktop),
        (FlagVsCode, McpClient.VsCode),
        (FlagCodex, McpClient.Codex),
        (FlagCopilotCli, McpClient.CopilotCli),
        (FlagCursor, McpClient.Cursor),
        (FlagGemini, McpClient.GeminiCli),
        (FlagWindsurf, McpClient.Windsurf),
        (FlagVisualStudio, McpClient.VisualStudio2022),
    };
    List<McpClient> selected = map.Where(m => HasFlag(args, m.Flag)).Select(m => m.Client).ToList();
    return selected.Count > 0 ? selected : SnoopMCP.ClientIntegration.ClientRegistration.AllClients;
}
```

- [ ] **Step 3: Update usage text** in `PrintUsage` to list the 4 new flags.

- [ ] **Step 4: Run CLI tests + build — expect PASS**; **Commit.**

```bash
git add src/SnoopMCP.Cli tests/SnoopMCP.Cli.Tests
git commit -F commit-B.txt
```

---

## Phase C — Tray (Install into ▸ / Uninstall from ▸ / Status)

### Task C1: Tray view-model commands

**Files:** Modify `src/SnoopMCP.Host/TrayViewModel.cs`; Modify `src/SnoopMCP.Host/App.xaml`

Surfacing results: the tray uses H.NotifyIcon; show a balloon tip via the `TaskbarIcon` for the aggregate result, and write a summary to the existing log. Commands live on `TrayViewModel`. Keep behaviour XAML-first (per [[wpf-xaml-first]]): all menu items declared in XAML, bound to commands.

- [ ] **Step 1: Add commands to `TrayViewModel`** — one parameterized install/uninstall command taking the `McpClient` (via `CommandParameter`), plus All and Status. Sketch:

```csharp
using SnoopMCP.ClientIntegration;

private readonly RelayCommand<McpClient?> mInstallCommand;
private readonly RelayCommand<McpClient?> mUninstallCommand;
private readonly RelayCommand mStatusCommand;
private readonly Action<string, string> mNotify; // (title, message) -> balloon

// ctor param: Action<string,string> notify
mInstallCommand = new RelayCommand<McpClient?>(InstallExecute);
mUninstallCommand = new RelayCommand<McpClient?>(UninstallExecute);
mStatusCommand = new RelayCommand(StatusExecute);

public ICommand InstallCommand => mInstallCommand;
public ICommand UninstallCommand => mUninstallCommand;
public ICommand StatusCommand => mStatusCommand;

private void InstallExecute(McpClient? client)
{
    IReadOnlyList<IClientWriter> writers = client is { } c
        ? new[] { ClientRegistration.CreateWriter(c) }
        : ClientRegistration.DetectedWriters();
    IReadOnlyList<RegisterResult> results = ClientRegistration.Register(writers, McpEndpoint.Default);
    mNotify("SnoopMCP — install", Summarize(writers, results.Select(r => (r.Success, r.Message))));
}
```

(Define `RelayCommand<T>` alongside the existing `RelayCommand`, or extend it. `Summarize` builds `"OK Claude Code; OK Cursor; ..."`. `UninstallExecute` mirrors with `Unregister`. `StatusExecute` runs `GetStatus` over all 9 and balloons the summary.)

The "All" menu item binds `InstallCommand` with no `CommandParameter` (null ⇒ detected-only).

- [ ] **Step 2: Wire `App.xaml.cs`** to pass a `notify` that calls `TrayIcon.ShowBalloonTip(...)` (resolve the `TaskbarIcon` resource already created in `OnStartup`).

- [ ] **Step 3: Add the submenus to `App.xaml`** (XAML-first). Insert between "Stop MCP" and the Exit separator:

```xml
<Separator />
<MenuItem Header="Install into">
    <MenuItem Header="All detected agents" Command="{Binding InstallCommand}" />
    <Separator />
    <MenuItem Header="Claude Code"        Command="{Binding InstallCommand}" CommandParameter="{x:Static ci:McpClient.ClaudeCode}" />
    <MenuItem Header="Claude Desktop"     Command="{Binding InstallCommand}" CommandParameter="{x:Static ci:McpClient.ClaudeDesktop}" />
    <MenuItem Header="VS Code"            Command="{Binding InstallCommand}" CommandParameter="{x:Static ci:McpClient.VsCode}" />
    <MenuItem Header="Copilot CLI"        Command="{Binding InstallCommand}" CommandParameter="{x:Static ci:McpClient.CopilotCli}" />
    <MenuItem Header="Codex"              Command="{Binding InstallCommand}" CommandParameter="{x:Static ci:McpClient.Codex}" />
    <MenuItem Header="Cursor"             Command="{Binding InstallCommand}" CommandParameter="{x:Static ci:McpClient.Cursor}" />
    <MenuItem Header="Gemini CLI"         Command="{Binding InstallCommand}" CommandParameter="{x:Static ci:McpClient.GeminiCli}" />
    <MenuItem Header="Windsurf"           Command="{Binding InstallCommand}" CommandParameter="{x:Static ci:McpClient.Windsurf}" />
    <MenuItem Header="Visual Studio 2022" Command="{Binding InstallCommand}" CommandParameter="{x:Static ci:McpClient.VisualStudio2022}" />
</MenuItem>
<MenuItem Header="Uninstall from"> ... same 9 + "All registered agents" bound to UninstallCommand ... </MenuItem>
<MenuItem Header="Status" Command="{Binding StatusCommand}" />
```

Add the namespace to the `Application` root: `xmlns:ci="clr-namespace:SnoopMCP.ClientIntegration;assembly=SnoopMCP.ClientIntegration"`, and add a `ProjectReference` from `SnoopMCP.Host` to `SnoopMCP.ClientIntegration` if not present.

- [ ] **Step 4: Test** — `RelayCommand<T>` + a `TrayViewModel` test with a fake `notify` and writers pointed at temp paths is hard against `ForCurrentUser`; instead unit-test the new `Summarize` formatting and `RelayCommand<T>` execution in `SnoopMCP.Host.Tests`. Verify the build compiles and the menu binds (manual smoke in Phase F).

- [ ] **Step 5: Build host + Commit.**

```bash
git add src/SnoopMCP.Host tests/SnoopMCP.Host.Tests
git commit -F commit-C.txt
```

---

## Phase D — Installer (opt-in "register detected agents now")

### Task D1: Finish-page checkbox + deferred register action

**Files:** Modify `SnoopMCP.Installer/Package.wxs`

The exit dialog already hosts one optional checkbox ("Launch SnoopMCP now"). WixUI_Minimal's ExitDialog supports a single optional checkbox, so model "register detected agents" as a **deferred install action gated by a property set from a second UI control is not available in Minimal** — instead add it as a deferred action that runs by default during install, controlled by a public property `REGISTERAGENTS` (default `1`) that the existing checkbox cannot also drive. Decision: add the registration as a **deferred, best-effort action that runs on install by default**, and expose `REGISTERAGENTS=0` to suppress it via command line, AND repurpose the visible finish checkbox text to make it user-visible. Concretely:

- [ ] **Step 1: Add a property + register action** (best-effort; `Return="ignore"` so a failure never fails the install). Registration uses the CLI against detected agents only — add a CLI verb `register-clients --detected-only` (see D2) or pass no flags and rely on detection. This plan adds `--detected-only`:

```xml
<Property Id="REGISTERAGENTS" Value="1" />

<SetProperty Id="RegisterAgents" Before="RegisterAgents" Sequence="execute"
             Value="&quot;[INSTALLFOLDER]SnoopMCP.Cli.exe&quot; register-clients --detected-only" />
<CustomAction Id="RegisterAgents" BinaryRef="Wix4UtilCA_X86" DllEntry="WixQuietExec"
              Execute="deferred" Return="ignore" Impersonate="yes" />
```

- [ ] **Step 2: Sequence it after autostart, gated by the property and a fresh install:**

```xml
<Custom Action="RegisterAgents" After="InstallAutostart"
        Condition="(NOT Installed OR REINSTALL) AND REGISTERAGENTS = &quot;1&quot;" />
```

- [ ] **Step 3: Make it user-visible on the finish page.** WixUI_Minimal allows one optional checkbox; keep "Launch SnoopMCP now" as is, and add the agent-registration choice as a **second checkbox via a customized ExitDialog** OR (simpler, chosen here) change the optional checkbox to drive registration and launch the host unconditionally. Final decision for this plan: set the existing finish checkbox text to "Register SnoopMCP into detected AI agents" and bind it to `REGISTERAGENTS`; launch the host unconditionally after install (the host is the tray and should always come up). Replace the exit-dialog block:

```xml
<Property Id="WIXUI_EXITDIALOGOPTIONALCHECKBOXTEXT" Value="Register SnoopMCP into detected AI agents now" />
<Property Id="WIXUI_EXITDIALOGOPTIONALCHECKBOX" Value="1" />
<!-- Launch the tray host unconditionally on a fresh install. -->
<CustomAction Id="SetLaunchTarget" Property="WixShellExecTarget" Value="[INSTALLFOLDER]SnoopMCP.Host.exe" />
<CustomAction Id="LaunchSnoopMcp" BinaryRef="Wix4UtilCA_X86" DllEntry="WixShellExec"
              Execute="immediate" Impersonate="yes" Return="ignore" />
```

And set `REGISTERAGENTS` from the checkbox at finish:

```xml
<Publish Dialog="ExitDialog" Control="Finish" Event="DoAction" Value="LaunchSnoopMcp"
         Condition="NOT Installed" />
```

Because the deferred `RegisterAgents` runs during `InstallExecuteSequence` (before the finish page), gate it on `REGISTERAGENTS` which defaults to 1; allow suppression via `msiexec /i SnoopMCP.msi REGISTERAGENTS=0`. (Note in the plan: a per-finish-page-checkbox gate would require a custom ExitDialog; the default-on + command-line-suppress model is the pragmatic WixUI_Minimal fit and still "best-effort, detected-only, never fails the install".)

- [ ] **Step 4: Update the `ProgressText`/comments** — replace the old "Client registration is intentionally NOT done here" comment with the new best-effort detected-only behaviour. Add:

```xml
<ProgressText Action="RegisterAgents" Message="Registering SnoopMCP into detected AI tools..." />
```

- [ ] **Step 5: Build the MSI locally to validate WiX** (requires WiX 5 + the publish stage):

Run: `pwsh -File SnoopMCP.Installer/build-installer.ps1 -Version 1.2.0 -Configuration Release`
Expected: "Built ...SnoopMCP.msi (version 1.2.0)." (No WiX errors.)

- [ ] **Step 6: Commit.**

```bash
git add SnoopMCP.Installer/Package.wxs
git commit -F commit-D.txt
```

### Task D2: CLI `--detected-only` flag

**Files:** Modify `src/SnoopMCP.Cli/Program.cs`

- [ ] **Step 1: Failing test** — `register-clients --detected-only` selects only detected writers (test via `SelectClients`/a new `SelectWriters` path that honours `--detected-only` by filtering on `IsDetected()`).

- [ ] **Step 2: Implement** — in `SelectWriters`, if `--detected-only` present, filter the chosen writers by `IsDetected()`:

```csharp
private const string FlagDetectedOnly = "--detected-only";
// after building writers from SelectClients:
if (HasFlag(args, FlagDetectedOnly))
{
    writers = writers.Where(w => w.IsDetected()).ToList();
}
```

- [ ] **Step 3: Run + Commit** (fold into Phase B commit if done together).

---

## Phase E — Docs + end-to-end proof

### Task E1: README

**Files:** Modify `README.md`

- [ ] Update the agent list to all 9; document tray Install into ▸ / Uninstall from ▸ / Status; document the 4 new CLI flags + `--detected-only`; note Claude Code now gets timeout(ms) + permissions + the `snoopmcp-first` skill; note the installer's opt-in registration. Commit.

### Task E2: Full solution build + test

- [ ] **Step 1:** `dotnet build SnoopMCP.sln -c Release -p:TreatWarningsAsErrors=true` — expect success.
- [ ] **Step 2:** `dotnet test SnoopMCP.sln -c Release` — expect all green.

### Task E3: End-to-end install proof (manual, documented in PR)

- [ ] Build MSI (`build-installer.ps1`), install per-user, log out/in (or run the logon task), confirm: tray icon appears, tooltip reflects running state, Start/Stop work, "Install into ▸ Claude Code" writes `~/.claude.json` (timeout 120000) + `~/.claude/settings.json` (`mcp__snoopmcp`) + `~/.claude/skills/snoopmcp-first/SKILL.md`, "Status" balloons per-agent state, and the installer's detected-only registration ran without failing the install. Record results in the PR description.

### Task E4: Open PR

- [ ] Push `agent-install-experience`; open a PR to `master` (body = user content only, no AI attribution). Wait for the required `build` check.

---

## Self-review

**Spec coverage:**
- Req 1 (new writers Cursor/Gemini/Windsurf/VS2022 + enum + factory, keep base, own-entry atomic) → A7, A8 ✓
- Req 2 (permissions.allow + skill, or skip) → A3 (skill created; whole-server permission) ✓
- Req 3 (Claude Code timeout in MS = Max(s,10)*1000) → A3 Step 8 `BuildEntry` ✓
- Req 4 (tray Install/Uninstall/Status submenus, 9 + All, commands → registrar, keep Start/Stop/Exit) → C1 ✓
- Req 5 (CLI all 9, individually + all-at-once) → B1 ✓
- Req 6 (installer best-effort detected-only post-install register, never fail) → D1, D2 ✓ (opt-in surfaced per user's choice)
- Req 7 (best-effort, present-only, per-agent results, no throw on absence) → `IsDetected`/`DetectedWriters` (A2/A8), `Return="ignore"` (D1), per-result records throughout ✓
- Req 8 (verify formats) → done pre-plan; corrections baked into A5/A6/A7 ✓
- Req 9 (coding standards, tests per writer + permissions/skill, feature branch, leave for review) → all phases TDD; branch `agent-install-experience` created off master; E4 ✓
- Tray-as-primary-deliverable + auto-launch/auto-start + deployed exe → installer already lays down `SnoopMCP.Host.exe`, ONLOGON task (verified); D1 launches host on install; E3 proves end-to-end ✓

**Placeholder scan:** Tray Task C1 Steps 1/4 are sketch-level (RelayCommand<T>, Summarize) rather than full files — flagged as the one area to finalize during execution against the real `RelayCommand`. D1 Step 3 documents a WixUI_Minimal limitation and the chosen pragmatic model rather than a custom ExitDialog. All ClientIntegration tasks (the bulk) have complete code.

**Type consistency:** `IsDetected()` (interface A1) used in A2/A4/A6/A8/D2 ✓. `ClientRegistration.CreateWriter`/`AllClients`/`DetectedWriters`/`Register` signatures consistent across B1/C1/A8 ✓. `SnoopSkill.Install/Remove(skillsDir)` consistent A3 ✓. New writer ctors are all `(configPath, detectionPath)`; ClaudeCode is `(configPath, settingsPath, skillsDir)` — reflected in every test ✓.

**Known follow-ups to finalize in execution:** (a) confirm `SnoopMCP.Host` already references `SnoopMCP.ClientIntegration` (add ProjectReference if not); (b) verify `InternalsVisibleTo` for the CLI test project before relying on `internal SelectClients`; (c) finalize the tray `RelayCommand<T>` and result-balloon wiring.
