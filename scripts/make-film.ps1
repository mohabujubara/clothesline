# Renders the demo frames with the app itself and encodes them:
#   docs\demo.mp4   H.264, 1600x900, 30 fps, for Twitter and the release
#   docs\demo.gif   for the README
#   docs\hero-light.png, docs\hero-dark.png  the README banner
# Needs ffmpeg on the PATH or in $env:FFMPEG.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src\Clothesline\bin\Debug\net8.0-windows10.0.19041.0\Clothesline.exe'
$frames = Join-Path $env:TEMP 'clothesline-frames'
$ffmpeg = if ($env:FFMPEG) { $env:FFMPEG } else { 'ffmpeg' }

if (Test-Path $frames) { Get-ChildItem $frames -Filter *.png | ForEach-Object { [System.IO.File]::Delete($_.FullName) } }
New-Item -ItemType Directory -Force $frames | Out-Null

Set-Location $root
Start-Process $exe -ArgumentList '--hero', (Join-Path $root 'docs\hero-light.png') -Wait
Start-Process $exe -ArgumentList '--hero-dark', (Join-Path $root 'docs\hero-dark.png') -Wait
$p = Start-Process $exe -ArgumentList '--demo', $frames -Wait -PassThru
"frames exit $($p.ExitCode): $((Get-ChildItem $frames -Filter *.png).Count) frames"

& $ffmpeg -y -loglevel error -framerate 30 -i (Join-Path $frames 'frame_%04d.png') -c:v libx264 -pix_fmt yuv420p -crf 20 -preset slow -movflags +faststart (Join-Path $root 'docs\demo.mp4')
& $ffmpeg -y -loglevel error -framerate 30 -i (Join-Path $frames 'frame_%04d.png') -vf "fps=15,scale=1000:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=160[p];[s1][p]paletteuse=dither=bayer:bayer_scale=4" -loop 0 (Join-Path $root 'docs\demo.gif')
Get-Item (Join-Path $root 'docs\demo.mp4'), (Join-Path $root 'docs\demo.gif'), (Join-Path $root 'docs\hero-light.png') | Select-Object Name, @{n='MB';e={[math]::Round($_.Length/1MB,2)}}
