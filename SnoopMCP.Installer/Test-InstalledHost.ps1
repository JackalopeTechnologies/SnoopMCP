param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$EvidenceDirectory
)
$ErrorActionPreference = 'Stop'
# This script installs, stops, and removes the real per-user product and scheduled task.
# Never allow it to run against a developer's normal installation or a self-hosted runner.
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Installed-host qualification requires a disposable GitHub-hosted Windows runner.'
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'The disposable runner must have an administrator token.'
}
$installedRoot = Join-Path $env:LOCALAPPDATA 'SnoopMCP'
if (Test-Path -LiteralPath $installedRoot) { throw 'An existing SnoopMCP installation must not be replaced by qualification.' }
if (Get-ScheduledTask -TaskName 'SnoopMCP Host' -ErrorAction SilentlyContinue) { throw 'An existing SnoopMCP task must not be changed.' }
$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
$EvidenceDirectory = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$result = [ordered]@{ Machine=$env:COMPUTERNAME; User=$identity.Name; Interactive=[Environment]::UserInteractive; Success=$false }
function Invoke-Msi([string]$operation, [string]$name) {
    $log = Join-Path $EvidenceDirectory ($name + '.msi.log')
    $process = Start-Process "$env:WINDIR\System32\msiexec.exe" -ArgumentList @(
        $operation, ('"' + $PackagePath + '"'), '/qn', '/norestart', '/l*v', ('"' + $log + '"')
    ) -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "$name failed with MSI exit code $($process.ExitCode)." }
}
try {
    Invoke-Msi '/i' 'fresh-install'
    $task = Get-ScheduledTask -TaskName 'SnoopMCP Host'
    if ($task.Actions.Execute -ne (Join-Path $installedRoot 'SnoopMCP.Host.exe') -or
        $task.Settings.ExecutionTimeLimit -ne 'PT0S' -or $task.Settings.RestartCount -ne 3 -or
        $task.Settings.RestartInterval -ne 'PT1M' -or $task.Settings.StopIfGoingOnBatteries -or
        $task.Settings.DisallowStartIfOnBatteries) { throw 'Installed task does not have the required recovery settings.' }
    Export-ScheduledTask -TaskName 'SnoopMCP Host' | Set-Content (Join-Path $EvidenceDirectory 'installed-task.xml')
    $cli = Join-Path $installedRoot 'SnoopMCP.Cli.exe'
    & $cli start
    if ($LASTEXITCODE -ne 0) { throw 'Installed CLI could not start the scheduled host.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(40)
    $health = $null
    while ([DateTime]::UtcNow -lt $deadline -and -not $health) {
        try { $health = Invoke-RestMethod 'http://127.0.0.1:6300/health' -TimeoutSec 2 } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $health) {
        Get-ScheduledTaskInfo -TaskName 'SnoopMCP Host' | ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory 'failed-task.json')
        throw 'The installed scheduled host did not become healthy in the disposable runner.'
    }
    if ($health.attached -or $health.interactionEnabled) { throw 'Fresh host must be unattached with interaction disabled.' }
    $result.Health = $health
    $result.HostProcess = (Get-Process -Name SnoopMCP.Host).Id
    & $cli stop
    if ($LASTEXITCODE -ne 0) { throw 'Installed CLI could not stop its scheduled host.' }
    Start-Sleep -Seconds 10
    if (Get-Process -Name SnoopMCP.Host -ErrorAction SilentlyContinue) { throw 'Stopped host is still running.' }
    Invoke-Msi '/x' 'fresh-uninstall'
    if (Test-Path (Join-Path $installedRoot 'SnoopMCP.Host.exe')) { throw 'Uninstall left the owned host executable.' }
    if (Get-ScheduledTask -TaskName 'SnoopMCP Host' -ErrorAction SilentlyContinue) { throw 'Uninstall left the owned scheduled task.' }
    $result.Success = $true
} catch {
    $result.Error = $_.Exception.Message
    throw
} finally {
    if (Test-Path (Join-Path $installedRoot 'logs')) {
        Copy-Item -LiteralPath (Join-Path $installedRoot 'logs') -Destination (Join-Path $EvidenceDirectory 'host-logs') -Recurse -Force
    }
    $result | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $EvidenceDirectory 'installed-host.json')
}
