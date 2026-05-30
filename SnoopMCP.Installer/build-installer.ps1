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
# -nodeReuse:false: never adopt a persistent MSBuild node spawned by Visual Studio, whose
# duplicate-cased PATH otherwise crashes the nested native (CL.exe) build.
dotnet build $hostProj -c $Configuration -nodeReuse:false
if ($LASTEXITCODE -ne 0) { throw "Host build failed." }

# Publish host (exe + framework-dependent deps) into the stage root.
dotnet publish $hostProj -c $Configuration --no-build -o $stage
if ($LASTEXITCODE -ne 0) { throw "Host publish failed." }

# injector/ + payload/ are loose staged folders that publish does not carry — copy them in.
Copy-Item (Join-Path $hostBin "injector") (Join-Path $stage "injector") -Recurse -Force
Copy-Item (Join-Path $hostBin "payload")  (Join-Path $stage "payload")  -Recurse -Force

# Publish the CLI alongside the host. Safe in one dir: the host (net10.0-windows) and CLI
# (net10.0) have disjoint app-dependency graphs, so no shared app DLL collides.
dotnet publish $cliProj -c $Configuration -o $stage
if ($LASTEXITCODE -ne 0) { throw "CLI publish failed." }

# Publish the sample app under samples/.
dotnet publish $sampleProj -c $Configuration -o (Join-Path $stage "samples")
if ($LASTEXITCODE -ne 0) { throw "Sample publish failed." }

# Remove PDBs from stage — they are not shipped.
Get-ChildItem -Path $stage -Recurse -Filter "*.pdb" | Remove-Item -Force

# Build the MSI.
$msi = Join-Path $here "SnoopMCP.msi"
wix build (Join-Path $here "Package.wxs") `
    -d "Version=$Version" `
    -d "PublishDir=$stage" `
    -ext WixToolset.Util.wixext `
    -ext WixToolset.UI.wixext `
    -arch x64 `
    -b "$here" `
    -o "$msi"
if ($LASTEXITCODE -ne 0) { throw "wix build failed." }

Write-Host "Built $msi (version $Version)."
