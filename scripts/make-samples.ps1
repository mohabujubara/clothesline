# Draws three sample "screenshots" into docs\samples, for the README art and for trying the app.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$dir = Join-Path $root 'docs\samples'
New-Item -ItemType Directory -Force $dir | Out-Null

function Rounded([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}
function Color($a, $r, $g, $b) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

# 1. A window with a document.
$bmp = New-Object System.Drawing.Bitmap 1200, 800
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'
$g.Clear((Color 255 246 246 248))
$g.FillRectangle((New-Object System.Drawing.SolidBrush (Color 255 232 232 236)), 0, 0, 1200, 60)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (Color 255 255 95 87)), 24, 20, 20, 20)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (Color 255 255 189 46)), 54, 20, 20, 20)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (Color 255 40 200 64)), 84, 20, 20, 20)
$g.FillPath((New-Object System.Drawing.SolidBrush (Color 255 98 110 255)), (Rounded 80 130 480 40 8))
$y = 220
foreach ($w in 860, 700, 800, 560, 740, 640, 880, 500) {
    $g.FillPath((New-Object System.Drawing.SolidBrush (Color 255 200 200 206)), (Rounded 80 $y $w 18 9)); $y += 50
}
$bmp.Save((Join-Path $dir 'document.png')); $g.Dispose(); $bmp.Dispose()

# 2. A sunset.
$bmp = New-Object System.Drawing.Bitmap 1600, 1000
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'
$sky = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Point(0, 0)), (New-Object System.Drawing.Point(0, 1000)), (Color 255 110 78 220), (Color 255 255 150 110))
$g.FillRectangle($sky, 0, 0, 1600, 1000)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (Color 255 255 226 150)), 720, 560, 160, 160)
$hill = New-Object System.Drawing.Drawing2D.GraphicsPath
$hill.AddBezier(0, 760, 500, 560, 900, 980, 1600, 720); $hill.AddLine(1600, 720, 1600, 1000); $hill.AddLine(1600, 1000, 0, 1000); $hill.CloseFigure()
$g.FillPath((New-Object System.Drawing.SolidBrush (Color 255 70 50 120)), $hill)
$bmp.Save((Join-Path $dir 'sunset.png')); $g.Dispose(); $bmp.Dispose()

# 3. A dark dashboard, tall.
$bmp = New-Object System.Drawing.Bitmap 900, 1100
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'
$g.Clear((Color 255 32 34 48))
$g.FillPath((New-Object System.Drawing.SolidBrush (Color 255 90 94 120)), (Rounded 60 60 240 22 11))
$x = 70; $i = 0
foreach ($h in 300, 420, 360, 560, 520, 760, 640) {
    $c = if ($i -eq 5) { Color 255 120 130 250 } else { Color 255 62 70 110 }
    $g.FillPath((New-Object System.Drawing.SolidBrush $c), (Rounded $x (1000 - $h) 90 $h 10)); $x += 110; $i++
}
$bmp.Save((Join-Path $dir 'dashboard.png')); $g.Dispose(); $bmp.Dispose()
Write-Host "Wrote samples to $dir"
