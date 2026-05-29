# SnoopMCP Installer — PR 3: WiX MSI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A per-user WiX MSI that installs the SnoopMCP host + its `injector/`/`payload/` + the management CLI + the sample app to `%LocalAppData%\SnoopMCP`, registers the MCP server in Claude Code + VS Code, creates the per-user logon autostart task, and starts the host — all via the `SnoopMCP.Cli` verbs from PR 2 invoked as custom actions.

**Architecture:** A PowerShell packaging script (`build-installer.ps1`) assembles the full payload tree — `dotnet publish` the host (which builds + stages `injector/`+`payload/` into its bin), copy those two folders into the publish dir, publish the CLI alongside, publish the sample under `samples/` — then runs `wix build` (WiX 5 CLI, no `.wixproj`) over a `Package.wxs` that `<Files>`-harvests the tree, adds a Start-menu shortcut, and wires install/uninstall custom actions to `SnoopMCP.Cli`. Mirrors SaddleRAG's installer with three swaps: perMachine→perUser, ServiceInstall→`install-autostart` CA, and no Mongo/Ollama/GPU dialogs.

**Tech Stack:** WiX 5.0.2 (`wix` global tool, `WixToolset.Util.wixext` + `WixToolset.UI.wixext`), PowerShell, `dotnet publish`.

**Spec:** `docs/superpowers/specs/2026-05-29-snoopmcp-installer-design.md` (§7).

**Verification boundary (important):** WiX is not unit-testable, and a real install mutates the user's live Claude Code / VS Code configs and registers a logon task — so it is NOT run automatically. The automated gate for every task is **`build-installer.ps1` produces `SnoopMCP.msi`** (which requires the host's native-injector build via VS C++ workload + the WiX toolset, both present). The install/uninstall *behavior* is verified by the human via the documented checklist (Task 4). The logic underneath the installer (writers, CLI, `/health`) is already unit-tested + smoke-checked in PR 1 / PR 2.

**v1.1 simplifications vs spec (flagged):**
- The spec mentioned a `/health` readiness gate (`Return="check"`, poll ~60s) after `start`. This plan makes `start` `Return="ignore"` (best-effort): the durable guarantees are the registered clients + the logon task (which starts the host every login); a hard poll-gate would need a separate PS script and risks failing the install on a slow/blocked host. The user confirms reachability post-install with `SnoopMCP.Cli status`. A poll-gate is a v1.2 nicety.
- Publishes are **framework-dependent** (target audience has the .NET 10 + Windows Desktop runtime — they build WPF apps). Smaller MSI than SaddleRAG's self-contained build.

---

## WiX / analyzer notes
- The `.wxs` and `.ps1` are not under the C# analyzer ruleset. Follow WiX 5 conventions (mirror SaddleRAG's `Package.wxs`). PowerShell: no `cd` (use `$PSScriptRoot`-relative absolute paths); `$ErrorActionPreference = "Stop"` + explicit `$LASTEXITCODE` checks after each native call.
- perUser install: deferred custom actions run as the installing user (no elevation, no impersonation gymnastics). `Impersonate="yes"` is set explicitly to be unambiguous.
- Fixed GUIDs (do not regenerate — the UpgradeCode identifies the product across versions): UpgradeCode `7E3B1C2A-9D4F-4A6E-B8C1-2F5A9E0D3B47`; shortcut component `1B9D4C6E-3A2F-4E7B-9C5D-8F1A0E2B6D34`.

---

## File Structure
- `SnoopMCP.Installer/Package.wxs` — the WiX package (perUser, `LocalAppDataFolder` install, `<Files>` harvest, Start-menu shortcut, install/uninstall CAs).
- `SnoopMCP.Installer/build-installer.ps1` — assembles the publish tree + runs `wix build`.
- `SnoopMCP.Installer/License.rtf` — minimal license shown by `WixUI_Minimal`.
- `.gitignore` (modify) — ignore the installer's `stage/`, `*.msi`, `*.wixpdb` build artifacts.
- `README.md` (modify) — Install section + manual verification checklist.

---

### Task 1: Packaging script + minimal MSI (files only)

**Files:**
- Create: `SnoopMCP.Installer/build-installer.ps1`, `SnoopMCP.Installer/Package.wxs`, `SnoopMCP.Installer/License.rtf`
- Modify: `.gitignore`

