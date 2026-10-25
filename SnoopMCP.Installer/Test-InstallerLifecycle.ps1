param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$EvidenceDirectory
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Installer lifecycle qualification requires a disposable GitHub-hosted Windows runner.'
}
$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
$EvidenceDirectory = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$repository = Split-Path $PSScriptRoot
$installedRoot = Join-Path $env:LOCALAPPDATA 'SnoopMCP'
$profileRoot = [Environment]::GetFolderPath('UserProfile')
$roamingRoot = [Environment]::GetFolderPath('ApplicationData')
$shortcutRoot = Join-Path ([Environment]::GetFolderPath('Programs')) 'SnoopMCP'
$cli = Join-Path $installedRoot 'SnoopMCP.Cli.exe'
$jsonFiles = @('.claude.json','.cursor/mcp.json','.gemini/settings.json','.copilot/mcp-config.json',
    '.codeium/windsurf/mcp_config.json','.mcp.json') | ForEach-Object { Join-Path $profileRoot $_ }
$jsonFiles += @('Claude/claude_desktop_config.json','Code/User/mcp.json') | ForEach-Object { Join-Path $roamingRoot $_ }
$tomlFiles = @('.codex/config.toml','.grok/config.toml') | ForEach-Object { Join-Path $profileRoot $_ }
$permissionPath = Join-Path $profileRoot '.claude/settings.json'
$skillRoots = @('.claude/skills','.agents/skills','.codeium/windsurf/skills') | ForEach-Object { Join-Path $profileRoot $_ }
$ownedFiles = @($jsonFiles) + @($tomlFiles) + @($permissionPath)
$ownedDirectories = @($installedRoot,$shortcutRoot)
foreach ($root in $skillRoots) {
    $ownedDirectories += @('snoopmcp-first','snoopmcp-uia','qualification-unrelated') | ForEach-Object { Join-Path $root $_ }
}
foreach ($path in $ownedFiles + $ownedDirectories) {
    if (Test-Path -LiteralPath $path) { throw "Qualification refuses to overwrite an existing path: $path" }
}
if (Get-ScheduledTask -TaskName 'SnoopMCP Host' -ErrorAction SilentlyContinue) { throw 'An existing SnoopMCP task must not be changed.' }
$cases = [Collections.Generic.List[object]]::new()
$currentCase = 'prepare'
function Assert-True([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Write-Fixture([string]$path, [string]$content) {
    New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
    [IO.File]::WriteAllText($path, $content)
}
function Save-Json([string]$path, $value) { Write-Fixture $path ($value | ConvertTo-Json -Depth 20) }
function Seed-Preferences {
    foreach ($path in $jsonFiles) {
        $key = if ($path -eq (Join-Path $roamingRoot 'Code/User/mcp.json')) { 'servers' } else { 'mcpServers' }
        Save-Json $path @{ theme='qualification-dark'; $key=@{ unrelated=@{ command='fixture-command'; args=@('preserve'); disabled=$true } } }
    }
    foreach ($path in $tomlFiles) {
        Write-Fixture $path "# preserve the developer comment`nmodel = 'qualification-model'`n[mcp_servers.unrelated]`nurl = 'https://fixture.invalid/mcp'`n"
    }
    Save-Json $permissionPath @{ permissions=@{ allow=@('Read'); deny=@('mcp__snoopmcp__execute_command') }; theme='preserve' }
    foreach ($root in $skillRoots) { Write-Fixture (Join-Path $root 'qualification-unrelated/SKILL.md') 'Unrelated personal skill.' }
}
function Assert-Preferences {
    foreach ($path in $jsonFiles) {
        $root = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
        $key = if ($path -eq (Join-Path $roamingRoot 'Code/User/mcp.json')) { 'servers' } else { 'mcpServers' }
        Assert-True ($root.theme -eq 'qualification-dark' -and $root[$key].unrelated.command -eq 'fixture-command' -and
            $root[$key].unrelated.disabled -eq $true) "Unrelated JSON preferences changed: $path"
    }
    foreach ($path in $tomlFiles) {
        $text = Get-Content -LiteralPath $path -Raw
        Assert-True ($text.Contains('qualification-model') -and $text.Contains('https://fixture.invalid/mcp') -and
            $text.Contains('# preserve the developer comment')) "Unrelated TOML preferences changed: $path"
    }
    $permissions = Get-Content -LiteralPath $permissionPath -Raw | ConvertFrom-Json
    Assert-True ($permissions.permissions.deny -contains 'mcp__snoopmcp__execute_command') 'Developer deny permission was removed.'
}
function Invoke-Msi([string]$package, [string]$operation, [string]$label, [int[]]$expected = @(0), [string[]]$extra = @()) {
    $log = Join-Path $EvidenceDirectory ($label + '.msi.log')
    $arguments = @($operation, ('"' + $package + '"'), '/qn', '/norestart', '/l*v', ('"' + $log + '"')) + $extra
    $process = Start-Process "$env:WINDIR\System32\msiexec.exe" -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    Assert-True ($process.ExitCode -in $expected) "$label returned $($process.ExitCode), expected $($expected -join ',')."
    return $process.ExitCode
}
function Invoke-Registration([int]$expected = 0) {
    & $cli register-clients | Out-Host
    Assert-True ($LASTEXITCODE -eq $expected) "Client registration returned $LASTEXITCODE, expected $expected."
}
function Get-StateHashes {
    $hashes = @{}
    foreach ($path in $ownedFiles) {
        if (Test-Path -LiteralPath $path -PathType Leaf) { $hashes[$path] = (Get-FileHash -LiteralPath $path).Hash }
    }
    foreach ($root in $skillRoots) {
        if (Test-Path -LiteralPath $root) {
            Get-ChildItem -LiteralPath $root -File -Recurse | ForEach-Object { $hashes[$_.FullName] = (Get-FileHash -LiteralPath $_.FullName).Hash }
        }
    }
    foreach ($relative in @('interaction-gate.json','personal.txt','logs/personal-history.log')) {
        $path = Join-Path $installedRoot $relative
        if (Test-Path -LiteralPath $path) { $hashes[$path] = (Get-FileHash -LiteralPath $path).Hash }
    }
    return $hashes
}
function Assert-Hashes($expected) {
    foreach ($entry in $expected.GetEnumerator()) {
        Assert-True ((Test-Path -LiteralPath $entry.Key) -and (Get-FileHash -LiteralPath $entry.Key).Hash -eq $entry.Value) "Preserved file changed: $($entry.Key)"
    }
}
function Assert-CanonicalSkills {
    foreach ($root in $skillRoots) {
        foreach ($name in @('snoopmcp-first','snoopmcp-uia')) {
            $path = Join-Path $root "$name/SKILL.md"
            Assert-True ([IO.File]::ReadAllText($path) -eq [IO.File]::ReadAllText((Join-Path $repository "skills/$name/SKILL.md"))) "Canonical skill mismatch: $path"
            Assert-True (Test-Path (Join-Path $root "$name/.snoopmcp-install.json")) "Missing skill ownership receipt: $path"
        }
    }
}
function Customize-InstalledState {
    foreach ($path in $jsonFiles) {
        $root = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
        $key = if ($path -eq (Join-Path $roamingRoot 'Code/User/mcp.json')) { 'servers' } else { 'mcpServers' }
        if ($root[$key].ContainsKey('snoopmcp')) {
            $root[$key].snoopmcp['timeout'] = 777
            $root[$key].snoopmcp['disabled'] = $true
            $root[$key].snoopmcp['headers'] = @{ 'X-Qualification'='preserve' }
        }
        Save-Json $path $root
    }
    foreach ($path in $tomlFiles) {
        Write-Fixture $path "# preserve the developer comment`nmodel = 'qualification-model'`n[mcp_servers.unrelated]`nurl = 'https://fixture.invalid/mcp'`n[mcp_servers.snoopmcp]`nurl = 'http://127.0.0.1:6300/mcp'`nenabled = false`ntool_timeout_sec = 133`n"
    }
    Save-Json $permissionPath @{ permissions=@{ allow=@('Read'); deny=@('mcp__snoopmcp__execute_command') }; theme='preserve' }
    foreach ($root in $skillRoots) {
        Write-Fixture (Join-Path $root 'snoopmcp-first/SKILL.md') 'User-edited Snoop inspection guidance.'
        Write-Fixture (Join-Path $root 'snoopmcp-first/personal.txt') 'Personal supporting notes.'
        Write-Fixture (Join-Path $root 'snoopmcp-uia/personal.txt') 'Personal screenshot notes.'
    }
    Write-Fixture (Join-Path $installedRoot 'interaction-gate.json') '{"Enabled":false}'
    Write-Fixture (Join-Path $installedRoot 'personal.txt') 'Non-package runtime-folder file.'
    Write-Fixture (Join-Path $installedRoot 'logs/personal-history.log') 'Historical diagnostics.'
    Write-Fixture (Join-Path $shortcutRoot 'personal.txt') 'Personal Start menu note.'
}
function Reset-OwnedFixtures {
    foreach ($path in $ownedFiles) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force } }
    foreach ($path in $ownedDirectories) {
        if (Test-Path -LiteralPath $path) {
            $resolved = (Resolve-Path -LiteralPath $path).Path
            $expected = [IO.Path]::GetFullPath($path)
            Assert-True ($resolved -eq $expected -and $resolved.StartsWith($profileRoot + '\',[StringComparison]::OrdinalIgnoreCase) -and
                -not (Get-Item -LiteralPath $path).LinkType) "Unsafe fixture cleanup target: $path"
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
function New-FaultPackage([string]$source, [string]$target, [string]$entryPoint, [string]$phase) {
    Copy-Item -LiteralPath $source -Destination $target
    $db = [WixToolset.Dtf.WindowsInstaller.Database]::new($target,[WixToolset.Dtf.WindowsInstaller.DatabaseOpenMode]::Transact)
    try {
        $view = $db.OpenView('INSERT INTO `Binary` (`Name`, `Data`) VALUES (?, ?)')
        $record = [WixToolset.Dtf.WindowsInstaller.Record]::new(2)
        try { $record[1]='QualificationFaults'; $record.SetStream(2,$faultAssembly); $view.Execute($record) } finally { $record.Dispose(); $view.Dispose() }
        $deferred = $phase -ne 'after-removal'
        $type = if ($deferred) { 1025 } else { 1 }
        $condition = if ($phase -eq 'legacy-removal') { 'UPGRADINGPRODUCTCODE AND REMOVE="ALL"' } else { 'WIX_UPGRADE_DETECTED' }
        $anchor = if ($phase -eq 'legacy-removal') { 'RemoveFiles' } elseif ($deferred) { 'InstallFinalize' } else { 'RemoveExistingProducts' }
        $sequence = [int]$db.ExecuteIntegerQuery("SELECT ``Sequence`` FROM ``InstallExecuteSequence`` WHERE ``Action`` = '$anchor'")[0]
        $sequence += $(if ($anchor -eq 'InstallFinalize') { -1 } else { 1 })
        $db.Execute("INSERT INTO ``CustomAction`` (``Action``, ``Type``, ``Source``, ``Target``) VALUES ('QualificationFault', $type, 'QualificationFaults', '$entryPoint')")
        $db.Execute("INSERT INTO ``InstallExecuteSequence`` (``Action``, ``Condition``, ``Sequence``) VALUES ('QualificationFault', '$condition', $sequence)")
        $summary = $db.SummaryInfo
        try {
            $summary.RevisionNumber = '{' + [Guid]::NewGuid().ToString().ToUpperInvariant() + '}'
            $summary.Persist()
        } finally { $summary.Dispose() }
        $db.Commit()
    } finally { $db.Dispose() }
}
try {
    $dtf = Join-Path $repository 'src/SnoopMCP.InstallerActions/bin/Release/net472/WixToolset.Dtf.WindowsInstaller.dll'
    Add-Type -Path $dtf
    $legacy = Join-Path $EvidenceDirectory 'SnoopMCP-1.4.2.msi'
    Invoke-WebRequest 'https://github.com/JackalopeTechnologies/SnoopMCP/releases/download/v1.4.2/SnoopMCP-1.4.2.msi' -OutFile $legacy
    Assert-True ((Get-FileHash $legacy).Hash -eq '4776DF42B57E8003E85CEC058335B09B76C5CDF8A3D366AC2868F4AC01470C0A') 'Published legacy installer hash mismatch.'
    $faultSource = Join-Path $EvidenceDirectory 'QualificationFaults.cs'
    $faultDll = Join-Path $EvidenceDirectory 'QualificationFaults.dll'
    $faultAssembly = Join-Path $EvidenceDirectory 'QualificationFaults.CA.dll'
    Write-Fixture $faultSource @'
using WixToolset.Dtf.WindowsInstaller;
public static class QualificationFaults
{
    [CustomAction] public static ActionResult Fail(Session session) { session.Log("Qualification: injected failure."); return ActionResult.Failure; }
    [CustomAction] public static ActionResult Cancel(Session session) { session.Log("Qualification: injected cancellation."); return ActionResult.UserExit; }
}
'@
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:library "/out:$faultDll" "/reference:$dtf" $faultSource
    Assert-True ($LASTEXITCODE -eq 0) 'Fault fixture compilation failed.'
    $tools = Join-Path $profileRoot '.nuget/packages/wixtoolset.dtf.customaction/5.0.2/tools'
    & (Join-Path $tools 'WixToolset.Dtf.MakeSfxCA.exe') $faultAssembly (Join-Path $tools 'x86/SfxCA.dll') $faultDll $dtf (Join-Path $repository 'src/SnoopMCP.InstallerActions/CustomAction.config')
    Assert-True ($LASTEXITCODE -eq 0) 'Native fault fixture packaging failed.'

    $currentCase = 'fresh-repeat-uninstall'
    Seed-Preferences
    $preferencesBefore = Get-StateHashes
    Invoke-Msi $PackagePath '/i' 'fresh' | Out-Null
    Assert-Hashes $preferencesBefore
    Invoke-Registration
    Assert-Preferences
    Assert-CanonicalSkills
    foreach ($name in @('SnoopMCP - WPF inspection','SnoopMCP - diagnostic logs','SnoopMCP Sample App')) {
        Assert-True (Test-Path (Join-Path $shortcutRoot ($name + '.lnk'))) "Missing Start menu entry: $name"
    }
    $registered = Get-StateHashes
    Invoke-Registration
    Assert-Hashes $registered
    Invoke-Msi $PackagePath '/i' 'repeat-repair' @(0) @('REINSTALL=ALL','REINSTALLMODE=vomus') | Out-Null
    Assert-Hashes $registered
    Customize-InstalledState
    $custom = Get-StateHashes
    $codePath = Join-Path $roamingRoot 'Code/User/mcp.json'
    $code = Get-Content $codePath -Raw | ConvertFrom-Json -AsHashtable
    $code.servers.snoopmcp.url = 'https://replacement.invalid/mcp'
    Save-Json $codePath $code
    Invoke-Msi $PackagePath '/x' 'owned-uninstall' | Out-Null
    Assert-Preferences
    Assert-True (-not (Test-Path (Join-Path $installedRoot 'SnoopMCP.Host.exe'))) 'Owned executable survived uninstall.'
    Assert-True (-not (Get-ScheduledTask -TaskName 'SnoopMCP Host' -ErrorAction SilentlyContinue)) 'Owned task survived uninstall.'
    Assert-True (@(Get-ChildItem $shortcutRoot -Filter '*.lnk').Count -eq 0) 'Owned Start menu shortcuts survived uninstall.'
    Assert-True ([IO.File]::ReadAllText((Join-Path $shortcutRoot 'personal.txt')) -eq 'Personal Start menu note.') 'Personal Start menu content was deleted.'
    foreach ($root in $skillRoots) {
        Assert-True ([IO.File]::ReadAllText((Join-Path $root 'snoopmcp-first/SKILL.md')) -eq 'User-edited Snoop inspection guidance.') 'Edited skill was deleted.'
        Assert-True (-not (Test-Path (Join-Path $root 'snoopmcp-uia/SKILL.md'))) 'Owned unedited skill survived uninstall.'
        Assert-True (Test-Path (Join-Path $root 'snoopmcp-uia/personal.txt')) 'Personal skill supporting file was deleted.'
    }
    foreach ($relative in @('personal.txt','interaction-gate.json','logs/personal-history.log')) {
        $path = Join-Path $installedRoot $relative
        Assert-True ((Get-FileHash $path).Hash -eq $custom[$path]) "Uninstall removed user runtime data: $path"
    }
    $code = Get-Content $codePath -Raw | ConvertFrom-Json -AsHashtable
    Assert-True ($code.servers.snoopmcp.url -eq 'https://replacement.invalid/mcp') 'Uninstall removed a replacement connection.'
    $cases.Add(@{Case=$currentCase;Passed=$true})
    Reset-OwnedFixtures

    foreach ($scenario in @('upgrade','after-removal-Fail','after-removal-Cancel','late-Fail','late-Cancel','legacy-removal-Fail','legacy-removal-Cancel')) {
        $currentCase = $scenario
        Seed-Preferences
        $oldPackage = $legacy
        $newPackage = $PackagePath
        if ($scenario -ne 'upgrade') {
            $parts = $scenario.Split('-')
            $entry = $parts[-1]
            $phase = $scenario.Substring(0,$scenario.Length-$entry.Length-1)
            $faultPackage = Join-Path $EvidenceDirectory ($scenario + '.msi')
            if ($phase -eq 'legacy-removal') { New-FaultPackage $legacy $faultPackage $entry $phase; $oldPackage = $faultPackage }
            else { New-FaultPackage $PackagePath $faultPackage $entry $phase; $newPackage = $faultPackage }
        }
        Invoke-Msi $oldPackage '/i' "$scenario-old-install" | Out-Null
        Invoke-Registration
        Customize-InstalledState
        $before = Get-StateHashes
        $oldBinary = (Get-FileHash (Join-Path $installedRoot 'SnoopMCP.Server.dll')).Hash
        if ($scenario -eq 'upgrade') {
            Invoke-Msi $newPackage '/i' $scenario | Out-Null
            Assert-Hashes $before
            Assert-True ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $installedRoot 'SnoopMCP.Server.dll')).ProductVersion.StartsWith('1.4.3+')) 'Upgrade did not replace old binaries.'
            Invoke-Registration 2
            foreach ($root in $skillRoots) {
                Assert-True ([IO.File]::ReadAllText((Join-Path $root 'snoopmcp-first/SKILL.md')) -eq 'User-edited Snoop inspection guidance.') 'Registration replaced edited legacy guidance.'
            }
            Invoke-Msi $PackagePath '/x' "$scenario-cleanup" | Out-Null
        } else {
            $exitCode = Invoke-Msi $newPackage '/i' $scenario @(1602,1603)
            Assert-Hashes $before
            Assert-True ((Get-FileHash (Join-Path $installedRoot 'SnoopMCP.Server.dll')).Hash -eq $oldBinary) 'Rollback did not restore old application binaries.'
            Assert-True ([bool](Get-ScheduledTask -TaskName 'SnoopMCP Host' -ErrorAction SilentlyContinue)) 'Rollback did not restore the previous autostart task.'
            $backupRoot = Join-Path $env:LOCALAPPDATA 'SnoopMCP.Upgrade'
            Assert-True (@(Get-ChildItem $backupRoot -Filter 'manifest.xml' -File -Recurse).Count -gt 0) 'Interrupted upgrade did not retain a recovery snapshot.'
            $logText = Get-Content (Join-Path $EvidenceDirectory "$scenario.msi.log") -Raw
            Assert-True ($logText.Contains('RecoverFailedUpgrade') -or $logText.Contains('RecoverCancelledUpgrade')) 'No interrupted-upgrade recovery action was recorded.'
            Invoke-Msi $oldPackage '/x' "$scenario-cleanup" | Out-Null
        }
        $cases.Add(@{Case=$scenario;Passed=$true})
        $cases | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $EvidenceDirectory 'lifecycle-cases.json')
        Reset-OwnedFixtures
    }
} catch {
    $cases.Add(@{Case=$currentCase;Passed=$false;Error=$_.Exception.Message})
    throw
} finally {
    $cases | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $EvidenceDirectory 'lifecycle-cases.json')
}
