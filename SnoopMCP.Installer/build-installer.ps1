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
$recoveryProj = Join-Path $root "src/SnoopMCP.Recovery/SnoopMCP.Recovery.csproj"
$sampleProj = Join-Path $root "samples/SampleWpfApp/SampleWpfApp.csproj"
$actionsProj = Join-Path $root "src/SnoopMCP.InstallerActions/SnoopMCP.InstallerActions.csproj"
$actionsDll = Join-Path $root "src/SnoopMCP.InstallerActions/bin/$Configuration/net472/SnoopMCP.InstallerActions.CA.dll"
$hostBin    = Join-Path $root "src/SnoopMCP.Host/bin/$Configuration/net10.0-windows"

if (Test-Path -LiteralPath $stage) {
    $resolvedStage = (Resolve-Path -LiteralPath $stage).Path
    $expectedStage = [IO.Path]::GetFullPath((Join-Path $here 'stage'))
    if ($resolvedStage -ne $expectedStage -or (Get-Item -LiteralPath $stage).LinkType) {
        throw "Refusing to remove unexpected installer staging directory: $resolvedStage"
    }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
New-Item -ItemType Directory -Path $stage | Out-Null

# Build the host first so its AfterBuild copy targets stage injector/ + payload/ into bin.
# -nodeReuse:false: never adopt a persistent MSBuild node spawned by Visual Studio, whose
# duplicate-cased PATH otherwise crashes the nested native (CL.exe) build.
dotnet build $hostProj -c $Configuration -p:Version=$Version -m:2 -nodeReuse:false
if ($LASTEXITCODE -ne 0) { throw "Host build failed." }

# Publish host (exe + framework-dependent deps) into the stage root.
dotnet publish $hostProj -c $Configuration -p:Version=$Version --no-build -m:2 -nodeReuse:false -o $stage
if ($LASTEXITCODE -ne 0) { throw "Host publish failed." }

# injector/ + payload/ are loose staged folders that publish does not carry — copy them in.
Copy-Item (Join-Path $hostBin "injector") (Join-Path $stage "injector") -Recurse -Force
Copy-Item (Join-Path $hostBin "payload")  (Join-Path $stage "payload")  -Recurse -Force

# Fail loudly on a partial/empty copy: an injector- or payload-less stage produces a silently broken MSI.
if (-not (Get-ChildItem (Join-Path $stage "injector") -File -Recurse -ErrorAction SilentlyContinue)) { throw "Staged injector folder is empty." }
if (-not (Get-ChildItem (Join-Path $stage "payload")  -File -Recurse -ErrorAction SilentlyContinue)) { throw "Staged payload folder is empty." }

# Publish the CLI alongside the host. Safe in one dir: the host (net10.0-windows) and CLI
# (net10.0) have disjoint app-dependency graphs, so no shared app DLL collides.
dotnet publish $cliProj -c $Configuration -p:Version=$Version -m:2 -nodeReuse:false -o $stage
if ($LASTEXITCODE -ne 0) { throw "CLI publish failed." }
dotnet publish $recoveryProj -c $Configuration -p:Version=$Version -m:2 -nodeReuse:false -o $stage
if ($LASTEXITCODE -ne 0) { throw "Recovery launcher publish failed." }

# Publish the sample app under samples/.
dotnet publish $sampleProj -c $Configuration -p:Version=$Version -m:2 -nodeReuse:false -o (Join-Path $stage "samples")
if ($LASTEXITCODE -ne 0) { throw "Sample publish failed." }

# Remove PDBs from stage — they are not shipped.
Get-ChildItem -Path $stage -Recurse -Filter "*.pdb" | Remove-Item -Force

# Build the MSI.
dotnet build $actionsProj -c $Configuration -p:Version=$Version -f net472 -m:2 -nodeReuse:false
if ($LASTEXITCODE -ne 0) { throw "Upgrade migration actions build failed." }
$msi = Join-Path $here "SnoopMCP.msi"
wix build (Join-Path $here "Package.wxs") `
    -d "Version=$Version" `
    -d "PublishDir=$stage" `
    -d "UpgradeActions=$actionsDll" `
    -ext WixToolset.Util.wixext `
    -ext WixToolset.UI.wixext `
    -arch x64 `
    -b "$here" `
    -o "$msi"
if ($LASTEXITCODE -ne 0) { throw "wix build failed." }
if (-not (Test-Path $msi)) { throw "wix reported success but $msi was not produced." }
& (Join-Path $here 'Test-Package.ps1') -PackagePath $msi `
    -DtfAssemblyPath (Join-Path (Split-Path $actionsDll) 'WixToolset.Dtf.WindowsInstaller.dll') `
    -PublishDirectory $stage

Write-Host "Built $msi (version $Version)."
