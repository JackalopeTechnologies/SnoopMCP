param(
    [Parameter(Mandatory)][string]$CustomActionAssemblyPath,
    [Parameter(Mandatory)][string]$WorkDirectory
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $WorkDirectory) { throw 'Recovery test requires a new scratch directory.' }
New-Item -ItemType Directory -Path $WorkDirectory | Out-Null
$WorkDirectory = (Resolve-Path -LiteralPath $WorkDirectory).Path
$CustomActionAssemblyPath = (Resolve-Path -LiteralPath $CustomActionAssemblyPath).Path
Add-Type -Path ($CustomActionAssemblyPath -replace '\.CA\.dll$', '.dll')
$preferencePath = Join-Path $WorkDirectory 'preference.txt'
$snapshotPath = Join-Path $WorkDirectory 'snapshot'
[IO.File]::WriteAllText($preferencePath, 'original preference')
$snapshot = [SnoopMCP.InstallerActions.UpgradeSnapshot]::new($snapshotPath)
$snapshot.Capture([string[]]@($preferencePath), [string[]]@(), [Collections.Generic.HashSet[string]]::new())
[IO.File]::WriteAllText($preferencePath, 'simulated legacy cleanup')
$escapedAssembly = [Security.SecurityElement]::Escape($CustomActionAssemblyPath)
$escapedSnapshot = [Security.SecurityElement]::Escape($snapshotPath)
$source = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="SnoopMCP recovery verification" Manufacturer="Jackalope Technologies, Inc."
           Version="1.0.0" Scope="perUser" UpgradeCode="$(New-Guid)">
    <Property Id="RestoreUpgradeState" Value="Snapshot=$escapedSnapshot" />
    <Binary Id="UpgradeActions" SourceFile="$escapedAssembly" />
    <CustomAction Id="RecoverFailedUpgrade" BinaryRef="UpgradeActions"
                  DllEntry="RestoreInterruptedUpgradeState" Execute="immediate" Return="check" />
    <CustomAction Id="ExpectedFailure" Error="Expected failure for isolated recovery verification." />
    <InstallExecuteSequence>
      <Custom Action="ExpectedFailure" Before="InstallInitialize" />
      <Custom Action="RecoverFailedUpgrade" OnExit="error" Condition="RestoreUpgradeState" />
    </InstallExecuteSequence>
    <Feature Id="Empty" Title="No installed components" Level="1" />
  </Package>
</Wix>
"@
$sourcePath = Join-Path $WorkDirectory 'recovery-probe.wxs'
$packagePath = Join-Path $WorkDirectory 'recovery-probe.msi'
$logPath = Join-Path $WorkDirectory 'recovery-probe.log'
Set-Content -LiteralPath $sourcePath -Value $source
& wix build $sourcePath -arch x64 -o $packagePath
if ($LASTEXITCODE -ne 0) { throw 'Could not build isolated recovery probe.' }
$process = Start-Process -FilePath "$env:WINDIR\System32\msiexec.exe" -ArgumentList @(
    '/i', "`"$packagePath`"", '/qn', '/norestart', '/l*v', "`"$logPath`""
) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 1603) { throw "Expected deliberate installation failure; got $($process.ExitCode)." }
if ([IO.File]::ReadAllText($preferencePath) -ne 'original preference') {
    throw "The packaged recovery helper did not restore the scratch preference. Inspect $logPath"
}
if (-not (Test-Path -LiteralPath $snapshotPath)) { throw 'Failure must retain the recovery snapshot.' }
Write-Host "Packaged failure recovery restored the scratch preference and retained its snapshot. Log: $logPath"