- [ ] **Step 1: Write `License.rtf`** — a minimal RTF (WixUI_Minimal requires a license file). `SnoopMCP.Installer/License.rtf`:

```rtf
{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\f0\fs20
SnoopMCP\par
Copyright (c) 2026 Jackalope Technologies, Inc.\par
\par
The bundled snoopwpf injector is licensed under the Microsoft Public License (Ms-PL); see THIRD_PARTY_NOTICES. All other components are proprietary to Jackalope Technologies, Inc.\par
}
```

- [ ] **Step 2: Write the minimal `Package.wxs`** (perUser, harvest only — shortcut + CAs come in later tasks). `SnoopMCP.Installer/Package.wxs`:

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui"
     xmlns:util="http://wixtoolset.org/schemas/v4/wxs/util">

    <Package Name="SnoopMCP"
             Manufacturer="Jackalope Technologies, Inc."
             Version="$(var.Version)"
             UpgradeCode="7E3B1C2A-9D4F-4A6E-B8C1-2F5A9E0D3B47"
             Scope="perUser">

        <MajorUpgrade DowngradeErrorMessage="A newer version of SnoopMCP is already installed."
                      AllowSameVersionUpgrades="yes" />
        <MediaTemplate EmbedCab="yes" />

        <Property Id="MsiLogging" Value="voicewarmupx!" />

        <StandardDirectory Id="LocalAppDataFolder">
            <Directory Id="INSTALLFOLDER" Name="SnoopMCP" />
        </StandardDirectory>

        <ComponentGroup Id="PublishOutput" Directory="INSTALLFOLDER">
            <Files Include="$(var.PublishDir)\**" Exclude="$(var.PublishDir)\**\*.pdb" />
        </ComponentGroup>

        <Feature Id="Main" Title="SnoopMCP" Level="1">
            <ComponentGroupRef Id="PublishOutput" />
        </Feature>

        <ui:WixUI Id="WixUI_Minimal" />
        <WixVariable Id="WixUILicenseRtf" Value="License.rtf" />

    </Package>

