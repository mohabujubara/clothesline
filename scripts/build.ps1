# Builds Snapline into dist\.
#   scripts\build.ps1            framework-dependent single file (needs the .NET 8 Desktop Runtime), ~25 MB
#   scripts\build.ps1 -Portable  self-contained single file, runs anywhere, ~70 MB
param([switch]$Portable, [switch]$Installer, [string]$Version)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'src\Snapline\Snapline.csproj'
$dist = Join-Path $root 'dist'

if (-not (Test-Path (Join-Path $root 'src\Snapline\Assets\Snapline.ico'))) {
    & (Join-Path $PSScriptRoot 'make-brand.ps1')
}

if ($Installer -and -not $Portable) { $Portable = $true }
$outDir = if ($Portable) { Join-Path $dist 'portable' } else { Join-Path $dist 'win-x64' }
$args = @('publish', $proj, '-c', 'Release', '-r', 'win-x64', '-o', $outDir,
          '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=none')
$args += if ($Portable) { '--self-contained', 'true', '-p:EnableCompressionInSingleFile=true' } else { '--self-contained', 'false' }
# The version from the release tag goes into the exe's own properties too.
if ($Version -and $Version -ne '0.0.0') { $args += "-p:Version=$Version" }
dotnet @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "`nBuilt $(Join-Path $outDir 'Snapline.exe')"

# scripts\build.ps1 -Installer  also builds dist\Snapline-<version>-Setup.exe with Inno Setup
if ($Installer) {
    $iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { Write-Error "Inno Setup 6 not found"; exit 1 }
    $portableDir = Join-Path $dist "portable"
    if (-not (Test-Path (Join-Path $portableDir "Snapline.exe"))) { Write-Error "Build the portable exe first: scripts\build.ps1 -Portable"; exit 1 }
    $ver = if ($Version) { $Version } else { ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Select-Object -First 1 }
    & $iscc (Join-Path $root 'installer\snapline.iss') "/DVersion=$ver" "/DSource=$portableDir" /Q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "Built $(Join-Path $dist "Snapline-$ver-Setup.exe")"
}
