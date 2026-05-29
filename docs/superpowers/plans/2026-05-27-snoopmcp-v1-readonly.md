# SnoopMCP v1 (Read-Only) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a read-only MCP server (`SnoopMCP.Host.exe`) that injects a payload DLL (`SnoopMCP.Payload.dll`) into any running .NET 10+ WPF process and exposes 19 inspection tools over stdio so an LLM client can diagnose styling, binding, and resolution problems live.

**Architecture:** Host process speaks MCP/JSON-RPC over stdio to its client (external transport) and length-prefixed JSON over a named pipe to its payload (internal transport). Payload runs inside the target, marshals every WPF call onto the Dispatcher, and owns an element-identity registry mapping stable integer ids to `WeakReference<DependencyObject>`. Cross-process injection uses Snoop's `ManagedInjector`, pinned as a **git submodule** under `external/snoopwpf/` and referenced via a thin `SnoopMCP.Injection` wrapper csproj.

**Tech Stack:**
- .NET 10 (target framework `net10.0` for cross-cutting libs; `net10.0-windows` with `<UseWPF>true</UseWPF>` for payload and sample app)
- `ModelContextProtocol.AspNetCore` 1.2.0 (MCP C# SDK — provides `StdioServerTransport`, `[McpServerTool]`/`[McpServerToolType]`)
- `Microsoft.Extensions.Hosting` 10.0.0 for the host's lifecycle
- `Microsoft.Extensions.Logging` 10.0.0 for logging (no `IPenskeLogger` — see Coding Standards Note)
- `System.IO.Pipes.NamedPipeServerStream` / `NamedPipeClientStream` for host↔payload (internal transport — not exposed to the LLM)
- `System.Text.Json` for all JSON serialization
- xUnit v3 + `Xunit.StaFact` for unit tests requiring an STA thread
- `CodeStructure.Analyzers` 1.0.7 — Jackalope's Roslyn analyzer (same package SaddleRAG uses), applied repo-wide via `Directory.Build.props`
- Snoop's `ManagedInjector` from https://github.com/snoopwpf/snoopwpf, **pinned as a git submodule** under `external/snoopwpf/` with attribution

**Transport summary (called out so it stays straight):**

- **MCP / stdio** is the *external* transport — between the LLM client and `SnoopMCP.Host.exe`.
- **Named pipe / length-prefixed JSON** is the *internal* transport — between `SnoopMCP.Host.exe` and the `SnoopMCP.Payload.dll` we injected into the target. Snoop itself does not run; only its `ManagedInjector` source is used, and only to load our payload.

**Repository:** `https://github.com/JackalopeTechnologies/SnoopMCP` (org `JackalopeTechnologies`, owner `wyodoug`). Identity is wired via `~/.gitconfig` `includeIf` for `E:/GitHub/` → `douglas@jackalopetechnologies.com`; do not change it.

---

## Coding Standards Note

This is a Jackalope-identity repo, not Penske. The Penske coding rules in `CLAUDE.md` apply with these substitutions:

| Rule | Apply? | Substitution |
|---|---|---|
| Big Three (single return, no if/else chains, no continue) | Yes | Strict |
| Universal (no magic numbers, max 3 nesting, Allman braces, 4-space indent, 120 char lines) | Yes | Strict |
| Field prefixes `m`/`ps`/`sm`/`pm` | Yes | Strict |
| Allowed abbreviations | Yes | Add `MCP`, `JSON`, `RPC`, `DP`, `WPF`, `STA` as project-local |
| Regions for grouped members | Yes | Strict — per templates in `CLAUDE.md` |
| Nullability enabled | Yes | `<Nullable>enable</Nullable>` in every csproj |
| `IPenskeLogger` | **No** | Use `Microsoft.Extensions.Logging.ILogger<T>` |
| Penske exception hierarchy | **No** | Use BCL: `ArgumentException`, `InvalidOperationException`, `TimeoutException`, plus project-local `SnoopMcpException` base |
| File header `// ${File.FileName}` + Penske copyright | **No** | No header |
| JSON G17 converters | **No** | Standard `System.Text.Json` options; specify `JsonSerializerOptions.WriteIndented = false` for wire JSON |
| Code-standards analyzer | **Added** | `CodeStructure.Analyzers` 1.0.7 wired in `Directory.Build.props` (same package SaddleRAG uses). Enforces `STR*` / `STY*` / `NUM*` / `ENC*` rules at build time so style drift fails CI, not review. |

---

## File Structure

```text
E:\GitHub\SnoopMCP\
├── .editorconfig                                                  ← created in Task 1
├── Directory.Build.props                                          ← created in Task 1
├── global.json                                                    ← created in Task 1
├── SnoopMCP.sln                                                   ← created in Task 1
│
├── src\
│   ├── SnoopMCP.Protocol\                                         ← created in Task 4
│   │   ├── SnoopMCP.Protocol.csproj
│   │   ├── Wire\
│   │   │   ├── RpcRequest.cs                                      ← Task 4
│   │   │   ├── RpcResponse.cs                                     ← Task 4
│   │   │   ├── RpcError.cs                                        ← Task 4
│   │   │   └── WireSerializer.cs                                  ← Task 4
│   │   ├── Tools\                                                 ← per-tool DTOs, Task 4 + tool tasks
│   │   │   ├── AttachDto.cs
│   │   │   ├── ListVisualRootsDto.cs
│   │   │   ├── DescribeElementDto.cs
│   │   │   ├── GetChildrenDto.cs
│   │   │   ├── GetParentDto.cs
│   │   │   ├── GetTemplatedParentDto.cs
│   │   │   ├── FindElementsDto.cs
│   │   │   ├── HitTestDto.cs
│   │   │   ├── ResolvePathDto.cs
│   │   │   ├── DescribeDataContextDto.cs
│   │   │   ├── ReadDataContextPathDto.cs
│   │   │   ├── ListDependencyPropertiesDto.cs
│   │   │   ├── GetDependencyPropertyDto.cs
│   │   │   ├── ResolveStyleDto.cs
│   │   │   ├── ResolveTemplateDto.cs
│   │   │   └── InspectBindingDto.cs
│   │   └── Errors\
│   │       ├── ErrorCode.cs                                       ← Task 4
│   │       └── SnoopMcpException.cs                               ← Task 4
│   │
│   ├── SnoopMCP.Payload\                                          ← created in Task 6
│   │   ├── SnoopMCP.Payload.csproj
│   │   ├── PayloadEntryPoint.cs                                   ← Task 6
│   │   ├── PipeServer.cs                                          ← Task 6
│   │   ├── DispatcherMarshal.cs                                   ← Task 7
│   │   ├── ElementRegistry.cs                                     ← Task 8
│   │   ├── PathStrings\
│   │   │   ├── PathStringEmitter.cs                               ← Task 9
│   │   │   └── PathStringParser.cs                                ← Task 9
│   │   └── Inspection\
│   │       ├── ElementDescriber.cs                                ← Task 10
│   │       ├── RootEnumerator.cs                                  ← Task 11
│   │       ├── PopupOwnerResolver.cs                              ← Task 11
│   │       ├── ChildrenEnumerator.cs                              ← Task 12
│   │       ├── ParentNavigator.cs                                 ← Task 13
│   │       ├── ElementFinder.cs                                   ← Task 14
│   │       ├── HitTester.cs                                       ← Task 15
│   │       ├── PathResolver.cs                                    ← Task 16
│   │       ├── DataContextInspector.cs                            ← Tasks 17, 18
│   │       ├── DependencyPropertyInspector.cs                     ← Tasks 19, 20
│   │       ├── StyleResolver.cs                                   ← Task 21
│   │       ├── TemplateResolver.cs                                ← Task 22
│   │       ├── BindingInspector.cs                                ← Tasks 23, 24
│   │       └── XamlExporter.cs                                    ← Task 25
│   │
│   ├── SnoopMCP.Host\                                             ← created in Task 26
│   │   ├── SnoopMCP.Host.csproj
│   │   ├── Program.cs                                             ← Task 26
│   │   ├── PipeClient.cs                                          ← Task 27
│   │   ├── SessionManager.cs                                      ← Task 28
│   │   ├── IInjectorService.cs                                    ← Task 29
│   │   ├── NullInjectorService.cs                                 ← Task 29
│   │   ├── Tools\
│   │   │   └── McpTools.cs                                        ← Task 29 (single file holds the [McpServerTool] facade)
│   │   └── Injection\
│   │       ├── InjectorService.cs                                 ← Task 31
│   │       └── ProcessProbe.cs                                    ← Task 31
│   │
│   └── SnoopMCP.Injection\                                        ← created in Task 30 (submodule wrapper)
│       ├── SnoopMCP.Injection.csproj                               ← ProjectReferences into ../../external/snoopwpf/
│       └── THIRD_PARTY_NOTICES.md
│
├── external\                                                       ← created in Task 30
│   └── snoopwpf\                                                   ← git submodule
│       └── ... (snoopwpf upstream tree, pinned commit)
│
├── tests\
│   ├── SnoopMCP.Protocol.Tests\                                   ← Task 5
│   │   ├── SnoopMCP.Protocol.Tests.csproj
│   │   └── WireSerializerTests.cs
│   ├── SnoopMCP.Payload.Tests\                                    ← Tasks 7+, per-tool tests
│   │   ├── SnoopMCP.Payload.Tests.csproj
│   │   ├── DispatcherMarshalTests.cs
│   │   ├── ElementRegistryTests.cs
│   │   ├── PathStringEmitterTests.cs
│   │   ├── PathStringParserTests.cs
│   │   ├── ElementDescriberTests.cs
│   │   ├── ChildrenEnumeratorTests.cs
│   │   ├── ParentNavigatorTests.cs
│   │   ├── ElementFinderTests.cs
│   │   ├── HitTesterTests.cs
│   │   ├── PathResolverTests.cs
│   │   ├── DataContextInspectorTests.cs
│   │   ├── DependencyPropertyInspectorTests.cs
│   │   ├── StyleResolverTests.cs
│   │   ├── TemplateResolverTests.cs
│   │   ├── BindingInspectorTests.cs
│   │   ├── ListBindingsTests.cs                                   ← Task 24
│   │   └── XamlExporterTests.cs                                   ← Task 25
│   ├── SnoopMCP.Host.Tests\                                       ← Tasks 27, 28
│   │   ├── SnoopMCP.Host.Tests.csproj
│   │   ├── PipeClientTests.cs                                     ← Task 27
│   │   └── SessionManagerTests.cs                                 ← Task 28
│   └── SnoopMCP.IntegrationTests\                                 ← Task 33
│       ├── SnoopMCP.IntegrationTests.csproj
│       └── EndToEndTests.cs
│
├── samples\
│   └── SampleWpfApp\                                              ← created in Task 2
│       ├── SampleWpfApp.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── MainWindow.xaml                                        ← intentional styling bugs
│       ├── MainWindow.xaml.cs
│       ├── ViewModels\
│       │   ├── MainViewModel.cs
│       │   └── Customer.cs
│       └── Resources\
│           └── Themes.xaml
│
└── docs\
    ├── superpowers\
    │   ├── specs\2026-05-27-snoopmcp-investigation-design.md      ← exists
    │   └── plans\2026-05-27-snoopmcp-v1-readonly.md               ← this file
    └── README.md                                                  ← Task 32
```

**Why this shape:**
- `SnoopMCP.Protocol` is referenced by both Host and Payload. Wire DTOs and error codes live in one place.
- `SnoopMCP.Payload` cannot reference `SnoopMCP.Host` (it loads into other people's processes; the host isn't there).
- `SnoopMCP.Injection` is its own project so the third-party license boundary is unambiguous and we can pin via submodule with attribution cleanly.
- Each Inspection class corresponds 1:1 with a tool (or pair, for register-style symmetry).
- Tests mirror the source tree; one test file per source class.

---

## Task 0: Repository configuration (governance up front)

**Goal:** Stand up the GitHub repo for SnoopMCP with the same governance posture as SaddleRAG **before any code lands**. PRs required on `master`, no force-push, build-status check required, conversation resolution required, admins enforced. From Task 1 onward every commit hits a protected branch and the workflow is enforced from day one.

Default branch: **`master`** (consistent with every Jackalope repo). Snoop's upstream uses `main` for their fork — that's their convention and we don't touch it. Our fork (Task 30) keeps `main` synced with upstream and uses a `snoopmcp` branch for our work; SnoopMCP itself is `master`-default.

Protection mirrors `JackalopeTechnologies/SaddleRAG` exactly — captured by `gh api repos/JackalopeTechnologies/SaddleRAG/branches/master/protection` at plan time.

**Files:**
- Create: `.github\branch-protection.json` (committed for reproducibility)

- [ ] **Step 1: Push the local repo to GitHub (if not already)**

The repo path `E:/GitHub/SnoopMCP` exists locally with `master` set up. Push to GitHub:

```text
gh repo create JackalopeTechnologies/SnoopMCP --private --source E:/GitHub/SnoopMCP --remote origin --push
```

If the repo already exists on GitHub, skip and verify the remote:

```text
git -C E:/GitHub/SnoopMCP remote -v
```

Expected: `origin  https://github.com/JackalopeTechnologies/SnoopMCP.git (fetch/push)`.

- [ ] **Step 2: Verify the default branch is `master`**

```text
gh repo view JackalopeTechnologies/SnoopMCP --json defaultBranchRef
```

Expected: `{"defaultBranchRef":{"name":"master"}}`. If GitHub picked `main` (a default for new repos), correct it:

```text
gh api -X PATCH repos/JackalopeTechnologies/SnoopMCP --field default_branch=master
```

- [ ] **Step 3: Write the protection JSON** — `E:\GitHub\SnoopMCP\.github\branch-protection.json`

```json
{
    "required_status_checks": {
        "strict": true,
        "contexts": ["build"]
    },
    "enforce_admins": true,
    "required_pull_request_reviews": {
        "dismiss_stale_reviews": true,
        "require_code_owner_reviews": false,
        "require_last_push_approval": false,
        "required_approving_review_count": 0
    },
    "restrictions": null,
    "required_linear_history": false,
    "allow_force_pushes": false,
    "allow_deletions": false,
    "required_conversation_resolution": true
}
```

Settings rationale (each matches SaddleRAG):
- `required_approving_review_count: 0` — solo workflow, PRs required for the discipline (diff review, CI gate) but no second-reviewer block.
- `required_status_checks.contexts: ["build"]` + `strict: true` — a check named `build` must exist, pass, and be up to date with `master`.
- `enforce_admins: true` — the rules apply to you too. No "I'll just push this real quick."
- `required_conversation_resolution: true` — review comments must be resolved before merge.
- `dismiss_stale_reviews: true` — new commits invalidate approvals.
- `allow_force_pushes: false` + `allow_deletions: false` — `master` history is append-only.
- `required_linear_history: false` — match SaddleRAG; merge commits are allowed (the merge UI defaults to squash so this rarely shows up).

- [ ] **Step 4: Apply branch protection**

```text
gh api -X PUT repos/JackalopeTechnologies/SnoopMCP/branches/master/protection --input E:/GitHub/SnoopMCP/.github/branch-protection.json
```

- [ ] **Step 5: Verify the protection landed**

```text
gh api repos/JackalopeTechnologies/SnoopMCP/branches/master/protection --jq "{prs: .required_pull_request_reviews, checks: .required_status_checks.contexts, force: .allow_force_pushes.enabled, conv: .required_conversation_resolution.enabled, admins: .enforce_admins.enabled}"
```

Expected (whitespace ignored):

```text
{
  "prs": { "dismiss_stale_reviews": true, "require_code_owner_reviews": false, "require_last_push_approval": false, "required_approving_review_count": 0, ... },
  "checks": ["build"],
  "force": false,
  "conv": true,
  "admins": true
}
```

- [ ] **Step 6: Repo-level settings (issues, wiki, merge methods)**

Match SaddleRAG: issues on, wiki on, all three merge methods allowed, branches are not auto-deleted on merge:

```text
gh repo edit JackalopeTechnologies/SnoopMCP --enable-issues --enable-wiki --enable-squash-merge --enable-merge-commit --enable-rebase-merge
```

- [ ] **Step 7: Note about the `build` status check**

The protection requires a status check named `build`. We don't have CI yet — that lands as part of the team's broader workflow, not within these 31 tasks. Until a `build` check has reported success on a PR, merging requires the admin to acknowledge "Merging without required status checks" in the GitHub UI. This is intentional friction — it should drive us to wire up CI early.

- [ ] **Step 8: Commit the protection JSON**

```text
git -C E:/GitHub/SnoopMCP add .github/branch-protection.json
git -C E:/GitHub/SnoopMCP commit -F E:/tmp/msg-task-0.txt
```

`E:/tmp/msg-task-0.txt`:

```text
Task 0: repo governance — branch protection on master mirrors SaddleRAG

Captures the gh api PUT payload as .github/branch-protection.json so the
protection set is reproducible if it ever drifts. PRs required, no force-push,
build status check required (CI lands separately), conversation resolution
required, admins enforced.
```

Note: this is the only commit that will land directly on `master`. From Task 1 onward, everything goes through a PR.

---

## Task 1: Solution scaffold

**Files:**
- Create: `E:\GitHub\SnoopMCP\global.json`
- Create: `E:\GitHub\SnoopMCP\Directory.Build.props`
- Create: `E:\GitHub\SnoopMCP\.editorconfig`
- Create: `E:\GitHub\SnoopMCP\SnoopMCP.sln`

- [ ] **Step 1: Create `global.json` pinning the .NET 10 SDK**

```json
{
    "sdk": {
        "version": "10.0.0",
        "rollForward": "latestFeature",
        "allowPrerelease": false
    }
}
```

- [ ] **Step 2: Create `Directory.Build.props`** — shared csproj settings inherited by every project

```xml
<Project>
    <PropertyGroup>
        <LangVersion>latest</LangVersion>
        <Nullable>enable</Nullable>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
        <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
        <AnalysisLevel>latest-recommended</AnalysisLevel>
        <ImplicitUsings>enable</ImplicitUsings>
        <NeutralLanguage>en-US</NeutralLanguage>
        <Deterministic>true</Deterministic>
        <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    </PropertyGroup>

    <PropertyGroup>
        <Authors>Jackalope Technologies</Authors>
        <Product>SnoopMCP</Product>
        <Copyright>Copyright (c) 2026 Jackalope Technologies</Copyright>
        <RepositoryUrl>https://github.com/JackalopeTechnologies/SnoopMCP</RepositoryUrl>
    </PropertyGroup>

    <ItemGroup>
        <Using Include="System" />
        <Using Include="System.Collections.Generic" />
        <Using Include="System.Threading" />
        <Using Include="System.Threading.Tasks" />
    </ItemGroup>

    <!--
        Jackalope code-standards analyzer (same package SaddleRAG uses).
        Applied to every project automatically via Directory.Build.props.
        Ships its own .editorconfig that we use as a baseline; our repo-root
        .editorconfig (Step 3 below) layers the field-prefix rules on top.
    -->
    <ItemGroup>
        <PackageReference Include="CodeStructure.Analyzers" Version="1.0.7" PrivateAssets="all" />
    </ItemGroup>
</Project>
```

- [ ] **Step 3: Create `.editorconfig`** — enforces formatting + the project naming/style rules

```ini
root = true

[*]
indent_style = space
indent_size = 4
charset = utf-8
end_of_line = crlf
insert_final_newline = true
trim_trailing_whitespace = true
max_line_length = 120

[*.{cs,vb}]
# Allman braces
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true
csharp_new_line_within_query_expression_clauses = true

# var policy: when apparent
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = false:suggestion

# Modern patterns
csharp_style_expression_bodied_methods = false:suggestion
csharp_style_expression_bodied_properties = true:suggestion
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion
csharp_prefer_switch_expression = true:warning

# Field naming (m, ps, sm, pm prefixes per CLAUDE.md)
dotnet_naming_rule.private_instance_fields.severity = warning
dotnet_naming_rule.private_instance_fields.symbols = private_instance_field
dotnet_naming_rule.private_instance_fields.style = m_prefix_camel_case

dotnet_naming_symbols.private_instance_field.applicable_kinds = field
dotnet_naming_symbols.private_instance_field.applicable_accessibilities = private
dotnet_naming_symbols.private_instance_field.required_modifiers =

dotnet_naming_style.m_prefix_camel_case.required_prefix = m
dotnet_naming_style.m_prefix_camel_case.capitalization = camel_case

dotnet_naming_rule.private_static_readonly_fields.severity = warning
dotnet_naming_rule.private_static_readonly_fields.symbols = private_static_readonly_field
dotnet_naming_rule.private_static_readonly_fields.style = sm_prefix_camel_case

dotnet_naming_symbols.private_static_readonly_field.applicable_kinds = field
dotnet_naming_symbols.private_static_readonly_field.applicable_accessibilities = private
dotnet_naming_symbols.private_static_readonly_field.required_modifiers = static,readonly

dotnet_naming_style.sm_prefix_camel_case.required_prefix = sm
dotnet_naming_style.sm_prefix_camel_case.capitalization = camel_case

dotnet_naming_rule.private_static_fields.severity = warning
dotnet_naming_rule.private_static_fields.symbols = private_static_field
dotnet_naming_rule.private_static_fields.style = ps_prefix_camel_case

dotnet_naming_symbols.private_static_field.applicable_kinds = field
dotnet_naming_symbols.private_static_field.applicable_accessibilities = private
dotnet_naming_symbols.private_static_field.required_modifiers = static

dotnet_naming_style.ps_prefix_camel_case.required_prefix = ps
dotnet_naming_style.ps_prefix_camel_case.capitalization = camel_case

[*.xaml]
indent_size = 4

[*.{xml,csproj,props,targets,json,yml,yaml}]
indent_size = 2
```

- [ ] **Step 4: Create the empty solution**

Run from `E:\GitHub\SnoopMCP`:

```text
dotnet new sln --name SnoopMCP
```

Expected: `SnoopMCP.sln` created at repo root.

- [ ] **Step 5: Verify dotnet sees the SDK**

Run:

```text
dotnet --info
```

Expected: `.NET SDK Version: 10.0.x` reported. Workload list non-fatal.

- [ ] **Step 6: Verify a clean restore succeeds**

Run from `E:\GitHub\SnoopMCP`:

```text
dotnet restore SnoopMCP.sln
```

Expected: `Restore complete` with no errors. The solution is empty so this is fast; we're verifying the SDK pin works.

- [ ] **Step 7: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 1: solution scaffold, SDK pin, code-style baseline"
```

---

## Task 2: Sample WPF target app

The Sample app is the integration test fixture. It must exercise every diagnostic scenario the v1 tools claim to handle: visual/logical divergence, popups, virtualization, custom template, BasedOn style chain, deliberately-broken binding, trigger.

**Files:**
- Create: `samples\SampleWpfApp\SampleWpfApp.csproj`
- Create: `samples\SampleWpfApp\App.xaml`
- Create: `samples\SampleWpfApp\App.xaml.cs`
- Create: `samples\SampleWpfApp\MainWindow.xaml`
- Create: `samples\SampleWpfApp\MainWindow.xaml.cs`
- Create: `samples\SampleWpfApp\ViewModels\MainViewModel.cs`
- Create: `samples\SampleWpfApp\ViewModels\Customer.cs`
- Create: `samples\SampleWpfApp\Resources\Themes.xaml`

- [ ] **Step 1: Create `samples\SampleWpfApp\SampleWpfApp.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>WinExe</OutputType>
        <TargetFramework>net10.0-windows</TargetFramework>
        <UseWPF>true</UseWPF>
        <RootNamespace>SampleWpfApp</RootNamespace>
        <AssemblyName>SampleWpfApp</AssemblyName>
        <ApplicationManifest>app.manifest</ApplicationManifest>
        <PlatformTarget>x64</PlatformTarget>
    </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create `samples\SampleWpfApp\App.xaml`**

```xml
<Application x:Class="SampleWpfApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Resources/Themes.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 3: Create `samples\SampleWpfApp\App.xaml.cs`**

```csharp
namespace SampleWpfApp;

using System.Windows;

public partial class App : Application
{
}
```

- [ ] **Step 4: Create `samples\SampleWpfApp\Resources\Themes.xaml`** — includes the BasedOn chain we want to trace

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Style x:Key="BaseButtonStyle" TargetType="Button">
        <Setter Property="FontFamily" Value="Segoe UI" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="Padding" Value="12,6" />
        <Setter Property="Background" Value="LightGray" />
    </Style>

    <Style x:Key="PrimaryButtonStyle" TargetType="Button" BasedOn="{StaticResource BaseButtonStyle}">
        <Setter Property="Background" Value="DodgerBlue" />
        <Setter Property="Foreground" Value="White" />
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="RoyalBlue" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
                <Setter Property="Background" Value="Gray" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style x:Key="DangerButtonStyle" TargetType="Button" BasedOn="{StaticResource PrimaryButtonStyle}">
        <Setter Property="Background" Value="Crimson" />
    </Style>

</ResourceDictionary>
```

- [ ] **Step 5: Create `samples\SampleWpfApp\ViewModels\Customer.cs`**

```csharp
namespace SampleWpfApp.ViewModels;

using System.ComponentModel;
using System.Runtime.CompilerServices;

public sealed class Customer : INotifyPropertyChanged
{
    private string mName = string.Empty;
    private string mEmail = string.Empty;
    private Address? mAddress;

    public string Name
    {
        get => mName;
        set => SetField(ref mName, value);
    }

    public string Email
    {
        get => mEmail;
        set => SetField(ref mEmail, value);
    }

    public Address? Address
    {
        get => mAddress;
        set => SetField(ref mAddress, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        bool changed = !EqualityComparer<T>.Default.Equals(field, value);
        if (changed)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

public sealed class Address
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
}
```

- [ ] **Step 6: Create `samples\SampleWpfApp\ViewModels\MainViewModel.cs`**

```csharp
namespace SampleWpfApp.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private Customer? mSelectedCustomer;
    private string mSearchText = string.Empty;

    public MainViewModel()
    {
        Customers = new ObservableCollection<Customer>();
        SeedCustomers();
        SelectedCustomer = Customers[0];
    }

    public ObservableCollection<Customer> Customers { get; }

    public Customer? SelectedCustomer
    {
        get => mSelectedCustomer;
        set => SetField(ref mSelectedCustomer, value);
    }

    public string SearchText
    {
        get => mSearchText;
        set => SetField(ref mSearchText, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SeedCustomers()
    {
        const int seedCount = 1000;
        for (int i = 0; i < seedCount; i++)
        {
            Customers.Add(new Customer
            {
                Name = $"Customer {i:D4}",
                Email = $"customer{i:D4}@example.com",
                Address = new Address
                {
                    Street = $"{i + 1} Main St",
                    City = "Springfield",
                    PostalCode = $"{10000 + i}"
                }
            });
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        bool changed = !EqualityComparer<T>.Default.Equals(field, value);
        if (changed)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
```

- [ ] **Step 7: Create `samples\SampleWpfApp\MainWindow.xaml`** — intentionally exercises:
  - Virtualized `ListBox` with 1000 items
  - `ComboBox` (popup)
  - Custom `ControlTemplate` on a `Button` with a named part
  - `BasedOn` chain (DangerButton → PrimaryButton → BaseButton)
  - A binding with a wrong path so `hasBindingErrors` flips true
  - A `Trigger` (IsMouseOver) on the primary button
  - Logical-vs-visual divergence (a `ContentControl` with a `DataTemplate`)

```xml
<Window x:Class="SampleWpfApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:SampleWpfApp.ViewModels"
        Title="SnoopMCP Sample App"
        Width="900" Height="600">

    <Window.DataContext>
        <vm:MainViewModel />
    </Window.DataContext>

    <Window.Resources>
        <ControlTemplate x:Key="CustomButtonTemplate" TargetType="Button">
            <Border x:Name="PART_Border"
                    Background="{TemplateBinding Background}"
                    CornerRadius="4"
                    Padding="{TemplateBinding Padding}">
                <ContentPresenter x:Name="PART_Content"
                                  HorizontalAlignment="Center"
                                  VerticalAlignment="Center" />
            </Border>
            <ControlTemplate.Triggers>
                <Trigger Property="IsPressed" Value="True">
                    <Setter TargetName="PART_Border" Property="Opacity" Value="0.8" />
                </Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>
    </Window.Resources>

    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,12">
            <TextBlock Text="Search:" VerticalAlignment="Center" Margin="0,0,8,0" />
            <TextBox x:Name="SearchBox"
                     Width="240"
                     Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" />
            <ComboBox x:Name="ThemeCombo"
                      Width="160"
                      Margin="16,0,0,0"
                      AutomationProperties.AutomationId="ThemePicker">
                <ComboBoxItem Content="Light" IsSelected="True" />
                <ComboBoxItem Content="Dark" />
                <ComboBoxItem Content="High Contrast" />
            </ComboBox>
        </StackPanel>

        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="280" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>

            <ListBox x:Name="CustomerList"
                     Grid.Column="0"
                     ItemsSource="{Binding Customers}"
                     SelectedItem="{Binding SelectedCustomer}"
                     VirtualizingPanel.IsVirtualizing="True"
                     VirtualizingPanel.VirtualizationMode="Recycling">
                <ListBox.ItemTemplate>
                    <DataTemplate DataType="{x:Type vm:Customer}">
                        <StackPanel>
                            <TextBlock Text="{Binding Name}" FontWeight="Bold" />
                            <TextBlock Text="{Binding Email}" Opacity="0.7" />
                        </StackPanel>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>

            <ContentControl x:Name="DetailPane"
                            Grid.Column="1"
                            Margin="12,0,0,0"
                            Content="{Binding SelectedCustomer}">
                <ContentControl.ContentTemplate>
                    <DataTemplate DataType="{x:Type vm:Customer}">
                        <StackPanel>
                            <TextBlock Text="{Binding Name}" FontSize="22" FontWeight="SemiBold" />
                            <TextBlock Text="{Binding Email}" Opacity="0.7" Margin="0,0,0,12" />
                            <TextBlock Text="Address" FontWeight="Bold" Margin="0,12,0,4" />
                            <TextBlock Text="{Binding Address.Street}" />
                            <TextBlock Text="{Binding Address.City}" />
                            <TextBlock Text="{Binding Address.PostalCode}" />
                            <!-- Intentional binding error: path does not exist -->
                            <TextBlock x:Name="BrokenBinding"
                                       Text="{Binding Address.Country.Name}"
                                       Foreground="Red"
                                       Margin="0,12,0,0" />
                        </StackPanel>
                    </DataTemplate>
                </ContentControl.ContentTemplate>
            </ContentControl>
        </Grid>

        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,12,0,0">
            <Button x:Name="CancelButton"
                    Content="Cancel"
                    Style="{StaticResource BaseButtonStyle}"
                    Margin="0,0,8,0" />
            <Button x:Name="SaveButton"
                    Content="Save"
                    Style="{StaticResource PrimaryButtonStyle}"
                    Template="{StaticResource CustomButtonTemplate}"
                    Margin="0,0,8,0" />
            <Button x:Name="DeleteButton"
                    Content="Delete"
                    Style="{StaticResource DangerButtonStyle}" />
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 8: Create `samples\SampleWpfApp\MainWindow.xaml.cs`**

```csharp
namespace SampleWpfApp;

using System.Windows;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 9: Add the sample project to the solution**

Run from `E:\GitHub\SnoopMCP`:

```text
dotnet sln SnoopMCP.sln add samples/SampleWpfApp/SampleWpfApp.csproj
```

Expected: `Project ... added to the solution.`

- [ ] **Step 10: Build the sample**

Run:

```text
dotnet build samples/SampleWpfApp/SampleWpfApp.csproj -c Debug
```

Expected: Build succeeds. No warnings (we have `TreatWarningsAsErrors`). One binding-trace warning at runtime is expected — that's the deliberate broken binding.

- [ ] **Step 11: Smoke-launch the sample**

Run:

```text
samples/SampleWpfApp/bin/Debug/net10.0-windows/SampleWpfApp.exe
```

Expected: A window opens titled "SnoopMCP Sample App" with a 1000-item list, a detail pane on the right, three buttons at the bottom (Cancel gray, Save blue, Delete crimson), and a ComboBox dropdown that opens. Close it.

- [ ] **Step 12: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 2: SampleWpfApp with diagnostic fixtures (popup, virtualization, BasedOn chain, broken binding, custom template)"
```

---

## Task 3: Snoop precondition smoke test (GO/NO-GO gate)

**This task is a manual validation gate.** If Snoop's current main cannot attach to our `net10.0-windows` SampleWpfApp, then the architecture's premise (use Snoop's injector via submodule) is invalid and we revisit before doing any more work. No code is produced; the deliverable is a written result.

**Files:**
- Create: `docs\snoop-smoke-test-result.md`

- [ ] **Step 1: Clone Snoop in a temp directory**

Run:

```text
git clone https://github.com/snoopwpf/snoopwpf.git E:/tmp/snoopwpf
```

Expected: Clone succeeds.

- [ ] **Step 2: Build Snoop**

Run from `E:/tmp/snoopwpf`:

```text
dotnet build Snoop.sln -c Release
```

Expected: Snoop builds. If the build fails, capture the exact error and stop — investigate before continuing. Snoop builds being broken is itself a signal we may be on a stale assumption.

- [ ] **Step 3: Launch the sample app**

Run:

```text
samples/SampleWpfApp/bin/Debug/net10.0-windows/SampleWpfApp.exe
```

Let it sit open. Note its PID via `Get-Process SampleWpfApp` (PowerShell) or Task Manager.

- [ ] **Step 4: Launch Snoop and attach to the sample**

Run the Snoop binary that the Release build produced (path will be something like `E:/tmp/snoopwpf/Snoop/bin/Release/net10.0-windows/Snoop.exe`). In Snoop's UI, find the SampleWpfApp process and attach.

Expected: Snoop's main inspection window opens, showing the visual tree of the SampleWpfApp window. You can click on elements and see their properties.

- [ ] **Step 5: Verify the diagnostic features Snoop is supposed to expose**

In Snoop, manually verify:

1. The visual tree shows the `Window` → `Grid` → ... hierarchy
2. Selecting the `SaveButton` shows its `Style` setters and the resolved `Background = DodgerBlue`
3. Switching to the "Properties" tab on `SaveButton` shows DPs with their value sources
4. The broken-binding `TextBlock` (BrokenBinding) shows a binding error indicator
5. Opening the ComboBox in the sample app then refreshing Snoop's tree shows the popup as a separate visual root

If any of those fail, **stop and report** — these are the things our submoduled injector + payload must also support. If Snoop can't see them, our derivative work cannot.

- [ ] **Step 6: Document the result**

Create `docs\snoop-smoke-test-result.md`:

```markdown
# Snoop Smoke Test — 2026-05-27

**Snoop version (commit):** <paste output of `git -C E:/tmp/snoopwpf rev-parse HEAD`>
**Sample target:** SampleWpfApp built against net10.0-windows
**Result:** PASS | FAIL

## Observations

- Visual tree visible: yes / no
- Style precedence visible on SaveButton: yes / no
- DP value sources visible: yes / no
- Broken binding flagged: yes / no
- ComboBox popup appeared as separate visual root: yes / no

## Notes

<free text — any quirks, errors, surprises>
```

Fill in honestly.

- [ ] **Step 7: Decision**

- If **PASS** on all five observations → proceed to Task 4.
- If **FAIL** on any → stop, escalate, do not write more code. The recommendation needs revisiting.

- [ ] **Step 8: Commit the result document**

```text
git -C E:/GitHub/SnoopMCP add docs/snoop-smoke-test-result.md
git -C E:/GitHub/SnoopMCP commit -m "Task 3: Snoop precondition smoke test result"
```

---

## Task 4: Protocol library — wire envelope and error types

**Goal:** Define the framing and DTO base for the host↔payload pipe. JSON payload, length-prefixed framing, structured error responses.

**Files:**
- Create: `src\SnoopMCP.Protocol\SnoopMCP.Protocol.csproj`
- Create: `src\SnoopMCP.Protocol\Wire\RpcRequest.cs`
- Create: `src\SnoopMCP.Protocol\Wire\RpcResponse.cs`
- Create: `src\SnoopMCP.Protocol\Wire\RpcError.cs`
- Create: `src\SnoopMCP.Protocol\Wire\WireSerializer.cs`
- Create: `src\SnoopMCP.Protocol\Errors\ErrorCode.cs`
- Create: `src\SnoopMCP.Protocol\Errors\SnoopMcpException.cs`

- [ ] **Step 1: Create `src\SnoopMCP.Protocol\SnoopMCP.Protocol.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <RootNamespace>SnoopMCP.Protocol</RootNamespace>
        <AssemblyName>SnoopMCP.Protocol</AssemblyName>
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
    </PropertyGroup>
</Project>
```

- [ ] **Step 2: Add the project to the solution**

```text
dotnet sln SnoopMCP.sln add src/SnoopMCP.Protocol/SnoopMCP.Protocol.csproj
```

- [ ] **Step 3: Create `src\SnoopMCP.Protocol\Errors\ErrorCode.cs`**

```csharp
namespace SnoopMCP.Protocol.Errors;

public enum ErrorCode
{
    Unknown = 0,
    AttachFailed = 1,
    PayloadLoadFailed = 2,
    DispatcherTimeout = 3,
    SessionLost = 4,
    AccessDenied = 5,
    ElementExpired = 6,
    InvalidArgument = 7,
    ToolNotFound = 8,
    ProtocolError = 9,
    BindingPathError = 10,
    PathParseError = 11
}
```

- [ ] **Step 4: Create `src\SnoopMCP.Protocol\Errors\SnoopMcpException.cs`**

```csharp
namespace SnoopMCP.Protocol.Errors;

public class SnoopMcpException : Exception
{
    public SnoopMcpException(ErrorCode code, string message) : base(message)
    {
        Code = code;
    }

    public SnoopMcpException(ErrorCode code, string message, Exception inner) : base(message, inner)
    {
        Code = code;
    }

    public ErrorCode Code { get; }
}
```

- [ ] **Step 5: Create `src\SnoopMCP.Protocol\Wire\RpcError.cs`**

```csharp
namespace SnoopMCP.Protocol.Wire;

using SnoopMCP.Protocol.Errors;

public sealed class RpcError
{
    public ErrorCode Code { get; init; } = ErrorCode.Unknown;
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
}
```

- [ ] **Step 6: Create `src\SnoopMCP.Protocol\Wire\RpcRequest.cs`**

```csharp
namespace SnoopMCP.Protocol.Wire;

using System.Text.Json;

public sealed class RpcRequest
{
    public long Id { get; init; }
    public string Tool { get; init; } = string.Empty;
    public JsonElement Arguments { get; init; }
}
```

- [ ] **Step 7: Create `src\SnoopMCP.Protocol\Wire\RpcResponse.cs`**

```csharp
namespace SnoopMCP.Protocol.Wire;

using System.Text.Json;

public sealed class RpcResponse
{
    public long Id { get; init; }
    public JsonElement? Result { get; init; }
    public RpcError? Error { get; init; }

    public bool IsSuccess => Error is null;
}
```

- [ ] **Step 8: Create `src\SnoopMCP.Protocol\Wire\WireSerializer.cs`**

The wire format is length-prefixed JSON: 4-byte little-endian uint32 payload length, then UTF-8 JSON. Per the CLAUDE.md no-magic-numbers rule, frame-length width is a named constant.

```csharp
namespace SnoopMCP.Protocol.Wire;

using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;

public static class WireSerializer
{
    private const int FrameLengthBytes = 4;
    private const int MaxFrameSizeBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions smJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static JsonSerializerOptions JsonOptions => smJsonOptions;

    public static async Task WriteFrameAsync<T>(Stream destination, T payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(payload);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload, smJsonOptions);
        if (body.Length > MaxFrameSizeBytes)
        {
            throw new InvalidOperationException($"Frame exceeds {MaxFrameSizeBytes} bytes ({body.Length}).");
        }

        byte[] header = new byte[FrameLengthBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint) body.Length);

        await destination.WriteAsync(header.AsMemory(), cancellationToken).ConfigureAwait(false);
        await destination.WriteAsync(body.AsMemory(), cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<T?> ReadFrameAsync<T>(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        byte[] header = new byte[FrameLengthBytes];
        int headerRead = await ReadExactAsync(source, header, cancellationToken).ConfigureAwait(false);
        T? result = default;

        bool gotFullHeader = headerRead == FrameLengthBytes;
        if (gotFullHeader)
        {
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(header);
            if (length > MaxFrameSizeBytes)
            {
                throw new InvalidDataException($"Incoming frame {length} bytes exceeds {MaxFrameSizeBytes}.");
            }

            byte[] body = new byte[length];
            int bodyRead = await ReadExactAsync(source, body, cancellationToken).ConfigureAwait(false);
            bool gotFullBody = bodyRead == (int) length;
            if (gotFullBody)
            {
                result = JsonSerializer.Deserialize<T>(body, smJsonOptions);
            }
        }

        return result;
    }

    private static async Task<int> ReadExactAsync(Stream source, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int total = 0;
        bool keepReading = buffer.Length > 0;
        while (keepReading)
        {
            int chunk = await source.ReadAsync(buffer.Slice(total), cancellationToken).ConfigureAwait(false);
            bool eof = chunk == 0;
            if (eof)
            {
                keepReading = false;
            }
            else
            {
                total += chunk;
                keepReading = total < buffer.Length;
            }
        }
        return total;
    }
}
```

- [ ] **Step 9: Build the protocol project**

```text
dotnet build src/SnoopMCP.Protocol/SnoopMCP.Protocol.csproj -c Debug
```

Expected: Build succeeds with no warnings.

- [ ] **Step 10: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 4: SnoopMCP.Protocol — wire envelope, length-prefixed framing, error types"
```

---

## Task 5: Wire serializer round-trip tests

**Goal:** Pin the wire format with executable tests before either Host or Payload depends on it. If framing changes, these tests break and we notice.

**Files:**
- Create: `tests\SnoopMCP.Protocol.Tests\SnoopMCP.Protocol.Tests.csproj`
- Create: `tests\SnoopMCP.Protocol.Tests\WireSerializerTests.cs`

- [ ] **Step 1: Create the test project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <RootNamespace>SnoopMCP.Protocol.Tests</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="xunit.v3" Version="1.0.0" />
        <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\src\SnoopMCP.Protocol\SnoopMCP.Protocol.csproj" />
    </ItemGroup>
</Project>
```

> **Note on xunit.v3:** if the xunit.v3 package version above does not resolve at the time of execution, query saddlerag for the current version: `mcp__saddlerag__search_docs query:"xunit v3 NuGet package version" library:"xunit-v3"`. Update the version and proceed.

- [ ] **Step 2: Add the test project to the solution**

```text
dotnet sln SnoopMCP.sln add tests/SnoopMCP.Protocol.Tests/SnoopMCP.Protocol.Tests.csproj
```

- [ ] **Step 3: Write the failing test file** — round-trip request, round-trip response, malformed frame, oversized frame

```csharp
namespace SnoopMCP.Protocol.Tests;

using System.IO;
using System.Text.Json;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Wire;
using Xunit;

public sealed class WireSerializerTests
{
    [Fact]
    public async Task RoundTrip_Request_PreservesIdToolAndArguments()
    {
        var args = JsonDocument.Parse("{\"pid\":1234}").RootElement;
        var original = new RpcRequest
        {
            Id = 42,
            Tool = "attach",
            Arguments = args
        };

        using var stream = new MemoryStream();
        await WireSerializer.WriteFrameAsync(stream, original, CancellationToken.None);
        stream.Position = 0;

        var roundTripped = await WireSerializer.ReadFrameAsync<RpcRequest>(stream, CancellationToken.None);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.Id, roundTripped!.Id);
        Assert.Equal(original.Tool, roundTripped.Tool);
        Assert.Equal(1234, roundTripped.Arguments.GetProperty("pid").GetInt32());
    }

    [Fact]
    public async Task RoundTrip_Response_WithResult_PreservesPayload()
    {
        var resultPayload = JsonDocument.Parse("{\"ok\":true,\"count\":3}").RootElement;
        var original = new RpcResponse
        {
            Id = 7,
            Result = resultPayload
        };

        using var stream = new MemoryStream();
        await WireSerializer.WriteFrameAsync(stream, original, CancellationToken.None);
        stream.Position = 0;

        var roundTripped = await WireSerializer.ReadFrameAsync<RpcResponse>(stream, CancellationToken.None);

        Assert.NotNull(roundTripped);
        Assert.Equal(7, roundTripped!.Id);
        Assert.True(roundTripped.IsSuccess);
        Assert.NotNull(roundTripped.Result);
        Assert.Equal(3, roundTripped.Result!.Value.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task RoundTrip_Response_WithError_PreservesErrorFields()
    {
        var original = new RpcResponse
        {
            Id = 9,
            Error = new RpcError
            {
                Code = ErrorCode.ElementExpired,
                Message = "Element id 42 has been garbage collected.",
                Details = "live=false"
            }
        };

        using var stream = new MemoryStream();
        await WireSerializer.WriteFrameAsync(stream, original, CancellationToken.None);
        stream.Position = 0;

        var roundTripped = await WireSerializer.ReadFrameAsync<RpcResponse>(stream, CancellationToken.None);

        Assert.NotNull(roundTripped);
        Assert.False(roundTripped!.IsSuccess);
        Assert.NotNull(roundTripped.Error);
        Assert.Equal(ErrorCode.ElementExpired, roundTripped.Error!.Code);
        Assert.Equal("Element id 42 has been garbage collected.", roundTripped.Error.Message);
        Assert.Equal("live=false", roundTripped.Error.Details);
    }

    [Fact]
    public async Task ReadFrame_OnEofBeforeHeader_ReturnsDefault()
    {
        using var stream = new MemoryStream();
        var result = await WireSerializer.ReadFrameAsync<RpcRequest>(stream, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task ReadFrame_OnTruncatedBody_ReturnsDefault()
    {
        using var stream = new MemoryStream();
        byte[] header = { 0xFF, 0x00, 0x00, 0x00 };
        await stream.WriteAsync(header);
        stream.Position = 0;

        var result = await WireSerializer.ReadFrameAsync<RpcRequest>(stream, CancellationToken.None);
        Assert.Null(result);
    }
}
```

- [ ] **Step 4: Run the tests — they must fail because the assembly does not yet build cleanly**

Run:

```text
dotnet test tests/SnoopMCP.Protocol.Tests/SnoopMCP.Protocol.Tests.csproj
```

Expected: tests run. If `WireSerializer` was correctly written in Task 4, all five tests pass. If they fail, fix `WireSerializer` until they do. The point of running the suite here is to lock in the contract.

- [ ] **Step 5: Confirm all five tests pass**

Expected: `Passed: 5, Failed: 0`. If any fail, fix `WireSerializer` and re-run.

- [ ] **Step 6: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 5: WireSerializer round-trip + edge-case tests"
```

---

## Task 6: Payload — pipe server skeleton with echo tool

**Goal:** Stand up `SnoopMCP.Payload.dll` with a `PayloadEntryPoint.Inject(string)` static entry point that the (eventually submodule-referenced) `ManagedInjector` will call. The entry point launches a pipe server that accepts one client at a time and dispatches requests to a tool registry. A single `echo` tool proves the pipe round-trip end-to-end. Real inspection tools land in later tasks.

The entry point signature is dictated by Snoop's `ManagedInjector` convention: `public static int <MethodName>(string args)`. We use `args` to receive the pipe name (so the host and payload agree on which named pipe to use).

**Files:**
- Create: `src\SnoopMCP.Payload\SnoopMCP.Payload.csproj`
- Create: `src\SnoopMCP.Payload\PayloadEntryPoint.cs`
- Create: `src\SnoopMCP.Payload\PipeServer.cs`
- Create: `src\SnoopMCP.Payload\Tools\IToolHandler.cs`
- Create: `src\SnoopMCP.Payload\Tools\ToolRegistry.cs`
- Create: `src\SnoopMCP.Payload\Tools\EchoToolHandler.cs`
- Create: `tests\SnoopMCP.Payload.Tests\SnoopMCP.Payload.Tests.csproj`
- Create: `tests\SnoopMCP.Payload.Tests\PipeServerEchoTests.cs`

- [ ] **Step 1: Create `src\SnoopMCP.Payload\SnoopMCP.Payload.csproj`**

Library project. `net10.0-windows` because we will reference WPF types in later tasks; `UseWPF=true` enables `System.Windows.*`.

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0-windows</TargetFramework>
        <UseWPF>true</UseWPF>
        <RootNamespace>SnoopMCP.Payload</RootNamespace>
        <AssemblyName>SnoopMCP.Payload</AssemblyName>
        <OutputType>Library</OutputType>
        <PlatformTarget>x64</PlatformTarget>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\SnoopMCP.Protocol\SnoopMCP.Protocol.csproj" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
    </ItemGroup>
</Project>
```

- [ ] **Step 2: Add to solution**

```text
dotnet sln SnoopMCP.sln add src/SnoopMCP.Payload/SnoopMCP.Payload.csproj
```

- [ ] **Step 3: Create the tool-handler abstraction** — `src\SnoopMCP.Payload\Tools\IToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;

public interface IToolHandler
{
    string ToolName { get; }
    Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Create the registry** — `src\SnoopMCP.Payload\Tools\ToolRegistry.cs`

Per CLAUDE.md: no early returns, use the variable pattern; argument validation at method entry.

```csharp
namespace SnoopMCP.Payload.Tools;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, IToolHandler> mHandlers = new(StringComparer.Ordinal);

    public void Register(IToolHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        mHandlers[handler.ToolName] = handler;
    }

    public bool TryGet(string toolName, out IToolHandler handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        bool found = mHandlers.TryGetValue(toolName, out IToolHandler? candidate);
        handler = candidate ?? NullToolHandler.Instance;
        return found;
    }

    private sealed class NullToolHandler : IToolHandler
    {
        public static NullToolHandler Instance { get; } = new();

        public string ToolName => string.Empty;

        public Task<System.Text.Json.JsonElement> ExecuteAsync(
            System.Text.Json.JsonElement arguments,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Null tool handler invoked.");
        }
    }
}
```

- [ ] **Step 5: Create the echo handler** — `src\SnoopMCP.Payload\Tools\EchoToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;

public sealed class EchoToolHandler : IToolHandler
{
    public string ToolName => "echo";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var payload = new { echoed = arguments.GetRawText() };
        string json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
```

- [ ] **Step 6: Create the pipe server** — `src\SnoopMCP.Payload\PipeServer.cs`

One connected client at a time. Accept loop reads frames, dispatches to the registry, writes responses. Errors are reported as `RpcResponse` with `Error` set, never as exceptions to the caller.

```csharp
namespace SnoopMCP.Payload;

using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SnoopMCP.Payload.Tools;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Wire;

public sealed class PipeServer : IAsyncDisposable
{
    private const int PipeBufferSize = 64 * 1024;

    private readonly string mPipeName;
    private readonly ToolRegistry mRegistry;
    private readonly ILogger<PipeServer> mLogger;
    private readonly CancellationTokenSource mShutdown = new();
    private Task? mAcceptLoop;

    public PipeServer(string pipeName, ToolRegistry registry, ILogger<PipeServer> logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(logger);
        mPipeName = pipeName;
        mRegistry = registry;
        mLogger = logger;
    }

    public void Start()
    {
        mAcceptLoop = Task.Run(() => AcceptLoopAsync(mShutdown.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        bool keepRunning = true;
        while (keepRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    mPipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    PipeBufferSize,
                    PipeBufferSize);

                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                await ServeClientAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                keepRunning = false;
            }
            catch (IOException ex)
            {
                mLogger.LogWarning(ex, "Pipe IO exception in accept loop; will retry.");
            }
        }
    }

    private async Task ServeClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        bool clientConnected = true;
        while (clientConnected && !cancellationToken.IsCancellationRequested)
        {
            RpcRequest? request = await WireSerializer
                .ReadFrameAsync<RpcRequest>(pipe, cancellationToken)
                .ConfigureAwait(false);

            bool gotRequest = request is not null;
            if (gotRequest)
            {
                RpcResponse response = await DispatchAsync(request!, cancellationToken).ConfigureAwait(false);
                await WireSerializer.WriteFrameAsync(pipe, response, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                clientConnected = false;
            }
        }
    }

    private async Task<RpcResponse> DispatchAsync(RpcRequest request, CancellationToken cancellationToken)
    {
        RpcResponse response;
        bool toolFound = mRegistry.TryGet(request.Tool, out IToolHandler handler);
        if (!toolFound)
        {
            response = new RpcResponse
            {
                Id = request.Id,
                Error = new RpcError
                {
                    Code = ErrorCode.ToolNotFound,
                    Message = $"No handler registered for tool '{request.Tool}'."
                }
            };
        }
        else
        {
            try
            {
                JsonElement result = await handler
                    .ExecuteAsync(request.Arguments, cancellationToken)
                    .ConfigureAwait(false);
                response = new RpcResponse { Id = request.Id, Result = result };
            }
            catch (SnoopMcpException ex)
            {
                response = new RpcResponse
                {
                    Id = request.Id,
                    Error = new RpcError { Code = ex.Code, Message = ex.Message }
                };
            }
            catch (Exception ex)
            {
                mLogger.LogError(ex, "Unhandled exception in tool '{Tool}'.", request.Tool);
                response = new RpcResponse
                {
                    Id = request.Id,
                    Error = new RpcError
                    {
                        Code = ErrorCode.Unknown,
                        Message = ex.Message,
                        Details = ex.GetType().FullName
                    }
                };
            }
        }
        return response;
    }

    public async ValueTask DisposeAsync()
    {
        await mShutdown.CancelAsync().ConfigureAwait(false);
        if (mAcceptLoop is not null)
        {
            try
            {
                await mAcceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
        mShutdown.Dispose();
    }
}
```

- [ ] **Step 7: Create the payload entry point** — `src\SnoopMCP.Payload\PayloadEntryPoint.cs`

Static method called by `ManagedInjector`. Must NOT block — start the pipe server and return.

```csharp
namespace SnoopMCP.Payload;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SnoopMCP.Payload.Tools;

public static class PayloadEntryPoint
{
    private static PipeServer? psServer;

    public static int Inject(string args)
    {
        ArgumentException.ThrowIfNullOrEmpty(args);
        int exitCode = 0;
        try
        {
            string pipeName = args.Trim();
            var registry = new ToolRegistry();
            registry.Register(new EchoToolHandler());

            ILogger<PipeServer> logger = NullLogger<PipeServer>.Instance;
            psServer = new PipeServer(pipeName, registry, logger);
            psServer.Start();
        }
        catch (Exception)
        {
            const int injectionFailedExitCode = 1;
            exitCode = injectionFailedExitCode;
        }
        return exitCode;
    }
}
```

- [ ] **Step 8: Build the payload**

```text
dotnet build src/SnoopMCP.Payload/SnoopMCP.Payload.csproj -c Debug
```

Expected: build succeeds with no warnings.

- [ ] **Step 9: Create the payload test project**

`tests\SnoopMCP.Payload.Tests\SnoopMCP.Payload.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0-windows</TargetFramework>
        <UseWPF>true</UseWPF>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <RootNamespace>SnoopMCP.Payload.Tests</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="xunit.v3" Version="1.0.0" />
        <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
        <PackageReference Include="Xunit.StaFact" Version="1.2.69" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
        <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\src\SnoopMCP.Payload\SnoopMCP.Payload.csproj" />
        <ProjectReference Include="..\..\src\SnoopMCP.Protocol\SnoopMCP.Protocol.csproj" />
    </ItemGroup>
</Project>
```

> **Note on `Xunit.StaFact`:** required because later tasks create real WPF objects in tests, which must run on STA threads. Pin to 1.2.69 or query saddlerag for the current version.

- [ ] **Step 10: Add to solution**

```text
dotnet sln SnoopMCP.sln add tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj
```

- [ ] **Step 11: Write the failing echo round-trip test** — `tests\SnoopMCP.Payload.Tests\PipeServerEchoTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Tools;
using SnoopMCP.Protocol.Wire;
using Xunit;

public sealed class PipeServerEchoTests
{
    [Fact]
    public async Task Echo_ViaPipe_RoundTrips()
    {
        const int connectTimeoutMs = 5000;
        string pipeName = $"snoopmcp-echo-{Guid.NewGuid():N}";
        var registry = new ToolRegistry();
        registry.Register(new EchoToolHandler());

        await using var server = new PipeServer(pipeName, registry, NullLogger<PipeServer>.Instance);
        server.Start();

        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(connectTimeoutMs);

        using var argsDoc = JsonDocument.Parse("{\"hello\":\"world\"}");
        var request = new RpcRequest
        {
            Id = 1,
            Tool = "echo",
            Arguments = argsDoc.RootElement
        };

        await WireSerializer.WriteFrameAsync(client, request, CancellationToken.None);
        var response = await WireSerializer.ReadFrameAsync<RpcResponse>(client, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(1, response!.Id);
        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Result);

        string echoedRaw = response.Result!.Value.GetProperty("echoed").GetString() ?? string.Empty;
        Assert.Contains("\"hello\"", echoedRaw);
        Assert.Contains("\"world\"", echoedRaw);
    }
}
```

- [ ] **Step 12: Run the test**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~PipeServerEchoTests
```

Expected: `Passed: 1, Failed: 0`.

- [ ] **Step 13: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 6: Payload pipe server skeleton with echo tool"
```

---

## Task 7: Payload — DispatcherMarshal with timeout

**Goal:** Every WPF read must marshal to the Dispatcher of the object being read. `DispatcherMarshal` is the single chokepoint that owns that discipline. A stuck UI thread becomes a structured `DispatcherTimeout` per call, never a hang of the whole payload.

The class is an instance, not static, so tests can supply their own STA dispatcher and so payload code can swap dispatchers (popups can have a different Dispatcher from the main window if they were created on a different thread, though in practice in WPF apps they share).

**Files:**
- Create: `src\SnoopMCP.Payload\DispatcherMarshal.cs`
- Create: `tests\SnoopMCP.Payload.Tests\DispatcherMarshalTests.cs`

- [ ] **Step 1: Write the failing tests first** — `tests\SnoopMCP.Payload.Tests\DispatcherMarshalTests.cs`

`Xunit.StaFact` provides `[StaFact]` (single-threaded apartment) which is what WPF needs.

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Threading;
using System.Windows.Threading;
using SnoopMCP.Payload;
using SnoopMCP.Protocol.Errors;
using Xunit;

public sealed class DispatcherMarshalTests
{
    [StaFact]
    public void Invoke_FastFunction_ReturnsResult()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromSeconds(2));

        int result = marshal.Invoke(() => 42 + 8, CancellationToken.None);

        Assert.Equal(50, result);
    }

    [StaFact]
    public void Invoke_OnSameThread_RunsInline()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromSeconds(2));

        int dispatcherThreadId = Environment.CurrentManagedThreadId;
        int observedThreadId = marshal.Invoke(() => Environment.CurrentManagedThreadId, CancellationToken.None);

        Assert.Equal(dispatcherThreadId, observedThreadId);
    }

    [StaFact]
    public void Invoke_FromForeignThread_RunsOnDispatcherThread()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromSeconds(2));
        int dispatcherThreadId = Environment.CurrentManagedThreadId;
        int observed = 0;

        var worker = new Thread(() =>
        {
            observed = marshal.Invoke(() => Environment.CurrentManagedThreadId, CancellationToken.None);
        });
        worker.Start();

        DispatcherFrame frame = new();
        var pump = new Thread(() =>
        {
            worker.Join();
            frame.Continue = false;
        });
        pump.Start();
        Dispatcher.PushFrame(frame);
        pump.Join();

        Assert.Equal(dispatcherThreadId, observed);
    }

    [StaFact]
    public void Invoke_ExceedingTimeout_ThrowsDispatcherTimeout()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var shortTimeout = TimeSpan.FromMilliseconds(100);
        var marshal = new DispatcherMarshal(dispatcher, shortTimeout);

        // Block the dispatcher with a long-running operation, then enqueue another and expect timeout.
        // Easier: create a marshal whose dispatcher is a *separate* thread's dispatcher, then enqueue
        // a slow function. Done via a worker thread that owns its own dispatcher.
        Dispatcher? workerDispatcher = null;
        var ready = new ManualResetEventSlim();
        var workerThread = new Thread(() =>
        {
            workerDispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        workerThread.SetApartmentState(ApartmentState.STA);
        workerThread.IsBackground = true;
        workerThread.Start();
        ready.Wait();

        var slowMarshal = new DispatcherMarshal(workerDispatcher!, shortTimeout);

        // Block worker for longer than the timeout, then enqueue something behind it.
        workerDispatcher!.BeginInvoke(() => Thread.Sleep(TimeSpan.FromSeconds(1)));

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(
            () => slowMarshal.Invoke(() => 1, CancellationToken.None));
        Assert.Equal(ErrorCode.DispatcherTimeout, ex.Code);

        workerDispatcher.InvokeShutdown();
        workerThread.Join();
    }

    [StaFact]
    public void Invoke_OnShutDownDispatcher_ThrowsInvalidOperation()
    {
        Dispatcher? workerDispatcher = null;
        var ready = new ManualResetEventSlim();
        var workerThread = new Thread(() =>
        {
            workerDispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        workerThread.SetApartmentState(ApartmentState.STA);
        workerThread.IsBackground = true;
        workerThread.Start();
        ready.Wait();

        workerDispatcher!.InvokeShutdown();
        workerThread.Join();

        var marshal = new DispatcherMarshal(workerDispatcher, TimeSpan.FromSeconds(1));

        Assert.Throws<InvalidOperationException>(
            () => marshal.Invoke(() => 1, CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run the tests — they must fail because `DispatcherMarshal` does not exist**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~DispatcherMarshalTests
```

Expected: compile error, `The type or namespace name 'DispatcherMarshal' could not be found`.

- [ ] **Step 3: Implement `DispatcherMarshal`** — `src\SnoopMCP.Payload\DispatcherMarshal.cs`

```csharp
namespace SnoopMCP.Payload;

using System.Windows.Threading;
using SnoopMCP.Protocol.Errors;

public sealed class DispatcherMarshal
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly Dispatcher mDispatcher;
    private readonly TimeSpan mTimeout;

    public DispatcherMarshal(Dispatcher dispatcher) : this(dispatcher, DefaultTimeout)
    {
    }

    public DispatcherMarshal(Dispatcher dispatcher, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
        }
        mDispatcher = dispatcher;
        mTimeout = timeout;
    }

    public T Invoke<T>(Func<T> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        cancellationToken.ThrowIfCancellationRequested();

        if (mDispatcher.HasShutdownStarted || mDispatcher.HasShutdownFinished)
        {
            throw new InvalidOperationException("Dispatcher has been shut down.");
        }

        T result;
        bool onDispatcherThread = mDispatcher.CheckAccess();
        if (onDispatcherThread)
        {
            result = work();
        }
        else
        {
            result = InvokeFromForeignThread(work, cancellationToken);
        }
        return result;
    }

    private T InvokeFromForeignThread<T>(Func<T> work, CancellationToken cancellationToken)
    {
        var operation = mDispatcher.InvokeAsync(work, DispatcherPriority.Normal, cancellationToken);
        bool completed = operation.Task.Wait(mTimeout, cancellationToken);
        T result;
        if (completed)
        {
            result = operation.Task.GetAwaiter().GetResult();
        }
        else
        {
            operation.Abort();
            throw new SnoopMcpException(
                ErrorCode.DispatcherTimeout,
                $"Dispatcher invoke exceeded {mTimeout.TotalMilliseconds:F0}ms.");
        }
        return result;
    }
}
```

- [ ] **Step 4: Run the tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~DispatcherMarshalTests
```

Expected: `Passed: 5, Failed: 0`. If the timeout test is flaky on slow machines, raise `shortTimeout` in the test (not in production).

- [ ] **Step 5: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 7: DispatcherMarshal with per-call timeout and structured DispatcherTimeout error"
```

---

## Task 8: Payload — ElementRegistry (stable id ↔ live element)

**Goal:** Stable integer ids for `DependencyObject` instances. Same object always gets the same id; id resolves to the live object via `WeakReference` so we never accidentally extend lifetime; expired ids return `false` from `TryResolve` and downstream tools translate that to `ErrorCode.ElementExpired`.

Reverse lookup (element → id) uses `ConditionalWeakTable` so the registry doesn't pin the element either way.

**Files:**
- Create: `src\SnoopMCP.Payload\ElementRegistry.cs`
- Create: `tests\SnoopMCP.Payload.Tests\ElementRegistryTests.cs`

- [ ] **Step 1: Write the failing tests** — `tests\SnoopMCP.Payload.Tests\ElementRegistryTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Windows.Controls;
using SnoopMCP.Payload;
using Xunit;

public sealed class ElementRegistryTests
{
    [StaFact]
    public void GetOrAssign_FirstTime_AssignsNewId()
    {
        var registry = new ElementRegistry();
        var button = new Button();

        int id = registry.GetOrAssign(button);

        Assert.True(id > 0, $"Expected positive id, got {id}.");
    }

    [StaFact]
    public void GetOrAssign_SameElementTwice_ReturnsSameId()
    {
        var registry = new ElementRegistry();
        var button = new Button();

        int first = registry.GetOrAssign(button);
        int second = registry.GetOrAssign(button);

        Assert.Equal(first, second);
    }

    [StaFact]
    public void GetOrAssign_DifferentElements_ReturnsDifferentIds()
    {
        var registry = new ElementRegistry();
        var buttonA = new Button();
        var buttonB = new Button();

        int idA = registry.GetOrAssign(buttonA);
        int idB = registry.GetOrAssign(buttonB);

        Assert.NotEqual(idA, idB);
    }

    [StaFact]
    public void TryResolve_LiveElement_ReturnsTrueAndSameInstance()
    {
        var registry = new ElementRegistry();
        var button = new Button();
        int id = registry.GetOrAssign(button);

        bool resolved = registry.TryResolve(id, out System.Windows.DependencyObject element);

        Assert.True(resolved);
        Assert.Same(button, element);
    }

    [StaFact]
    public void TryResolve_UnknownId_ReturnsFalse()
    {
        var registry = new ElementRegistry();
        const int unknownId = 99999;

        bool resolved = registry.TryResolve(unknownId, out System.Windows.DependencyObject element);

        Assert.False(resolved);
        Assert.Null(element);
    }

    [StaFact]
    public void TryResolve_AfterElementCollected_ReturnsFalse()
    {
        var registry = new ElementRegistry();
        int id = AssignAndDropReference(registry);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        bool resolved = registry.TryResolve(id, out System.Windows.DependencyObject element);

        Assert.False(resolved);
        Assert.Null(element);
    }

    private static int AssignAndDropReference(ElementRegistry registry)
    {
        var button = new Button();
        int id = registry.GetOrAssign(button);
        return id;
    }
}
```

- [ ] **Step 2: Run the tests — expect compile failure (`ElementRegistry` does not exist)**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ElementRegistryTests
```

Expected: compile error referencing `ElementRegistry`.

- [ ] **Step 3: Implement `ElementRegistry`** — `src\SnoopMCP.Payload\ElementRegistry.cs`

```csharp
namespace SnoopMCP.Payload;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;

public sealed class ElementRegistry
{
    private readonly ConcurrentDictionary<int, WeakReference<DependencyObject>> mById = new();
    private readonly ConditionalWeakTable<DependencyObject, IdHolder> mByElement = new();
    private int mNextId;

    public int GetOrAssign(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        IdHolder holder = mByElement.GetValue(element, e => AllocateId(e));
        return holder.Id;
    }

    public bool TryResolve(int id, out DependencyObject element)
    {
        DependencyObject? resolved = null;
        bool found = false;
        bool hasEntry = mById.TryGetValue(id, out WeakReference<DependencyObject>? weakRef);
        if (hasEntry && weakRef!.TryGetTarget(out resolved))
        {
            found = true;
        }
        else if (hasEntry)
        {
            mById.TryRemove(id, out _);
        }
        element = resolved!;
        return found;
    }

    public bool IsAlive(int id)
    {
        bool alive = TryResolve(id, out _);
        return alive;
    }

    private IdHolder AllocateId(DependencyObject element)
    {
        int newId = Interlocked.Increment(ref mNextId);
        mById[newId] = new WeakReference<DependencyObject>(element);
        return new IdHolder(newId);
    }

    private sealed class IdHolder
    {
        public IdHolder(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }
}
```

- [ ] **Step 4: Run the tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ElementRegistryTests
```

Expected: `Passed: 6, Failed: 0`. The GC-collection test can be sensitive — if it fails intermittently, add a second `GC.Collect()` cycle before the assert; do **not** weaken the assert.

- [ ] **Step 5: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 8: ElementRegistry — stable ids backed by weak references"
```

---

## Task 9: Payload — Path string parser + emitter

**Goal:** Canonical path strings of the form `/TypeName[Name='X', AutomationId='Y'][n]/...`. Used by `describeElement` (emit) and `resolvePath` (parse). Stable, human-readable, survive across tool calls in a way numeric ids do not.

Path step shape: `TypeName` (short type name, no namespace) + zero or more attribute predicates in brackets + optional integer index `[n]` for disambiguating same-typed siblings (0-based).

**Files:**
- Create: `src\SnoopMCP.Payload\PathStrings\PathStep.cs`
- Create: `src\SnoopMCP.Payload\PathStrings\PathStringParser.cs`
- Create: `src\SnoopMCP.Payload\PathStrings\PathStringEmitter.cs`
- Create: `tests\SnoopMCP.Payload.Tests\PathStringParserTests.cs`
- Create: `tests\SnoopMCP.Payload.Tests\PathStringEmitterTests.cs`

- [ ] **Step 1: Create the step record** — `src\SnoopMCP.Payload\PathStrings\PathStep.cs`

```csharp
namespace SnoopMCP.Payload.PathStrings;

public sealed record PathStep(
    string TypeName,
    IReadOnlyDictionary<string, string> Attributes,
    int? Index);
```

- [ ] **Step 2: Write the failing parser tests** — `tests\SnoopMCP.Payload.Tests\PathStringParserTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Errors;
using Xunit;

public sealed class PathStringParserTests
{
    [Fact]
    public void Parse_SingleStep_NoPredicates()
    {
        var parser = new PathStringParser();
        IReadOnlyList<PathStep> steps = parser.Parse("/Window");

        Assert.Single(steps);
        Assert.Equal("Window", steps[0].TypeName);
        Assert.Empty(steps[0].Attributes);
        Assert.Null(steps[0].Index);
    }

    [Fact]
    public void Parse_MultipleSteps_NoPredicates()
    {
        var parser = new PathStringParser();
        IReadOnlyList<PathStep> steps = parser.Parse("/Window/Grid/StackPanel/Button");

        Assert.Equal(4, steps.Count);
        Assert.Equal("Window", steps[0].TypeName);
        Assert.Equal("Grid", steps[1].TypeName);
        Assert.Equal("StackPanel", steps[2].TypeName);
        Assert.Equal("Button", steps[3].TypeName);
    }

    [Fact]
    public void Parse_StepWithNameAttribute()
    {
        var parser = new PathStringParser();
        IReadOnlyList<PathStep> steps = parser.Parse("/Window[Name='Main']");

        Assert.Single(steps);
        Assert.Equal("Window", steps[0].TypeName);
        Assert.Equal("Main", steps[0].Attributes["Name"]);
    }

    [Fact]
    public void Parse_StepWithMultipleAttributes()
    {
        var parser = new PathStringParser();
        IReadOnlyList<PathStep> steps = parser.Parse("/Button[Name='Save', AutomationId='SaveBtn']");

        Assert.Single(steps);
        Assert.Equal("Save", steps[0].Attributes["Name"]);
        Assert.Equal("SaveBtn", steps[0].Attributes["AutomationId"]);
    }

    [Fact]
    public void Parse_StepWithIndex()
    {
        var parser = new PathStringParser();
        IReadOnlyList<PathStep> steps = parser.Parse("/StackPanel/Button[2]");

        Assert.Equal(2, steps.Count);
        Assert.Equal(2, steps[1].Index);
    }

    [Fact]
    public void Parse_StepWithAttributesAndIndex()
    {
        var parser = new PathStringParser();
        IReadOnlyList<PathStep> steps = parser.Parse("/Button[Name='Save'][1]");

        Assert.Single(steps);
        Assert.Equal("Save", steps[0].Attributes["Name"]);
        Assert.Equal(1, steps[0].Index);
    }

    [Fact]
    public void Parse_EmptyString_Throws()
    {
        var parser = new PathStringParser();
        Assert.Throws<SnoopMcpException>(() => parser.Parse(""));
    }

    [Fact]
    public void Parse_MissingLeadingSlash_Throws()
    {
        var parser = new PathStringParser();
        var ex = Assert.Throws<SnoopMcpException>(() => parser.Parse("Window/Grid"));
        Assert.Equal(ErrorCode.PathParseError, ex.Code);
    }

    [Fact]
    public void Parse_UnclosedBracket_Throws()
    {
        var parser = new PathStringParser();
        var ex = Assert.Throws<SnoopMcpException>(() => parser.Parse("/Button[Name='Save'"));
        Assert.Equal(ErrorCode.PathParseError, ex.Code);
    }
}
```

- [ ] **Step 3: Write the failing emitter tests** — `tests\SnoopMCP.Payload.Tests\PathStringEmitterTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Payload.PathStrings;
using Xunit;

public sealed class PathStringEmitterTests
{
    [StaFact]
    public void Emit_SingleElement_ReturnsTypeName()
    {
        var window = new Window();
        var emitter = new PathStringEmitter();

        string path = emitter.Emit(window);

        Assert.Equal("/Window", path);
    }

    [StaFact]
    public void Emit_NamedChild_IncludesNameAttribute()
    {
        var grid = new Grid();
        var button = new Button { Name = "SaveBtn" };
        grid.Children.Add(button);
        var emitter = new PathStringEmitter();

        string path = emitter.Emit(button);

        Assert.Equal("/Grid/Button[Name='SaveBtn']", path);
    }

    [StaFact]
    public void Emit_AnonymousSibling_UsesIndex()
    {
        var stack = new StackPanel();
        var first = new Button();
        var second = new Button();
        var third = new Button();
        stack.Children.Add(first);
        stack.Children.Add(second);
        stack.Children.Add(third);
        var emitter = new PathStringEmitter();

        string secondPath = emitter.Emit(second);

        Assert.Equal("/StackPanel/Button[1]", secondPath);
    }

    [StaFact]
    public void Emit_MixedTypes_OnlyIndexesAmongSameType()
    {
        var stack = new StackPanel();
        var label = new TextBlock();
        var button = new Button();
        stack.Children.Add(label);
        stack.Children.Add(button);
        var emitter = new PathStringEmitter();

        string buttonPath = emitter.Emit(button);

        Assert.Equal("/StackPanel/Button", buttonPath);
    }

    [StaFact]
    public void Emit_NameAndAutomationId_IncludesBoth()
    {
        var grid = new Grid();
        var button = new Button { Name = "Save" };
        System.Windows.Automation.AutomationProperties.SetAutomationId(button, "SaveBtn");
        grid.Children.Add(button);
        var emitter = new PathStringEmitter();

        string path = emitter.Emit(button);

        Assert.Equal("/Grid/Button[Name='Save', AutomationId='SaveBtn']", path);
    }
}
```

- [ ] **Step 4: Run both test files — expect failures**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~PathString
```

Expected: compile errors referencing `PathStringParser` and `PathStringEmitter`.

- [ ] **Step 5: Implement `PathStringParser`** — `src\SnoopMCP.Payload\PathStrings\PathStringParser.cs`

```csharp
namespace SnoopMCP.Payload.PathStrings;

using SnoopMCP.Protocol.Errors;

public sealed class PathStringParser
{
    public IReadOnlyList<PathStep> Parse(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new SnoopMcpException(ErrorCode.PathParseError, "Path is empty.");
        }
        if (path[0] != '/')
        {
            throw new SnoopMcpException(ErrorCode.PathParseError, "Path must start with '/'.");
        }

        string remaining = path[1..];
        string[] rawSteps = remaining.Split('/', StringSplitOptions.None);
        var steps = new List<PathStep>(rawSteps.Length);
        foreach (string raw in rawSteps.Where(s => s.Length > 0))
        {
            steps.Add(ParseStep(raw));
        }
        if (steps.Count == 0)
        {
            throw new SnoopMcpException(ErrorCode.PathParseError, "Path has no steps.");
        }
        return steps;
    }

    private static PathStep ParseStep(string raw)
    {
        int firstBracket = raw.IndexOf('[');
        string typeName;
        string remainder;
        if (firstBracket < 0)
        {
            typeName = raw;
            remainder = string.Empty;
        }
        else
        {
            typeName = raw[..firstBracket];
            remainder = raw[firstBracket..];
        }

        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new SnoopMcpException(ErrorCode.PathParseError, $"Step '{raw}' has no type name.");
        }

        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        int? index = null;

        while (remainder.Length > 0)
        {
            int close = remainder.IndexOf(']');
            if (close < 0)
            {
                throw new SnoopMcpException(ErrorCode.PathParseError, $"Unclosed '[' in step '{raw}'.");
            }
            string inside = remainder[1..close];
            remainder = remainder[(close + 1)..];

            bool isIndex = int.TryParse(inside, out int parsedIndex);
            if (isIndex)
            {
                index = parsedIndex;
            }
            else
            {
                ParseAttributes(inside, attributes, raw);
            }
        }

        return new PathStep(typeName, attributes, index);
    }

    private static void ParseAttributes(string inside, Dictionary<string, string> attributes, string fullStep)
    {
        string[] pairs = inside.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (string pair in pairs)
        {
            int equals = pair.IndexOf('=');
            if (equals < 0)
            {
                throw new SnoopMcpException(
                    ErrorCode.PathParseError,
                    $"Attribute '{pair}' missing '=' in step '{fullStep}'.");
            }
            string key = pair[..equals].Trim();
            string valuePart = pair[(equals + 1)..].Trim();
            bool isQuoted = valuePart.Length >= 2 && valuePart[0] == '\'' && valuePart[^1] == '\'';
            if (!isQuoted)
            {
                throw new SnoopMcpException(
                    ErrorCode.PathParseError,
                    $"Attribute value '{valuePart}' must be single-quoted in step '{fullStep}'.");
            }
            attributes[key] = valuePart[1..^1];
        }
    }
}
```

- [ ] **Step 6: Implement `PathStringEmitter`** — `src\SnoopMCP.Payload\PathStrings\PathStringEmitter.cs`

```csharp
namespace SnoopMCP.Payload.PathStrings;

using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

public sealed class PathStringEmitter
{
    public string Emit(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var chain = new List<DependencyObject>();
        DependencyObject? current = element;
        while (current is not null)
        {
            chain.Add(current);
            current = GetParent(current);
        }
        chain.Reverse();

        var builder = new StringBuilder();
        foreach (DependencyObject node in chain)
        {
            builder.Append('/');
            builder.Append(BuildStep(node));
        }
        return builder.ToString();
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        DependencyObject? parent = null;
        bool isVisual = element is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            parent = VisualTreeHelper.GetParent(element);
        }
        return parent;
    }

    private static string BuildStep(DependencyObject element)
    {
        string typeName = element.GetType().Name;
        string? name = TryGetName(element);
        string? automationId = TryGetAutomationId(element);
        int? siblingIndex = TryGetSiblingIndex(element);

        var attrs = new List<string>();
        if (!string.IsNullOrEmpty(name))
        {
            attrs.Add($"Name='{name}'");
        }
        if (!string.IsNullOrEmpty(automationId))
        {
            attrs.Add($"AutomationId='{automationId}'");
        }

        var builder = new StringBuilder(typeName);
        if (attrs.Count > 0)
        {
            builder.Append('[').Append(string.Join(", ", attrs)).Append(']');
        }
        if (siblingIndex is int idx && attrs.Count == 0)
        {
            builder.Append('[').Append(idx).Append(']');
        }
        return builder.ToString();
    }

    private static string? TryGetName(DependencyObject element)
    {
        string? name = (element as FrameworkElement)?.Name;
        bool hasName = !string.IsNullOrEmpty(name);
        return hasName ? name : null;
    }

    private static string? TryGetAutomationId(DependencyObject element)
    {
        string? id = AutomationProperties.GetAutomationId(element);
        bool hasId = !string.IsNullOrEmpty(id);
        return hasId ? id : null;
    }

    private static int? TryGetSiblingIndex(DependencyObject element)
    {
        DependencyObject? parent = GetParent(element);
        int? result = null;
        if (parent is not null)
        {
            int siblingCount = VisualTreeHelper.GetChildrenCount(parent);
            Type myType = element.GetType();
            int sameTypeIndex = 0;
            int foundAt = -1;
            for (int i = 0; i < siblingCount; i++)
            {
                DependencyObject sibling = VisualTreeHelper.GetChild(parent, i);
                bool sameType = sibling.GetType() == myType;
                if (sameType)
                {
                    bool isThisOne = ReferenceEquals(sibling, element);
                    if (isThisOne)
                    {
                        foundAt = sameTypeIndex;
                    }
                    sameTypeIndex++;
                }
            }
            bool needsIndex = sameTypeIndex > 1 && foundAt >= 0;
            if (needsIndex)
            {
                result = foundAt;
            }
        }
        return result;
    }
}
```

- [ ] **Step 7: Run both test files**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~PathString
```

Expected: all parser tests (9) and all emitter tests (5) pass.

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 9: PathStringParser + PathStringEmitter (canonical /Type[Name='X'][n]/... grammar)"
```

---

## Tool tasks — pattern note

Tasks 10 through 24 each implement one inspection tool. Every task follows the same shape:

1. **DTO** in `src\SnoopMCP.Protocol\Tools\<Tool>Dto.cs` — request + response records the host and payload share.
2. **Inspector class** in `src\SnoopMCP.Payload\Inspection\<Tool>.cs` — pure logic, takes `(DependencyObject, …) → response DTO`. No pipe code, no JSON.
3. **Tool handler** in `src\SnoopMCP.Payload\Tools\<Tool>ToolHandler.cs` — `IToolHandler` wrapper: deserialize args, resolve element via `ElementRegistry`, marshal to dispatcher via `DispatcherMarshal`, return JSON.
4. **Register the handler** in `PayloadEntryPoint.Inject` so the running payload exposes it.
5. **Unit tests** under `tests\SnoopMCP.Payload.Tests\` — `[StaFact]` driving real WPF objects through the inspector.

Common conventions:
- Inspector methods are synchronous. Async-ness lives at the handler boundary.
- Inspector methods accept `DependencyObject` (already resolved), not ids. Id resolution is the handler's job.
- All inspector code MUST be called via `DispatcherMarshal.Invoke` from the handler — the inspector itself does not marshal.
- Argument validation at method entry. Single return. No early returns. No if/else chains. Per `CLAUDE.md`.

---

## Task 10: Tool — describeElement

**Goal:** Foundational tool. Every other tool returns shapes of `DescribeElementResponse`. Includes the navigation-enrichment fields: `bounds`, `visibleText`, `isInTemplate`, `hasBindingErrors`, `path`.

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\DescribeElementDto.cs`
- Create: `src\SnoopMCP.Payload\Inspection\ElementDescriber.cs`
- Create: `src\SnoopMCP.Payload\Tools\DescribeElementToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs` (register handler)
- Create: `tests\SnoopMCP.Payload.Tests\ElementDescriberTests.cs`

- [ ] **Step 1: Create the DTO** — `src\SnoopMCP.Protocol\Tools\DescribeElementDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record DescribeElementRequest(int Id);

public sealed record DescribeElementResponse(
    int Id,
    string Type,
    string? Name,
    string? AutomationId,
    BoundsDto Bounds,
    string VisibleText,
    bool IsInTemplate,
    bool HasBindingErrors,
    string Path,
    int ChildCount,
    string? DataContextType,
    int HashCode,
    bool IsAlive);

public sealed record BoundsDto(double X, double Y, double Width, double Height);
```

- [ ] **Step 2: Write the failing tests** — `tests\SnoopMCP.Payload.Tests\ElementDescriberTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class ElementDescriberTests
{
    private static ElementDescriber CreateDescriber(ElementRegistry registry)
    {
        return new ElementDescriber(registry, new PathStringEmitter());
    }

    [StaFact]
    public void Describe_PlainButton_ReturnsTypeAndChildCount()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var button = new Button { Name = "SaveButton" };

        DescribeElementResponse response = describer.Describe(button);

        Assert.Equal("Button", response.Type);
        Assert.Equal("SaveButton", response.Name);
        Assert.True(response.IsAlive);
        Assert.Equal(0, response.ChildCount);
    }

    [StaFact]
    public void Describe_AutomationIdSet_IsReturned()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var button = new Button();
        System.Windows.Automation.AutomationProperties.SetAutomationId(button, "ThemePicker");

        DescribeElementResponse response = describer.Describe(button);

        Assert.Equal("ThemePicker", response.AutomationId);
    }

    [StaFact]
    public void Describe_TextBlockWithText_VisibleTextIsContent()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var text = new TextBlock { Text = "Hello world" };

        DescribeElementResponse response = describer.Describe(text);

        Assert.Equal("Hello world", response.VisibleText);
    }

    [StaFact]
    public void Describe_PanelWithTextChildren_AggregatesVisibleText()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = "Alpha" });
        stack.Children.Add(new TextBlock { Text = "Beta" });

        DescribeElementResponse response = describer.Describe(stack);

        Assert.Contains("Alpha", response.VisibleText);
        Assert.Contains("Beta", response.VisibleText);
    }

    [StaFact]
    public void Describe_DataContextSet_TypeNameReturned()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var grid = new Grid { DataContext = new Customer { Name = "Alice" } };

        DescribeElementResponse response = describer.Describe(grid);

        Assert.Equal(typeof(Customer).FullName, response.DataContextType);
    }

    [StaFact]
    public void Describe_NoDataContext_TypeIsNull()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var grid = new Grid();

        DescribeElementResponse response = describer.Describe(grid);

        Assert.Null(response.DataContextType);
    }

    [StaFact]
    public void Describe_BrokenBinding_HasBindingErrorsTrue()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var text = new TextBlock();
        var binding = new Binding("ThisPathDoesNotExist") { Source = new object() };
        BindingOperations.SetBinding(text, TextBlock.TextProperty, binding);

        DescribeElementResponse response = describer.Describe(text);

        Assert.True(response.HasBindingErrors);
    }

    [StaFact]
    public void Describe_PathEmits_WithEmitter()
    {
        var registry = new ElementRegistry();
        var describer = CreateDescriber(registry);
        var grid = new Grid();
        var button = new Button { Name = "SaveBtn" };
        grid.Children.Add(button);

        DescribeElementResponse response = describer.Describe(button);

        Assert.Equal("/Grid/Button[Name='SaveBtn']", response.Path);
    }

    private sealed class Customer
    {
        public string Name { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 3: Run the tests — expect failure (`ElementDescriber` missing)**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ElementDescriberTests
```

- [ ] **Step 4: Implement `ElementDescriber`** — `src\SnoopMCP.Payload\Inspection\ElementDescriber.cs`

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;

public sealed class ElementDescriber
{
    private const int VisibleTextCharCap = 200;

    private readonly ElementRegistry mRegistry;
    private readonly PathStringEmitter mEmitter;

    public ElementDescriber(ElementRegistry registry, PathStringEmitter emitter)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(emitter);
        mRegistry = registry;
        mEmitter = emitter;
    }

    public DescribeElementResponse Describe(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        int id = mRegistry.GetOrAssign(element);
        string typeName = element.GetType().Name;
        string? name = (element as FrameworkElement)?.Name;
        string? automationId = AutomationProperties.GetAutomationId(element);
        BoundsDto bounds = ComputeBounds(element);
        string visibleText = ExtractVisibleText(element);
        bool isInTemplate = ResolveTemplatedParent(element) is not null;
        bool hasBindingErrors = AnyBindingHasError(element);
        string path = mEmitter.Emit(element);
        int childCount = SafeChildCount(element);
        string? dataContextType = (element as FrameworkElement)?.DataContext?.GetType().FullName;
        int hashCode = RuntimeHelpers.GetHashCode(element);

        return new DescribeElementResponse(
            Id: id,
            Type: typeName,
            Name: string.IsNullOrEmpty(name) ? null : name,
            AutomationId: string.IsNullOrEmpty(automationId) ? null : automationId,
            Bounds: bounds,
            VisibleText: visibleText,
            IsInTemplate: isInTemplate,
            HasBindingErrors: hasBindingErrors,
            Path: path,
            ChildCount: childCount,
            DataContextType: dataContextType,
            HashCode: hashCode,
            IsAlive: true);
    }

    private static DependencyObject? ResolveTemplatedParent(DependencyObject element)
    {
        DependencyObject? templated = null;
        if (element is FrameworkElement fe)
        {
            templated = fe.TemplatedParent;
        }
        else if (element is FrameworkContentElement fce)
        {
            templated = fce.TemplatedParent;
        }
        return templated;
    }

    private static int SafeChildCount(DependencyObject element)
    {
        int count = 0;
        bool isVisual = element is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            count = VisualTreeHelper.GetChildrenCount(element);
        }
        return count;
    }

    private static BoundsDto ComputeBounds(DependencyObject element)
    {
        BoundsDto bounds = new(0, 0, 0, 0);
        if (element is UIElement ui && ui.IsArrangeValid)
        {
            try
            {
                Visual? rootVisual = FindRootVisual(ui);
                if (rootVisual is not null)
                {
                    GeneralTransform transform = ui.TransformToAncestor(rootVisual);
                    Rect rect = transform.TransformBounds(new Rect(new Point(0, 0), ui.RenderSize));
                    bounds = new BoundsDto(rect.X, rect.Y, rect.Width, rect.Height);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }
        return bounds;
    }

    private static Visual? FindRootVisual(Visual start)
    {
        DependencyObject? current = start;
        DependencyObject? lastVisual = start;
        while (current is not null)
        {
            lastVisual = current;
            current = VisualTreeHelper.GetParent(current);
        }
        return lastVisual as Visual;
    }

    private static string ExtractVisibleText(DependencyObject element)
    {
        var builder = new StringBuilder();
        AppendVisibleText(element, builder);
        string result = builder.ToString().Trim();
        if (result.Length > VisibleTextCharCap)
        {
            result = result[..VisibleTextCharCap] + "…";
        }
        return result;
    }

    private static void AppendVisibleText(DependencyObject element, StringBuilder builder)
    {
        bool budgetExceeded = builder.Length >= VisibleTextCharCap;
        if (!budgetExceeded)
        {
            if (element is TextBlock tb && !string.IsNullOrEmpty(tb.Text))
            {
                AppendSpaced(builder, tb.Text);
            }
            else if (element is TextBox tx && !string.IsNullOrEmpty(tx.Text))
            {
                AppendSpaced(builder, tx.Text);
            }
            else if (element is ContentControl cc && cc.Content is string s && !string.IsNullOrEmpty(s))
            {
                AppendSpaced(builder, s);
            }

            bool isVisual = element is Visual or System.Windows.Media.Media3D.Visual3D;
            if (isVisual)
            {
                int childCount = VisualTreeHelper.GetChildrenCount(element);
                for (int i = 0; i < childCount; i++)
                {
                    AppendVisibleText(VisualTreeHelper.GetChild(element, i), builder);
                }
            }
        }
    }

    private static void AppendSpaced(StringBuilder builder, string fragment)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }
        builder.Append(fragment);
    }

    private static bool AnyBindingHasError(DependencyObject element)
    {
        bool anyError = false;
        var enumerator = element.GetLocalValueEnumerator();
        while (enumerator.MoveNext() && !anyError)
        {
            LocalValueEntry entry = enumerator.Current;
            BindingExpressionBase? expr = BindingOperations.GetBindingExpressionBase(element, entry.Property);
            if (expr is not null)
            {
                bool errored = expr.HasError || expr.Status == BindingStatus.PathError
                    || expr.Status == BindingStatus.UpdateSourceError
                    || expr.Status == BindingStatus.UpdateTargetError;
                if (errored)
                {
                    anyError = true;
                }
            }
        }
        return anyError;
    }
}