</Wix>
```

(`util` namespace is declared now because Task 3's custom actions reference `Wix4UtilCA_X86`; declaring it early keeps the namespace stable. It is harmless before then.)

- [ ] **Step 3: Write `build-installer.ps1`** `SnoopMCP.Installer/build-installer.ps1`:

```powershell
param(
    [string]$Version = "1.1.0",
    [string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"

$here = $PSScriptRoot
$root = (Resolve-Path (Join-Path $here "..")).Path
$stage = Join-Path $here "stage"

$hostProj   = Join-Path $root "src/SnoopMCP.Host/SnoopMCP.Host.csproj"
$cliProj    = Join-Path $root "src/SnoopMCP.Cli/SnoopMCP.Cli.csproj"
$sampleProj = Join-Path $root "samples/SampleWpfApp/SampleWpfApp.csproj"
$hostBin    = Join-Path $root "src/SnoopMCP.Host/bin/$Configuration/net10.0-windows"

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

# Build the host first so its AfterBuild copy targets stage injector/ + payload/ into bin.
dotnet build $hostProj -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Host build failed." }

# Publish host (exe + framework-dependent deps) into the stage root.
dotnet publish $hostProj -c $Configuration --no-build -o $stage
if ($LASTEXITCODE -ne 0) { throw "Host publish failed." }

# injector/ + payload/ are loose staged folders that publish does not carry — copy them in.
Copy-Item (Join-Path $hostBin "injector") (Join-Path $stage "injector") -Recurse -Force
Copy-Item (Join-Path $hostBin "payload")  (Join-Path $stage "payload")  -Recurse -Force

# Publish the CLI alongside the host (shared deps overwrite identically).
dotnet publish $cliProj -c $Configuration -o $stage
if ($LASTEXITCODE -ne 0) { throw "CLI publish failed." }

# Publish the sample app under samples/.
dotnet publish $sampleProj -c $Configuration -o (Join-Path $stage "samples")
if ($LASTEXITCODE -ne 0) { throw "Sample publish failed." }

# Build the MSI.
$msi = Join-Path $here "SnoopMCP.msi"
wix build (Join-Path $here "Package.wxs") `
    -d Version=$Version `
    -d PublishDir=$stage `
    -ext WixToolset.Util.wixext `
    -ext WixToolset.UI.wixext `
    -arch x64 `
    -b $here `
    -o $msi
if ($LASTEXITCODE -ne 0) { throw "wix build failed." }

Write-Host "Built $msi (version $Version)."
```

(`--no-build` on the host publish reuses the build from the prior step so the injector/payload staging in `$hostBin` is the same one we copy. If `--no-build` publish errors on a fresh tree, drop it — the prior `dotnet build` already populated `$hostBin`.)

- [ ] **Step 4: Add `.gitignore` entries.** Append to the repo-root `.gitignore` (read it first; add only if absent):

```gitignore
# SnoopMCP installer build artifacts
SnoopMCP.Installer/stage/
SnoopMCP.Installer/*.msi
SnoopMCP.Installer/*.wixpdb
```

- [ ] **Step 5: Build the MSI (the automated gate).**

Run (PowerShell): `pwsh -NoProfile -File E:/GitHub/SnoopMCP/SnoopMCP.Installer/build-installer.ps1 -Version 1.1.0`
Expected: ends with `Built ...SnoopMCP.msi (version 1.1.0).` and exit 0.

- [ ] **Step 6: Verify the stage tree is complete.**

Confirm these exist under `SnoopMCP.Installer/stage/`: `SnoopMCP.Host.exe`, `SnoopMCP.Cli.exe`, `injector/Snoop.InjectorLauncher.x64.exe`, `payload/SnoopMCP.Payload.dll`, `samples/SampleWpfApp.exe`. (Use Glob/Read; do NOT commit `stage/` or the MSI — they are gitignored.)

- [ ] **Step 7: Confirm artifacts are untracked.**

Run: `git -C E:/GitHub/SnoopMCP status --short`
Expected: `Package.wxs`, `build-installer.ps1`, `License.rtf`, `.gitignore` show as changes; `stage/`, `SnoopMCP.msi`, `*.wixpdb` do NOT appear (gitignored).

- [ ] **Step 8: Commit (source only).**

Stage `SnoopMCP.Installer/Package.wxs`, `SnoopMCP.Installer/build-installer.ps1`, `SnoopMCP.Installer/License.rtf`, `.gitignore`. Commit via E:/tmp/pr3-t1-msg.txt:
```
A3 PR3: installer scaffold — packaging script + files-only MSI

build-installer.ps1 assembles the publish tree (host + injector/ + payload/ +
CLI + samples/) and runs wix build. Package.wxs is a perUser MSI installing to
%LocalAppData%\SnoopMCP via a <Files> harvest, WixUI_Minimal. Shortcut + custom
actions land in later tasks. stage/ + *.msi + *.wixpdb gitignored.
```

---

### Task 2: Start-menu shortcut for the sample app

**Files:**
- Modify: `SnoopMCP.Installer/Package.wxs`

- [ ] **Step 1: Add the shortcut directory + component.** In `Package.wxs`, add a second `StandardDirectory` after the `LocalAppDataFolder` block:

```xml
        <StandardDirectory Id="ProgramMenuFolder">
            <Directory Id="AppShortcutFolder" Name="SnoopMCP" />
        </StandardDirectory>
```

Add this component before the `<Feature>` element:

```xml
        <Component Id="SampleShortcut" Directory="AppShortcutFolder"
                   Guid="1B9D4C6E-3A2F-4E7B-9C5D-8F1A0E2B6D34">
            <Shortcut Id="SampleStartMenuShortcut"
                      Name="SnoopMCP Sample App"
                      Description="A WPF app with authored styling/binding bugs to try SnoopMCP against."
                      Target="[INSTALLFOLDER]samples\SampleWpfApp.exe"
                      WorkingDirectory="INSTALLFOLDER" />
            <RemoveFolder Id="RemoveAppShortcutFolder" Directory="AppShortcutFolder" On="uninstall" />
            <RegistryValue Root="HKCU" Key="Software\JackalopeTechnologies\SnoopMCP"
                           Name="installed" Type="integer" Value="1" KeyPath="yes" />
        </Component>
```

(A per-user shortcut component needs an HKCU `RegistryValue` keypath — the shortcut's target lives in a different component, so the registry value is the stable keypath WiX requires. `RemoveFolder` cleans up the Start-menu folder on uninstall.)

- [ ] **Step 2: Reference the component in the feature.** Change the `<Feature>` to include it:

```xml
        <Feature Id="Main" Title="SnoopMCP" Level="1">
            <ComponentGroupRef Id="PublishOutput" />
            <ComponentRef Id="SampleShortcut" />
        </Feature>
```

- [ ] **Step 3: Build the MSI.**

Run: `pwsh -NoProfile -File E:/GitHub/SnoopMCP/SnoopMCP.Installer/build-installer.ps1 -Version 1.1.0`
Expected: `Built ...SnoopMCP.msi`. (If `wix build` errors that the shortcut Target references a file not in a referenced component, confirm `samples\SampleWpfApp.exe` is harvested by `PublishOutput` — it is, from `stage/samples/`.)

- [ ] **Step 4: Commit.** Stage `Package.wxs`. Commit via E:/tmp/pr3-t2-msg.txt:
```
A3 PR3: Start-menu shortcut for SampleWpfApp

perUser shortcut component (HKCU registry keypath, RemoveFolder on uninstall)
targeting samples\SampleWpfApp.exe, so a new user has a known-buggy WPF target
to try SnoopMCP against from the Start menu.
```

---

### Task 3: Install / uninstall custom actions

**Files:**
- Modify: `SnoopMCP.Installer/Package.wxs`

- [ ] **Step 1: Add the custom actions + SetProperty command lines.** In `Package.wxs`, add before the `<Feature>` element. Each deferred CA pairs with a `SetProperty` that builds its command (the CLI sits at `[INSTALLFOLDER]SnoopMCP.Cli.exe`):

```xml
        <!-- Install: register clients, create the logon task, start the host (best-effort). -->
        <SetProperty Id="RegisterClients" Before="RegisterClients" Sequence="execute"
                     Value="&quot;[INSTALLFOLDER]SnoopMCP.Cli.exe&quot; register-clients" />
        <CustomAction Id="RegisterClients" BinaryRef="Wix4UtilCA_X86" DllEntry="WixQuietExec"
                      Execute="deferred" Return="ignore" Impersonate="yes" />

        <SetProperty Id="InstallAutostart" Before="InstallAutostart" Sequence="execute"
                     Value="&quot;[INSTALLFOLDER]SnoopMCP.Cli.exe&quot; install-autostart" />
        <CustomAction Id="InstallAutostart" BinaryRef="Wix4UtilCA_X86" DllEntry="WixQuietExec"
                      Execute="deferred" Return="ignore" Impersonate="yes" />

        <SetProperty Id="StartHost" Before="StartHost" Sequence="execute"
                     Value="&quot;[INSTALLFOLDER]SnoopMCP.Cli.exe&quot; start" />
        <CustomAction Id="StartHost" BinaryRef="Wix4UtilCA_X86" DllEntry="WixQuietExec"
                      Execute="deferred" Return="ignore" Impersonate="yes" />

        <!-- Uninstall: stop the host, remove the logon task, unregister clients. -->
        <SetProperty Id="StopHost" Before="StopHost" Sequence="execute"
                     Value="&quot;[INSTALLFOLDER]SnoopMCP.Cli.exe&quot; stop" />
        <CustomAction Id="StopHost" BinaryRef="Wix4UtilCA_X86" DllEntry="WixQuietExec"
                      Execute="deferred" Return="ignore" Impersonate="yes" />

        <SetProperty Id="UninstallAutostart" Before="UninstallAutostart" Sequence="execute"
                     Value="&quot;[INSTALLFOLDER]SnoopMCP.Cli.exe&quot; uninstall-autostart" />
        <CustomAction Id="UninstallAutostart" BinaryRef="Wix4UtilCA_X86" DllEntry="WixQuietExec"
                      Execute="deferred" Return="ignore" Impersonate="yes" />

        <SetProperty Id="UnregisterClients" Before="UnregisterClients" Sequence="execute"
                     Value="&quot;[INSTALLFOLDER]SnoopMCP.Cli.exe&quot; unregister-clients" />
        <CustomAction Id="UnregisterClients" BinaryRef="Wix4UtilCA_X86" DllEntry="WixQuietExec"
                      Execute="deferred" Return="ignore" Impersonate="yes" />
```

- [ ] **Step 2: Sequence the custom actions.** Add an `<InstallExecuteSequence>` before the closing `</Package>` (after the `WixVariable` line):

```xml
        <InstallExecuteSequence>
            <Custom Action="RegisterClients" After="InstallFiles" Condition="NOT Installed OR REINSTALL" />
            <Custom Action="InstallAutostart" After="RegisterClients" Condition="NOT Installed OR REINSTALL" />
            <Custom Action="StartHost" After="InstallAutostart" Condition="NOT Installed OR REINSTALL" />
            <Custom Action="StopHost" Before="UninstallAutostart" Condition="REMOVE = &quot;ALL&quot;" />
            <Custom Action="UninstallAutostart" Before="UnregisterClients" Condition="REMOVE = &quot;ALL&quot;" />
            <Custom Action="UnregisterClients" Before="RemoveFiles" Condition="REMOVE = &quot;ALL&quot;" />
        </InstallExecuteSequence>
```

(Install order: files land → register clients → create logon task → start host. Uninstall order: stop host → remove task → unregister clients → **before** `RemoveFiles` so `SnoopMCP.Cli.exe` still exists when these run.)

- [ ] **Step 3: Add progress text (optional polish).** Inside the `<Package>`, add a `<UI>` block before `</Package>`:

```xml
        <UI>
            <ProgressText Action="RegisterClients" Message="Registering SnoopMCP with your AI tools..." />
            <ProgressText Action="InstallAutostart" Message="Creating the logon autostart task..." />
            <ProgressText Action="StartHost" Message="Starting the SnoopMCP host..." />
            <ProgressText Action="StopHost" Message="Stopping the SnoopMCP host..." />
            <ProgressText Action="UninstallAutostart" Message="Removing the logon autostart task..." />
            <ProgressText Action="UnregisterClients" Message="Removing SnoopMCP from your AI tools..." />
        </UI>
```

- [ ] **Step 4: Build the MSI.**

Run: `pwsh -NoProfile -File E:/GitHub/SnoopMCP/SnoopMCP.Installer/build-installer.ps1 -Version 1.1.0`
Expected: `Built ...SnoopMCP.msi`. (If `wix build` errors `Wix4UtilCA_X86` unresolved, confirm `-ext WixToolset.Util.wixext` is in the build command — it is.)

- [ ] **Step 5: Commit.** Stage `Package.wxs`. Commit via E:/tmp/pr3-t3-msg.txt:
```
A3 PR3: install/uninstall custom actions

Deferred WixQuietExec CAs drive SnoopMCP.Cli: on install register-clients ->
install-autostart -> start (after InstallFiles); on uninstall stop ->
uninstall-autostart -> unregister-clients (before RemoveFiles, while the CLI
still exists). All Return=ignore (best-effort; the logon task is the durable
autostart guarantee). perUser, Impersonate=yes (runs as the installing user).
```

---

### Task 4: README install section + manual verification checklist

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add an Install section.** After the `## Quickstart` section's intro (or as a new top-level section before `## Tool surface`), add:

```markdown
## Install (MSI)

A per-user MSI installs SnoopMCP without administrator rights to
`%LocalAppData%\SnoopMCP`, registers the MCP server in Claude Code and VS Code,
creates a logon autostart task, and starts the host.

**Build the installer** (needs the .NET 10 SDK, the VS C++ x64 workload, and the
[WiX 5 toolset](https://wixtoolset.org) — `dotnet tool install --global wix`):

```text
pwsh -File SnoopMCP.Installer/build-installer.ps1 -Version 1.1.0
```

This produces `SnoopMCP.Installer/SnoopMCP.msi`. Double-click it (or
`msiexec /i SnoopMCP.msi`) to install. After install, both AI clients point at
`http://127.0.0.1:6300/mcp` and a "SnoopMCP Sample App" Start-menu shortcut is
available. Confirm with `SnoopMCP.Cli status`.

**Limitations (v1.1):** non-elevated targets only (the host attaches to
same-elevation WPF apps — see Known limitations); single user per machine (port
6300 is not multiplexed); the host start at install time is best-effort (the
logon task starts it on every sign-in regardless).
```

- [ ] **Step 2: Add the manual verification checklist** to the installer dir as a note the human runs. Append to the Install section:

```markdown
### Verifying an install (manual)

After running the MSI on a test machine:
1. `%LocalAppData%\SnoopMCP\` contains `SnoopMCP.Host.exe`, `SnoopMCP.Cli.exe`, `injector\`, `payload\`, and `samples\SampleWpfApp.exe`.
2. `schtasks /Query /TN "SnoopMCP Host"` shows the logon task.
3. `~/.claude.json` and `%APPDATA%\Code\User\mcp.json` each carry a `snoopmcp` HTTP entry — and nothing else was disturbed.
4. `http://127.0.0.1:6300/health` returns 200 (or run `SnoopMCP.Cli status`).
5. The "SnoopMCP Sample App" Start-menu shortcut launches the sample.
6. Uninstall (Apps & features, or `msiexec /x`) removes the task, the client entries, and the files — leaving other MCP servers intact.
```

- [ ] **Step 3: Build the MSI (confirm nothing regressed).**

Run: `pwsh -NoProfile -File E:/GitHub/SnoopMCP/SnoopMCP.Installer/build-installer.ps1 -Version 1.1.0`
Expected: `Built ...SnoopMCP.msi`.

- [ ] **Step 4: Render check + commit.** Read `README.md` to confirm the new section is well-formed. Stage `README.md`. Commit via E:/tmp/pr3-t4-msg.txt:
```
A3 PR3: README install section + manual verification checklist

Documents building the MSI (build-installer.ps1), what it does, the v1.1
limitations, and a 6-point manual install/uninstall verification checklist.
```

---

### Task 5: Finalize PR 3

**Files:** none (verification + PR).

- [ ] **Step 1: Full solution build (zero-warning) — confirm the installer work didn't perturb the solution.**

Run: `dotnet build E:/GitHub/SnoopMCP/SnoopMCP.sln -c Debug -p:TreatWarningsAsErrors=true`
Expected: `0 Warning(s) 0 Error(s)`. (The installer dir is not a solution project, so this just confirms the source projects are intact.)

- [ ] **Step 2: Full test suite (unchanged by this PR).**

Run each project `--no-build`: Protocol (5), Host (9), Payload (149), IntegrationTests (2 passed / 1 skipped), ClientIntegration (16), Cli (12). Expected: **193 passed, 1 skipped** — identical to PR 2 (this PR adds no source/tests).

- [ ] **Step 3: Final MSI build (the deliverable gate).**

Run: `pwsh -NoProfile -File E:/GitHub/SnoopMCP/SnoopMCP.Installer/build-installer.ps1 -Version 1.1.0`
Expected: `Built ...SnoopMCP.msi`. Note the MSI size for the PR description.

- [ ] **Step 4: Push, status, PR.**
```text
git -C E:/GitHub/SnoopMCP push -u origin <branch>
git -C E:/GitHub/SnoopMCP rev-parse <branch>
gh api -X POST repos/JackalopeTechnologies/SnoopMCP/statuses/<sha> --field state=success --field context=build --field description="solution build green; MSI builds"
gh pr create --repo JackalopeTechnologies/SnoopMCP --base master --head <branch> --title "A3 PR3: SnoopMCP.Installer (per-user WiX MSI)" --body-file <file>
```
Then STOP for user review (the human runs the install/uninstall verification before/after merge).

---

## Notes for the executor
- **One Bash/PowerShell command per call. No `&&`/`;`/`|`. No `cd`. Commits via `-F <msgfile>` (write to `E:/tmp/...`), never `-m`. No AI attribution. No hook-bypass flags.**
- **Do NOT run the MSI** (`msiexec /i`) — installing mutates the real user's Claude Code / VS Code configs and registers a logon task. The automated gate is only that the MSI *builds*. Real install verification is the human's (Task 4 checklist).
- **Do NOT commit** `stage/`, `*.msi`, or `*.wixpdb` (Task 1 gitignores them). If `git status` shows them, the gitignore entries are missing — fix before committing.
- The publish-assembly is the fiddly part. If `stage/` is missing `injector/` or `payload/` after Step 5, the host's `AfterBuild` copy targets didn't populate `$hostBin` — ensure the `dotnet build $hostProj` step ran (it stages them) before the copy, and that `$hostBin` is `bin/$Configuration/net10.0-windows`. If `--no-build` publish fails, drop `--no-build`.
- If `wix build` complains about the `WixUI_Minimal`/license, confirm `-ext WixToolset.UI.wixext` and the `-b $here` bind path (so `License.rtf` resolves).
- If host+CLI publishing into one `stage` produces a file conflict, publish the CLI to a `cli/` subdir instead and update the CA paths to `[INSTALLFOLDER]cli\SnoopMCP.Cli.exe` — but same-dir is the SaddleRAG-proven default; try it first.
- Branch from a master that has PR 1 + PR 2 merged.
