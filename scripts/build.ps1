# Builds Snapline into dist\.
#   scripts\build.ps1            framework-dependent single file (needs the .NET 8 Desktop Runtime), ~1 MB
#   scripts\build.ps1 -Portable  self-contained single file, runs anywhere, ~70 MB
param([switch]$Portable)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'src\Snapline\Snapline.csproj'
$dist = Join-Path $root 'dist'

if (-not (Test-Path (Join-Path $root 'src\Snapline\Assets\Snapline.ico'))) {
    & (Join-Path $PSScriptRoot 'make-icon.ps1')
}

$outDir = if ($Portable) { Join-Path $dist 'portable' } else { Join-Path $dist 'win-x64' }
$args = @('publish', $proj, '-c', 'Release', '-r', 'win-x64', '-o', $outDir,
          '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=none')
$args += if ($Portable) { '--self-contained', 'true', '-p:EnableCompressionInSingleFile=true' } else { '--self-contained', 'false' }
dotnet @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "`nBuilt $(Join-Path $outDir 'Snapline.exe')"