internal static class RuntimeHelpers
{
    public static int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
```

- [ ] **Step 5: Run the tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ElementDescriberTests
```

Expected: all 8 tests pass.

- [ ] **Step 6: Create the tool handler** — `src\SnoopMCP.Payload\Tools\DescribeElementToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class DescribeElementToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly ElementDescriber mDescriber;
    private readonly DispatcherMarshal mMarshal;

    public DescribeElementToolHandler(
        ElementRegistry registry,
        ElementDescriber describer,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(describer);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mDescriber = describer;
        mMarshal = marshal;
    }

    public string ToolName => "describeElement";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<DescribeElementRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject element);
        if (!resolved)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        DescribeElementResponse response = mMarshal.Invoke(
            () => mDescriber.Describe(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
```

- [ ] **Step 7: Register the handler in `PayloadEntryPoint`** — modify `src\SnoopMCP.Payload\PayloadEntryPoint.cs`

Add wiring. `Application.Current.Dispatcher` is the right dispatcher inside the target WPF process; the payload runs after the app has started so `Application.Current` is non-null.

```csharp
namespace SnoopMCP.Payload;

using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Payload.Tools;

public static class PayloadEntryPoint
{
    private static PipeServer? psServer;

    public static int Inject(string args)
    {
        ArgumentException.ThrowIfNullOrEmpty(args);
        int exitCode = 0;
        try
        {
            string pipeName = args.Trim();

            if (Application.Current is null)
            {
                throw new InvalidOperationException(
                    "Application.Current is null; payload must inject into a running WPF app.");
            }

            var registry = new ElementRegistry();
            var emitter = new PathStringEmitter();
            var describer = new ElementDescriber(registry, emitter);
            var marshal = new DispatcherMarshal(Application.Current.Dispatcher);

            var toolRegistry = new ToolRegistry();
            toolRegistry.Register(new EchoToolHandler());
            toolRegistry.Register(new DescribeElementToolHandler(registry, describer, marshal));

            ILogger<PipeServer> logger = NullLogger<PipeServer>.Instance;
            psServer = new PipeServer(pipeName, toolRegistry, logger);
            psServer.Start();
        }
        catch (Exception)
        {
            const int injectionFailedExitCode = 1;
            exitCode = injectionFailedExitCode;
        }
        return exitCode;
    }
}
```

- [ ] **Step 8: Build and re-run the full payload test suite**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj
```

Expected: all tests so far pass.

- [ ] **Step 9: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 10: describeElement tool — enriched per-node identity, bounds, visible text, binding-error flag, path"
```

---

## Task 11: Tool — listVisualRoots (with popup back-references)

**Goal:** Enumerate every active `PresentationSource` in the target process and classify each as `Window`, `Popup`, or `Other`. For popups, attempt to identify the `Popup` element that opened them (`openedBy`).

Detection rules used:
- `RootVisual` is a `Window` → `kind = Window`, `openedBy = null`
- `RootVisual.GetType().Name == "PopupRoot"` → `kind = Popup`; scan known Window roots for open `Popup` elements whose `Child` lives under this `PopupRoot`
- Anything else → `kind = Other`, `openedBy = null`

`Tooltip` and `Adorner` are folded into `Other` for v1. Real tooltip popups are still detected as `Popup` (a `ToolTip` shown via WPF infrastructure is hosted in a popup).

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\ListVisualRootsDto.cs`
- Create: `src\SnoopMCP.Payload\Inspection\RootEnumerator.cs`
- Create: `src\SnoopMCP.Payload\Inspection\PopupOwnerResolver.cs`
- Create: `src\SnoopMCP.Payload\Tools\ListVisualRootsToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs` (register handler)
- Create: `tests\SnoopMCP.Payload.Tests\RootEnumeratorTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\ListVisualRootsDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record ListVisualRootsRequest();

public sealed record ListVisualRootsResponse(IReadOnlyList<VisualRootDto> Roots);

public sealed record VisualRootDto(
    int RootId,
    string Kind,
    long Hwnd,
    string? Title,
    int RootElementId,
    int? OpenedBy);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\RootEnumeratorTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class RootEnumeratorTests
{
    private static RootEnumerator CreateEnumerator(ElementRegistry registry)
    {
        return new RootEnumerator(registry, new PopupOwnerResolver());
    }

    [StaFact]
    public void Enumerate_VisibleWindow_AppearsAsWindowRoot()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);

