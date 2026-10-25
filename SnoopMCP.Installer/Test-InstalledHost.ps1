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
$result.SourceCommit = (git -C (Split-Path $PSScriptRoot) rev-parse HEAD)
$result.PackageSha256 = (Get-FileHash -LiteralPath $PackagePath).Hash
$events = [Collections.Generic.List[object]]::new()
function Wait-Healthy([int]$previousId = 0) {
    $deadline = [DateTime]::UtcNow.AddSeconds(95)
    while ([DateTime]::UtcNow -lt $deadline) {
        $hostProcess = Get-Process -Name SnoopMCP.Host -ErrorAction SilentlyContinue
        if ($hostProcess -and $hostProcess.Id -ne $previousId) {
            $health = $null
            try {
                $health = Invoke-RestMethod 'http://127.0.0.1:6300/health' -TimeoutSec 2
            } catch { }
            if ($health) {
                if ($health.attached -or $health.interactionEnabled) { throw 'Qualification requires an unattached host with interaction disabled.' }
                $null = $hostProcess.Handle
                return @{ Health=$health; Process=$hostProcess }
            }
        }
        Start-Sleep -Milliseconds 500
    }
    throw 'The installed scheduled host did not become healthy within 95 seconds.'
}
function Assert-StaysStopped([string]$reason) {
    $deadline = [DateTime]::UtcNow.AddSeconds(80)
    while ([DateTime]::UtcNow -lt $deadline) {
        $remaining = @(Get-Process -Name SnoopMCP.Host -ErrorAction SilentlyContinue)
        if ($remaining.Count -gt 0) {
            $events.Add(@{ Event='Unexpected host'; Reason=$reason; Time=[DateTime]::UtcNow;
                Processes=@($remaining | Select-Object Id,HasExited,StartTime) })
            throw "$reason unexpectedly restarted the host."
        }
        Start-Sleep -Seconds 1
    }
    if (Get-Process -Name SnoopMCP.Recovery -ErrorAction SilentlyContinue) { throw "$reason left its recovery launcher running." }
    $events.Add(@{ Event=$reason; StayedStoppedSeconds=80; Time=[DateTime]::UtcNow })
}
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
    if ($task.Actions.Execute -ne (Join-Path $installedRoot 'SnoopMCP.Recovery.exe') -or
        $task.Settings.ExecutionTimeLimit -ne 'PT0S' -or $task.Settings.RestartCount -ne 0 -or
        $task.Settings.StopIfGoingOnBatteries -or
        $task.Settings.DisallowStartIfOnBatteries) { throw 'Installed task does not have the required recovery settings.' }
    Export-ScheduledTask -TaskName 'SnoopMCP Host' | Set-Content (Join-Path $EvidenceDirectory 'installed-task.xml')
    $cli = Join-Path $installedRoot 'SnoopMCP.Cli.exe'
    & $cli start
    if ($LASTEXITCODE -ne 0) { throw 'Installed CLI could not start the scheduled host.' }
    $running = Wait-Healthy
    $result.Health = $running.Health
    $result.HostProcess = $running.Process.Id
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        # Kill only this disposable installation after checking inspection ownership and the gate.
        $health = Invoke-RestMethod 'http://127.0.0.1:6300/health' -TimeoutSec 2
        if ($health.attached -or $health.interactionEnabled -or $running.Process.Path -ne (Join-Path $installedRoot 'SnoopMCP.Host.exe')) {
            throw 'Refusing to crash a host outside the isolated qualification session.'
        }
        $previousId = $running.Process.Id
        Stop-Process -Id $previousId -Force
        $running.Process.WaitForExit()
        $events.Add(@{ Event='Crash'; Attempt=$attempt; Process=$previousId; Time=[DateTime]::UtcNow })
        $running.Process.Dispose()
        if ($attempt -le 3) {
            $running = Wait-Healthy $previousId
            $events.Add(@{ Event='Recovered'; Attempt=$attempt; Process=$running.Process.Id; Time=[DateTime]::UtcNow })
        } else { Assert-StaysStopped 'Retry limit' }
    }
    $logText = (Get-ChildItem (Join-Path $installedRoot 'logs') -Filter '*.log' | Get-Content -Raw) -join "`n"
    if (-not $logText.Contains('Previous host run ended without a clean shutdown')) { throw 'Crash recovery did not record useful diagnostics.' }
    if (-not $logText.Contains('restart limit reached')) { throw 'Recovery did not explain that its retry limit was reached.' }
    & $cli start
    if ($LASTEXITCODE -ne 0) { throw 'CLI start after the retry limit failed.' }
    $running = Wait-Healthy
    & $cli stop
    if ($LASTEXITCODE -ne 0) { throw 'Installed CLI could not stop its scheduled host.' }
    if (-not $running.Process.HasExited) { throw 'CLI stop returned before the original host exited.' }
    $running.Process.Dispose()
    Assert-StaysStopped 'CLI stop'
    if (Test-Path (Join-Path $installedRoot 'logs/host-running.txt')) { throw 'An intentional CLI stop left a false crash marker.' }

    # Test-only runtime hook invokes the installed tray's actual ExitCommand on its WPF dispatcher.
    # It changes no task settings, server code, interaction permission, or inspection session.
    # This verifies command execution and shutdown, not a physical tray-menu click or appearance.
    $hookRoot = Join-Path $env:RUNNER_TEMP ('snoop-tray-exit-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $hookRoot | Out-Null
    @'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0-windows</TargetFramework><UseWPF>true</UseWPF><Nullable>enable</Nullable></PropertyGroup></Project>
'@ | Set-Content (Join-Path $hookRoot 'TrayExitHook.csproj')
    @'
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
public static class StartupHook
{
    public static void Initialize()
    {
        new Thread(() =>
        {
            string root = AppContext.BaseDirectory;
            try
            {
                string request = Path.Combine(root, "qualification-exit.request");
                DateTime deadline = DateTime.UtcNow.AddMinutes(2);
                while (!File.Exists(request) && DateTime.UtcNow < deadline) Thread.Sleep(100);
                if (!File.Exists(request)) throw new TimeoutException("Tray exit request was not received.");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    object tray = Application.Current.FindResource("TrayIcon");
                    object model = tray.GetType().GetProperty("DataContext")!.GetValue(tray)!;
                    var command = (ICommand)model.GetType().GetProperty("ExitCommand")!.GetValue(model)!;
                    if (!command.CanExecute(null)) throw new InvalidOperationException("ExitCommand is disabled.");
                    File.WriteAllText(Path.Combine(root, "qualification-exit.executed"), DateTime.UtcNow.ToString("O"));
                    command.Execute(null);
                });
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(root, "qualification-exit.error"), ex.ToString()); }
        }) { IsBackground = true }.Start();
    }
}
'@ | Set-Content (Join-Path $hookRoot 'StartupHook.cs')
    dotnet build (Join-Path $hookRoot 'TrayExitHook.csproj') -c Release -m:2 -nodeReuse:false
    if ($LASTEXITCODE -ne 0) { throw 'Tray command test hook build failed.' }
    $runtimePath = Join-Path $installedRoot 'SnoopMCP.Host.runtimeconfig.json'
    $originalRuntime = [IO.File]::ReadAllBytes($runtimePath)
    $runtime = Get-Content $runtimePath -Raw | ConvertFrom-Json -AsHashtable
    $runtime.runtimeOptions.configProperties.STARTUP_HOOKS = Join-Path $hookRoot 'bin/Release/net10.0-windows/TrayExitHook.dll'
    $runtime | ConvertTo-Json -Depth 10 | Set-Content $runtimePath
    & $cli start
    if ($LASTEXITCODE -ne 0) { throw 'CLI start for tray Exit verification failed.' }
    $running = Wait-Healthy
    Set-Content (Join-Path $installedRoot 'qualification-exit.request') 'Execute ExitCommand'
    if (-not $running.Process.WaitForExit(15000)) { throw 'The installed tray Exit command did not terminate the host.' }
    if ($running.Process.ExitCode -ne 0 -or -not (Test-Path (Join-Path $installedRoot 'qualification-exit.executed')) -or
        (Test-Path (Join-Path $installedRoot 'qualification-exit.error')) -or
        (Test-Path (Join-Path $installedRoot 'logs/host-running.txt'))) { throw 'Tray Exit did not complete a clean shutdown.' }
    $running.Process.Dispose()
    Assert-StaysStopped 'Tray Exit command'
    [IO.File]::WriteAllBytes($runtimePath,$originalRuntime)
    $taskResult = (Get-ScheduledTaskInfo -TaskName 'SnoopMCP Host').LastTaskResult
    if ($taskResult -ne 0) { throw "Tray Exit returned scheduled-task result $taskResult instead of success." }
    $result.TrayExitTaskResult = $taskResult
    Invoke-Msi '/x' 'fresh-uninstall'
    if (Test-Path (Join-Path $installedRoot 'SnoopMCP.Host.exe')) { throw 'Uninstall left the owned host executable.' }
    if (Test-Path (Join-Path $installedRoot 'SnoopMCP.Recovery.exe')) { throw 'Uninstall left the owned recovery launcher.' }
    if (Get-ScheduledTask -TaskName 'SnoopMCP Host' -ErrorAction SilentlyContinue) { throw 'Uninstall left the owned scheduled task.' }
    $result.Success = $true
} catch {
    $result.Error = $_.Exception.Message
    throw
} finally {
    if ($hookRoot -and (Test-Path $hookRoot)) {
        Copy-Item -LiteralPath $hookRoot -Destination (Join-Path $EvidenceDirectory 'tray-exit-hook') -Recurse
    }
    Get-ScheduledTaskInfo -TaskName 'SnoopMCP Host' -ErrorAction SilentlyContinue |
        Select-Object TaskName,LastRunTime,LastTaskResult,NumberOfMissedRuns |
        ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory 'final-task.json')
    $events | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $EvidenceDirectory 'host-events.json')
    Get-ChildItem $installedRoot -Filter 'qualification-exit.*' -ErrorAction SilentlyContinue | Copy-Item -Destination $EvidenceDirectory
    if (Test-Path (Join-Path $installedRoot 'logs')) {
        Copy-Item -LiteralPath (Join-Path $installedRoot 'logs') -Destination (Join-Path $EvidenceDirectory 'host-logs') -Recurse -Force
    }
    $result | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $EvidenceDirectory 'installed-host.json')
}
