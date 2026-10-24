param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$DtfAssemblyPath,
    [string]$PublishDirectory
)
$ErrorActionPreference = 'Stop'
Add-Type -Path $DtfAssemblyPath
$database = [WixToolset.Dtf.WindowsInstaller.Database]::new(
    $PackagePath, [WixToolset.Dtf.WindowsInstaller.DatabaseOpenMode]::ReadOnly)
try {
    if ($PublishDirectory) {
        $packageVersion = $database.ExecuteStringQuery('SELECT `Value` FROM `Property` WHERE `Property` = ''ProductVersion''')[0]
        foreach ($relativePath in @('SnoopMCP.Host.dll', 'SnoopMCP.Server.dll', 'SnoopMCP.Cli.dll',
                'SnoopMCP.ClientIntegration.dll', 'payload/SnoopMCP.Payload.dll', 'samples/SampleWpfApp.dll')) {
            $path = Join-Path $PublishDirectory $relativePath
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing packaged assembly: $path" }
            $reportedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($path).ProductVersion
            if ($reportedVersion.Split('+')[0] -ne $packageVersion) {
                throw "Package version $packageVersion does not match $relativePath version $reportedVersion"
            }
        }
        Write-Host "Packaged application versions match installer version $packageVersion."
    }
    $rows = $database.ExecuteQuery('SELECT `Action`, `Condition`, `Sequence` FROM `InstallExecuteSequence` ORDER BY `Sequence`')
    $actions = @{}
    for ($i = 0; $i -lt $rows.Count; $i += 3) {
        $actions[$rows[$i]] = @{ Condition = $rows[$i + 1]; Sequence = [int]$rows[$i + 2] }
    }
    $requiredOrder = @(
        'PrepareUpgradeState', 'InstallInitialize', 'RollbackUpgradeState', 'RemoveExistingProducts',
        'InstallFiles', 'RestoreUpgradeState', 'CleanupUpgradeState', 'InstallFinalize'
    )
    $previous = -1
    foreach ($name in $requiredOrder) {
        if (-not $actions.ContainsKey($name)) { throw "Missing installer action: $name" }
        $current = $actions[$name].Sequence
        if ($current -le $previous) { throw "Unsafe installer ordering at $name ($current <= $previous)" }
        $previous = $current
    }
    foreach ($name in @('UninstallAutostart', 'UnregisterClients')) {
        if ($actions[$name].Condition -notmatch 'NOT UPGRADINGPRODUCTCODE') {
            throw "Upgrade must not unregister client state: $name"
        }
    }
    $shortcuts = $database.ExecuteStringQuery('SELECT `Name` FROM `Shortcut`')
    if (-not ($shortcuts -match 'WPF inspection')) { throw 'Host shortcut missing' }
    if (-not ($shortcuts -match 'diagnostic logs')) { throw 'Logs shortcut missing' }
    if ($database.Tables.Contains('Wix4RemoveFolderEx')) { throw 'Recursive runtime-data cleanup is prohibited' }
    Write-Host 'Installer preservation order, cleanup guards, and recovery shortcuts verified.'
} finally {
    $database.Dispose()
}