        var window = new Window
        {
            Title = "Test Window",
            Width = 200,
            Height = 100,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
            Visibility = Visibility.Hidden
        };

        try
        {
            window.Show();
            ListVisualRootsResponse response = enumerator.Enumerate();

            VisualRootDto? match = response.Roots.FirstOrDefault(r => r.Title == "Test Window");
            Assert.NotNull(match);
            Assert.Equal("Window", match!.Kind);
            Assert.Null(match.OpenedBy);
            Assert.NotEqual(0, match.Hwnd);
        }
        finally
        {
            window.Close();
        }
    }

    [StaFact]
    public void Enumerate_OpenPopup_AppearsAsPopupRoot_WithOpenedBy()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);

        var popupContent = new Border
        {
            Width = 50,
            Height = 50,
            Background = System.Windows.Media.Brushes.Yellow
        };
        var popup = new Popup
        {
            Child = popupContent,
            IsOpen = false,
            StaysOpen = true
        };

        var grid = new Grid();
        grid.Children.Add(popup);

        var window = new Window
        {
            Title = "Popup Host",
            Width = 200,
            Height = 100,
            Content = grid,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
            Visibility = Visibility.Hidden
        };

        try
        {
            window.Show();
            popup.IsOpen = true;

            ListVisualRootsResponse response = enumerator.Enumerate();

            VisualRootDto? popupRoot = response.Roots.FirstOrDefault(r => r.Kind == "Popup");
            Assert.NotNull(popupRoot);
            Assert.NotNull(popupRoot!.OpenedBy);

            int expectedOwnerId = registry.GetOrAssign(popup);
            Assert.Equal(expectedOwnerId, popupRoot.OpenedBy);
        }
        finally
        {
            popup.IsOpen = false;
            window.Close();
        }
    }

    [StaFact]
    public void Enumerate_MultipleWindows_AllReturned()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);

        var a = NewHiddenWindow("Alpha");
        var b = NewHiddenWindow("Beta");

        try
        {
            a.Show();
            b.Show();

            ListVisualRootsResponse response = enumerator.Enumerate();

            Assert.Contains(response.Roots, r => r.Title == "Alpha");
            Assert.Contains(response.Roots, r => r.Title == "Beta");
        }
        finally
        {
            a.Close();
            b.Close();
        }
    }

    private static Window NewHiddenWindow(string title) => new()
    {
        Title = title,
        Width = 200,
        Height = 100,
        ShowInTaskbar = false,
        WindowStyle = WindowStyle.None,
        ShowActivated = false,
        Visibility = Visibility.Hidden
    };
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~RootEnumeratorTests
```

- [ ] **Step 4: Implement `PopupOwnerResolver`** — `src\SnoopMCP.Payload\Inspection\PopupOwnerResolver.cs`

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

public sealed class PopupOwnerResolver
{
    public Popup? ResolveOwner(Visual popupRoot, IEnumerable<Window> candidateWindows)
    {
        ArgumentNullException.ThrowIfNull(popupRoot);
        ArgumentNullException.ThrowIfNull(candidateWindows);

        Popup? owner = null;
        foreach (Window window in candidateWindows)
        {
            owner = FindOwnerInWindow(window, popupRoot);
            if (owner is not null)
            {
                break;
            }
        }
        return owner;
    }

    private static Popup? FindOwnerInWindow(Window window, Visual popupRoot)
    {
        var candidates = new List<Popup>();
        CollectPopups(window, candidates);

        Popup? match = null;
        foreach (Popup popup in candidates.Where(p => p.IsOpen && p.Child is not null))
        {
            Visual? childRoot = ResolveChildVisualRoot(popup.Child!);
            bool isMatch = ReferenceEquals(childRoot, popupRoot);
            if (isMatch)
            {
                match = popup;
                break;
            }
        }
        return match;
    }

    private static void CollectPopups(DependencyObject node, List<Popup> sink)
    {
        if (node is Popup p)
        {
            sink.Add(p);
        }
        bool isVisual = node is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                CollectPopups(VisualTreeHelper.GetChild(node, i), sink);
            }
        }
    }

    private static Visual? ResolveChildVisualRoot(UIElement child)
    {
        PresentationSource? source = PresentationSource.FromVisual(child);
        Visual? root = source?.RootVisual;
        return root;
    }
}
```

- [ ] **Step 5: Implement `RootEnumerator`** — `src\SnoopMCP.Payload\Inspection\RootEnumerator.cs`

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using SnoopMCP.Payload;
using SnoopMCP.Protocol.Tools;

public sealed class RootEnumerator
{
    private readonly ElementRegistry mRegistry;
    private readonly PopupOwnerResolver mOwnerResolver;

    public RootEnumerator(ElementRegistry registry, PopupOwnerResolver ownerResolver)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(ownerResolver);
        mRegistry = registry;
        mOwnerResolver = ownerResolver;
    }

    public ListVisualRootsResponse Enumerate()
    {
        IList sources = PresentationSource.CurrentSources;
        var roots = new List<VisualRootDto>(sources.Count);
        var windowRoots = new List<Window>();

        foreach (object? sourceObj in sources)
        {
            PresentationSource source = (PresentationSource) sourceObj!;
            if (source.RootVisual is Window window)
            {
                windowRoots.Add(window);
            }
        }

        int nextRootId = 0;
        foreach (object? sourceObj in sources)
        {
            PresentationSource source = (PresentationSource) sourceObj!;
            Visual? rootVisual = source.RootVisual;
            if (rootVisual is null)
            {
                continue;
            }

            VisualRootDto dto = BuildRoot(source, rootVisual, nextRootId, windowRoots);
            roots.Add(dto);
            nextRootId++;
        }

        return new ListVisualRootsResponse(roots);
    }

    private VisualRootDto BuildRoot(
        PresentationSource source,
        Visual rootVisual,
        int rootId,
        IReadOnlyList<Window> windowRoots)
    {
        string kind = ClassifyKind(rootVisual);
        string? title = (rootVisual as Window)?.Title;
        long hwnd = (source is HwndSource hs) ? hs.Handle.ToInt64() : 0;
        int rootElementId = mRegistry.GetOrAssign(rootVisual);
        int? openedBy = ResolveOpenedBy(kind, rootVisual, windowRoots);

        return new VisualRootDto(
            RootId: rootId,
            Kind: kind,
            Hwnd: hwnd,
            Title: title,
            RootElementId: rootElementId,
            OpenedBy: openedBy);
    }

    private static string ClassifyKind(Visual rootVisual)
    {
        string kind = rootVisual switch
        {
            Window => "Window",
            _ when rootVisual.GetType().Name == "PopupRoot" => "Popup",
            _ => "Other"
        };
        return kind;
    }

    private int? ResolveOpenedBy(string kind, Visual rootVisual, IReadOnlyList<Window> windowRoots)
    {
        int? openedBy = null;
        bool isPopup = kind == "Popup";
        if (isPopup)
        {
            Popup? owner = mOwnerResolver.ResolveOwner(rootVisual, windowRoots);
            if (owner is not null)
            {
                openedBy = mRegistry.GetOrAssign(owner);
            }
        }
        return openedBy;
    }
}
```

- [ ] **Step 6: Tool handler** — `src\SnoopMCP.Payload\Tools\ListVisualRootsToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class ListVisualRootsToolHandler : IToolHandler
{
    private readonly RootEnumerator mEnumerator;
    private readonly DispatcherMarshal mMarshal;

    public ListVisualRootsToolHandler(RootEnumerator enumerator, DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(enumerator);
        ArgumentNullException.ThrowIfNull(marshal);
        mEnumerator = enumerator;
        mMarshal = marshal;
    }

    public string ToolName => "listVisualRoots";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ListVisualRootsResponse response = mMarshal.Invoke(
            () => mEnumerator.Enumerate(),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
```

- [ ] **Step 7: Register in `PayloadEntryPoint`** — modify `src\SnoopMCP.Payload\PayloadEntryPoint.cs`

Add the new wiring inside `Inject`, after the existing registrations:

```csharp
var ownerResolver = new PopupOwnerResolver();
var rootEnumerator = new RootEnumerator(registry, ownerResolver);
toolRegistry.Register(new ListVisualRootsToolHandler(rootEnumerator, marshal));
```

Place the lines next to the existing `var marshal = ...; toolRegistry.Register(...);` block. The relevant imports to add at the top: `using SnoopMCP.Payload.Inspection;`.

- [ ] **Step 8: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~RootEnumeratorTests
```

Expected: 3 tests pass. If the popup-owner test is flaky, the most common cause is the popup not yet being arranged when `Enumerate()` runs; insert `popup.UpdateLayout()` after `popup.IsOpen = true` in the test.

- [ ] **Step 9: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 11: listVisualRoots tool with popup openedBy back-references"
```

---

## Task 12: Tool — getChildren (virtualization-aware)

**Goal:** Enumerate visual or logical children of an element. For `ItemsControl`s using `VirtualizingPanel`, report realized + total counts so the LLM never gets a silent lie ("12 children" when there are really 10,000).

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\GetChildrenDto.cs`
- Create: `src\SnoopMCP.Payload\Inspection\ChildrenEnumerator.cs`
- Create: `src\SnoopMCP.Payload\Tools\GetChildrenToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs`
- Create: `tests\SnoopMCP.Payload.Tests\ChildrenEnumeratorTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\GetChildrenDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record GetChildrenRequest(int Id, string Tree);

public sealed record GetChildrenResponse(
    IReadOnlyList<DescribeElementResponse> Children,
    VirtualizationDto? Virtualization);

public sealed record VirtualizationDto(
    bool IsVirtualizing,
    int RealizedItems,
    int? TotalItems);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\ChildrenEnumeratorTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class ChildrenEnumeratorTests
{
    private static ChildrenEnumerator CreateEnumerator(ElementRegistry registry)
    {
        var emitter = new PathStringEmitter();
        var describer = new ElementDescriber(registry, emitter);
        return new ChildrenEnumerator(describer);
    }

    [StaFact]
    public void Enumerate_VisualChildrenOfPanel_ReturnsButtons()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);
        var stack = new StackPanel();
        stack.Children.Add(new Button { Name = "A" });
        stack.Children.Add(new Button { Name = "B" });

        GetChildrenResponse response = enumerator.Enumerate(stack, "visual");

        Assert.Equal(2, response.Children.Count);
        Assert.Equal("A", response.Children[0].Name);
        Assert.Equal("B", response.Children[1].Name);
        Assert.Null(response.Virtualization);
    }

    [StaFact]
    public void Enumerate_LogicalChildrenOfContentControl_ReturnsContent()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);
        var inner = new TextBlock { Text = "inside" };
        var content = new ContentControl { Content = inner };

        GetChildrenResponse response = enumerator.Enumerate(content, "logical");

        Assert.Single(response.Children);
        Assert.Equal("TextBlock", response.Children[0].Type);
    }

    [StaFact]
    public void Enumerate_NonVirtualizingItemsControl_ReportsCountsWithoutVirtualizing()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);
        var listBox = new ListBox
        {
            ItemsSource = Enumerable.Range(0, 5).Select(i => $"Item {i}").ToArray()
        };
        VirtualizingPanel.SetIsVirtualizing(listBox, false);
        ForceArrange(listBox);

        GetChildrenResponse response = enumerator.Enumerate(listBox, "visual");

        Assert.NotNull(response.Virtualization);
        Assert.False(response.Virtualization!.IsVirtualizing);
        Assert.Equal(5, response.Virtualization.TotalItems);
    }

    [StaFact]
    public void Enumerate_VirtualizingListBox_ReportsVirtualizationMetadata()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);
        var items = Enumerable.Range(0, 1000).Select(i => new { Id = i }).ToArray();
        var listBox = new ListBox
        {
            ItemsSource = items,
            Width = 200,
            Height = 100
        };
        VirtualizingPanel.SetIsVirtualizing(listBox, true);
        VirtualizingPanel.SetVirtualizationMode(listBox, VirtualizationMode.Recycling);
        ForceArrange(listBox);

        GetChildrenResponse response = enumerator.Enumerate(listBox, "visual");

        Assert.NotNull(response.Virtualization);
        Assert.True(response.Virtualization!.IsVirtualizing);
        Assert.Equal(1000, response.Virtualization.TotalItems);
        Assert.True(response.Virtualization.RealizedItems < 1000,
            $"Expected fewer than 1000 realized items, got {response.Virtualization.RealizedItems}.");
    }

    [StaFact]
    public void Enumerate_UnknownTreeKind_Throws()
    {
        var registry = new ElementRegistry();
        var enumerator = CreateEnumerator(registry);
        var grid = new Grid();

        Assert.Throws<ArgumentException>(() => enumerator.Enumerate(grid, "magical"));
    }

    private static void ForceArrange(FrameworkElement element)
    {
        element.Measure(new Size(200, 100));
        element.Arrange(new Rect(0, 0, 200, 100));
        element.UpdateLayout();
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ChildrenEnumeratorTests
```

- [ ] **Step 4: Implement `ChildrenEnumerator`** — `src\SnoopMCP.Payload\Inspection\ChildrenEnumerator.cs`

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SnoopMCP.Protocol.Tools;

public sealed class ChildrenEnumerator
{
    private readonly ElementDescriber mDescriber;

    public ChildrenEnumerator(ElementDescriber describer)
    {
        ArgumentNullException.ThrowIfNull(describer);
        mDescriber = describer;
    }

    public GetChildrenResponse Enumerate(DependencyObject parent, string tree)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(tree);

        IReadOnlyList<DependencyObject> kids = tree switch
        {
            "visual" => CollectVisualChildren(parent),
            "logical" => CollectLogicalChildren(parent),
            _ => throw new ArgumentException($"Tree '{tree}' must be 'visual' or 'logical'.", nameof(tree))
        };

        var described = kids.Select(k => mDescriber.Describe(k)).ToList();
        VirtualizationDto? virtualization = ComputeVirtualization(parent, described.Count);

        return new GetChildrenResponse(described, virtualization);
    }

    private static IReadOnlyList<DependencyObject> CollectVisualChildren(DependencyObject parent)
    {
        var children = new List<DependencyObject>();
        bool isVisual = parent is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                children.Add(VisualTreeHelper.GetChild(parent, i));
            }
        }
        return children;
    }

    private static IReadOnlyList<DependencyObject> CollectLogicalChildren(DependencyObject parent)
    {
        var children = new List<DependencyObject>();
        IEnumerable logical = LogicalTreeHelper.GetChildren(parent);
        foreach (object? child in logical)
        {
            if (child is DependencyObject dep)
            {
                children.Add(dep);
            }
        }
        return children;
    }

    private static VirtualizationDto? ComputeVirtualization(DependencyObject parent, int realizedCount)
    {
        VirtualizationDto? virtualization = null;
        if (parent is ItemsControl ic)
        {
            bool isVirtualizing = VirtualizingPanel.GetIsVirtualizing(ic);
            int? total = ic.Items?.Count;
            virtualization = new VirtualizationDto(
                IsVirtualizing: isVirtualizing,
                RealizedItems: realizedCount,
                TotalItems: total);
        }
        return virtualization;
    }
}
```

- [ ] **Step 5: Tool handler** — `src\SnoopMCP.Payload\Tools\GetChildrenToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class GetChildrenToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly ChildrenEnumerator mEnumerator;
    private readonly DispatcherMarshal mMarshal;

    public GetChildrenToolHandler(
        ElementRegistry registry,
        ChildrenEnumerator enumerator,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(enumerator);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mEnumerator = enumerator;
        mMarshal = marshal;
    }

    public string ToolName => "getChildren";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<GetChildrenRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject element);
        if (!resolved)
        {
            throw new SnoopMcpException(ErrorCode.ElementExpired, $"Element id {request.Id} not alive.");
        }

        GetChildrenResponse response = mMarshal.Invoke(
            () => mEnumerator.Enumerate(element, request.Tree),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — add inside `Inject`, near the other registrations:

```csharp
var childrenEnumerator = new ChildrenEnumerator(describer);
toolRegistry.Register(new GetChildrenToolHandler(registry, childrenEnumerator, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ChildrenEnumeratorTests
```

Expected: 5 tests pass. The virtualizing-listbox test can be sensitive — if `RealizedItems == TotalItems`, the layout did not actually virtualize. Confirm `Width`/`Height` are bounded and that `ForceArrange` ran before enumerating.

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 12: getChildren tool with virtualization metadata (realized + total)"
```

---

## Task 13: Tools — getParent and getTemplatedParent

**Goal:** Climb upward from any element. Two tools share one `ParentNavigator` class because the navigation logic is identical except for which parent is asked for.

`getParent(id, tree)` walks `VisualTreeHelper.GetParent` or `LogicalTreeHelper.GetParent`. `getTemplatedParent(id)` returns `FrameworkElement.TemplatedParent` / `FrameworkContentElement.TemplatedParent`. Both return `null` when there is no parent of that kind.

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\GetParentDto.cs`
- Create: `src\SnoopMCP.Protocol\Tools\GetTemplatedParentDto.cs`
- Create: `src\SnoopMCP.Payload\Inspection\ParentNavigator.cs`
- Create: `src\SnoopMCP.Payload\Tools\GetParentToolHandler.cs`
- Create: `src\SnoopMCP.Payload\Tools\GetTemplatedParentToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs`
- Create: `tests\SnoopMCP.Payload.Tests\ParentNavigatorTests.cs`

- [ ] **Step 1: DTOs**

`src\SnoopMCP.Protocol\Tools\GetParentDto.cs`:

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record GetParentRequest(int Id, string Tree);

public sealed record GetParentResponse(DescribeElementResponse? Parent);
```

`src\SnoopMCP.Protocol\Tools\GetTemplatedParentDto.cs`:

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record GetTemplatedParentRequest(int Id);

public sealed record GetTemplatedParentResponse(DescribeElementResponse? TemplatedParent);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\ParentNavigatorTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class ParentNavigatorTests
{
    private static ParentNavigator CreateNavigator(ElementRegistry registry)
    {
        var emitter = new PathStringEmitter();
        var describer = new ElementDescriber(registry, emitter);
        return new ParentNavigator(describer);
    }

    [StaFact]
    public void GetVisualParent_ChildInPanel_ReturnsPanel()
    {
        var registry = new ElementRegistry();
        var nav = CreateNavigator(registry);
        var stack = new StackPanel();
        var button = new Button { Name = "Save" };
        stack.Children.Add(button);

        GetParentResponse response = nav.GetParent(button, "visual");

        Assert.NotNull(response.Parent);
        Assert.Equal("StackPanel", response.Parent!.Type);
    }

    [StaFact]
    public void GetLogicalParent_ContentControl_ReturnsHost()
    {
        var registry = new ElementRegistry();
        var nav = CreateNavigator(registry);
        var inner = new TextBlock { Text = "x" };
        var host = new ContentControl { Content = inner };

        GetParentResponse response = nav.GetParent(inner, "logical");

        Assert.NotNull(response.Parent);
        Assert.Equal("ContentControl", response.Parent!.Type);
    }

    [StaFact]
    public void GetVisualParent_OfRoot_ReturnsNull()
    {
        var registry = new ElementRegistry();
        var nav = CreateNavigator(registry);
        var orphan = new Button();

        GetParentResponse response = nav.GetParent(orphan, "visual");

        Assert.Null(response.Parent);
    }

    [StaFact]
    public void GetTemplatedParent_OfPlainElement_ReturnsNull()
    {
        var registry = new ElementRegistry();
        var nav = CreateNavigator(registry);
        var button = new Button();

        GetTemplatedParentResponse response = nav.GetTemplatedParent(button);

        Assert.Null(response.TemplatedParent);
    }

    [StaFact]
    public void GetTemplatedParent_OfTemplateChild_ReturnsTemplatedHost()
    {
        var registry = new ElementRegistry();
        var nav = CreateNavigator(registry);

        var button = new Button { Content = "Save" };
        button.ApplyTemplate();

        DependencyObject? insideTemplate = FindFirstTemplateChild(button);
        Assert.NotNull(insideTemplate);

        GetTemplatedParentResponse response = nav.GetTemplatedParent(insideTemplate!);

        Assert.NotNull(response.TemplatedParent);
        Assert.Equal("Button", response.TemplatedParent!.Type);
    }

    [StaFact]
    public void GetParent_UnknownTree_Throws()
    {
        var registry = new ElementRegistry();
        var nav = CreateNavigator(registry);
        var button = new Button();

        Assert.Throws<ArgumentException>(() => nav.GetParent(button, "elven"));
    }

    private static DependencyObject? FindFirstTemplateChild(DependencyObject root)
    {
        DependencyObject? result = null;
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count && result is null; i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            FrameworkElement? fe = child as FrameworkElement;
            bool isFromTemplate = fe?.TemplatedParent is not null;
            result = isFromTemplate ? child : FindFirstTemplateChild(child);
        }
        return result;
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ParentNavigatorTests
```

- [ ] **Step 4: Implement `ParentNavigator`** — `src\SnoopMCP.Payload\Inspection\ParentNavigator.cs`

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.Windows;
using System.Windows.Media;
using SnoopMCP.Protocol.Tools;

public sealed class ParentNavigator
{
    private readonly ElementDescriber mDescriber;

    public ParentNavigator(ElementDescriber describer)
    {
        ArgumentNullException.ThrowIfNull(describer);
        mDescriber = describer;
    }

    public GetParentResponse GetParent(DependencyObject element, string tree)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(tree);

        DependencyObject? parent = tree switch
        {
            "visual" => ResolveVisualParent(element),
            "logical" => LogicalTreeHelper.GetParent(element),
            _ => throw new ArgumentException($"Tree '{tree}' must be 'visual' or 'logical'.", nameof(tree))
        };

        DescribeElementResponse? described = parent is null ? null : mDescriber.Describe(parent);
        return new GetParentResponse(described);
    }

    public GetTemplatedParentResponse GetTemplatedParent(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        DependencyObject? templated = element switch
        {
            FrameworkElement fe => fe.TemplatedParent,
            FrameworkContentElement fce => fce.TemplatedParent,
            _ => null
        };

        DescribeElementResponse? described = templated is null ? null : mDescriber.Describe(templated);
        return new GetTemplatedParentResponse(described);
    }

    private static DependencyObject? ResolveVisualParent(DependencyObject element)
    {
        DependencyObject? parent = null;
        bool isVisual = element is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            parent = VisualTreeHelper.GetParent(element);
        }
        return parent;
    }
}
```

- [ ] **Step 5: Tool handlers**

`src\SnoopMCP.Payload\Tools\GetParentToolHandler.cs`:

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class GetParentToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly ParentNavigator mNav;
    private readonly DispatcherMarshal mMarshal;

    public GetParentToolHandler(ElementRegistry registry, ParentNavigator nav, DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(nav);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mNav = nav;
        mMarshal = marshal;
    }

    public string ToolName => "getParent";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<GetParentRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");
        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject element);
        if (!resolved)
        {
            throw new SnoopMcpException(ErrorCode.ElementExpired, $"Element id {request.Id} not alive.");
        }

        GetParentResponse response = mMarshal.Invoke(
            () => mNav.GetParent(element, request.Tree),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

`src\SnoopMCP.Payload\Tools\GetTemplatedParentToolHandler.cs`:

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class GetTemplatedParentToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly ParentNavigator mNav;
    private readonly DispatcherMarshal mMarshal;

    public GetTemplatedParentToolHandler(ElementRegistry registry, ParentNavigator nav, DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(nav);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mNav = nav;
        mMarshal = marshal;
    }

    public string ToolName => "getTemplatedParent";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<GetTemplatedParentRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");
        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject element);
        if (!resolved)
        {
            throw new SnoopMcpException(ErrorCode.ElementExpired, $"Element id {request.Id} not alive.");
        }

        GetTemplatedParentResponse response = mMarshal.Invoke(
            () => mNav.GetTemplatedParent(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — add inside `Inject`:

```csharp
var parentNavigator = new ParentNavigator(describer);
toolRegistry.Register(new GetParentToolHandler(registry, parentNavigator, marshal));
toolRegistry.Register(new GetTemplatedParentToolHandler(registry, parentNavigator, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ParentNavigatorTests
```

Expected: 6 tests pass.

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 13: getParent + getTemplatedParent (shared ParentNavigator)"
```

---

## Task 14: Tool — findElements (rich predicates)

**Goal:** Locate elements under a given root by AND-combined optional predicates. Search-first navigation is the whole point of the v1 navigation investment (see spec §6) — the LLM finds elements by *shape* and *content* instead of walking a 10,000-node tree. The predicate shape mirrors the spec §7 contract exactly: every field is optional; an element matches only when *every supplied field* matches.

Predicate fields (all optional, AND-combined):

- `type` — case-sensitive substring match on the full type name (`element.GetType().FullName`)
- `name` — exact `x:Name` match
- `automationId` — exact `AutomationProperties.AutomationId` match
- `textContains` — case-insensitive substring match in the visible text extracted by `ElementDescriber`
- `propertyEquals` — `{ property, value }` — the named DP's current value, stringified via `Convert.ToString` with `InvariantCulture`, equals the requested value
- `hasAncestor` — recursive predicate; matches when *any* visual-tree ancestor satisfies the inner predicate
- `hasDescendant` — recursive predicate; matches when *any* visual-tree descendant satisfies the inner predicate
- `inTemplateOf` — recursive predicate; matches when the element's `TemplatedParent` satisfies the inner predicate

Notes baked in deliberately for v1:

- The search is rooted at the supplied `rootId` and walks the **visual tree** (consistent with `getChildren tree:"visual"`). The root element itself is eligible to match.
- `propertyEquals` resolves DPs by reflecting on the `<PropertyName>Property` static field of the element's type or its bases. Attached properties (`Grid.RowProperty` set on a child, etc.) are deferred to Phase 2.
- `textContains` operates on the *capped* (~200 character) visible text that `ElementDescriber` returns; text past the cap is not searchable in v1.
- The result list is unbounded — the LLM scopes by predicate. A `maxResults` cap is a Phase 2 candidate.

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\FindElementsDto.cs`
- Create: `src\SnoopMCP.Payload\Inspection\ElementFinder.cs`
- Create: `src\SnoopMCP.Payload\Tools\FindElementsToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs` (register handler)
- Create: `tests\SnoopMCP.Payload.Tests\ElementFinderTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\FindElementsDto.cs`

`ElementPredicateDto` is a record class (not a positional record) so every field defaults to `null` when omitted from JSON — the AND-combined "all optional" semantics require that. `PropertyEqualsDto` stays positional because both fields are required when the LLM uses it.

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record FindElementsRequest(int RootId, ElementPredicateDto Predicate);

public sealed record FindElementsResponse(IReadOnlyList<DescribeElementResponse> Matches);

public sealed record ElementPredicateDto
{
    public string? Type { get; init; }
    public string? Name { get; init; }
    public string? AutomationId { get; init; }
    public string? TextContains { get; init; }
    public PropertyEqualsDto? PropertyEquals { get; init; }
    public ElementPredicateDto? HasAncestor { get; init; }
    public ElementPredicateDto? HasDescendant { get; init; }
    public ElementPredicateDto? InTemplateOf { get; init; }
}

public sealed record PropertyEqualsDto(string Property, string Value);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\ElementFinderTests.cs`

Thirteen tests covering every predicate field, AND-combination, recursive predicates, and argument validation. Each test builds a small WPF tree on the STA test thread (no real `Window.Show`) and exercises one matching dimension.

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class ElementFinderTests
{
    private static ElementFinder CreateFinder(ElementRegistry registry)
    {
        var emitter = new PathStringEmitter();
        var describer = new ElementDescriber(registry, emitter);
        return new ElementFinder(describer);
    }

    [StaFact]
    public void Find_ByType_MatchesSubstringOfFullTypeName()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        grid.Children.Add(new Button());
        grid.Children.Add(new TextBlock());

        var predicate = new ElementPredicateDto { Type = "Button" };

        FindElementsResponse response = finder.Find(grid, predicate);

        Assert.Single(response.Matches);
        Assert.Equal("Button", response.Matches[0].Type);
    }

    [StaFact]
    public void Find_ByName_ExactMatch()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        grid.Children.Add(new Button { Name = "Save" });
        grid.Children.Add(new Button { Name = "Cancel" });

        var predicate = new ElementPredicateDto { Name = "Cancel" };

        FindElementsResponse response = finder.Find(grid, predicate);

        Assert.Single(response.Matches);
        Assert.Equal("Cancel", response.Matches[0].Name);
    }

    [StaFact]
    public void Find_ByAutomationId_ExactMatch()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        var tagged = new Button();
        System.Windows.Automation.AutomationProperties.SetAutomationId(tagged, "ThemePicker");
        grid.Children.Add(tagged);
        grid.Children.Add(new Button());

        var predicate = new ElementPredicateDto { AutomationId = "ThemePicker" };

        FindElementsResponse response = finder.Find(grid, predicate);

        Assert.Single(response.Matches);
        Assert.Equal("ThemePicker", response.Matches[0].AutomationId);
    }

    [StaFact]
    public void Find_ByTextContains_CaseInsensitive()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = "Hello World" });
        stack.Children.Add(new TextBlock { Text = "Goodbye" });

        var predicate = new ElementPredicateDto { TextContains = "hello" };

        FindElementsResponse response = finder.Find(stack, predicate);

        Assert.Contains(response.Matches, m => m.VisibleText.Contains("Hello"));
        Assert.DoesNotContain(response.Matches, m => m.VisibleText == "Goodbye");
    }

    [StaFact]
    public void Find_ByPropertyEquals_StringifiedComparison()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        grid.Children.Add(new Button { IsEnabled = false });
        grid.Children.Add(new Button { IsEnabled = true });

        var predicate = new ElementPredicateDto
        {
            PropertyEquals = new PropertyEqualsDto("IsEnabled", "False")
        };

        FindElementsResponse response = finder.Find(grid, predicate);

        Assert.Single(response.Matches);
    }

    [StaFact]
    public void Find_ByPropertyEquals_UnknownPropertyMatchesNothing()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        grid.Children.Add(new Button());

        var predicate = new ElementPredicateDto
        {
            PropertyEquals = new PropertyEqualsDto("ThisDoesNotExist", "anything")
        };

        FindElementsResponse response = finder.Find(grid, predicate);

        Assert.Empty(response.Matches);
    }

    [StaFact]
    public void Find_ByMultiplePredicates_AndCombined()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        grid.Children.Add(new Button { Name = "Save" });
        grid.Children.Add(new TextBlock { Name = "Save" });

        var predicate = new ElementPredicateDto
        {
            Type = "Button",
            Name = "Save"
        };

        FindElementsResponse response = finder.Find(grid, predicate);

        Assert.Single(response.Matches);
        Assert.Equal("Button", response.Matches[0].Type);
        Assert.Equal("Save", response.Matches[0].Name);
    }

    [StaFact]
    public void Find_HasAncestor_MatchesWhenAnyAncestorMatches()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var outer = new Grid { Name = "Outer" };
        var inner = new StackPanel();
        var deep = new Button { Name = "Deep" };
        outer.Children.Add(inner);
        inner.Children.Add(deep);

        var predicate = new ElementPredicateDto
        {
            Type = "Button",
            HasAncestor = new ElementPredicateDto { Name = "Outer" }
        };

        FindElementsResponse response = finder.Find(outer, predicate);

        Assert.Single(response.Matches);
        Assert.Equal("Deep", response.Matches[0].Name);
    }

    [StaFact]
    public void Find_HasAncestor_NoMatchWhenAncestorMissing()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var outer = new Grid { Name = "Different" };
        outer.Children.Add(new Button { Name = "B" });

        var predicate = new ElementPredicateDto
        {
            Type = "Button",
            HasAncestor = new ElementPredicateDto { Name = "MissingAncestor" }
        };

        FindElementsResponse response = finder.Find(outer, predicate);

        Assert.Empty(response.Matches);
    }

    [StaFact]
    public void Find_HasDescendant_MatchesWhenAnyDescendantMatches()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var root = new Grid();
        var holder = new StackPanel { Name = "HasButton" };
        holder.Children.Add(new Button { Name = "TheButton" });
        var empty = new StackPanel { Name = "Empty" };
        root.Children.Add(holder);
        root.Children.Add(empty);

        var predicate = new ElementPredicateDto
        {
            Type = "StackPanel",
            HasDescendant = new ElementPredicateDto { Name = "TheButton" }
        };

        FindElementsResponse response = finder.Find(root, predicate);

        Assert.Single(response.Matches);
        Assert.Equal("HasButton", response.Matches[0].Name);
    }

    [StaFact]
    public void Find_InTemplateOf_MatchesElementsInsideMatchingTemplatedHost()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var host = new Button { Name = "Host", Content = "x" };
        host.ApplyTemplate();
        var page = new Grid();
        page.Children.Add(host);

        var predicate = new ElementPredicateDto
        {
            InTemplateOf = new ElementPredicateDto { Name = "Host" }
        };

        FindElementsResponse response = finder.Find(page, predicate);

        Assert.NotEmpty(response.Matches);
        foreach (DescribeElementResponse match in response.Matches)
        {
            Assert.True(
                match.IsInTemplate,
                $"Match {match.Type} (id={match.Id}) was expected to be in template.");
        }
    }

    [StaFact]
    public void Find_EmptyPredicate_ReturnsRootAndDescendants()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();
        grid.Children.Add(new Button());
        grid.Children.Add(new TextBlock());

        FindElementsResponse response = finder.Find(grid, new ElementPredicateDto());

        Assert.True(
            response.Matches.Count >= 3,
            $"Expected root + 2 children = at least 3 matches; got {response.Matches.Count}.");
    }

    [StaFact]
    public void Find_NullRoot_Throws()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);

        Assert.Throws<ArgumentNullException>(() => finder.Find(null!, new ElementPredicateDto()));
    }

    [StaFact]
    public void Find_NullPredicate_Throws()
    {
        var registry = new ElementRegistry();
        var finder = CreateFinder(registry);
        var grid = new Grid();

        Assert.Throws<ArgumentNullException>(() => finder.Find(grid, null!));
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ElementFinderTests
```

Expected: compile errors referencing `ElementFinder`, `FindElementsResponse`, `ElementPredicateDto`, `PropertyEqualsDto`.

- [ ] **Step 4: Implement `ElementFinder`** — `src\SnoopMCP.Payload\Inspection\ElementFinder.cs`

The finder walks the visual tree from a root, evaluating `MatchesPredicate` on each element. Predicate evaluation is recursive — `hasAncestor`, `hasDescendant`, and `inTemplateOf` all call back into `MatchesPredicate` against the inner predicate, so arbitrarily-nested predicates unfold via mutual recursion. Each field is gated on the running `matches` boolean, preserving the AND-combined semantics and the single-return discipline from `CLAUDE.md`.

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using SnoopMCP.Protocol.Tools;

public sealed class ElementFinder
{
    private readonly ElementDescriber mDescriber;

    public ElementFinder(ElementDescriber describer)
    {
        ArgumentNullException.ThrowIfNull(describer);
        mDescriber = describer;
    }

    public FindElementsResponse Find(DependencyObject root, ElementPredicateDto predicate)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(predicate);

        var matches = new List<DescribeElementResponse>();
        Walk(root, predicate, matches);
        return new FindElementsResponse(matches);
    }

    private void Walk(DependencyObject element, ElementPredicateDto predicate, List<DescribeElementResponse> sink)
    {
        bool isVisual = element is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            if (MatchesPredicate(element, predicate))
            {
                sink.Add(mDescriber.Describe(element));
            }
            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                Walk(VisualTreeHelper.GetChild(element, i), predicate, sink);
            }
        }
    }

    private bool MatchesPredicate(DependencyObject element, ElementPredicateDto predicate)
    {
        bool matches = true;

        if (matches && predicate.Type is not null)
        {
            string fullName = element.GetType().FullName ?? string.Empty;
            matches = fullName.Contains(predicate.Type, StringComparison.Ordinal);
        }

        if (matches && predicate.Name is not null)
        {
            string? actualName = (element as FrameworkElement)?.Name;
            matches = string.Equals(actualName, predicate.Name, StringComparison.Ordinal);
        }

        if (matches && predicate.AutomationId is not null)
        {
            string actualId = AutomationProperties.GetAutomationId(element);
            matches = string.Equals(actualId, predicate.AutomationId, StringComparison.Ordinal);
        }

        if (matches && predicate.TextContains is not null)
        {
            DescribeElementResponse description = mDescriber.Describe(element);
            matches = description.VisibleText.Contains(
                predicate.TextContains,
                StringComparison.OrdinalIgnoreCase);
        }

        if (matches && predicate.PropertyEquals is not null)
        {
            matches = MatchesPropertyEquals(element, predicate.PropertyEquals);
        }

        if (matches && predicate.HasAncestor is not null)
        {
            matches = AnyAncestorMatches(element, predicate.HasAncestor);
        }

        if (matches && predicate.HasDescendant is not null)
        {
            matches = AnyDescendantMatches(element, predicate.HasDescendant);
        }

        if (matches && predicate.InTemplateOf is not null)
        {
            matches = TemplatedParentMatches(element, predicate.InTemplateOf);
        }

        return matches;
    }

    private static bool MatchesPropertyEquals(DependencyObject element, PropertyEqualsDto request)
    {
        bool matches = false;
        DependencyProperty? dp = ResolveDependencyProperty(element.GetType(), request.Property);
        if (dp is not null)
        {
            object? value = element.GetValue(dp);
            string stringified = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            matches = string.Equals(stringified, request.Value, StringComparison.Ordinal);
        }
        return matches;
    }

    private static DependencyProperty? ResolveDependencyProperty(Type ownerType, string propertyName)
    {
        DependencyProperty? found = null;
        string fieldName = propertyName + "Property";
        Type? current = ownerType;
        while (current is not null && found is null)
        {
            FieldInfo? field = current.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            bool isDpField = field is not null && typeof(DependencyProperty).IsAssignableFrom(field.FieldType);
            if (isDpField)
            {
                found = field!.GetValue(null) as DependencyProperty;
            }
            current = current.BaseType;
        }
        return found;
    }

    private bool AnyAncestorMatches(DependencyObject element, ElementPredicateDto inner)
    {
        bool found = false;
        DependencyObject? current = VisualTreeHelper.GetParent(element);
        while (current is not null && !found)
        {
            bool matchedHere = MatchesPredicate(current, inner);
            if (matchedHere)
            {
                found = true;
            }
            else
            {
                current = VisualTreeHelper.GetParent(current);
            }
        }
        return found;
    }

    private bool AnyDescendantMatches(DependencyObject element, ElementPredicateDto inner)
    {
        bool found = false;
        bool isVisual = element is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            int count = VisualTreeHelper.GetChildrenCount(element);
            int i = 0;
            while (i < count && !found)
            {
                DependencyObject child = VisualTreeHelper.GetChild(element, i);
                bool childMatches = MatchesPredicate(child, inner) || AnyDescendantMatches(child, inner);
                found = childMatches;
                i++;
            }
        }
        return found;
    }

    private bool TemplatedParentMatches(DependencyObject element, ElementPredicateDto inner)
    {
        DependencyObject? templated = element switch
        {
            FrameworkElement fe => fe.TemplatedParent,
            FrameworkContentElement fce => fce.TemplatedParent,
            _ => null
        };
        bool matches = false;
        if (templated is not null)
        {
            matches = MatchesPredicate(templated, inner);
        }
        return matches;
    }
}
```

- [ ] **Step 5: Tool handler** — `src\SnoopMCP.Payload\Tools\FindElementsToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class FindElementsToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly ElementFinder mFinder;
    private readonly DispatcherMarshal mMarshal;

    public FindElementsToolHandler(
        ElementRegistry registry,
        ElementFinder finder,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(finder);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mFinder = finder;
        mMarshal = marshal;
    }

    public string ToolName => "findElements";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<FindElementsRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.RootId, out DependencyObject root);
        if (!resolved)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Root element id {request.RootId} is not alive.");
        }

        FindElementsResponse response = mMarshal.Invoke(
            () => mFinder.Find(root, request.Predicate),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — modify `src\SnoopMCP.Payload\PayloadEntryPoint.cs`

Add the new wiring inside `Inject`, alongside the other registrations:

```csharp
var elementFinder = new ElementFinder(describer);
toolRegistry.Register(new FindElementsToolHandler(registry, elementFinder, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ElementFinderTests
```

Expected: 13 tests pass. If `Find_InTemplateOf_MatchesElementsInsideMatchingTemplatedHost` reports zero matches, the most likely cause is that `host.ApplyTemplate()` did not generate visual children — verify the default `Button` theme registered in your test environment by checking `host.ApplyTemplate()`'s return value (it returns `true` when content was generated).

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 14: findElements with AND-combined predicates and recursive hasAncestor/hasDescendant/inTemplateOf"
```

---

## Task 15: Tool — hitTest

**Goal:** Given a root and a root-relative `(x, y)` point, return the deepest hittable `Visual` at that point. Wraps `VisualTreeHelper.HitTest(Visual, Point)`; the result is wrapped in a `DescribeElementResponse` or `null` when nothing is hit. This is the "I clicked at this location, what's there?" tool — the LLM uses it to anchor navigation to a coordinate it found another way (a screenshot, a manual probe, a logged Mouse event).

WPF hit-testing rules baked in:
- Coordinates are in the **root's** coordinate space (same convention as `describeElement.bounds`).
- An element is hittable only when its visual bounds contain the point AND it is not transparent for hit-testing purposes. A `Panel` with `Background = null` is *not* hittable; the same panel with `Background = Brushes.Transparent` *is*.
- The result is the deepest visual in the subtree that satisfies the rules above — that may be a template descendant of the control the LLM was thinking about, which is the truthful answer.
- A non-`Visual` root (e.g., a `Run`, `Paragraph`, or any `ContentElement`) is rejected with `ArgumentException`. The handler translates this into `ErrorCode.InvalidArgument`.

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\HitTestDto.cs`
- Create: `src\SnoopMCP.Payload\Inspection\HitTester.cs`
- Create: `src\SnoopMCP.Payload\Tools\HitTestToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs` (register handler)
- Create: `tests\SnoopMCP.Payload.Tests\HitTesterTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\HitTestDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record HitTestRequest(int RootId, double X, double Y);

public sealed record HitTestResponse(DescribeElementResponse? Element);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\HitTesterTests.cs`

Each test arranges its visual tree manually via `Measure`/`Arrange`/`UpdateLayout` so hit testing has valid bounds without needing a real `Window.Show()`. Backgrounds are explicitly set on the elements we expect to be hittable — without them, WPF's default `Background = null` makes a `Panel` invisible to hit testing.

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class HitTesterTests
{
    private static HitTester CreateTester(ElementRegistry registry)
    {
        var emitter = new PathStringEmitter();
        var describer = new ElementDescriber(registry, emitter);
        return new HitTester(describer);
    }

    private static void ForceArrange(FrameworkElement element)
    {
        element.Measure(new Size(200, 200));
        element.Arrange(new Rect(0, 0, 200, 200));
        element.UpdateLayout();
    }

    [StaFact]
    public void HitTest_OnRootWithBackground_ReturnsRoot()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);
        var grid = new Grid
        {
            Width = 100,
            Height = 100,
            Background = Brushes.Red
        };
        ForceArrange(grid);

        HitTestResponse response = tester.HitTest(grid, 50, 50);

        Assert.NotNull(response.Element);
        int gridId = registry.GetOrAssign(grid);
        Assert.Equal(gridId, response.Element!.Id);
    }

    [StaFact]
    public void HitTest_InsideChildBounds_ReturnsChild()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);
        var grid = new Grid { Width = 200, Height = 200 };
        var border = new Border
        {
            Width = 80,
            Height = 60,
            Background = Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10)
        };
        grid.Children.Add(border);
        ForceArrange(grid);

        HitTestResponse response = tester.HitTest(grid, 30, 30);

        Assert.NotNull(response.Element);
        int borderId = registry.GetOrAssign(border);
        Assert.Equal(borderId, response.Element!.Id);
        Assert.Equal("Border", response.Element.Type);
    }

    [StaFact]
    public void HitTest_NestedHittables_ReturnsDeepest()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);
        var outer = new Border
        {
            Width = 200,
            Height = 200,
            Background = Brushes.Red
        };
        var inner = new Border
        {
            Width = 50,
            Height = 50,
            Background = Brushes.Blue,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        outer.Child = inner;
        ForceArrange(outer);

        HitTestResponse response = tester.HitTest(outer, 25, 25);

        Assert.NotNull(response.Element);
        int innerId = registry.GetOrAssign(inner);
        Assert.Equal(innerId, response.Element!.Id);
    }

    [StaFact]
    public void HitTest_OutsideAllHittables_ReturnsNull()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);
        var grid = new Grid { Width = 200, Height = 200 };
        var border = new Border
        {
            Width = 40,
            Height = 40,
            Background = Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        grid.Children.Add(border);
        ForceArrange(grid);

        HitTestResponse response = tester.HitTest(grid, 180, 180);

        Assert.Null(response.Element);
    }

    [StaFact]
    public void HitTest_NullBackgroundPanel_NotHittable()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);
        var grid = new Grid { Width = 200, Height = 200 };
        ForceArrange(grid);

        HitTestResponse response = tester.HitTest(grid, 100, 100);

        Assert.Null(response.Element);
    }

    [StaFact]
    public void HitTest_NullRoot_Throws()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);

        Assert.Throws<ArgumentNullException>(() => tester.HitTest(null!, 0, 0));
    }

    [StaFact]
    public void HitTest_NonVisualRoot_Throws()
    {
        var registry = new ElementRegistry();
        var tester = CreateTester(registry);
        var contentElement = new Run("not a visual");

        Assert.Throws<ArgumentException>(() => tester.HitTest(contentElement, 0, 0));
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~HitTesterTests
```

Expected: compile errors referencing `HitTester`, `HitTestResponse`.

- [ ] **Step 4: Implement `HitTester`** — `src\SnoopMCP.Payload\Inspection\HitTester.cs`

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.Windows;
using System.Windows.Media;
using SnoopMCP.Protocol.Tools;

public sealed class HitTester
{
    private readonly ElementDescriber mDescriber;

    public HitTester(ElementDescriber describer)
    {
        ArgumentNullException.ThrowIfNull(describer);
        mDescriber = describer;
    }

    public HitTestResponse HitTest(DependencyObject root, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (root is not Visual visualRoot)
        {
            throw new ArgumentException("Hit test root must be a Visual.", nameof(root));
        }

        Point point = new(x, y);
        HitTestResult? result = VisualTreeHelper.HitTest(visualRoot, point);
        DescribeElementResponse? described = null;
        if (result?.VisualHit is DependencyObject hit)
        {
            described = mDescriber.Describe(hit);
        }
        return new HitTestResponse(described);
    }
}
```

- [ ] **Step 5: Tool handler** — `src\SnoopMCP.Payload\Tools\HitTestToolHandler.cs`

The handler maps the `ArgumentException` from the inspector (non-visual root) to `ErrorCode.InvalidArgument` so the LLM sees a structured failure instead of an `Unknown` bucket.

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class HitTestToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly HitTester mTester;
    private readonly DispatcherMarshal mMarshal;

    public HitTestToolHandler(
        ElementRegistry registry,
        HitTester tester,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(tester);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mTester = tester;
        mMarshal = marshal;
    }

    public string ToolName => "hitTest";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<HitTestRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.RootId, out DependencyObject root);
        if (!resolved)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Root element id {request.RootId} is not alive.");
        }

        HitTestResponse response;
        try
        {
            response = mMarshal.Invoke(
                () => mTester.HitTest(root, request.X, request.Y),
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            throw new SnoopMcpException(ErrorCode.InvalidArgument, ex.Message, ex);
        }

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — modify `src\SnoopMCP.Payload\PayloadEntryPoint.cs`

Add inside `Inject`, alongside the other registrations:

```csharp
var hitTester = new HitTester(describer);
toolRegistry.Register(new HitTestToolHandler(registry, hitTester, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~HitTesterTests
```

Expected: 7 tests pass. If `HitTest_OutsideAllHittables_ReturnsNull` returns the inner `Border` anyway, the most likely cause is that `ForceArrange` ran with a different size than the test assumes — verify `grid.ActualWidth`/`ActualHeight` after arrange. If `HitTest_NestedHittables_ReturnsDeepest` returns the outer border instead of the inner, the inner border may not have arranged into the expected position; print `inner.TransformToAncestor(outer).TransformBounds(...)` to confirm placement.

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 15: hitTest tool — deepest Visual at a root-relative point"
```

---

## Task 16: Tool — resolvePath

**Goal:** Inverse of `PathStringEmitter`. Given a root and a path string in the canonical `/TypeName[Name='X', AutomationId='Y'][n]/...` grammar (Task 9), walk the visual tree to find the element the path identifies. Returns `null` when no element matches; throws `SnoopMcpException(PathParseError)` when the path itself is malformed.

**The path is symmetric with the emitter:** the first step matches the *root* element (not its first child), so `resolvePath(rootId, describer.Path(element))` round-trips to the same element. If the root's type/attributes don't match the first step, the resolution returns `null` rather than skipping the root.

v1 attribute support is `Name` and `AutomationId` (everything `PathStringEmitter` produces). Unknown attribute names in the path are silently ignored — a known v1 wart that Phase 2 will tighten by surfacing a structured error.

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\ResolvePathDto.cs`
- Create: `src\SnoopMCP.Payload\Inspection\PathResolver.cs`
- Create: `src\SnoopMCP.Payload\Tools\ResolvePathToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs` (register handler)
- Create: `tests\SnoopMCP.Payload.Tests\PathResolverTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\ResolvePathDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record ResolvePathRequest(int RootId, string PathString);

public sealed record ResolvePathResponse(DescribeElementResponse? Element);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\PathResolverTests.cs`

Eleven tests covering single-step, multi-step, attribute matching, index disambiguation, round-trip with `PathStringEmitter`, no-match → `null`, root-type-mismatch → `null`, and error paths.

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class PathResolverTests
{
    private static PathResolver CreateResolver(ElementRegistry registry)
    {
        var emitter = new PathStringEmitter();
        var describer = new ElementDescriber(registry, emitter);
        var parser = new PathStringParser();
        return new PathResolver(describer, parser);
    }

    [StaFact]
    public void Resolve_SingleStep_MatchingRoot_ReturnsRoot()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();

        ResolvePathResponse response = resolver.Resolve(grid, "/Grid");

        Assert.NotNull(response.Element);
        int gridId = registry.GetOrAssign(grid);
        Assert.Equal(gridId, response.Element!.Id);
    }

    [StaFact]
    public void Resolve_RootTypeMismatch_ReturnsNull()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();

        ResolvePathResponse response = resolver.Resolve(grid, "/Window");

        Assert.Null(response.Element);
    }

    [StaFact]
    public void Resolve_PathToChild_ByName_ReturnsChild()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();
        var button = new Button { Name = "SaveBtn" };
        grid.Children.Add(button);

        ResolvePathResponse response = resolver.Resolve(grid, "/Grid/Button[Name='SaveBtn']");

        Assert.NotNull(response.Element);
        int buttonId = registry.GetOrAssign(button);
        Assert.Equal(buttonId, response.Element!.Id);
    }

    [StaFact]
    public void Resolve_PathToChild_ByAutomationId_ReturnsChild()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();
        var tagged = new Button();
        AutomationProperties.SetAutomationId(tagged, "ThemePicker");
        grid.Children.Add(tagged);
        grid.Children.Add(new Button());

        ResolvePathResponse response = resolver.Resolve(grid, "/Grid/Button[AutomationId='ThemePicker']");

        Assert.NotNull(response.Element);
        int taggedId = registry.GetOrAssign(tagged);
        Assert.Equal(taggedId, response.Element!.Id);
    }

    [StaFact]
    public void Resolve_PathWithIndex_PicksNthSameTypeSibling()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var stack = new StackPanel();
        var first = new Button();
        var second = new Button();
        var third = new Button();
        stack.Children.Add(first);
        stack.Children.Add(second);
        stack.Children.Add(third);

        ResolvePathResponse response = resolver.Resolve(stack, "/StackPanel/Button[1]");

        Assert.NotNull(response.Element);
        int secondId = registry.GetOrAssign(second);
        Assert.Equal(secondId, response.Element!.Id);
    }

    [StaFact]
    public void Resolve_DeepPath_TraversesMultipleLevels()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();
        var stack = new StackPanel();
        var border = new Border();
        var button = new Button { Name = "Deep" };
        grid.Children.Add(stack);
        stack.Children.Add(border);
        border.Child = button;

        ResolvePathResponse response = resolver.Resolve(
            grid,
            "/Grid/StackPanel/Border/Button[Name='Deep']");

        Assert.NotNull(response.Element);
        int buttonId = registry.GetOrAssign(button);
        Assert.Equal(buttonId, response.Element!.Id);
    }

    [StaFact]
    public void Resolve_RoundTripWithEmitter_ReturnsOriginal()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var emitter = new PathStringEmitter();
        var grid = new Grid();
        var button = new Button { Name = "Save" };
        grid.Children.Add(button);

        string path = emitter.Emit(button);
        ResolvePathResponse response = resolver.Resolve(grid, path);

        Assert.NotNull(response.Element);
        int buttonId = registry.GetOrAssign(button);
        Assert.Equal(buttonId, response.Element!.Id);
    }

    [StaFact]
    public void Resolve_NoMatchingChild_ReturnsNull()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();
        grid.Children.Add(new Button { Name = "Save" });

        ResolvePathResponse response = resolver.Resolve(grid, "/Grid/Button[Name='DoesNotExist']");

        Assert.Null(response.Element);
    }

    [StaFact]
    public void Resolve_IndexOutOfRange_ReturnsNull()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var stack = new StackPanel();
        stack.Children.Add(new Button());
        stack.Children.Add(new Button());

        ResolvePathResponse response = resolver.Resolve(stack, "/StackPanel/Button[9]");

        Assert.Null(response.Element);
    }

    [StaFact]
    public void Resolve_InvalidPath_ThrowsPathParseError()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(
            () => resolver.Resolve(grid, "no-leading-slash"));
        Assert.Equal(ErrorCode.PathParseError, ex.Code);
    }

    [StaFact]
    public void Resolve_NullRoot_Throws()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);

        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!, "/Grid"));
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~PathResolverTests
```

Expected: compile errors referencing `PathResolver`, `ResolvePathResponse`.

- [ ] **Step 4: Implement `PathResolver`** — `src\SnoopMCP.Payload\Inspection\PathResolver.cs`

The resolver parses the path, verifies the first step against the root, then walks the visual tree step by step. Same-typed siblings are collected per step and disambiguated by the optional `Index` field; an out-of-range index produces `null` rather than throwing, so the LLM gets a normal "no match" response.

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;

public sealed class PathResolver
{
    private readonly ElementDescriber mDescriber;
    private readonly PathStringParser mParser;

    public PathResolver(ElementDescriber describer, PathStringParser parser)
    {
        ArgumentNullException.ThrowIfNull(describer);
        ArgumentNullException.ThrowIfNull(parser);
        mDescriber = describer;
        mParser = parser;
    }

    public ResolvePathResponse Resolve(DependencyObject root, string pathString)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrEmpty(pathString);

        IReadOnlyList<PathStep> steps = mParser.Parse(pathString);
        DependencyObject? current = MatchesStep(root, steps[0]) ? root : null;

        for (int i = 1; i < steps.Count && current is not null; i++)
        {
            current = FindChildMatchingStep(current, steps[i]);
        }

        DescribeElementResponse? described = current is null ? null : mDescriber.Describe(current);
        return new ResolvePathResponse(described);
    }

    private static bool MatchesStep(DependencyObject element, PathStep step)
    {
        bool matches = element.GetType().Name == step.TypeName;

        if (matches && step.Attributes.TryGetValue("Name", out string? expectedName))
        {
            string? actualName = (element as FrameworkElement)?.Name;
            matches = string.Equals(actualName, expectedName, StringComparison.Ordinal);
        }

        if (matches && step.Attributes.TryGetValue("AutomationId", out string? expectedAutoId))
        {
            string actualAutoId = AutomationProperties.GetAutomationId(element);
            matches = string.Equals(actualAutoId, expectedAutoId, StringComparison.Ordinal);
        }

        return matches;
    }

    private static DependencyObject? FindChildMatchingStep(DependencyObject parent, PathStep step)
    {
        DependencyObject? match = null;
        bool isVisual = parent is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            var candidates = CollectMatchingChildren(parent, step);
            int effectiveIndex = step.Index ?? 0;
            bool inRange = effectiveIndex >= 0 && effectiveIndex < candidates.Count;
            if (inRange)
            {
                match = candidates[effectiveIndex];
            }
        }
        return match;
    }

    private static List<DependencyObject> CollectMatchingChildren(DependencyObject parent, PathStep step)
    {
        var candidates = new List<DependencyObject>();
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (MatchesStep(child, step))
            {
                candidates.Add(child);
            }
        }
        return candidates;
    }
}
```

- [ ] **Step 5: Tool handler** — `src\SnoopMCP.Payload\Tools\ResolvePathToolHandler.cs`

`SnoopMcpException(PathParseError)` thrown by `PathStringParser.Parse` propagates through `DispatcherMarshal.Invoke` (which re-throws the inner exception from the dispatcher operation) and back to `PipeServer.DispatchAsync`, where it is already caught and translated to a structured `RpcError` (see Task 6). No special-case handling needed here.

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class ResolvePathToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly PathResolver mResolver;
    private readonly DispatcherMarshal mMarshal;

    public ResolvePathToolHandler(
        ElementRegistry registry,
        PathResolver resolver,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mResolver = resolver;
        mMarshal = marshal;
    }

    public string ToolName => "resolvePath";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<ResolvePathRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.RootId, out DependencyObject root);
        if (!resolved)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Root element id {request.RootId} is not alive.");
        }

        ResolvePathResponse response = mMarshal.Invoke(
            () => mResolver.Resolve(root, request.PathString),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — modify `src\SnoopMCP.Payload\PayloadEntryPoint.cs`

`PathStringParser` is created here for the first time (the existing wiring only constructs `PathStringEmitter`). Add inside `Inject`, alongside the other registrations:

```csharp
var pathParser = new PathStringParser();
var pathResolver = new PathResolver(describer, pathParser);
toolRegistry.Register(new ResolvePathToolHandler(registry, pathResolver, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~PathResolverTests
```

Expected: 11 tests pass. If `Resolve_RoundTripWithEmitter_ReturnsOriginal` returns `null`, the emitter is producing a path the resolver can't parse — print the emitted path string and re-check the emitter's `BuildStep` output for the elements involved.

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 16: resolvePath tool — walk a canonical path string back to a live element"
```

---

## Task 17: Tool — describeDataContext

**Goal:** Report the CLR type shape of an element's `DataContext` so the LLM understands what's bindable from this node before it issues `readDataContextPath` calls. No values — type metadata only: type name, namespace, base-type chain, implemented interfaces, and declared CLR properties (name + type + readable/writable flags).

Decisions baked in:

- "Declared" means the spec wording in §7 — `BindingFlags.DeclaredOnly`. Inherited properties are reachable via `baseTypes` and a follow-up call against the parent type if the LLM wants that detail. Phase 2 may add an `includeInherited` flag.
- Both `FrameworkElement.DataContext` and `FrameworkContentElement.DataContext` are supported (the latter so `Run`, `Paragraph`, and other content elements inside `FlowDocument`s also work).
- For elements that don't carry a `DataContext` at all (plain `DependencyObject`), the response's `DataContext` field is `null` — same shape as the no-DataContext case so callers have one null-check, not two.
- The response wraps the type info in `DescribeDataContextResponse { DataContext: … | null }` instead of returning the bare object. This is consistent with `GetParentResponse`, `HitTestResponse`, and `ResolvePathResponse`, and gives a clean JSON null encoding for "no DataContext."
- This task creates `DataContextInspector` with one method. Task 18 (`readDataContextPath`) extends the same class with a second method — the two tools share a class because they share the "walk into the DataContext" intent.

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\DescribeDataContextDto.cs`
- Create: `src\SnoopMCP.Payload\Inspection\DataContextInspector.cs`
- Create: `src\SnoopMCP.Payload\Tools\DescribeDataContextToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs` (register handler)
- Create: `tests\SnoopMCP.Payload.Tests\DataContextInspectorTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\DescribeDataContextDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record DescribeDataContextRequest(int Id);

public sealed record DescribeDataContextResponse(DataContextInfo? DataContext);

public sealed record DataContextInfo(
    string TypeName,
    string Namespace,
    IReadOnlyList<string> BaseTypes,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<DeclaredPropertyDto> DeclaredProperties);

public sealed record DeclaredPropertyDto(
    string Name,
    string Type,
    bool CanRead,
    bool CanWrite);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\DataContextInspectorTests.cs`

Fourteen tests using private nested test models so the test file is self-contained. Each model exercises one shape — null DataContext, simple POCO, INotifyPropertyChanged, read-only property, inheritance.

```csharp
namespace SnoopMCP.Payload.Tests;

using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class DataContextInspectorTests
{
    [StaFact]
    public void DescribeDataContext_NullDataContext_ReturnsNullInfo()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid();

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.Null(response.DataContext);
    }

    [StaFact]
    public void DescribeDataContext_SimpleClass_ReturnsTypeName()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Equal("TestModel", response.DataContext!.TypeName);
    }

    [StaFact]
    public void DescribeDataContext_ReturnsNamespace()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Equal(typeof(TestModel).Namespace, response.DataContext!.Namespace);
    }

    [StaFact]
    public void DescribeDataContext_BaseTypesIncludeObject()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Contains(typeof(object).FullName, response.DataContext!.BaseTypes);
    }

    [StaFact]
    public void DescribeDataContext_DerivedType_BaseTypesIncludeImmediateAndUltimateBases()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new DerivedModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Contains(typeof(BaseModel).FullName, response.DataContext!.BaseTypes);
        Assert.Contains(typeof(object).FullName, response.DataContext.BaseTypes);
    }

    [StaFact]
    public void DescribeDataContext_InterfacesIncludeINotifyPropertyChanged()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new NotifyingModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Contains(typeof(INotifyPropertyChanged).FullName, response.DataContext!.Interfaces);
    }

    [StaFact]
    public void DescribeDataContext_DeclaredPropertiesEnumerated()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Contains(response.DataContext!.DeclaredProperties, p => p.Name == "Name");
        Assert.Contains(response.DataContext.DeclaredProperties, p => p.Name == "Age");
    }

    [StaFact]
    public void DescribeDataContext_PropertyType_IsFullName()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        DeclaredPropertyDto nameProp = response.DataContext!.DeclaredProperties.First(p => p.Name == "Name");
        Assert.Equal(typeof(string).FullName, nameProp.Type);
    }

    [StaFact]
    public void DescribeDataContext_ReadWriteProperty_BothFlagsTrue()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        DeclaredPropertyDto nameProp = response.DataContext!.DeclaredProperties.First(p => p.Name == "Name");
        Assert.True(nameProp.CanRead);
        Assert.True(nameProp.CanWrite);
    }

    [StaFact]
    public void DescribeDataContext_ReadOnlyProperty_CanWriteIsFalse()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new ReadOnlyModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        DeclaredPropertyDto prop = response.DataContext!.DeclaredProperties.First(p => p.Name == "Id");
        Assert.True(prop.CanRead);
        Assert.False(prop.CanWrite);
    }

    [StaFact]
    public void DescribeDataContext_DeclaredOnly_ExcludesInheritedProperties()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new DerivedModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(grid);

        Assert.NotNull(response.DataContext);
        Assert.Contains(response.DataContext!.DeclaredProperties, p => p.Name == "Child");
        Assert.DoesNotContain(response.DataContext.DeclaredProperties, p => p.Name == "Parent");
    }

    [StaFact]
    public void DescribeDataContext_ContentElement_AlsoSupported()
    {
        var inspector = new DataContextInspector();
        var run = new Run { DataContext = new TestModel() };

        DescribeDataContextResponse response = inspector.DescribeDataContext(run);

        Assert.NotNull(response.DataContext);
        Assert.Equal("TestModel", response.DataContext!.TypeName);
    }

    [StaFact]
    public void DescribeDataContext_PlainDependencyObject_ReturnsNullInfo()
    {
        var inspector = new DataContextInspector();
        var plain = new DependencyObject();

        DescribeDataContextResponse response = inspector.DescribeDataContext(plain);

        Assert.Null(response.DataContext);
    }

    [StaFact]
    public void DescribeDataContext_NullElement_Throws()
    {
        var inspector = new DataContextInspector();

        Assert.Throws<ArgumentNullException>(() => inspector.DescribeDataContext(null!));
    }

    private sealed class TestModel
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    private sealed class ReadOnlyModel
    {
        public int Id { get; } = 42;
    }

    private sealed class NotifyingModel : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private class BaseModel
    {
        public string Parent { get; set; } = string.Empty;
    }

    private sealed class DerivedModel : BaseModel
    {
        public string Child { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~DataContextInspectorTests
```

Expected: compile errors referencing `DataContextInspector`, `DescribeDataContextResponse`, `DataContextInfo`, `DeclaredPropertyDto`.

- [ ] **Step 4: Implement `DataContextInspector`** — `src\SnoopMCP.Payload\Inspection\DataContextInspector.cs`

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.Reflection;
using System.Windows;
using SnoopMCP.Protocol.Tools;

public sealed class DataContextInspector
{
    public DescribeDataContextResponse DescribeDataContext(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        object? dataContext = ResolveDataContext(element);
        DataContextInfo? info = dataContext is null ? null : BuildInfo(dataContext);
        return new DescribeDataContextResponse(info);
    }

    private static object? ResolveDataContext(DependencyObject element)
    {
        object? value = element switch
        {
            FrameworkElement fe => fe.DataContext,
            FrameworkContentElement fce => fce.DataContext,
            _ => null
        };
        return value;
    }

    private static DataContextInfo BuildInfo(object dataContext)
    {
        Type type = dataContext.GetType();
        string typeName = type.Name;
        string ns = type.Namespace ?? string.Empty;
        IReadOnlyList<string> baseTypes = CollectBaseTypes(type);
        IReadOnlyList<string> interfaces = CollectInterfaces(type);
        IReadOnlyList<DeclaredPropertyDto> properties = CollectDeclaredProperties(type);

        return new DataContextInfo(typeName, ns, baseTypes, interfaces, properties);
    }

    private static IReadOnlyList<string> CollectBaseTypes(Type type)
    {
        var names = new List<string>();
        Type? current = type.BaseType;
        while (current is not null)
        {
            names.Add(current.FullName ?? current.Name);
            current = current.BaseType;
        }
        return names;
    }

    private static IReadOnlyList<string> CollectInterfaces(Type type)
    {
        Type[] interfaces = type.GetInterfaces();
        var names = new List<string>(interfaces.Length);
        foreach (Type iface in interfaces)
        {
            names.Add(iface.FullName ?? iface.Name);
        }
        return names;
    }

    private static IReadOnlyList<DeclaredPropertyDto> CollectDeclaredProperties(Type type)
    {
        PropertyInfo[] props = type.GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var dtos = new List<DeclaredPropertyDto>(props.Length);
        foreach (PropertyInfo prop in props)
        {
            string propType = prop.PropertyType.FullName ?? prop.PropertyType.Name;
            dtos.Add(new DeclaredPropertyDto(
                Name: prop.Name,
                Type: propType,
                CanRead: prop.CanRead,
                CanWrite: prop.CanWrite));
        }
        return dtos;
    }
}
```

- [ ] **Step 5: Tool handler** — `src\SnoopMCP.Payload\Tools\DescribeDataContextToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class DescribeDataContextToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly DataContextInspector mInspector;
    private readonly DispatcherMarshal mMarshal;

    public DescribeDataContextToolHandler(
        ElementRegistry registry,
        DataContextInspector inspector,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mInspector = inspector;
        mMarshal = marshal;
    }

    public string ToolName => "describeDataContext";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<DescribeDataContextRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject element);
        if (!resolved)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        DescribeDataContextResponse response = mMarshal.Invoke(
            () => mInspector.DescribeDataContext(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — modify `src\SnoopMCP.Payload\PayloadEntryPoint.cs`

Add inside `Inject`, alongside the other registrations:

```csharp
var dataContextInspector = new DataContextInspector();
toolRegistry.Register(new DescribeDataContextToolHandler(registry, dataContextInspector, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~DataContextInspectorTests
```

Expected: 14 tests pass.

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 17: describeDataContext tool — CLR type shape of an element's DataContext"
```

---

## Task 18: Tool — readDataContextPath

**Goal:** Read a dotted property path off an element's `DataContext`, e.g. `SelectedCustomer.Address.Street`. Returns the value stringified, the runtime type of the value, a `pathReachable` flag, and — when not reachable — the exact segment at which traversal failed.

Decisions:
- Path is dot-separated, no array indexers, no method calls. Phase 2 may add indexers (`Customers[3].Name`) and indexer-by-key (`Lookup["foo"]`).
- Values are stringified via `Convert.ToString(value, CultureInfo.InvariantCulture)` for wire transport. Complex objects produce their `ToString()`; the LLM can use `valueType` to decide whether to drill further.
- Failure modes baked into the same response: null DataContext → `pathReachable=false, failureAt=""`; null intermediate → `failureAt` is the path up to but not including the null; unknown property → `failureAt` includes the offending segment.

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\ReadDataContextPathDto.cs`
- Modify: `src\SnoopMCP.Payload\Inspection\DataContextInspector.cs` (add `ReadPath` method)
- Create: `src\SnoopMCP.Payload\Tools\ReadDataContextPathToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs` (register handler)
- Create: `tests\SnoopMCP.Payload.Tests\ReadDataContextPathTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\ReadDataContextPathDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record ReadDataContextPathRequest(int Id, string Path);

public sealed record ReadDataContextPathResponse(
    string? Value,
    string? ValueType,
    bool PathReachable,
    string? FailureAt);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\ReadDataContextPathTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class ReadDataContextPathTests
{
    [StaFact]
    public void ReadPath_NullDataContext_PathReachableFalse()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid();

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "Anything");

        Assert.False(response.PathReachable);
        Assert.Equal(string.Empty, response.FailureAt);
    }

    [StaFact]
    public void ReadPath_SimpleProperty_ReturnsValue()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model { Name = "Alice" } };

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "Name");

        Assert.True(response.PathReachable);
        Assert.Equal("Alice", response.Value);
        Assert.Equal(typeof(string).FullName, response.ValueType);
    }

    [StaFact]
    public void ReadPath_DottedPath_WalksChain()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid
        {
            DataContext = new Model
            {
                Inner = new Inner { Label = "Deep" }
            }
        };

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "Inner.Label");

        Assert.True(response.PathReachable);
        Assert.Equal("Deep", response.Value);
    }

    [StaFact]
    public void ReadPath_NullIntermediate_FailureAtSegmentBefore()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model { Inner = null } };

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "Inner.Label");

        Assert.False(response.PathReachable);
        Assert.Equal("Inner", response.FailureAt);
    }

    [StaFact]
    public void ReadPath_UnknownProperty_FailureAtIncludesSegment()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model() };

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "DoesNotExist");

        Assert.False(response.PathReachable);
        Assert.Equal("DoesNotExist", response.FailureAt);
    }

    [StaFact]
    public void ReadPath_IntegerValue_StringifiedInvariant()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model { Age = 42 } };

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "Age");

        Assert.True(response.PathReachable);
        Assert.Equal("42", response.Value);
        Assert.Equal(typeof(int).FullName, response.ValueType);
    }

    [StaFact]
    public void ReadPath_NullLeafValue_PathReachableTrue_ValueNull()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model { Name = null! } };

        ReadDataContextPathResponse response = inspector.ReadPath(grid, "Name");

        Assert.True(response.PathReachable);
        Assert.Null(response.Value);
        Assert.Null(response.ValueType);
    }

    [StaFact]
    public void ReadPath_EmptyPath_Throws()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model() };

        Assert.Throws<ArgumentException>(() => inspector.ReadPath(grid, ""));
    }

    [StaFact]
    public void ReadPath_NullPath_Throws()
    {
        var inspector = new DataContextInspector();
        var grid = new Grid { DataContext = new Model() };

        Assert.Throws<ArgumentNullException>(() => inspector.ReadPath(grid, null!));
    }

    [StaFact]
    public void ReadPath_NullElement_Throws()
    {
        var inspector = new DataContextInspector();
        Assert.Throws<ArgumentNullException>(() => inspector.ReadPath(null!, "X"));
    }

    private sealed class Model
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public Inner? Inner { get; set; }
    }

    private sealed class Inner
    {
        public string Label { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ReadDataContextPathTests
```

Expected: compile errors referencing `DataContextInspector.ReadPath`.

- [ ] **Step 4: Extend `DataContextInspector`** — modify `src\SnoopMCP.Payload\Inspection\DataContextInspector.cs`

`ResolveDataContext` from Task 17 must now be reused; change it from `private static` to `private` instance-visibility-preserving (still static is fine because it doesn't touch instance state). Add the new method `ReadPath`. Add the required `using System.Globalization;` and `using System.Reflection;` at the top of the file (the latter was already there from Task 17).

Add this method to the existing class:

```csharp
public ReadDataContextPathResponse ReadPath(DependencyObject element, string path)
{
    ArgumentNullException.ThrowIfNull(element);
    ArgumentException.ThrowIfNullOrEmpty(path);

    object? root = ResolveDataContext(element);
    ReadDataContextPathResponse response;
    if (root is null)
    {
        response = new ReadDataContextPathResponse(null, null, false, string.Empty);
    }
    else
    {
        response = WalkPath(root, path);
    }
    return response;
}

private static ReadDataContextPathResponse WalkPath(object root, string path)
{
    string[] segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
    object? current = root;
    string traversed = string.Empty;
    bool failed = false;
    string? failedAt = null;

    for (int i = 0; i < segments.Length && !failed; i++)
    {
        string segment = segments[i];
        string nextTraversed = traversed.Length == 0 ? segment : $"{traversed}.{segment}";
        if (current is null)
        {
            failed = true;
            failedAt = traversed;
        }
        else
        {
            PropertyInfo? prop = current.GetType().GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance);
            bool isReadable = prop is not null && prop.CanRead;
            if (!isReadable)
            {
                failed = true;
                failedAt = nextTraversed;
            }
            else
            {
                current = prop!.GetValue(current);
                traversed = nextTraversed;
            }
        }
    }

    ReadDataContextPathResponse result;
    if (failed)
    {
        result = new ReadDataContextPathResponse(null, null, false, failedAt);
    }
    else
    {
        string? value = current is null ? null : Convert.ToString(current, CultureInfo.InvariantCulture);
        string? valueType = current?.GetType().FullName;
        result = new ReadDataContextPathResponse(value, valueType, true, null);
    }
    return result;
}
```

- [ ] **Step 5: Tool handler** — `src\SnoopMCP.Payload\Tools\ReadDataContextPathToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class ReadDataContextPathToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly DataContextInspector mInspector;
    private readonly DispatcherMarshal mMarshal;

    public ReadDataContextPathToolHandler(
        ElementRegistry registry,
        DataContextInspector inspector,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mInspector = inspector;
        mMarshal = marshal;
    }

    public string ToolName => "readDataContextPath";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<ReadDataContextPathRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject element);
        if (!resolved)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        ReadDataContextPathResponse response = mMarshal.Invoke(
            () => mInspector.ReadPath(element, request.Path),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — modify `src\SnoopMCP.Payload\PayloadEntryPoint.cs`

Add alongside the existing `dataContextInspector` registration:

```csharp
toolRegistry.Register(new ReadDataContextPathToolHandler(registry, dataContextInspector, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ReadDataContextPathTests
```

Expected: 10 tests pass.

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 18: readDataContextPath tool — walk a dotted path off DataContext with failure-locating diagnostics"
```

---

## Task 19: Tool — listDependencyProperties

**Goal:** Enumerate every dependency property reachable on an element: `(name, ownerType, valueType, isAttached)`. The LLM uses this to discover what to call `getDependencyProperty` on.

Strategy:
- **Type-walk DPs:** walk the element's type chain to `DependencyObject`, collecting every `public static` field of type `DependencyProperty` (or `DependencyPropertyKey`). Mark `isAttached = false`. Deduplicate by `(OwnerType, Name)`.
- **Locally-set attached DPs:** iterate the element's `GetLocalValueEnumerator()`. Any DP whose `OwnerType` is *not* assignable from the element's runtime type is an attached property currently in effect — add with `isAttached = true`.

Skipping unset attached properties is a v1 wart (we can't enumerate "every attached property the world has ever defined"). Phase 2 may add a curated allow-list for common attached property owner types (`Grid`, `Canvas`, `DockPanel`, `ScrollViewer`).

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\ListDependencyPropertiesDto.cs`
- Create: `src\SnoopMCP.Payload\Inspection\DependencyPropertyInspector.cs`
- Create: `src\SnoopMCP.Payload\Tools\ListDependencyPropertiesToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs` (register handler)
- Create: `tests\SnoopMCP.Payload.Tests\ListDependencyPropertiesTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\ListDependencyPropertiesDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record ListDependencyPropertiesRequest(int Id);

public sealed record ListDependencyPropertiesResponse(
    IReadOnlyList<DependencyPropertyDto> Properties);

public sealed record DependencyPropertyDto(
    string Name,
    string OwnerType,
    string ValueType,
    bool IsAttached);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\ListDependencyPropertiesTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class ListDependencyPropertiesTests
{
    [StaFact]
    public void List_Button_IncludesInheritedDpsLikeIsEnabled()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        ListDependencyPropertiesResponse response = inspector.ListProperties(button);

        Assert.Contains(response.Properties, p => p.Name == "IsEnabled");
    }

    [StaFact]
    public void List_Button_IncludesOwnDpLikeIsCancel()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        ListDependencyPropertiesResponse response = inspector.ListProperties(button);

        Assert.Contains(response.Properties, p => p.Name == "IsCancel" && p.OwnerType.EndsWith(".Button"));
    }

    [StaFact]
    public void List_HasNoDuplicateNameOwnerPairs()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        ListDependencyPropertiesResponse response = inspector.ListProperties(button);

        var seen = new HashSet<string>();
        foreach (DependencyPropertyDto dto in response.Properties)
        {
            string key = $"{dto.OwnerType}.{dto.Name}";
            Assert.True(seen.Add(key), $"Duplicate DP {key}");
        }
    }

    [StaFact]
    public void List_LocallySetAttachedDp_MarkedIsAttachedTrue()
    {
        var inspector = new DependencyPropertyInspector();
        var grid = new Grid();
        var button = new Button();
        Grid.SetRow(button, 2);
        grid.Children.Add(button);

        ListDependencyPropertiesResponse response = inspector.ListProperties(button);

        DependencyPropertyDto? rowEntry = response.Properties
            .FirstOrDefault(p => p.Name == "Row" && p.OwnerType.EndsWith(".Grid"));
        Assert.NotNull(rowEntry);
        Assert.True(rowEntry!.IsAttached);
    }

    [StaFact]
    public void List_OwnerType_FullName_NotShortName()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        ListDependencyPropertiesResponse response = inspector.ListProperties(button);

        DependencyPropertyDto? isEnabled = response.Properties.FirstOrDefault(p => p.Name == "IsEnabled");
        Assert.NotNull(isEnabled);
        Assert.Contains('.', isEnabled!.OwnerType);
    }

    [StaFact]
    public void List_NullElement_Throws()
    {
        var inspector = new DependencyPropertyInspector();
        Assert.Throws<ArgumentNullException>(() => inspector.ListProperties(null!));
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ListDependencyPropertiesTests
```

- [ ] **Step 4: Implement `DependencyPropertyInspector`** — `src\SnoopMCP.Payload\Inspection\DependencyPropertyInspector.cs`

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.Reflection;
using System.Windows;
using SnoopMCP.Protocol.Tools;

public sealed class DependencyPropertyInspector
{
    public ListDependencyPropertiesResponse ListProperties(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var dtos = new List<DependencyPropertyDto>();

        CollectTypeWalkDps(element.GetType(), seen, dtos);
        CollectLocalAttachedDps(element, seen, dtos);

        return new ListDependencyPropertiesResponse(dtos);
    }

    private static void CollectTypeWalkDps(Type startType, HashSet<string> seen, List<DependencyPropertyDto> sink)
    {
        Type? current = startType;
        while (current is not null && typeof(DependencyObject).IsAssignableFrom(current))
        {
            FieldInfo[] fields = current.GetFields(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (FieldInfo field in fields.Where(f => typeof(DependencyProperty).IsAssignableFrom(f.FieldType)))
            {
                DependencyProperty? dp = field.GetValue(null) as DependencyProperty;
                if (dp is not null)
                {
                    AddDto(dp, isAttached: false, seen, sink);
                }
            }
            current = current.BaseType;
        }
    }

    private static void CollectLocalAttachedDps(
        DependencyObject element,
        HashSet<string> seen,
        List<DependencyPropertyDto> sink)
    {
        Type elementType = element.GetType();
        LocalValueEnumerator enumerator = element.GetLocalValueEnumerator();
        while (enumerator.MoveNext())
        {
            DependencyProperty dp = enumerator.Current.Property;
            bool isOnHierarchy = dp.OwnerType.IsAssignableFrom(elementType);
            if (!isOnHierarchy)
            {
                AddDto(dp, isAttached: true, seen, sink);
            }
        }
    }

    private static void AddDto(
        DependencyProperty dp,
        bool isAttached,
        HashSet<string> seen,
        List<DependencyPropertyDto> sink)
    {
        string ownerType = dp.OwnerType.FullName ?? dp.OwnerType.Name;
        string key = $"{ownerType}.{dp.Name}";
        bool added = seen.Add(key);
        if (added)
        {
            sink.Add(new DependencyPropertyDto(
                Name: dp.Name,
                OwnerType: ownerType,
                ValueType: dp.PropertyType.FullName ?? dp.PropertyType.Name,
                IsAttached: isAttached));
        }
    }
}
```

- [ ] **Step 5: Tool handler** — `src\SnoopMCP.Payload\Tools\ListDependencyPropertiesToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class ListDependencyPropertiesToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly DependencyPropertyInspector mInspector;
    private readonly DispatcherMarshal mMarshal;

    public ListDependencyPropertiesToolHandler(
        ElementRegistry registry,
        DependencyPropertyInspector inspector,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mInspector = inspector;
        mMarshal = marshal;
    }

    public string ToolName => "listDependencyProperties";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<ListDependencyPropertiesRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject element);
        if (!resolved)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        ListDependencyPropertiesResponse response = mMarshal.Invoke(
            () => mInspector.ListProperties(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — add alongside the others:

```csharp
var dpInspector = new DependencyPropertyInspector();
toolRegistry.Register(new ListDependencyPropertiesToolHandler(registry, dpInspector, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ListDependencyPropertiesTests
```

Expected: 6 tests pass.

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 19: listDependencyProperties tool — type-walk DPs plus locally-set attached"
```

---

## Task 20: Tool — getDependencyProperty (value + precedence trace)

**Goal:** The single most important diagnostic tool in v1. Returns the current effective DP value AND a precedence trace explaining which source won and what the losing values were.

WPF's value precedence (highest to lowest, simplified): Local, Animated, Trigger-based, StyleSetter, TemplateChild, Inherited, Default. The winning source is reported by `DependencyPropertyHelper.GetValueSource(d, dp).BaseValueSource` (enum `BaseValueSource`). The losing entries are reconstructed by inspecting the surfaces where each could have come from:

- **Default** — always available via `dp.GetMetadata(element).DefaultValue`.
- **Local** — `element.ReadLocalValue(dp)` returns `DependencyProperty.UnsetValue` if not set; otherwise that's the local.
- **StyleSetter** — walk `fe.Style` and its `BasedOn` chain, find any `Setter` with `Property == dp`. Each level in the chain is a candidate (the chain itself reflects style override order).
- **Inherited** — if `dp.GetMetadata(element).Inherits` and the parent's effective value differs from the default, report the parent value.

Sources we can't trace cheaply in v1 (animation, trigger setters, template parent) are not enumerated — the *winning source name* still tells the LLM which family won, even if we don't enumerate the losers from that family. Document.

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\GetDependencyPropertyDto.cs`
- Modify: `src\SnoopMCP.Payload\Inspection\DependencyPropertyInspector.cs` (add `GetProperty` method)
- Create: `src\SnoopMCP.Payload\Tools\GetDependencyPropertyToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs`
- Create: `tests\SnoopMCP.Payload.Tests\GetDependencyPropertyTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\GetDependencyPropertyDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record GetDependencyPropertyRequest(int Id, string PropertyName);

public sealed record GetDependencyPropertyResponse(
    string Name,
    string? CurrentValue,
    string? CurrentValueType,
    IReadOnlyList<PrecedenceEntryDto> Precedence,
    string WinningSource);

public sealed record PrecedenceEntryDto(
    string Source,
    string? Value,
    string? ValueType,
    string SourceDescription);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\GetDependencyPropertyTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class GetDependencyPropertyTests
{
    [StaFact]
    public void Get_LocalValue_CurrentValueAndWinningSourceLocal()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button { Width = 123 };

        GetDependencyPropertyResponse response = inspector.GetProperty(button, "Width");

        Assert.Equal("Width", response.Name);
        Assert.Equal("123", response.CurrentValue);
        Assert.Equal("Local", response.WinningSource);
    }

    [StaFact]
    public void Get_NotSet_CurrentValueIsDefault_WinningSourceDefault()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        GetDependencyPropertyResponse response = inspector.GetProperty(button, "IsCancel");

        Assert.Equal("False", response.CurrentValue);
        Assert.Equal("Default", response.WinningSource);
    }

    [StaFact]
    public void Get_HasDefaultEntryInPrecedence()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        GetDependencyPropertyResponse response = inspector.GetProperty(button, "IsCancel");

        Assert.Contains(response.Precedence, e => e.Source == "Default");
    }

    [StaFact]
    public void Get_WithStyleSetter_StyleSetterAppearsInPrecedence()
    {
        var inspector = new DependencyPropertyInspector();
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Crimson));
        var button = new Button { Style = style };

        GetDependencyPropertyResponse response = inspector.GetProperty(button, "Background");

        Assert.Contains(response.Precedence, e => e.Source == "StyleSetter");
    }

    [StaFact]
    public void Get_LocalOverridesStyle_WinnerIsLocal()
    {
        var inspector = new DependencyPropertyInspector();
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Crimson));
        var button = new Button
        {
            Style = style,
            Background = Brushes.Lime
        };

        GetDependencyPropertyResponse response = inspector.GetProperty(button, "Background");

        Assert.Equal("Local", response.WinningSource);
        Assert.Contains(response.Precedence, e => e.Source == "Local");
        Assert.Contains(response.Precedence, e => e.Source == "StyleSetter");
    }

    [StaFact]
    public void Get_BasedOnChain_RecordsEachLevel()
    {
        var inspector = new DependencyPropertyInspector();
        var baseStyle = new Style(typeof(Button));
        baseStyle.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Gray));
        var derivedStyle = new Style(typeof(Button)) { BasedOn = baseStyle };
        derivedStyle.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Blue));
        var button = new Button { Style = derivedStyle };

        GetDependencyPropertyResponse response = inspector.GetProperty(button, "Background");

        int styleSetterCount = response.Precedence.Count(e => e.Source == "StyleSetter");
        Assert.Equal(2, styleSetterCount);
    }

    [StaFact]
    public void Get_UnknownProperty_ThrowsInvalidArgument()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(
            () => inspector.GetProperty(button, "NoSuchProperty"));
        Assert.Equal(ErrorCode.InvalidArgument, ex.Code);
    }

    [StaFact]
    public void Get_NullElement_Throws()
    {
        var inspector = new DependencyPropertyInspector();
        Assert.Throws<ArgumentNullException>(() => inspector.GetProperty(null!, "X"));
    }

    [StaFact]
    public void Get_EmptyName_Throws()
    {
        var inspector = new DependencyPropertyInspector();
        var button = new Button();
        Assert.Throws<ArgumentException>(() => inspector.GetProperty(button, ""));
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~GetDependencyPropertyTests
```

- [ ] **Step 4: Extend `DependencyPropertyInspector`** — modify `src\SnoopMCP.Payload\Inspection\DependencyPropertyInspector.cs`

Add the following `using` directives and the `GetProperty` method to the existing class:

```csharp
using System.Globalization;
using SnoopMCP.Protocol.Errors;
```

```csharp
public GetDependencyPropertyResponse GetProperty(DependencyObject element, string propertyName)
{
    ArgumentNullException.ThrowIfNull(element);
    ArgumentException.ThrowIfNullOrEmpty(propertyName);

    DependencyProperty dp = ResolveDp(element.GetType(), propertyName)
        ?? throw new SnoopMcpException(
            ErrorCode.InvalidArgument,
            $"Dependency property '{propertyName}' not found on {element.GetType().FullName}.");

    object? currentValue = element.GetValue(dp);
    string winningSource = System.Windows.DependencyPropertyHelper
        .GetValueSource(element, dp)
        .BaseValueSource
        .ToString();

    var trace = new List<PrecedenceEntryDto>();
    AppendLocalEntry(element, dp, trace);
    AppendStyleSetterEntries(element, dp, trace);
    AppendInheritedEntry(element, dp, trace);
    AppendDefaultEntry(element, dp, trace);

    return new GetDependencyPropertyResponse(
        Name: dp.Name,
        CurrentValue: Stringify(currentValue),
        CurrentValueType: currentValue?.GetType().FullName,
        Precedence: trace,
        WinningSource: winningSource);
}

private static DependencyProperty? ResolveDp(Type ownerType, string propertyName)
{
    DependencyProperty? found = null;
    string fieldName = propertyName + "Property";
    Type? current = ownerType;
    while (current is not null && found is null)
    {
        FieldInfo? field = current.GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        bool isDpField = field is not null && typeof(DependencyProperty).IsAssignableFrom(field.FieldType);
        if (isDpField)
        {
            found = field!.GetValue(null) as DependencyProperty;
        }
        current = current.BaseType;
    }
    return found;
}

private static void AppendLocalEntry(DependencyObject element, DependencyProperty dp, List<PrecedenceEntryDto> trace)
{
    object localValue = element.ReadLocalValue(dp);
    bool hasLocal = localValue != DependencyProperty.UnsetValue;
    if (hasLocal)
    {
        trace.Add(new PrecedenceEntryDto(
            Source: "Local",
            Value: Stringify(localValue),
            ValueType: localValue?.GetType().FullName,
            SourceDescription: "Local value on the element"));
    }
}

private static void AppendStyleSetterEntries(
    DependencyObject element,
    DependencyProperty dp,
    List<PrecedenceEntryDto> trace)
{
    if (element is not FrameworkElement fe || fe.Style is null)
    {
        return;
    }
    Style? current = fe.Style;
    int depth = 0;
    while (current is not null)
    {
        foreach (SetterBase sb in current.Setters)
        {
            if (sb is Setter setter && setter.Property == dp)
            {
                trace.Add(new PrecedenceEntryDto(
                    Source: "StyleSetter",
                    Value: Stringify(setter.Value),
                    ValueType: setter.Value?.GetType().FullName,
                    SourceDescription: $"Style {DescribeStyle(current)} (chain depth {depth})"));
            }
        }
        current = current.BasedOn;
        depth++;
    }
}

private static void AppendInheritedEntry(
    DependencyObject element,
    DependencyProperty dp,
    List<PrecedenceEntryDto> trace)
{
    PropertyMetadata metadata = dp.GetMetadata(element);
    bool inherits = metadata is FrameworkPropertyMetadata fpm && fpm.Inherits;
    if (inherits)
    {
        DependencyObject? parent = System.Windows.Media.VisualTreeHelper.GetParent(element);
        if (parent is not null)
        {
            object? parentValue = parent.GetValue(dp);
            bool differsFromDefault = !Equals(parentValue, metadata.DefaultValue);
            if (differsFromDefault)
            {
                trace.Add(new PrecedenceEntryDto(
                    Source: "Inherited",
                    Value: Stringify(parentValue),
                    ValueType: parentValue?.GetType().FullName,
                    SourceDescription: $"Inherited from parent {parent.GetType().Name}"));
            }
        }
    }
}

private static void AppendDefaultEntry(
    DependencyObject element,
    DependencyProperty dp,
    List<PrecedenceEntryDto> trace)
{
    PropertyMetadata metadata = dp.GetMetadata(element);
    object? defaultValue = metadata.DefaultValue;
    trace.Add(new PrecedenceEntryDto(
        Source: "Default",
        Value: Stringify(defaultValue),
        ValueType: defaultValue?.GetType().FullName,
        SourceDescription: "Default value from type metadata"));
}

private static string DescribeStyle(Style style)
{
    string target = style.TargetType?.Name ?? "?";
    return $"TargetType={target}";
}

private static string? Stringify(object? value)
{
    string? result;
    if (value is null)
    {
        result = null;
    }
    else if (value == DependencyProperty.UnsetValue)
    {
        result = "{UnsetValue}";
    }
    else
    {
        result = Convert.ToString(value, CultureInfo.InvariantCulture);
    }
    return result;
}
```

- [ ] **Step 5: Tool handler** — `src\SnoopMCP.Payload\Tools\GetDependencyPropertyToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class GetDependencyPropertyToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly DependencyPropertyInspector mInspector;
    private readonly DispatcherMarshal mMarshal;

    public GetDependencyPropertyToolHandler(
        ElementRegistry registry,
        DependencyPropertyInspector inspector,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mInspector = inspector;
        mMarshal = marshal;
    }

    public string ToolName => "getDependencyProperty";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<GetDependencyPropertyRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject element);
        if (!resolved)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        GetDependencyPropertyResponse response = mMarshal.Invoke(
            () => mInspector.GetProperty(element, request.PropertyName),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — add alongside the others:

```csharp
toolRegistry.Register(new GetDependencyPropertyToolHandler(registry, dpInspector, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~GetDependencyPropertyTests
```

Expected: 9 tests pass.

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 20: getDependencyProperty tool — current value plus best-effort precedence trace"
```

---

## Task 21: Tool — resolveStyle

**Goal:** For an element, report the applied `Style`, the full `BasedOn` chain, every setter, and a summary of triggers. The LLM uses this to understand why a particular property has the value it has when the answer is "the style won."

v1 limitations baked in:
- `appliedStyleKey` is reported as the `TargetType.Name` (the implicit key) when the style is implicit, or `null` when we can't determine it. Recovering an explicit `x:Key` at runtime requires resource-dictionary archaeology that is out of scope for v1.
- `appliedStyleSource` is reported as `"Implicit"` (matched by `TargetType`) or `"Explicit"` (set via `Style="..."` — detected by reading the local value of `StyleProperty`).
- Trigger condition is summarized as `"<PropertyName>=<Value>"` for `Trigger`, `"DataTrigger"` for `DataTrigger`, `"MultiTrigger"` for `MultiTrigger`, etc. Full condition introspection is Phase 2.

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\ResolveStyleDto.cs`
- Create: `src\SnoopMCP.Payload\Inspection\StyleResolver.cs`
- Create: `src\SnoopMCP.Payload\Tools\ResolveStyleToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs`
- Create: `tests\SnoopMCP.Payload.Tests\StyleResolverTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\ResolveStyleDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record ResolveStyleRequest(int Id);

public sealed record ResolveStyleResponse(
    string? AppliedStyleKey,
    string? AppliedStyleSource,
    IReadOnlyList<BasedOnEntryDto> BasedOnChain,
    IReadOnlyList<StyleSetterDto> Setters,
    IReadOnlyList<TriggerSummaryDto> Triggers);

public sealed record BasedOnEntryDto(string TargetType, int Depth);

public sealed record StyleSetterDto(string Property, string? Value);

public sealed record TriggerSummaryDto(
    string Kind,
    string Condition,
    IReadOnlyList<StyleSetterDto> Setters);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\StyleResolverTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class StyleResolverTests
{
    [StaFact]
    public void Resolve_NoStyle_BasedOnAndSettersEmpty()
    {
        var resolver = new StyleResolver();
        var button = new Button();

        ResolveStyleResponse response = resolver.Resolve(button);

        Assert.Empty(response.BasedOnChain);
        Assert.Empty(response.Setters);
        Assert.Empty(response.Triggers);
    }

    [StaFact]
    public void Resolve_WithSingleSetter_SetterReported()
    {
        var resolver = new StyleResolver();
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Crimson));
        var button = new Button { Style = style };

        ResolveStyleResponse response = resolver.Resolve(button);

        Assert.Contains(response.Setters, s => s.Property == "Background");
    }

    [StaFact]
    public void Resolve_WithBasedOnChain_ChainReported()
    {
        var resolver = new StyleResolver();
        var baseStyle = new Style(typeof(Button));
        var middle = new Style(typeof(Button)) { BasedOn = baseStyle };
        var top = new Style(typeof(Button)) { BasedOn = middle };
        var button = new Button { Style = top };

        ResolveStyleResponse response = resolver.Resolve(button);

        Assert.Equal(3, response.BasedOnChain.Count);
        Assert.Equal(0, response.BasedOnChain[0].Depth);
        Assert.Equal(2, response.BasedOnChain[2].Depth);
    }

    [StaFact]
    public void Resolve_ExplicitStyle_SourceIsExplicit()
    {
        var resolver = new StyleResolver();
        var style = new Style(typeof(Button));
        var button = new Button { Style = style };

        ResolveStyleResponse response = resolver.Resolve(button);

        Assert.Equal("Explicit", response.AppliedStyleSource);
    }

    [StaFact]
    public void Resolve_WithTrigger_TriggerReported()
    {
        var resolver = new StyleResolver();
        var style = new Style(typeof(Button));
        var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Blue));
        style.Triggers.Add(trigger);
        var button = new Button { Style = style };

        ResolveStyleResponse response = resolver.Resolve(button);

        Assert.Contains(response.Triggers, t => t.Kind == "Trigger");
        TriggerSummaryDto summary = response.Triggers.First(t => t.Kind == "Trigger");
        Assert.Contains("IsMouseOver", summary.Condition);
        Assert.Contains(summary.Setters, s => s.Property == "Background");
    }

    [StaFact]
    public void Resolve_NullElement_Throws()
    {
        var resolver = new StyleResolver();
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~StyleResolverTests
```

- [ ] **Step 4: Implement `StyleResolver`** — `src\SnoopMCP.Payload\Inspection\StyleResolver.cs`

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Protocol.Tools;

public sealed class StyleResolver
{
    public ResolveStyleResponse Resolve(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        Style? style = (element as FrameworkElement)?.Style;
        ResolveStyleResponse response;
        if (style is null)
        {
            response = new ResolveStyleResponse(
                AppliedStyleKey: null,
                AppliedStyleSource: null,
                BasedOnChain: Array.Empty<BasedOnEntryDto>(),
                Setters: Array.Empty<StyleSetterDto>(),
                Triggers: Array.Empty<TriggerSummaryDto>());
        }
        else
        {
            response = BuildResponse(element, style);
        }
        return response;
    }

    private static ResolveStyleResponse BuildResponse(DependencyObject element, Style style)
    {
        string appliedKey = style.TargetType?.Name ?? string.Empty;
        string appliedSource = ClassifySource(element);
        IReadOnlyList<BasedOnEntryDto> chain = BuildChain(style);
        IReadOnlyList<StyleSetterDto> setters = CollectSetters(style);
        IReadOnlyList<TriggerSummaryDto> triggers = CollectTriggers(style);

        return new ResolveStyleResponse(
            AppliedStyleKey: string.IsNullOrEmpty(appliedKey) ? null : appliedKey,
            AppliedStyleSource: appliedSource,
            BasedOnChain: chain,
            Setters: setters,
            Triggers: triggers);
    }

    private static string ClassifySource(DependencyObject element)
    {
        object localStyle = element.ReadLocalValue(FrameworkElement.StyleProperty);
        bool isExplicit = localStyle != DependencyProperty.UnsetValue;
        return isExplicit ? "Explicit" : "Implicit";
    }

    private static IReadOnlyList<BasedOnEntryDto> BuildChain(Style style)
    {
        var chain = new List<BasedOnEntryDto>();
        Style? current = style;
        int depth = 0;
        while (current is not null)
        {
            string target = current.TargetType?.FullName ?? current.TargetType?.Name ?? "?";
            chain.Add(new BasedOnEntryDto(target, depth));
            current = current.BasedOn;
            depth++;
        }
        return chain;
    }

    private static IReadOnlyList<StyleSetterDto> CollectSetters(Style style)
    {
        var setters = new List<StyleSetterDto>();
        Style? current = style;
        while (current is not null)
        {
            foreach (SetterBase sb in current.Setters)
            {
                if (sb is Setter s)
                {
                    setters.Add(new StyleSetterDto(
                        Property: s.Property.Name,
                        Value: Convert.ToString(s.Value, CultureInfo.InvariantCulture)));
                }
            }
            current = current.BasedOn;
        }
        return setters;
    }

    private static IReadOnlyList<TriggerSummaryDto> CollectTriggers(Style style)
    {
        var triggers = new List<TriggerSummaryDto>();
        Style? current = style;
        while (current is not null)
        {
            foreach (TriggerBase t in current.Triggers)
            {
                triggers.Add(SummarizeTrigger(t));
            }
            current = current.BasedOn;
        }
        return triggers;
    }

    private static TriggerSummaryDto SummarizeTrigger(TriggerBase trigger)
    {
        string kind = trigger.GetType().Name;
        string condition = trigger switch
        {
            Trigger t => $"{t.Property?.Name ?? "?"}={Convert.ToString(t.Value, CultureInfo.InvariantCulture)}",
            _ => kind
        };

        var setters = new List<StyleSetterDto>();
        if (trigger is Trigger tt)
        {
            foreach (SetterBase sb in tt.Setters)
            {
                if (sb is Setter s)
                {
                    setters.Add(new StyleSetterDto(
                        Property: s.Property.Name,
                        Value: Convert.ToString(s.Value, CultureInfo.InvariantCulture)));
                }
            }
        }
        return new TriggerSummaryDto(kind, condition, setters);
    }
}
```

- [ ] **Step 5: Tool handler** — `src\SnoopMCP.Payload\Tools\ResolveStyleToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class ResolveStyleToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly StyleResolver mResolver;
    private readonly DispatcherMarshal mMarshal;

    public ResolveStyleToolHandler(
        ElementRegistry registry,
        StyleResolver resolver,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mResolver = resolver;
        mMarshal = marshal;
    }

    public string ToolName => "resolveStyle";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<ResolveStyleRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject element);
        if (!resolved)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        ResolveStyleResponse response = mMarshal.Invoke(
            () => mResolver.Resolve(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — add alongside the others:

```csharp
var styleResolver = new StyleResolver();
toolRegistry.Register(new ResolveStyleToolHandler(registry, styleResolver, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~StyleResolverTests
```

Expected: 6 tests pass.

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 21: resolveStyle tool — applied style, BasedOn chain, setters, triggers summary"
```

---

## Task 22: Tool — resolveTemplate

**Goal:** For a `Control`, report its applied `ControlTemplate`: the type of template, its source (best-effort), the live template tree under the control (which is the template's runtime expansion), and the named template parts (`PART_*` etc.) reachable via `GetTemplateChild`.

v1 limitations:
- `templateKey` is reported as the template's `TargetType.Name` when available. Looking up `x:Key` requires resource lookup — Phase 2.
- `templateSource` is null in v1; reliably surfacing this requires walking the resource tree which we defer.
- The template tree is built by walking the visual children of the templated control after `ApplyTemplate()` — each node is a `DescribeElementResponse` (so the LLM gets stable ids it can drill into).

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\ResolveTemplateDto.cs`
- Create: `src\SnoopMCP.Payload\Inspection\TemplateResolver.cs`
- Create: `src\SnoopMCP.Payload\Tools\ResolveTemplateToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs`
- Create: `tests\SnoopMCP.Payload.Tests\TemplateResolverTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\ResolveTemplateDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record ResolveTemplateRequest(int Id);

public sealed record ResolveTemplateResponse(
    string? TemplateType,
    string? TemplateKey,
    string? TemplateSource,
    TemplateNodeDto? TemplateTree,
    IReadOnlyList<NamedPartDto> NamedParts);

public sealed record TemplateNodeDto(
    int ElementId,
    string Type,
    string? Name,
    IReadOnlyList<TemplateNodeDto> Children);

public sealed record NamedPartDto(string PartName, string PartType, int ElementId);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\TemplateResolverTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class TemplateResolverTests
{
    private static TemplateResolver CreateResolver(ElementRegistry registry)
    {
        var emitter = new PathStringEmitter();
        var describer = new ElementDescriber(registry, emitter);
        return new TemplateResolver(registry, describer);
    }

    [StaFact]
    public void Resolve_NonControl_ReturnsEmptyResponse()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var grid = new Grid();

        ResolveTemplateResponse response = resolver.Resolve(grid);

        Assert.Null(response.TemplateType);
        Assert.Null(response.TemplateTree);
        Assert.Empty(response.NamedParts);
    }

    [StaFact]
    public void Resolve_ButtonWithDefaultTemplate_TemplateTypeReported()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var button = new Button { Content = "x" };
        button.ApplyTemplate();

        ResolveTemplateResponse response = resolver.Resolve(button);

        Assert.NotNull(response.TemplateType);
    }

    [StaFact]
    public void Resolve_TemplateTree_HasChildren()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var button = new Button { Content = "x" };
        button.ApplyTemplate();

        ResolveTemplateResponse response = resolver.Resolve(button);

        Assert.NotNull(response.TemplateTree);
    }

    [StaFact]
    public void Resolve_CustomTemplate_NamedPartsReported()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border), "PART_Border");
        template.VisualTree = border;
        var button = new Button { Template = template, Content = "x" };
        button.ApplyTemplate();

        ResolveTemplateResponse response = resolver.Resolve(button);

        Assert.Contains(response.NamedParts, p => p.PartName == "PART_Border");
    }

    [StaFact]
    public void Resolve_NullElement_Throws()
    {
        var registry = new ElementRegistry();
        var resolver = CreateResolver(registry);
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~TemplateResolverTests
```

- [ ] **Step 4: Implement `TemplateResolver`** — `src\SnoopMCP.Payload\Inspection\TemplateResolver.cs`

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SnoopMCP.Payload;
using SnoopMCP.Protocol.Tools;

public sealed class TemplateResolver
{
    private readonly ElementRegistry mRegistry;
    private readonly ElementDescriber mDescriber;

    public TemplateResolver(ElementRegistry registry, ElementDescriber describer)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(describer);
        mRegistry = registry;
        mDescriber = describer;
    }

    public ResolveTemplateResponse Resolve(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        ResolveTemplateResponse response;
        if (element is not Control control || control.Template is null)
        {
            response = new ResolveTemplateResponse(
                TemplateType: null,
                TemplateKey: null,
                TemplateSource: null,
                TemplateTree: null,
                NamedParts: Array.Empty<NamedPartDto>());
        }
        else
        {
            response = BuildResponse(control);
        }
        return response;
    }

    private ResolveTemplateResponse BuildResponse(Control control)
    {
        ControlTemplate template = control.Template;
        string templateType = template.GetType().FullName ?? template.GetType().Name;
        string? templateKey = template.TargetType?.Name;

        TemplateNodeDto? tree = BuildTree(control);
        IReadOnlyList<NamedPartDto> parts = CollectNamedParts(control);

        return new ResolveTemplateResponse(
            TemplateType: templateType,
            TemplateKey: templateKey,
            TemplateSource: null,
            TemplateTree: tree,
            NamedParts: parts);
    }

    private TemplateNodeDto? BuildTree(DependencyObject root)
    {
        TemplateNodeDto? node = null;
        bool isVisual = root is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            var children = new List<TemplateNodeDto>(count);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                TemplateNodeDto? childNode = BuildTree(child);
                if (childNode is not null)
                {
                    children.Add(childNode);
                }
            }
            node = new TemplateNodeDto(
                ElementId: mRegistry.GetOrAssign(root),
                Type: root.GetType().Name,
                Name: (root as FrameworkElement)?.Name,
                Children: children);
        }
        return node;
    }

    private IReadOnlyList<NamedPartDto> CollectNamedParts(Control control)
    {
        var parts = new List<NamedPartDto>();
        CollectNamedPartsRecursive(control, parts);
        return parts;
    }

    private void CollectNamedPartsRecursive(DependencyObject node, List<NamedPartDto> sink)
    {
        bool isVisual = node is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(node, i);
                string? name = (child as FrameworkElement)?.Name;
                bool hasName = !string.IsNullOrEmpty(name);
                if (hasName)
                {
                    sink.Add(new NamedPartDto(
                        PartName: name!,
                        PartType: child.GetType().FullName ?? child.GetType().Name,
                        ElementId: mRegistry.GetOrAssign(child)));
                }
                CollectNamedPartsRecursive(child, sink);
            }
        }
    }
}
```

- [ ] **Step 5: Tool handler** — `src\SnoopMCP.Payload\Tools\ResolveTemplateToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class ResolveTemplateToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly TemplateResolver mResolver;
    private readonly DispatcherMarshal mMarshal;

    public ResolveTemplateToolHandler(
        ElementRegistry registry,
        TemplateResolver resolver,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mResolver = resolver;
        mMarshal = marshal;
    }

    public string ToolName => "resolveTemplate";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<ResolveTemplateRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject element);
        if (!resolved)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        ResolveTemplateResponse response = mMarshal.Invoke(
            () => mResolver.Resolve(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — add alongside the others:

```csharp
var templateResolver = new TemplateResolver(registry, describer);
toolRegistry.Register(new ResolveTemplateToolHandler(registry, templateResolver, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~TemplateResolverTests
```

Expected: 5 tests pass.

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 22: resolveTemplate tool — applied template, runtime template tree, named parts"
```

---

## Task 23: Tool — inspectBinding

**Goal:** Surface the state of a `BindingExpression` on a specific DP — source object, path, mode, current value, status (Active, PathError, etc.). This is how the LLM diagnoses "why does this TextBlock show nothing?" without trawling output windows for binding errors.

v1 returns an empty `recentTraceLines` list — wiring up `PresentationTraceSources.DataBindingSource` as a `TraceListener` and forwarding lines back over the pipe is Phase 2. The other fields are populated from the live `BindingExpressionBase`.

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\InspectBindingDto.cs`
- Create: `src\SnoopMCP.Payload\Inspection\BindingInspector.cs`
- Create: `src\SnoopMCP.Payload\Tools\InspectBindingToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs`
- Create: `tests\SnoopMCP.Payload.Tests\BindingInspectorTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\InspectBindingDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record InspectBindingRequest(int Id, string PropertyName);

public sealed record InspectBindingResponse(
    string? BindingPath,
    string? Mode,
    string? ResolvedSourceType,
    int? ResolvedSourceHashCode,
    string? CurrentValue,
    string State,
    IReadOnlyList<BindingTraceLineDto> RecentTraceLines);

public sealed record BindingTraceLineDto(string Timestamp, string Severity, string Message);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\BindingInspectorTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class BindingInspectorTests
{
    [StaFact]
    public void Inspect_NoBinding_StateIsNoBinding()
    {
        var inspector = new BindingInspector();
        var text = new TextBlock { Text = "literal" };

        InspectBindingResponse response = inspector.Inspect(text, "Text");

        Assert.Equal("NoBinding", response.State);
        Assert.Null(response.BindingPath);
    }

    [StaFact]
    public void Inspect_ActiveBinding_StateIsActive()
    {
        var inspector = new BindingInspector();
        var source = new Source { Value = "live" };
        var text = new TextBlock();
        BindingOperations.SetBinding(text, TextBlock.TextProperty, new Binding("Value") { Source = source });

        InspectBindingResponse response = inspector.Inspect(text, "Text");

        Assert.Equal("Active", response.State);
        Assert.Equal("Value", response.BindingPath);
    }

    [StaFact]
    public void Inspect_BindingMode_Reported()
    {
        var inspector = new BindingInspector();
        var source = new Source();
        var text = new TextBlock();
        BindingOperations.SetBinding(
            text,
            TextBlock.TextProperty,
            new Binding("Value") { Source = source, Mode = BindingMode.TwoWay });

        InspectBindingResponse response = inspector.Inspect(text, "Text");

        Assert.Equal("TwoWay", response.Mode);
    }

    [StaFact]
    public void Inspect_ResolvedSourceType_Reported()
    {
        var inspector = new BindingInspector();
        var source = new Source { Value = "x" };
        var text = new TextBlock();
        BindingOperations.SetBinding(text, TextBlock.TextProperty, new Binding("Value") { Source = source });

        InspectBindingResponse response = inspector.Inspect(text, "Text");

        Assert.Equal(typeof(Source).FullName, response.ResolvedSourceType);
        Assert.NotNull(response.ResolvedSourceHashCode);
    }

    [StaFact]
    public void Inspect_BrokenBindingPath_StateIsPathError()
    {
        var inspector = new BindingInspector();
        var source = new Source { Value = "x" };
        var text = new TextBlock();
        BindingOperations.SetBinding(
            text,
            TextBlock.TextProperty,
            new Binding("DoesNotExist") { Source = source });

        InspectBindingResponse response = inspector.Inspect(text, "Text");

        Assert.NotEqual("Active", response.State);
    }

    [StaFact]
    public void Inspect_UnknownProperty_ThrowsInvalidArgument()
    {
        var inspector = new BindingInspector();
        var text = new TextBlock();

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(
            () => inspector.Inspect(text, "NoSuchProperty"));
        Assert.Equal(ErrorCode.InvalidArgument, ex.Code);
    }

    [StaFact]
    public void Inspect_NullElement_Throws()
    {
        var inspector = new BindingInspector();
        Assert.Throws<ArgumentNullException>(() => inspector.Inspect(null!, "Text"));
    }

    private sealed class Source : System.ComponentModel.INotifyPropertyChanged
    {
        public string Value { get; set; } = string.Empty;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~BindingInspectorTests
```

- [ ] **Step 4: Implement `BindingInspector`** — `src\SnoopMCP.Payload\Inspection\BindingInspector.cs`

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;

public sealed class BindingInspector
{
    public InspectBindingResponse Inspect(DependencyObject element, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);

        DependencyProperty dp = ResolveDp(element.GetType(), propertyName)
            ?? throw new SnoopMcpException(
                ErrorCode.InvalidArgument,
                $"Dependency property '{propertyName}' not found on {element.GetType().FullName}.");

        BindingExpressionBase? expression = BindingOperations.GetBindingExpressionBase(element, dp);
        InspectBindingResponse response;
        if (expression is null)
        {
            response = new InspectBindingResponse(
                BindingPath: null,
                Mode: null,
                ResolvedSourceType: null,
                ResolvedSourceHashCode: null,
                CurrentValue: null,
                State: "NoBinding",
                RecentTraceLines: Array.Empty<BindingTraceLineDto>());
        }
        else
        {
            response = BuildResponse(element, dp, expression);
        }
        return response;
    }

    private static InspectBindingResponse BuildResponse(
        DependencyObject element,
        DependencyProperty dp,
        BindingExpressionBase expression)
    {
        BindingExpression? typed = expression as BindingExpression;
        Binding? binding = typed?.ParentBinding;
        string state = MapState(expression);
        object? currentValue = element.GetValue(dp);

        return new InspectBindingResponse(
            BindingPath: binding?.Path?.Path,
            Mode: binding?.Mode.ToString(),
            ResolvedSourceType: typed?.ResolvedSource?.GetType().FullName,
            ResolvedSourceHashCode: typed?.ResolvedSource is null
                ? null
                : RuntimeHelpers.GetHashCode(typed.ResolvedSource),
            CurrentValue: Convert.ToString(currentValue, CultureInfo.InvariantCulture),
            State: state,
            RecentTraceLines: Array.Empty<BindingTraceLineDto>());
    }

    private static string MapState(BindingExpressionBase expression)
    {
        string state = expression.Status switch
        {
            BindingStatus.Active => "Active",
            BindingStatus.Inactive => "Inactive",
            BindingStatus.Detached => "Detached",
            BindingStatus.AsyncRequestPending => "AsyncRequestPending",
            BindingStatus.PathError => "PathError",
            BindingStatus.UpdateSourceError => "UpdateSourceError",
            BindingStatus.UpdateTargetError => "UpdateTargetError",
            BindingStatus.Unattached => "Unattached",
            _ => expression.Status.ToString()
        };
        return state;
    }

    private static DependencyProperty? ResolveDp(Type ownerType, string propertyName)
    {
        DependencyProperty? found = null;
        string fieldName = propertyName + "Property";
        Type? current = ownerType;
        while (current is not null && found is null)
        {
            FieldInfo? field = current.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            bool isDpField = field is not null && typeof(DependencyProperty).IsAssignableFrom(field.FieldType);
            if (isDpField)
            {
                found = field!.GetValue(null) as DependencyProperty;
            }
            current = current.BaseType;
        }
        return found;
    }
}
```

- [ ] **Step 5: Tool handler** — `src\SnoopMCP.Payload\Tools\InspectBindingToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class InspectBindingToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly BindingInspector mInspector;
    private readonly DispatcherMarshal mMarshal;

    public InspectBindingToolHandler(
        ElementRegistry registry,
        BindingInspector inspector,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mInspector = inspector;
        mMarshal = marshal;
    }

    public string ToolName => "inspectBinding";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<InspectBindingRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject element);
        if (!resolved)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        InspectBindingResponse response = mMarshal.Invoke(
            () => mInspector.Inspect(element, request.PropertyName),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — add alongside the others:

```csharp
var bindingInspector = new BindingInspector();
toolRegistry.Register(new InspectBindingToolHandler(registry, bindingInspector, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~BindingInspectorTests
```

Expected: 7 tests pass.

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 23: inspectBinding tool — BindingExpression state, source, path, mode, value"
```

---

## Task 24: Tool — listBindings (broad binding audit)

**Goal:** Enumerate every DP currently carrying a `BindingExpression` on an element, with an option to recurse through the visual tree. Where `inspectBinding` (Task 23) is the deep dive on ONE binding, `listBindings` is the wide audit — "show me every binding under this subtree" so the LLM can spot missing bindings, broken paths, and inconsistent modes at a glance.

Each result row is the same summary shape as `inspectBinding`'s response minus the (still-empty in v1) recent trace lines. Use case: the LLM asks "what's wrong with my DetailPane?" — calls `listBindings(detailPaneId, includeDescendants=true)`, gets back every binding under that pane, sees three with `state=PathError`, then dives into each with `inspectBinding` for the full story.

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\ListBindingsDto.cs`
- Modify: `src\SnoopMCP.Payload\Inspection\BindingInspector.cs` (add `ListBindings` method)
- Create: `src\SnoopMCP.Payload\Tools\ListBindingsToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs` (register handler)
- Create: `tests\SnoopMCP.Payload.Tests\ListBindingsTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\ListBindingsDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record ListBindingsRequest(int Id, bool IncludeDescendants);

public sealed record ListBindingsResponse(IReadOnlyList<BindingSummaryDto> Bindings);

public sealed record BindingSummaryDto(
    int ElementId,
    string ElementType,
    string Property,
    string? BindingPath,
    string? Mode,
    string State,
    bool HasError,
    string? ResolvedSourceType,
    string? CurrentValue);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\ListBindingsTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class ListBindingsTests
{
    private static BindingInspector CreateInspector(ElementRegistry registry)
    {
        return new BindingInspector(registry);
    }

    [StaFact]
    public void List_ElementWithNoBindings_ReturnsEmpty()
    {
        var registry = new ElementRegistry();
        var inspector = CreateInspector(registry);
        var text = new TextBlock { Text = "literal" };

        ListBindingsResponse response = inspector.ListBindings(text, includeDescendants: false);

        Assert.Empty(response.Bindings);
    }

    [StaFact]
    public void List_SingleBinding_IsReturned()
    {
        var registry = new ElementRegistry();
        var inspector = CreateInspector(registry);
        var source = new Source { Value = "x" };
        var text = new TextBlock();
        BindingOperations.SetBinding(text, TextBlock.TextProperty, new Binding("Value") { Source = source });

        ListBindingsResponse response = inspector.ListBindings(text, includeDescendants: false);

        Assert.Single(response.Bindings);
        Assert.Equal("Text", response.Bindings[0].Property);
        Assert.Equal("Value", response.Bindings[0].BindingPath);
    }

    [StaFact]
    public void List_MultipleBindingsOnSameElement_AllReturned()
    {
        var registry = new ElementRegistry();
        var inspector = CreateInspector(registry);
        var source = new Source { Value = "x" };
        var text = new TextBlock();
        BindingOperations.SetBinding(text, TextBlock.TextProperty, new Binding("Value") { Source = source });
        BindingOperations.SetBinding(text, TextBlock.ToolTipProperty, new Binding("Value") { Source = source });

        ListBindingsResponse response = inspector.ListBindings(text, includeDescendants: false);

        Assert.Equal(2, response.Bindings.Count);
        Assert.Contains(response.Bindings, b => b.Property == "Text");
        Assert.Contains(response.Bindings, b => b.Property == "ToolTip");
    }

    [StaFact]
    public void List_IncludeDescendantsFalse_DoesNotRecurse()
    {
        var registry = new ElementRegistry();
        var inspector = CreateInspector(registry);
        var source = new Source { Value = "x" };
        var inner = new TextBlock();
        BindingOperations.SetBinding(inner, TextBlock.TextProperty, new Binding("Value") { Source = source });
        var outer = new ContentControl { Content = inner };

        ListBindingsResponse response = inspector.ListBindings(outer, includeDescendants: false);

        Assert.Empty(response.Bindings);
    }

    [StaFact]
    public void List_IncludeDescendantsTrue_FindsNestedBindings()
    {
        var registry = new ElementRegistry();
        var inspector = CreateInspector(registry);
        var source = new Source { Value = "x" };
        var stack = new StackPanel();
        var a = new TextBlock { Name = "A" };
        var b = new TextBlock { Name = "B" };
        BindingOperations.SetBinding(a, TextBlock.TextProperty, new Binding("Value") { Source = source });
        BindingOperations.SetBinding(b, TextBlock.TextProperty, new Binding("Value") { Source = source });
        stack.Children.Add(a);
        stack.Children.Add(b);

        ListBindingsResponse response = inspector.ListBindings(stack, includeDescendants: true);

        Assert.Equal(2, response.Bindings.Count);
        Assert.Contains(response.Bindings, x => x.ElementType == "TextBlock");
    }

    [StaFact]
    public void List_BrokenBinding_HasErrorTrue()
    {
        var registry = new ElementRegistry();
        var inspector = CreateInspector(registry);
        var source = new Source { Value = "x" };
        var text = new TextBlock();
        BindingOperations.SetBinding(
            text,
            TextBlock.TextProperty,
            new Binding("DoesNotExist") { Source = source });

        ListBindingsResponse response = inspector.ListBindings(text, includeDescendants: false);

        Assert.Single(response.Bindings);
        Assert.True(
            response.Bindings[0].HasError || response.Bindings[0].State != "Active",
            $"Expected error state; got State={response.Bindings[0].State}, HasError={response.Bindings[0].HasError}");
    }

    [StaFact]
    public void List_NullElement_Throws()
    {
        var registry = new ElementRegistry();
        var inspector = CreateInspector(registry);
        Assert.Throws<ArgumentNullException>(() => inspector.ListBindings(null!, false));
    }

    private sealed class Source : INotifyPropertyChanged
    {
        public string Value { get; set; } = string.Empty;
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ListBindingsTests
```

- [ ] **Step 4: Extend `BindingInspector`** — modify `src\SnoopMCP.Payload\Inspection\BindingInspector.cs`

The existing `BindingInspector` (Task 23) has a parameterless constructor — change it to take an `ElementRegistry` so the `ListBindings` summaries can carry stable element ids that the LLM uses to drill into specific bindings. This requires three coordinated touches:

1. Update the `BindingInspector` constructor (this step)
2. Update the registration in `PayloadEntryPoint.Inject` (Step 6 below)
3. Update the factory in `BindingInspectorTests` from Task 23 — change `new BindingInspector()` to `new BindingInspector(new ElementRegistry())` so the existing 7 tests still compile and pass

Add these `using` directives to `BindingInspector.cs`:

```csharp
using System.Globalization;
using System.Windows.Media;
using SnoopMCP.Payload;
```

Replace the parameterless constructor with this constructor block, and add the `ListBindings` method and its helpers:

```csharp
private readonly ElementRegistry mRegistry;

public BindingInspector(ElementRegistry registry)
{
    ArgumentNullException.ThrowIfNull(registry);
    mRegistry = registry;
}

public ListBindingsResponse ListBindings(DependencyObject element, bool includeDescendants)
{
    ArgumentNullException.ThrowIfNull(element);
    var sink = new List<BindingSummaryDto>();
    Walk(element, includeDescendants, sink);
    return new ListBindingsResponse(sink);
}

private void Walk(DependencyObject element, bool includeDescendants, List<BindingSummaryDto> sink)
{
    CollectFrom(element, sink);
    if (includeDescendants)
    {
        bool isVisual = element is Visual or System.Windows.Media.Media3D.Visual3D;
        if (isVisual)
        {
            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                Walk(VisualTreeHelper.GetChild(element, i), true, sink);
            }
        }
    }
}

private void CollectFrom(DependencyObject element, List<BindingSummaryDto> sink)
{
    int elementId = mRegistry.GetOrAssign(element);
    string elementType = element.GetType().Name;
    LocalValueEnumerator enumerator = element.GetLocalValueEnumerator();
    while (enumerator.MoveNext())
    {
        DependencyProperty dp = enumerator.Current.Property;
        BindingExpressionBase? expression = BindingOperations.GetBindingExpressionBase(element, dp);
        if (expression is not null)
        {
            BindingExpression? typed = expression as BindingExpression;
            Binding? binding = typed?.ParentBinding;
            object? currentValue = element.GetValue(dp);
            sink.Add(new BindingSummaryDto(
                ElementId: elementId,
                ElementType: elementType,
                Property: dp.Name,
                BindingPath: binding?.Path?.Path,
                Mode: binding?.Mode.ToString(),
                State: MapState(expression),
                HasError: expression.HasError,
                ResolvedSourceType: typed?.ResolvedSource?.GetType().FullName,
                CurrentValue: Convert.ToString(currentValue, CultureInfo.InvariantCulture)));
        }
    }
}
```

(The existing `MapState`, `ResolveDp`, and `Inspect` methods stay as-is.)

- [ ] **Step 5: Tool handler** — `src\SnoopMCP.Payload\Tools\ListBindingsToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class ListBindingsToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly BindingInspector mInspector;
    private readonly DispatcherMarshal mMarshal;

    public ListBindingsToolHandler(
        ElementRegistry registry,
        BindingInspector inspector,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mInspector = inspector;
        mMarshal = marshal;
    }

    public string ToolName => "listBindings";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<ListBindingsRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject element);
        if (!resolved)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        ListBindingsResponse response = mMarshal.Invoke(
            () => mInspector.ListBindings(element, request.IncludeDescendants),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — modify `src\SnoopMCP.Payload\PayloadEntryPoint.cs`

The `BindingInspector` was registered as `new BindingInspector()` in Task 23 — update to pass `registry` and `describer`, then register the new handler:

```csharp
var bindingInspector = new BindingInspector(registry);
toolRegistry.Register(new InspectBindingToolHandler(registry, bindingInspector, marshal));
toolRegistry.Register(new ListBindingsToolHandler(registry, bindingInspector, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~ListBindingsTests
```

Expected: 7 tests pass. Also re-run the Task 23 tests to confirm the constructor change didn't break them:

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~BindingInspectorTests
```

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 24: listBindings tool — broad audit of every binding under an element"
```

---

## Task 25: Tool — exportXaml (XAML state dump)

**Goal:** Serialize an element to XAML using `System.Windows.Markup.XamlWriter.Save` — the snapshot the LLM uses to see the object's current declarative state. Output reflects live values: current property values (not bindings — XamlWriter emits the evaluated value), realized children, attached property values.

Why this matters: when the LLM is debugging "why does this look wrong?", being able to read the *full XAML* of a subtree is qualitatively different from reading a tree of `describeElement` calls. The XAML view is dense, declarative, and matches how a WPF developer thinks.

v1 limitations (called out so the LLM doesn't get confused):
- Output is XAML 2006 (legacy `XamlWriter`). A few properties don't roundtrip; some emit warnings; some are silently omitted.
- **Bindings appear as their evaluated values, not as `{Binding ...}` markup.** For the live binding spec, use `listBindings` (Task 24) or `inspectBinding` (Task 23).
- Templates/styles may serialize inline or as resource references, depending on context.
- Output can be large for big subtrees. The response includes a byte-count and a soft cap; if the serialized form exceeds the cap, only the first N bytes are returned plus a warning.

**Files:**
- Create: `src\SnoopMCP.Protocol\Tools\ExportXamlDto.cs`
- Create: `src\SnoopMCP.Payload\Inspection\XamlExporter.cs`
- Create: `src\SnoopMCP.Payload\Tools\ExportXamlToolHandler.cs`
- Modify: `src\SnoopMCP.Payload\PayloadEntryPoint.cs`
- Create: `tests\SnoopMCP.Payload.Tests\XamlExporterTests.cs`

- [ ] **Step 1: DTO** — `src\SnoopMCP.Protocol\Tools\ExportXamlDto.cs`

```csharp
namespace SnoopMCP.Protocol.Tools;

public sealed record ExportXamlRequest(int Id);

public sealed record ExportXamlResponse(
    string Xaml,
    int ByteCount,
    bool Truncated,
    string? Warning);
```

- [ ] **Step 2: Failing tests** — `tests\SnoopMCP.Payload.Tests\XamlExporterTests.cs`

```csharp
namespace SnoopMCP.Payload.Tests;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class XamlExporterTests
{
    [StaFact]
    public void Export_PlainButton_ReturnsXamlContainingButton()
    {
        var exporter = new XamlExporter();
        var button = new Button { Content = "Click me", Width = 80 };

        ExportXamlResponse response = exporter.Export(button);

        Assert.Contains("Button", response.Xaml);
        Assert.True(response.ByteCount > 0);
        Assert.False(response.Truncated);
    }

    [StaFact]
    public void Export_StackPanelWithChildren_IncludesChildrenInline()
    {
        var exporter = new XamlExporter();
        var stack = new StackPanel();
        stack.Children.Add(new Button { Content = "A" });
        stack.Children.Add(new Button { Content = "B" });

        ExportXamlResponse response = exporter.Export(stack);

        Assert.Contains("StackPanel", response.Xaml);
        Assert.Contains("Button", response.Xaml);
    }

    [StaFact]
    public void Export_TextBlockWithExplicitForeground_ReflectsLiveValue()
    {
        var exporter = new XamlExporter();
        var text = new TextBlock
        {
            Text = "Hello",
            Foreground = Brushes.Red
        };

        ExportXamlResponse response = exporter.Export(text);

        Assert.Contains("TextBlock", response.Xaml);
        Assert.Contains("Hello", response.Xaml);
    }

    [StaFact]
    public void Export_LargePayload_TruncatesAndWarns()
    {
        var exporter = new XamlExporter(softCapBytes: 256);
        var stack = new StackPanel();
        for (int i = 0; i < 200; i++)
        {
            stack.Children.Add(new TextBlock { Text = $"This is row number {i:D4} with extra padding text" });
        }

        ExportXamlResponse response = exporter.Export(stack);

        Assert.True(response.Truncated);
        Assert.NotNull(response.Warning);
        Assert.True(response.Xaml.Length <= 256 + 256);
    }

    [StaFact]
    public void Export_NullElement_Throws()
    {
        var exporter = new XamlExporter();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(null!));
    }
}
```

- [ ] **Step 3: Run tests — expect compile failure**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~XamlExporterTests
```

- [ ] **Step 4: Implement `XamlExporter`** — `src\SnoopMCP.Payload\Inspection\XamlExporter.cs`

```csharp
namespace SnoopMCP.Payload.Inspection;

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using System.Xml;
using SnoopMCP.Protocol.Tools;

public sealed class XamlExporter
{
    public const int DefaultSoftCapBytes = 256 * 1024;

    private readonly int mSoftCapBytes;

    public XamlExporter() : this(DefaultSoftCapBytes)
    {
    }

    public XamlExporter(int softCapBytes)
    {
        if (softCapBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(softCapBytes), "Soft cap must be positive.");
        }
        mSoftCapBytes = softCapBytes;
    }

    public ExportXamlResponse Export(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        string xaml = SerializeToXaml(element);
        int byteCount = Encoding.UTF8.GetByteCount(xaml);
        bool truncated = byteCount > mSoftCapBytes;
        string emitted = truncated ? TruncateUtf8(xaml, mSoftCapBytes) : xaml;
        string? warning = truncated
            ? $"XAML payload of {byteCount} bytes exceeded soft cap of {mSoftCapBytes}; truncated."
            : null;

        return new ExportXamlResponse(
            Xaml: emitted,
            ByteCount: byteCount,
            Truncated: truncated,
            Warning: warning);
    }

    private static string SerializeToXaml(DependencyObject element)
    {
        var builder = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = true
        };
        using (XmlWriter writer = XmlWriter.Create(builder, settings))
        {
            XamlWriter.Save(element, writer);
        }
        return builder.ToString();
    }

    private static string TruncateUtf8(string source, int byteCap)
    {
        byte[] all = Encoding.UTF8.GetBytes(source);
        int safeLen = Math.Min(byteCap, all.Length);
        // Walk back to a valid UTF-8 boundary so we don't split a code point.
        while (safeLen > 0 && (all[safeLen - 1] & 0b1100_0000) == 0b1000_0000)
        {
            safeLen--;
        }
        return Encoding.UTF8.GetString(all, 0, safeLen);
    }
}
```

- [ ] **Step 5: Tool handler** — `src\SnoopMCP.Payload\Tools\ExportXamlToolHandler.cs`

```csharp
namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

public sealed class ExportXamlToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly XamlExporter mExporter;
    private readonly DispatcherMarshal mMarshal;

    public ExportXamlToolHandler(
        ElementRegistry registry,
        XamlExporter exporter,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mExporter = exporter;
        mMarshal = marshal;
    }

    public string ToolName => "exportXaml";

    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var request = arguments.Deserialize<ExportXamlRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject element);
        if (!resolved)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        ExportXamlResponse response = mMarshal.Invoke(
            () => mExporter.Export(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
```

- [ ] **Step 6: Register in `PayloadEntryPoint`** — add alongside the others:

```csharp
var xamlExporter = new XamlExporter();
toolRegistry.Register(new ExportXamlToolHandler(registry, xamlExporter, marshal));
```

- [ ] **Step 7: Run tests**

```text
dotnet test tests/SnoopMCP.Payload.Tests/SnoopMCP.Payload.Tests.csproj --filter FullyQualifiedName~XamlExporterTests
```

Expected: 5 tests pass. If `Export_LargePayload_TruncatesAndWarns` doesn't truncate, the serializer produced less than 256 bytes for 200 small TextBlocks — boost the per-row text length or the row count.

- [ ] **Step 8: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 25: exportXaml tool — XamlWriter snapshot of an element's live state"
```

---

## Task 26: Host project scaffold + MCP stdio server

**Goal:** Stand up `SnoopMCP.Host.exe` — an `Microsoft.Extensions.Hosting`-based app that hosts the MCP server over stdio. No tools yet; that's Task 29. This task just gets the process bootable and the MCP transport wired so subsequent tasks plug into a working host.

`ModelContextProtocol.AspNetCore` 1.2.0 supplies `WithStdioServerTransport()`. Logging goes to stderr (stdout is reserved for the MCP transport).

**Files:**
- Create: `src\SnoopMCP.Host\SnoopMCP.Host.csproj`
- Create: `src\SnoopMCP.Host\Program.cs`

- [ ] **Step 1: Host project** — `src\SnoopMCP.Host\SnoopMCP.Host.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0-windows</TargetFramework>
        <RootNamespace>SnoopMCP.Host</RootNamespace>
        <AssemblyName>SnoopMCP.Host</AssemblyName>
        <PlatformTarget>x64</PlatformTarget>
        <InvariantGlobalization>true</InvariantGlobalization>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\SnoopMCP.Protocol\SnoopMCP.Protocol.csproj" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
        <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="10.0.0" />
        <PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.2.0" />
    </ItemGroup>
</Project>
```

- [ ] **Step 2: Add the project to the solution**

```text
dotnet sln SnoopMCP.sln add src/SnoopMCP.Host/SnoopMCP.Host.csproj
```

- [ ] **Step 3: Program entry** — `src\SnoopMCP.Host\Program.cs`

The MCP stdio transport requires that nothing else write to stdout. We route all logging to stderr via `LogToStandardErrorThreshold = LogLevel.Trace`. Tool registrations come in Task 29 via `WithToolsFromAssembly()`.

```csharp
namespace SnoopMCP.Host;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        using IHost host = builder.Build();
        await host.RunAsync();
        return 0;
    }
}
```

- [ ] **Step 4: Build**

```text
dotnet build src/SnoopMCP.Host/SnoopMCP.Host.csproj -c Debug
```

Expected: build succeeds. If `AddMcpServer` is unresolved, query saddlerag for the current `ModelContextProtocol.AspNetCore` API surface and adjust the using directives; the package consolidates its public extensions under `ModelContextProtocol.Server` and `Microsoft.Extensions.DependencyInjection`.

- [ ] **Step 5: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 26: Host project scaffold with MCP stdio server transport"
```

---

## Task 27: Host — PipeClient

**Goal:** Counterpart to the payload's `PipeServer` (Task 6). Connects to a named pipe, writes length-prefixed `RpcRequest` frames, reads `RpcResponse` frames, correlates them by `Id`. The MCP layer is stateless from the LLM's perspective; correlation lives inside `PipeClient`.

**Files:**
- Create: `src\SnoopMCP.Host\PipeClient.cs`
- Create: `tests\SnoopMCP.Host.Tests\SnoopMCP.Host.Tests.csproj`
- Create: `tests\SnoopMCP.Host.Tests\PipeClientTests.cs`

- [ ] **Step 1: Implement `PipeClient`** — `src\SnoopMCP.Host\PipeClient.cs`

```csharp
namespace SnoopMCP.Host;

using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Wire;

public sealed class PipeClient : IAsyncDisposable
{
    private const int ConnectTimeoutMs = 5000;

    private readonly string mPipeName;
    private readonly ILogger<PipeClient> mLogger;
    private readonly SemaphoreSlim mLock = new(1, 1);
    private NamedPipeClientStream? mStream;
    private long mNextRequestId;

    public PipeClient(string pipeName, ILogger<PipeClient> logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        ArgumentNullException.ThrowIfNull(logger);
        mPipeName = pipeName;
        mLogger = logger;
    }

    public bool IsConnected => mStream is { IsConnected: true };

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (mStream is not null)
            {
                throw new InvalidOperationException("Pipe client already connected.");
            }
            var stream = new NamedPipeClientStream(
                ".",
                mPipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await stream.ConnectAsync(ConnectTimeoutMs, cancellationToken).ConfigureAwait(false);
            mStream = stream;
            mLogger.LogInformation("Connected to pipe {PipeName}.", mPipeName);
        }
        finally
        {
            mLock.Release();
        }
    }

    public async Task<JsonElement> SendAsync(
        string toolName,
        object arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        ArgumentNullException.ThrowIfNull(arguments);

        await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            NamedPipeClientStream stream = mStream
                ?? throw new InvalidOperationException("Pipe client not connected.");

            long id = Interlocked.Increment(ref mNextRequestId);
            JsonElement argsElement = ToJsonElement(arguments);
            var request = new RpcRequest { Id = id, Tool = toolName, Arguments = argsElement };

            await WireSerializer.WriteFrameAsync(stream, request, cancellationToken).ConfigureAwait(false);
            RpcResponse? response = await WireSerializer
                .ReadFrameAsync<RpcResponse>(stream, cancellationToken)
                .ConfigureAwait(false);

            JsonElement result;
            if (response is null)
            {
                throw new SnoopMcpException(ErrorCode.SessionLost, "Pipe closed before response arrived.");
            }
            else if (response.Error is not null)
            {
                throw new SnoopMcpException(response.Error.Code, response.Error.Message);
            }
            else if (response.Result is null)
            {
                result = JsonDocument.Parse("null").RootElement.Clone();
            }
            else
            {
                result = response.Result.Value;
            }
            return result;
        }
        finally
        {
            mLock.Release();
        }
    }

    private static JsonElement ToJsonElement(object arguments)
    {
        string json = JsonSerializer.Serialize(arguments, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public async ValueTask DisposeAsync()
    {
        await mLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (mStream is not null)
            {
                await mStream.DisposeAsync().ConfigureAwait(false);
                mStream = null;
            }
        }
        finally
        {
            mLock.Release();
            mLock.Dispose();
        }
    }
}
```

- [ ] **Step 2: Test project** — `tests\SnoopMCP.Host.Tests\SnoopMCP.Host.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0-windows</TargetFramework>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <RootNamespace>SnoopMCP.Host.Tests</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="xunit.v3" Version="1.0.0" />
        <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
        <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\src\SnoopMCP.Host\SnoopMCP.Host.csproj" />
        <ProjectReference Include="..\..\src\SnoopMCP.Protocol\SnoopMCP.Protocol.csproj" />
    </ItemGroup>
</Project>
```

```text
dotnet sln SnoopMCP.sln add tests/SnoopMCP.Host.Tests/SnoopMCP.Host.Tests.csproj
```

- [ ] **Step 3: Pipe round-trip test** — `tests\SnoopMCP.Host.Tests\PipeClientTests.cs`

Spins up a `NamedPipeServerStream` in the test, has `PipeClient` connect to it, asserts a request/response round-trip.

```csharp
namespace SnoopMCP.Host.Tests;

using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SnoopMCP.Host;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Wire;
using Xunit;

public sealed class PipeClientTests
{
    [Fact]
    public async Task SendAsync_ReturnsResultJson()
    {
        string pipeName = $"snoopmcp-test-{Guid.NewGuid():N}";

        Task serverTask = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync();

            RpcRequest? request = await WireSerializer.ReadFrameAsync<RpcRequest>(server, default);
            Assert.NotNull(request);
            Assert.Equal("echo", request!.Tool);

            JsonElement result = JsonDocument.Parse("{\"echoed\":\"ok\"}").RootElement.Clone();
            var response = new RpcResponse { Id = request.Id, Result = result };
            await WireSerializer.WriteFrameAsync(server, response, default);
        });

        await using var client = new PipeClient(pipeName, NullLogger<PipeClient>.Instance);
        await client.ConnectAsync(default);
        JsonElement got = await client.SendAsync("echo", new { hello = "world" }, default);

        Assert.Equal("ok", got.GetProperty("echoed").GetString());
        await serverTask;
    }

    [Fact]
    public async Task SendAsync_ErrorResponse_ThrowsSnoopMcpException()
    {
        string pipeName = $"snoopmcp-test-{Guid.NewGuid():N}";

        Task serverTask = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync();

            RpcRequest? request = await WireSerializer.ReadFrameAsync<RpcRequest>(server, default);
            var response = new RpcResponse
            {
                Id = request!.Id,
                Error = new RpcError { Code = ErrorCode.ElementExpired, Message = "gone" }
            };
            await WireSerializer.WriteFrameAsync(server, response, default);
        });

        await using var client = new PipeClient(pipeName, NullLogger<PipeClient>.Instance);
        await client.ConnectAsync(default);

        SnoopMcpException ex = await Assert.ThrowsAsync<SnoopMcpException>(
            () => client.SendAsync("anything", new { }, default));
        Assert.Equal(ErrorCode.ElementExpired, ex.Code);
        await serverTask;
    }
}
```

- [ ] **Step 4: Run tests**

```text
dotnet test tests/SnoopMCP.Host.Tests/SnoopMCP.Host.Tests.csproj --filter FullyQualifiedName~PipeClientTests
```

Expected: 2 tests pass.

- [ ] **Step 5: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 27: Host PipeClient with framed JSON-RPC over named pipe"
```

---

## Task 28: Host — SessionManager

**Goal:** Owns the lifecycle of an attached session — generates the pipe name, holds the `PipeClient`, exposes `OpenSession(pipeName)` / `CloseSession()` / `SendAsync(tool, args)`. The MCP tool wrappers (Task 29) call `SessionManager.SendAsync`; the actual injection of the payload into the target process is glued together in Task 31 via `InjectorService`.

A `SessionManager` instance represents *one* attached target at a time. `OpenSession` while already connected throws — the MCP layer surfaces this as `InvalidOperationException`, which the SDK turns into a tool-error response.

**Files:**
- Create: `src\SnoopMCP.Host\SessionManager.cs`
- Create: `tests\SnoopMCP.Host.Tests\SessionManagerTests.cs`

- [ ] **Step 1: Implement `SessionManager`** — `src\SnoopMCP.Host\SessionManager.cs`

```csharp
namespace SnoopMCP.Host;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using SnoopMCP.Protocol.Errors;

public sealed class SessionManager : IAsyncDisposable
{
    private readonly ILogger<SessionManager> mLogger;
    private readonly ILoggerFactory mLoggerFactory;
    private readonly SemaphoreSlim mLock = new(1, 1);
    private PipeClient? mClient;
    private string? mPipeName;

    public SessionManager(ILogger<SessionManager> logger, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        mLogger = logger;
        mLoggerFactory = loggerFactory;
    }

    public bool IsAttached => mClient is { IsConnected: true };

    public string? PipeName => mPipeName;

    public string AllocatePipeName()
    {
        return $"snoopmcp-{Guid.NewGuid():N}";
    }

    public async Task OpenAsync(string pipeName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);

        await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (mClient is not null)
            {
                throw new InvalidOperationException(
                    "A session is already open; call CloseAsync before opening a new one.");
            }
            var client = new PipeClient(pipeName, mLoggerFactory.CreateLogger<PipeClient>());
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            mClient = client;
            mPipeName = pipeName;
            mLogger.LogInformation("Session opened on pipe {PipeName}.", pipeName);
        }
        finally
        {
            mLock.Release();
        }
    }

    public async Task CloseAsync()
    {
        await mLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (mClient is not null)
            {
                await mClient.DisposeAsync().ConfigureAwait(false);
                mClient = null;
                mPipeName = null;
                mLogger.LogInformation("Session closed.");
            }
        }
        finally
        {
            mLock.Release();
        }
    }

    public Task<JsonElement> SendAsync(string toolName, object arguments, CancellationToken cancellationToken)
    {
        PipeClient client = mClient
            ?? throw new SnoopMcpException(
                ErrorCode.SessionLost,
                "No attached session. Call attach(pid) first.");
        return client.SendAsync(toolName, arguments, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        mLock.Dispose();
    }
}
```

- [ ] **Step 2: SessionManager test** — `tests\SnoopMCP.Host.Tests\SessionManagerTests.cs`

```csharp
namespace SnoopMCP.Host.Tests;

using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SnoopMCP.Host;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Wire;
using Xunit;

public sealed class SessionManagerTests
{
    private static SessionManager CreateManager()
    {
        return new SessionManager(NullLogger<SessionManager>.Instance, NullLoggerFactory.Instance);
    }

    [Fact]
    public void AllocatePipeName_ReturnsUnique()
    {
        var manager = CreateManager();
        string a = manager.AllocatePipeName();
        string b = manager.AllocatePipeName();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task SendAsync_WithoutOpen_ThrowsSessionLost()
    {
        await using var manager = CreateManager();

        SnoopMcpException ex = await Assert.ThrowsAsync<SnoopMcpException>(
            () => manager.SendAsync("echo", new { }, default));
        Assert.Equal(ErrorCode.SessionLost, ex.Code);
    }

    [Fact]
    public async Task OpenAsync_ConnectsAndSendsThroughClient()
    {
        await using var manager = CreateManager();
        string pipeName = manager.AllocatePipeName();

        Task serverTask = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync();
            RpcRequest? request = await WireSerializer.ReadFrameAsync<RpcRequest>(server, default);
            JsonElement res = JsonDocument.Parse("{\"ok\":true}").RootElement.Clone();
            await WireSerializer.WriteFrameAsync(
                server,
                new RpcResponse { Id = request!.Id, Result = res },
                default);
        });

        await manager.OpenAsync(pipeName, default);
        Assert.True(manager.IsAttached);

        JsonElement got = await manager.SendAsync("anything", new { x = 1 }, default);
        Assert.True(got.GetProperty("ok").GetBoolean());

        await manager.CloseAsync();
        Assert.False(manager.IsAttached);
        await serverTask;
    }

    [Fact]
    public async Task OpenAsync_TwiceWithoutClose_Throws()
    {
        await using var manager = CreateManager();
        string pipeName = manager.AllocatePipeName();

        Task serverTask = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync();
            await Task.Delay(200);
        });

        await manager.OpenAsync(pipeName, default);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.OpenAsync(pipeName, default));

        await manager.CloseAsync();
        await serverTask;
    }
}
```

- [ ] **Step 3: Run tests**

```text
dotnet test tests/SnoopMCP.Host.Tests/SnoopMCP.Host.Tests.csproj --filter FullyQualifiedName~SessionManagerTests
```

Expected: 4 tests pass.

- [ ] **Step 4: Register `SessionManager` in `Program.cs`** — modify `src\SnoopMCP.Host\Program.cs`

Add `using SnoopMCP.Host;` and the registration in `builder.Services`:

```csharp
builder.Services.AddSingleton<SessionManager>();
```

Final `Services` block becomes:

```csharp
builder.Services.AddSingleton<SessionManager>();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
```

- [ ] **Step 5: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 28: Host SessionManager — lifecycle, pipe-name allocation, send dispatch"
```

---

## Task 29: Host — MCP tool wrappers

**Goal:** Expose the 19 inspection tools plus `attach`/`detach` to the MCP client. Each method is decorated with `[McpServerTool]`, takes typed args, calls `SessionManager.SendAsync`, returns the resulting `JsonElement` directly so the MCP SDK serializes it to the client unchanged.

The `Attach` tool is the only one that doesn't go through the pipe — it allocates a pipe name, hands it to the injector (Task 31 wires this up; this task uses an `IInjectorService` interface with a stub default), then opens the session.

**Files:**
- Create: `src\SnoopMCP.Host\IInjectorService.cs`
- Create: `src\SnoopMCP.Host\NullInjectorService.cs`
- Create: `src\SnoopMCP.Host\Tools\McpTools.cs`
- Modify: `src\SnoopMCP.Host\Program.cs` (register `IInjectorService`)

- [ ] **Step 1: Injector abstraction** — `src\SnoopMCP.Host\IInjectorService.cs`

```csharp
namespace SnoopMCP.Host;

public interface IInjectorService
{
    Task InjectAsync(int processId, string pipeName, CancellationToken cancellationToken);
    Task<ProcessProbeResult> ProbeAsync(int processId, CancellationToken cancellationToken);
}

public sealed record ProcessProbeResult(
    string ProcessName,
    string RuntimeVersion,
    string FrameworkVersion,
    string Bitness);
```

- [ ] **Step 2: Null implementation** — `src\SnoopMCP.Host\NullInjectorService.cs`

`NullInjectorService` is the default the host uses until Task 31 swaps in the real one. It records the request so an attach call surfaces a clear "injector not configured" error instead of silently succeeding.

```csharp
namespace SnoopMCP.Host;

using SnoopMCP.Protocol.Errors;

public sealed class NullInjectorService : IInjectorService
{
    public Task InjectAsync(int processId, string pipeName, CancellationToken cancellationToken)
    {
        throw new SnoopMcpException(
            ErrorCode.AttachFailed,
            "Injector not configured. Task 31 wires in the real ManagedInjector.");
    }

    public Task<ProcessProbeResult> ProbeAsync(int processId, CancellationToken cancellationToken)
    {
        throw new SnoopMcpException(
            ErrorCode.AttachFailed,
            "Injector not configured. Task 31 wires in the real ManagedInjector.");
    }
}
```

- [ ] **Step 3: The tool surface** — `src\SnoopMCP.Host\Tools\McpTools.cs`

One `[McpServerTool]` method per inspection tool. Each method's signature mirrors the corresponding payload `*Request` DTO so the MCP SDK can describe args to the client.

```csharp
namespace SnoopMCP.Host.Tools;

using System.Text.Json;
using ModelContextProtocol.Server;
using SnoopMCP.Protocol.Tools;

[McpServerToolType]
public sealed class McpTools
{
    private readonly SessionManager mSession;
    private readonly IInjectorService mInjector;

    public McpTools(SessionManager session, IInjectorService injector)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(injector);
        mSession = session;
        mInjector = injector;
    }

    [McpServerTool, System.ComponentModel.Description(
        "Attach to a running WPF process by PID. Generates a pipe, injects the payload, opens the session.")]
    public async Task<JsonElement> Attach(int pid, CancellationToken cancellationToken)
    {
        string pipeName = mSession.AllocatePipeName();
        ProcessProbeResult probe = await mInjector.ProbeAsync(pid, cancellationToken);
        await mInjector.InjectAsync(pid, pipeName, cancellationToken);
        await mSession.OpenAsync(pipeName, cancellationToken);

        var payload = new
        {
            sessionId = pipeName,
            processName = probe.ProcessName,
            runtimeVersion = probe.RuntimeVersion,
            frameworkVersion = probe.FrameworkVersion,
            bitness = probe.Bitness
        };
        return SerializeResult(payload);
    }

    [McpServerTool, System.ComponentModel.Description("Detach from the current session.")]
    public async Task<JsonElement> Detach(CancellationToken cancellationToken)
    {
        await mSession.CloseAsync();
        return SerializeResult(new { ok = true });
    }

    [McpServerTool, System.ComponentModel.Description(
        "Enumerate every active visual root (window, popup, etc.).")]
    public Task<JsonElement> ListVisualRoots(CancellationToken cancellationToken) =>
        mSession.SendAsync("listVisualRoots", new ListVisualRootsRequest(), cancellationToken);

    [McpServerTool, System.ComponentModel.Description("Describe an element by id.")]
    public Task<JsonElement> DescribeElement(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync("describeElement", new DescribeElementRequest(id), cancellationToken);

    [McpServerTool, System.ComponentModel.Description("Enumerate visual or logical children.")]
    public Task<JsonElement> GetChildren(int id, string tree, CancellationToken cancellationToken) =>
        mSession.SendAsync("getChildren", new GetChildrenRequest(id, tree), cancellationToken);

    [McpServerTool, System.ComponentModel.Description("Get the visual or logical parent.")]
    public Task<JsonElement> GetParent(int id, string tree, CancellationToken cancellationToken) =>
        mSession.SendAsync("getParent", new GetParentRequest(id, tree), cancellationToken);

    [McpServerTool, System.ComponentModel.Description("Get the TemplatedParent if any.")]
    public Task<JsonElement> GetTemplatedParent(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync("getTemplatedParent", new GetTemplatedParentRequest(id), cancellationToken);

    [McpServerTool, System.ComponentModel.Description(
        "Find elements under rootId matching the AND-combined predicate.")]
    public Task<JsonElement> FindElements(
        int rootId,
        ElementPredicateDto predicate,
        CancellationToken cancellationToken) =>
        mSession.SendAsync("findElements", new FindElementsRequest(rootId, predicate), cancellationToken);

    [McpServerTool, System.ComponentModel.Description(
        "Hit test a root-relative point and return the deepest visual.")]
    public Task<JsonElement> HitTest(
        int rootId,
        double x,
        double y,
        CancellationToken cancellationToken) =>
        mSession.SendAsync("hitTest", new HitTestRequest(rootId, x, y), cancellationToken);

    [McpServerTool, System.ComponentModel.Description("Resolve a canonical path string under rootId.")]
    public Task<JsonElement> ResolvePath(
        int rootId,
        string pathString,
        CancellationToken cancellationToken) =>
        mSession.SendAsync("resolvePath", new ResolvePathRequest(rootId, pathString), cancellationToken);

    [McpServerTool, System.ComponentModel.Description("Describe the CLR type shape of the DataContext.")]
    public Task<JsonElement> DescribeDataContext(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync("describeDataContext", new DescribeDataContextRequest(id), cancellationToken);

    [McpServerTool, System.ComponentModel.Description("Read a dotted property path off the DataContext.")]
    public Task<JsonElement> ReadDataContextPath(int id, string path, CancellationToken cancellationToken) =>
        mSession.SendAsync(
            "readDataContextPath",
            new ReadDataContextPathRequest(id, path),
            cancellationToken);

    [McpServerTool, System.ComponentModel.Description("List dependency properties on an element.")]
    public Task<JsonElement> ListDependencyProperties(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync(
            "listDependencyProperties",
            new ListDependencyPropertiesRequest(id),
            cancellationToken);

    [McpServerTool, System.ComponentModel.Description("Get DP value and precedence trace.")]
    public Task<JsonElement> GetDependencyProperty(
        int id,
        string propertyName,
        CancellationToken cancellationToken) =>
        mSession.SendAsync(
            "getDependencyProperty",
            new GetDependencyPropertyRequest(id, propertyName),
            cancellationToken);

    [McpServerTool, System.ComponentModel.Description("Resolve the applied Style and its BasedOn chain.")]
    public Task<JsonElement> ResolveStyle(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync("resolveStyle", new ResolveStyleRequest(id), cancellationToken);

    [McpServerTool, System.ComponentModel.Description("Resolve the applied ControlTemplate.")]
    public Task<JsonElement> ResolveTemplate(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync("resolveTemplate", new ResolveTemplateRequest(id), cancellationToken);

    [McpServerTool, System.ComponentModel.Description("Inspect the BindingExpression on a property.")]
    public Task<JsonElement> InspectBinding(
        int id,
        string propertyName,
        CancellationToken cancellationToken) =>
        mSession.SendAsync(
            "inspectBinding",
            new InspectBindingRequest(id, propertyName),
            cancellationToken);

    [McpServerTool, System.ComponentModel.Description(
        "List every BindingExpression on an element (and optionally its descendants).")]
    public Task<JsonElement> ListBindings(
        int id,
        bool includeDescendants,
        CancellationToken cancellationToken) =>
        mSession.SendAsync(
            "listBindings",
            new ListBindingsRequest(id, includeDescendants),
            cancellationToken);

    [McpServerTool, System.ComponentModel.Description(
        "Serialize an element to XAML reflecting its current live state.")]
    public Task<JsonElement> ExportXaml(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync("exportXaml", new ExportXamlRequest(id), cancellationToken);

    private static JsonElement SerializeResult(object payload)
    {
        string json = JsonSerializer.Serialize(payload, SnoopMCP.Protocol.Wire.WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
```

- [ ] **Step 4: Register the injector in `Program.cs`** — modify `src\SnoopMCP.Host\Program.cs`

Add to `builder.Services`:

```csharp
builder.Services.AddSingleton<IInjectorService, NullInjectorService>();
```

- [ ] **Step 5: Build**

```text
dotnet build src/SnoopMCP.Host/SnoopMCP.Host.csproj -c Debug
```

Expected: build succeeds. The `WithToolsFromAssembly()` call in `Program.cs` will discover `McpTools` via its `[McpServerToolType]` attribute.

- [ ] **Step 6: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 29: Host MCP tool wrappers — attach/detach plus one wrapper per inspection tool"
```

---

## Task 30: Fork Snoop into JackalopeTechnologies and add as a git submodule

**Goal:** Fork `snoopwpf/snoopwpf` to `JackalopeTechnologies/snoopwpf` (we own the pinned commit and can patch .NET 10 / Jackalope-specific issues without waiting on upstream), then pin **our fork** under `external/snoopwpf/` as a git submodule. Reference the upstream `Snoop.GenericInjector` project via `ProjectReference` from the fork.

The fork carries two branches:
- **`main`** — tracks upstream `snoopwpf/snoopwpf:main` via the `upstream` remote. Periodic fetch + merge keeps us current.
- **`snoopmcp`** — our active branch. SnoopMCP's submodule pins commits here. .NET 10 patches, Jackalope-specific tweaks, anything we're not ready to upstream lives on this branch.

Why fork (not pure-submodule of upstream): **.NET 10 compatibility risk.** Snoop upstream may not yet target `net10.0-windows`; the fork lets us land the compatibility bump without waiting for upstream review.

Pre-check: verify Snoop's license permits this. Acceptable: MS-PL, MIT, BSD-3-Clause, Apache 2.0. Snoop has historically been MS-PL; confirm at execution time.

**Files:**
- New GitHub repo: `JackalopeTechnologies/snoopwpf` (fork)
- Add: `external/snoopwpf` (git submodule pin)
- Create/update: `.gitmodules`
- Create: `src\SnoopMCP.Injection\SnoopMCP.Injection.csproj`
- Create: `src\SnoopMCP.Injection\THIRD_PARTY_NOTICES.md`

- [ ] **Step 1: Fork upstream into the org**

```text
gh repo fork snoopwpf/snoopwpf --org JackalopeTechnologies --default-branch-only=false --clone=false
```

Expected: GitHub creates `JackalopeTechnologies/snoopwpf` as a fork. The fork preserves upstream branches and adds the parent-relationship metadata.

- [ ] **Step 2: Clone the fork temporarily and create the `snoopmcp` branch**

```text
git clone https://github.com/JackalopeTechnologies/snoopwpf.git E:/tmp/snoopwpf-fork
```

```text
git -C E:/tmp/snoopwpf-fork remote add upstream https://github.com/snoopwpf/snoopwpf.git
```

```text
git -C E:/tmp/snoopwpf-fork fetch upstream
```

Create the working branch off the fork's `main` (which should mirror upstream `main` right after the fork):

```text
git -C E:/tmp/snoopwpf-fork checkout -b snoopmcp main
```

```text
git -C E:/tmp/snoopwpf-fork push -u origin snoopmcp
```

- [ ] **Step 3: License check + commit capture**

Open `E:/tmp/snoopwpf-fork/LICENSE.txt` (or `LICENSE.md`) and confirm one of: MS-PL, MIT, BSD-3-Clause, Apache 2.0. If different (GPL, AGPL, unclear), **stop**, escalate. Fallback is NuGet-reflection consumption.

Capture the commit hash for `snoopmcp`:

```text
git -C E:/tmp/snoopwpf-fork rev-parse snoopmcp
```

The temporary clone has served its purpose — the working tree we actually use comes from the submodule (Step 5). You can leave `E:/tmp/snoopwpf-fork` for now in case you need to make fork-side commits before the submodule add.

- [ ] **Step 4: Identify the upstream injector project**

```text
git -C E:/tmp/snoopwpf-fork grep -l "class Injector"
```

Note the directory of the matching `.csproj`. The remaining steps assume the path is `Snoop.GenericInjector/Snoop.GenericInjector.csproj`. If grep returns a different path, substitute it.

- [ ] **Step 5: Add the fork as a submodule pinned to `snoopmcp`**

```text
git -C E:/GitHub/SnoopMCP submodule add -b snoopmcp https://github.com/JackalopeTechnologies/snoopwpf.git external/snoopwpf
```

The `-b snoopmcp` flag records the tracked branch in `.gitmodules` (helps when running `git submodule update --remote`).

Inside the submodule, also set the `upstream` remote so we can fetch and merge from there:

```text
git -C E:/GitHub/SnoopMCP/external/snoopwpf remote add upstream https://github.com/snoopwpf/snoopwpf.git
```

- [ ] **Step 6: Project scaffolding** — `src\SnoopMCP.Injection\SnoopMCP.Injection.csproj`

The project is a thin wrapper that re-exports the upstream injector type via `ProjectReference`. No source files of our own.

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0-windows</TargetFramework>
        <RootNamespace>SnoopMCP.Injection</RootNamespace>
        <AssemblyName>SnoopMCP.Injection</AssemblyName>
        <PlatformTarget>x64</PlatformTarget>
        <!-- Upstream Snoop source predates our analyzer rules — suppress for the wrapper. -->
        <NoWarn>$(NoWarn);CS1591</NoWarn>
        <GenerateDocumentationFile>false</GenerateDocumentationFile>
        <!-- Don't apply CodeStructure.Analyzers to upstream code; we don't own its style. -->
        <RunAnalyzers>false</RunAnalyzers>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\external\snoopwpf\Snoop.GenericInjector\Snoop.GenericInjector.csproj" />
    </ItemGroup>
</Project>
```

```text
dotnet sln SnoopMCP.sln add src/SnoopMCP.Injection/SnoopMCP.Injection.csproj
```

**Fallback if the upstream csproj doesn't multi-target with `net10.0-windows`:**

1. Build the upstream project standalone — `dotnet build external/snoopwpf/Snoop.GenericInjector/Snoop.GenericInjector.csproj -c Release`
2. In `SnoopMCP.Injection.csproj`, replace the `ProjectReference` with a `<Reference Include="..\..\external\snoopwpf\Snoop.GenericInjector\bin\Release\<tfm>\Snoop.GenericInjector.dll" />`
3. Add an MSBuild `<Target Name="BuildUpstream" BeforeTargets="Build">` that invokes the upstream build so clean checkouts still work

- [ ] **Step 7: Third-party notices** — `src\SnoopMCP.Injection\THIRD_PARTY_NOTICES.md`

```markdown
# Third-Party Notices

This project references a fork of the snoopwpf project as a git submodule under
`external/snoopwpf/`. No source from snoopwpf is copied into this tree; the
submodule pin records the exact commit on our fork's `snoopmcp` branch that we
build against.

- Upstream repository: https://github.com/snoopwpf/snoopwpf
- Our fork: https://github.com/JackalopeTechnologies/snoopwpf
- Pinned commit: <paste output of `git -C E:/GitHub/SnoopMCP/external/snoopwpf rev-parse HEAD`>
- Pinned branch: snoopmcp
- License: <paste license name from external/snoopwpf/LICENSE file>
- License text: see `external/snoopwpf/LICENSE.txt`

## Maintenance: keeping the fork in sync with upstream

The fork carries two branches:

- `main` — tracks upstream `snoopwpf/snoopwpf:main`. Update with periodic merges.
- `snoopmcp` — our active branch. SnoopMCP's submodule pins commits here.

To sync upstream changes into our fork's `snoopmcp`:

    cd E:/GitHub/SnoopMCP/external/snoopwpf
    git fetch upstream
    git checkout main
    git merge upstream/main
    git push origin main
    git checkout snoopmcp
    git merge main
    git push origin snoopmcp

To bump SnoopMCP's submodule pin after fork commits land:

    git -C E:/GitHub/SnoopMCP submodule update --remote external/snoopwpf
    git -C E:/GitHub/SnoopMCP add external/snoopwpf
    git -C E:/GitHub/SnoopMCP commit -F msg.txt

To upstream a fork-side patch:

    cd E:/GitHub/SnoopMCP/external/snoopwpf
    git checkout main
    git cherry-pick <commit-on-snoopmcp>
    git push origin main
    gh pr create --repo snoopwpf/snoopwpf --base main --head JackalopeTechnologies:main
```

- [ ] **Step 8: Build the wrapper**

```text
dotnet build src/SnoopMCP.Injection/SnoopMCP.Injection.csproj -c Debug
```

Expected: build succeeds and produces `SnoopMCP.Injection.dll` plus `Snoop.GenericInjector.dll` (the latter from the fork) in the output. If the upstream project on the `snoopmcp` branch fails to build under .NET 10's SDK, the fix lives on the fork: commit the framework-bump (or whatever the compatibility patch is) to `snoopmcp`, push, then `git submodule update --remote` and rebuild.

- [ ] **Step 9: Commit**

Stage the submodule entry, the `.gitmodules` file, and our wrapper:

```text
git -C E:/GitHub/SnoopMCP add .gitmodules external src/SnoopMCP.Injection
git -C E:/GitHub/SnoopMCP commit -F E:/tmp/msg-task-28.txt
```

`E:/tmp/msg-task-28.txt`:

```text
Task 30: fork snoopwpf into JackalopeTechnologies and pin via git submodule

External dependency model: our fork at github.com/JackalopeTechnologies/snoopwpf
carries main (tracking upstream) + snoopmcp (our active branch). The submodule
under external/snoopwpf pins commits on snoopmcp; SnoopMCP.Injection is a thin
wrapper csproj that ProjectReferences the upstream Snoop.GenericInjector
project so we can patch .NET 10 issues without waiting on upstream review.
```

---

## Task 31: Host — InjectorService + ProcessProbe

**Goal:** Replace `NullInjectorService` (Task 29) with a real implementation that wraps the submoduled `Snoop.GenericInjector.Injector` and a process probe. Failures map to structured `ErrorCode`s: `AttachFailed` (process not found / not WPF / wrong arch), `PayloadLoadFailed` (assembly conflict in target's load context), `AccessDenied` (elevation mismatch).

`ProcessProbe` inspects the target's process to confirm bitness, .NET runtime version (.NET 10+), and that it is a WPF host (`PresentationFramework.dll` loaded).

**Files:**
- Create: `src\SnoopMCP.Host\Injection\ProcessProbe.cs`
- Create: `src\SnoopMCP.Host\Injection\InjectorService.cs`
- Modify: `src\SnoopMCP.Host\Program.cs` (swap `NullInjectorService` for `InjectorService`)
- Modify: `src\SnoopMCP.Host\SnoopMCP.Host.csproj` (add `SnoopMCP.Injection` reference)

- [ ] **Step 1: Add the injection project reference** — modify `src\SnoopMCP.Host\SnoopMCP.Host.csproj`

```xml
<ItemGroup>
    <ProjectReference Include="..\SnoopMCP.Protocol\SnoopMCP.Protocol.csproj" />
    <ProjectReference Include="..\SnoopMCP.Injection\SnoopMCP.Injection.csproj" />
    <ProjectReference Include="..\SnoopMCP.Payload\SnoopMCP.Payload.csproj">
        <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
        <OutputItemType>Content</OutputItemType>
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </ProjectReference>
</ItemGroup>
```

The `Payload` reference with `ReferenceOutputAssembly=false` builds `SnoopMCP.Payload.dll` alongside the host (so the injector can find it at runtime) without making the host load it. The host is fully decoupled from payload types at the CLR level.

- [ ] **Step 2: `ProcessProbe`** — `src\SnoopMCP.Host\Injection\ProcessProbe.cs`

```csharp
namespace SnoopMCP.Host.Injection;

using System.Diagnostics;
using System.Runtime.InteropServices;
using SnoopMCP.Protocol.Errors;

public sealed class ProcessProbe
{
    public ProcessProbeResult Probe(int processId)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException ex)
        {
            throw new SnoopMcpException(
                ErrorCode.AttachFailed,
                $"Process id {processId} not found.",
                ex);
        }

        using (process)
        {
            string processName = process.ProcessName;
            string bitness = DetermineBitness(process);
            (string runtime, string framework) = DetermineRuntime(process);
            EnsureWpfLoaded(process);

            return new ProcessProbeResult(
                ProcessName: processName,
                RuntimeVersion: runtime,
                FrameworkVersion: framework,
                Bitness: bitness);
        }
    }

    private static string DetermineBitness(Process process)
    {
        bool isWow64 = false;
        IntPtr handle = process.Handle;
        bool ok = IsWow64Process(handle, out isWow64);
        if (!ok)
        {
            throw new SnoopMcpException(
                ErrorCode.AccessDenied,
                "Could not query process bitness — usually means elevation mismatch (target Admin, host not).");
        }
        bool osIs64 = Environment.Is64BitOperatingSystem;
        string bitness = (osIs64 && !isWow64) ? "x64" : "x86";
        return bitness;
    }

    private static (string Runtime, string Framework) DetermineRuntime(Process process)
    {
        string runtime = "Unknown";
        string framework = "Unknown";
        foreach (ProcessModule module in process.Modules)
        {
            string name = module.ModuleName ?? string.Empty;
            if (string.Equals(name, "hostfxr.dll", StringComparison.OrdinalIgnoreCase))
            {
                runtime = module.FileVersionInfo.FileVersion ?? "Unknown";
            }
            if (string.Equals(name, "PresentationFramework.dll", StringComparison.OrdinalIgnoreCase))
            {
                framework = module.FileVersionInfo.FileVersion ?? "Unknown";
            }
        }
        return (runtime, framework);
    }

    private static void EnsureWpfLoaded(Process process)
    {
        bool found = false;
        foreach (ProcessModule module in process.Modules)
        {
            if (string.Equals(
                module.ModuleName,
                "PresentationFramework.dll",
                StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }
        }
        if (!found)
        {
            throw new SnoopMcpException(
                ErrorCode.AttachFailed,
                "Target process is not a WPF app (PresentationFramework.dll not loaded).");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWow64Process(IntPtr processHandle, out bool wow64Process);
}
```

- [ ] **Step 3: `InjectorService`** — `src\SnoopMCP.Host\Injection\InjectorService.cs`

The actual call into the upstream `Injector` depends on its surface — at execution time, inspect `external/snoopwpf/Snoop.GenericInjector/...` to find the public entry method and its namespace. The historical shape has been `Snoop.GenericInjector.Injector.Inject(int processId, string payloadPath, string typeName, string methodName, string args)`. Adjust both the namespace and the call below to match what you find in the pinned submodule commit.

```csharp
namespace SnoopMCP.Host.Injection;

using Microsoft.Extensions.Logging;
using SnoopMCP.Protocol.Errors;

public sealed class InjectorService : IInjectorService
{
    private readonly ProcessProbe mProbe;
    private readonly ILogger<InjectorService> mLogger;

    public InjectorService(ProcessProbe probe, ILogger<InjectorService> logger)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(logger);
        mProbe = probe;
        mLogger = logger;
    }

    public Task<ProcessProbeResult> ProbeAsync(int processId, CancellationToken cancellationToken)
    {
        return Task.FromResult(mProbe.Probe(processId));
    }

    public Task InjectAsync(int processId, string pipeName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);

        string payloadPath = LocatePayloadDll();
        try
        {
            // Replace the namespace + signature below with what you find in the submoduled
            // upstream Snoop.GenericInjector at execution time.
            Snoop.GenericInjector.Injector.Inject(
                processId,
                payloadPath,
                "SnoopMCP.Payload.PayloadEntryPoint",
                "Inject",
                pipeName);
            mLogger.LogInformation("Payload injected into pid {Pid} on pipe {PipeName}.", processId, pipeName);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new SnoopMcpException(
                ErrorCode.AccessDenied,
                "Access denied — the target may be running elevated while the host is not.",
                ex);
        }
        catch (Exception ex)
        {
            throw new SnoopMcpException(
                ErrorCode.PayloadLoadFailed,
                $"Payload injection failed: {ex.GetType().Name}: {ex.Message}",
                ex);
        }
        return Task.CompletedTask;
    }

    private static string LocatePayloadDll()
    {
        string hostDir = AppContext.BaseDirectory;
        string candidate = Path.Combine(hostDir, "SnoopMCP.Payload.dll");
        if (!File.Exists(candidate))
        {
            throw new SnoopMcpException(
                ErrorCode.PayloadLoadFailed,
                $"SnoopMCP.Payload.dll not found at {candidate}. Verify the project reference in Task 31 Step 1.");
        }
        return candidate;
    }
}
```

- [ ] **Step 4: Swap the registration in `Program.cs`** — modify `src\SnoopMCP.Host\Program.cs`

Replace the `NullInjectorService` registration with the real one:

```csharp
builder.Services.AddSingleton<SnoopMCP.Host.Injection.ProcessProbe>();
builder.Services.AddSingleton<IInjectorService, SnoopMCP.Host.Injection.InjectorService>();
```

- [ ] **Step 5: Build**

```text
dotnet build src/SnoopMCP.Host/SnoopMCP.Host.csproj -c Debug
```

Expected: build succeeds. If the call to `Injector.Inject` is unresolved, the upstream namespace or signature differs from the historical one — inspect `external/snoopwpf/Snoop.GenericInjector/...` at the pinned commit and update the call accordingly.

- [ ] **Step 6: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 31: InjectorService + ProcessProbe — wraps submoduled Snoop injector with structured failure codes"
```

---

## Task 32: End-to-end integration test

**Goal:** One test that spawns `SampleWpfApp`, attaches via the host, calls every read-only tool, and asserts the response shape. This is the gate that proves the v1 pipe is end-to-end working.

The test does *not* validate every tool's semantic correctness (that's covered by per-tool unit tests). It validates that each tool returns a response of the expected JSON shape, with no errors, against a real target.

**Files:**
- Create: `tests\SnoopMCP.IntegrationTests\SnoopMCP.IntegrationTests.csproj`
- Create: `tests\SnoopMCP.IntegrationTests\EndToEndTests.cs`

- [ ] **Step 1: Test project** — `tests\SnoopMCP.IntegrationTests\SnoopMCP.IntegrationTests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0-windows</TargetFramework>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <RootNamespace>SnoopMCP.IntegrationTests</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="xunit.v3" Version="1.0.0" />
        <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
        <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\src\SnoopMCP.Host\SnoopMCP.Host.csproj" />
        <ProjectReference Include="..\..\samples\SampleWpfApp\SampleWpfApp.csproj">
            <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
            <OutputItemType>Content</OutputItemType>
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </ProjectReference>
    </ItemGroup>
</Project>
```

```text
dotnet sln SnoopMCP.sln add tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj
```

- [ ] **Step 2: End-to-end test** — `tests\SnoopMCP.IntegrationTests\EndToEndTests.cs`

```csharp
namespace SnoopMCP.IntegrationTests;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SnoopMCP.Host;
using SnoopMCP.Host.Injection;
using SnoopMCP.Host.Tools;
using Xunit;

public sealed class EndToEndTests : IAsyncLifetime
{
    private Process? mSampleProcess;
    private SessionManager? mSession;
    private McpTools? mTools;

    public async ValueTask InitializeAsync()
    {
        string samplePath = Path.Combine(
            AppContext.BaseDirectory,
            "SampleWpfApp.exe");
        Assert.True(File.Exists(samplePath), $"SampleWpfApp.exe not found at {samplePath}.");

        mSampleProcess = Process.Start(new ProcessStartInfo
        {
            FileName = samplePath,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Failed to start SampleWpfApp.");

        // Allow main window to appear.
        await Task.Delay(TimeSpan.FromSeconds(3));

        mSession = new SessionManager(NullLogger<SessionManager>.Instance, NullLoggerFactory.Instance);
        var probe = new ProcessProbe();
        var injector = new InjectorService(probe, NullLogger<InjectorService>.Instance);
        mTools = new McpTools(mSession, injector);

        await mTools.Attach(mSampleProcess.Id, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (mTools is not null)
        {
            try
            {
                await mTools.Detach(CancellationToken.None);
            }
            catch
            {
            }
        }
        if (mSampleProcess is not null && !mSampleProcess.HasExited)
        {
            mSampleProcess.Kill(entireProcessTree: true);
            mSampleProcess.WaitForExit(5000);
            mSampleProcess.Dispose();
        }
        if (mSession is not null)
        {
            await mSession.DisposeAsync();
        }
    }

    [Fact]
    public async Task EveryReadOnlyTool_ReturnsShapedResponse()
    {
        Assert.NotNull(mTools);

        JsonElement roots = await mTools!.ListVisualRoots(CancellationToken.None);
        JsonElement rootArr = roots.GetProperty("roots");
        Assert.True(rootArr.GetArrayLength() > 0);
        int rootElementId = rootArr[0].GetProperty("rootElementId").GetInt32();

        JsonElement desc = await mTools.DescribeElement(rootElementId, CancellationToken.None);
        Assert.True(desc.TryGetProperty("type", out _));
        Assert.True(desc.TryGetProperty("bounds", out _));
        Assert.True(desc.TryGetProperty("path", out _));

        JsonElement kids = await mTools.GetChildren(rootElementId, "visual", CancellationToken.None);
        Assert.True(kids.TryGetProperty("children", out _));

        JsonElement parent = await mTools.GetParent(rootElementId, "visual", CancellationToken.None);
        Assert.True(parent.TryGetProperty("parent", out _));

        JsonElement templated = await mTools.GetTemplatedParent(rootElementId, CancellationToken.None);
        Assert.True(templated.TryGetProperty("templatedParent", out _));

        JsonElement found = await mTools.FindElements(
            rootElementId,
            new SnoopMCP.Protocol.Tools.ElementPredicateDto { Type = "Button" },
            CancellationToken.None);
        Assert.True(found.TryGetProperty("matches", out _));

        JsonElement hit = await mTools.HitTest(rootElementId, 50, 50, CancellationToken.None);
        Assert.True(hit.TryGetProperty("element", out _));

        JsonElement path = desc.GetProperty("path");
        JsonElement resolved = await mTools.ResolvePath(
            rootElementId,
            path.GetString() ?? "/Window",
            CancellationToken.None);
        Assert.True(resolved.TryGetProperty("element", out _));

        JsonElement dc = await mTools.DescribeDataContext(rootElementId, CancellationToken.None);
        Assert.True(dc.TryGetProperty("dataContext", out _));

        JsonElement dcRead = await mTools.ReadDataContextPath(
            rootElementId,
            "SelectedCustomer.Name",
            CancellationToken.None);
        Assert.True(dcRead.TryGetProperty("pathReachable", out _));

        JsonElement dps = await mTools.ListDependencyProperties(rootElementId, CancellationToken.None);
        Assert.True(dps.GetProperty("properties").GetArrayLength() > 0);

        JsonElement widthDp = await mTools.GetDependencyProperty(
            rootElementId,
            "Width",
            CancellationToken.None);
        Assert.True(widthDp.TryGetProperty("winningSource", out _));

        JsonElement style = await mTools.ResolveStyle(rootElementId, CancellationToken.None);
        Assert.True(style.TryGetProperty("setters", out _));

        JsonElement tpl = await mTools.ResolveTemplate(rootElementId, CancellationToken.None);
        Assert.True(tpl.TryGetProperty("namedParts", out _));

        JsonElement binding = await mTools.InspectBinding(
            rootElementId,
            "DataContext",
            CancellationToken.None);
        Assert.True(binding.TryGetProperty("state", out _));

        JsonElement listed = await mTools.ListBindings(rootElementId, true, CancellationToken.None);
        Assert.True(listed.TryGetProperty("bindings", out _));

        JsonElement xaml = await mTools.ExportXaml(rootElementId, CancellationToken.None);
        Assert.True(xaml.TryGetProperty("xaml", out _));
        Assert.True(xaml.GetProperty("byteCount").GetInt32() > 0);
    }
}
```

- [ ] **Step 3: Run the integration test**

```text
dotnet test tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj
```

Expected: test passes. Common failure modes:
- **SampleWpfApp doesn't start** → check the path and that the sample built (Task 2 Step 11).
- **Attach times out** → the injector did not complete; inspect the host log on stderr.
- **`PresentationFramework.dll` not loaded** → the sample hasn't fully initialized when probe runs; raise the 3-second initial delay.
- **`UnauthorizedAccessException`** → the test runner is non-elevated and the sample inherited elevation somewhere; rerun from a non-elevated terminal.

- [ ] **Step 4: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 32: End-to-end integration test exercising every v1 read-only tool"
```

---

## Task 33: README + usage docs

**Goal:** Make it possible for a new user to attach to a WPF process and use the tools without reading the source. Quickstart + a worked example + a minimal MCP client config snippet.

**Files:**
- Create: `docs\README.md`

- [ ] **Step 1: Write the README** — `docs\README.md`

```markdown
# SnoopMCP

An MCP server that injects a payload DLL into a running WPF process so an LLM
client can diagnose styling, binding, and dependency-property resolution
problems live. v1 is read-only.

## Quickstart

### Prerequisites
- Windows 10/11
- .NET 10 SDK
- A WPF app to attach to, running on .NET 10+ (x64)
- The host process must run at the **same elevation** as the target — if the
  target runs as Administrator, run the host as Administrator too.

### Clone with submodules

This repo references snoopwpf as a git submodule under `external/snoopwpf/`. A fresh clone needs:

```text
git clone --recurse-submodules https://github.com/JackalopeTechnologies/SnoopMCP.git
```

If you already cloned without `--recurse-submodules`, run:

```text
git submodule update --init --recursive
```

### Build

```text
dotnet build SnoopMCP.sln -c Release
```

The host is `src\SnoopMCP.Host\bin\Release\net10.0-windows\SnoopMCP.Host.exe`.

### Attach via the MCP client

Configure your MCP client with a `command` and `args` pointing at the host:

```json
{
  "mcpServers": {
    "snoopmcp": {
      "command": "C:\\path\\to\\SnoopMCP.Host.exe"
    }
  }
}
```

Then ask the LLM:

> Attach to the WPF process with PID 12345, list its visual roots, then describe
> the Save button.

The LLM will call:
1. `attach(pid: 12345)` — returns session info
2. `listVisualRoots()` — returns `{ roots: [...] }`
3. `findElements(rootId, predicate: { type: "Button", name: "Save" })`
4. `describeElement(matchedId)`

## Tool surface

Nineteen read-only tools, plus `attach`/`detach`:

| Tool | Use it for |
|---|---|
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
| `AttachFailed` | Target not found, not WPF, or wrong bitness |
| `PayloadLoadFailed` | Assembly conflict in target's load context |
| `DispatcherTimeout` | Per-call timeout (5s default) |
| `SessionLost` | Target exited or pipe closed |
| `AccessDenied` | Elevation mismatch |
| `ElementExpired` | Element id has been garbage collected |
| `InvalidArgument` | Bad tool argument |
| `PathParseError` | Malformed path string |

## Known v1 limitations

- **Read-only.** No property writes, no method invocation, no scripting.
- **One target at a time.** Phase 2 may add multi-target.
- **x64 only.** x86/ARM64 targets will be rejected.
- **No cross-process discovery.** Use `Get-Process` to find PIDs.
- **No persistent reattach.** Sessions die with the target process.
- **`textContains` searches capped visible text** (~200 chars per element).
- **`propertyEquals` does not support attached properties.**
- **`recentTraceLines` always empty** on `inspectBinding`. Phase 2 will wire up `PresentationTraceSources`.

See [`docs/superpowers/specs/2026-05-27-snoopmcp-investigation-design.md`](superpowers/specs/2026-05-27-snoopmcp-investigation-design.md)
for the Phase 2 candidate list.

## License

The `Snoop.GenericInjector` source pinned as a git submodule under
`external/snoopwpf/` is from the snoopwpf project and retains its upstream
license; see `src/SnoopMCP.Injection/THIRD_PARTY_NOTICES.md`. Everything else is
Copyright (c) 2026 Jackalope Technologies.
```

- [ ] **Step 2: Commit**

```text
git -C E:/GitHub/SnoopMCP add .
git -C E:/GitHub/SnoopMCP commit -m "Task 33: README — quickstart, tool surface, error codes, v1 limitations"
```

---

<!-- APPEND-NEXT-TASKS-HERE -->

