# Draws the app icon in code and packs it into src\Clothesline\Assets\Clothesline.ico.
# A soft blue to lilac square, a sagging line and three glass photos on clips.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'src\Clothesline\Assets\Clothesline.ico'
$pngOut = Join-Path $root 'docs\icon.png'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
New-Item -ItemType Directory -Force (Split-Path $pngOut) | Out-Null

function Rounded([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Draw([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 256.0

    # Background: a rounded square with a soft gradient, like the README art.
    $bg = Rounded (4 * $s) (4 * $s) (248 * $s) (248 * $s) (56 * $s)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, 0)), (New-Object System.Drawing.PointF($size, $size)),
        [System.Drawing.Color]::FromArgb(255, 226, 228, 255), [System.Drawing.Color]::FromArgb(255, 252, 226, 230))
    $g.FillPath($brush, $bg)

    # The line, sagging across.
    $rope = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 95, 95, 105)), (5 * $s)
    $rope.StartCap = 'Round'; $rope.EndCap = 'Round'
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddBezier((New-Object System.Drawing.PointF((-10 * $s), (78 * $s))), (New-Object System.Drawing.PointF((90 * $s), (104 * $s))),
                    (New-Object System.Drawing.PointF((166 * $s), (104 * $s))), (New-Object System.Drawing.PointF((266 * $s), (78 * $s))))
    $g.DrawPath($rope, $path)

    # Three photos hanging a little crooked.
    $cards = @(
        @{ x = 34;  y = 96;  w = 68; h = 56; tilt = -3; c1 = @(255, 255, 255, 255); c2 = @(255, 120, 130, 250) },
        @{ x = 94;  y = 100; w = 68; h = 56; tilt = 2;  c1 = @(255, 255, 150, 120); c2 = @(255, 120, 70, 170) },
        @{ x = 154; y = 96;  w = 68; h = 56; tilt = -2; c1 = @(255, 40, 44, 66);  c2 = @(255, 110, 120, 240) }
    )
    foreach ($c in $cards) {
        $state = $g.Save()
        $cx = ($c.x + $c.w / 2) * $s; $cy = ($c.y - 10) * $s
        $g.TranslateTransform($cx, $cy)
        $g.RotateTransform($c.tilt)
        $g.TranslateTransform(-$cx, -$cy)
        # Shadow
        $sh = Rounded (($c.x) * $s) (($c.y + 4) * $s) ($c.w * $s) ($c.h * $s) (12 * $s)
        $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(40, 0, 0, 0))), $sh)
        # Glass frame
        $frame = Rounded ($c.x * $s) ($c.y * $s) ($c.w * $s) ($c.h * $s) (12 * $s)
        $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(230, 250, 250, 252))), $frame)
        $g.DrawPath((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(50, 0, 0, 0)), (1 * $s)), $frame)
        # Photo
        $inset = 5 * $s
        $photo = Rounded ($c.x * $s + $inset) ($c.y * $s + $inset) ($c.w * $s - 2 * $inset) ($c.h * $s - 2 * $inset) (8 * $s)
        $pb = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.PointF(($c.x * $s), ($c.y * $s))), (New-Object System.Drawing.PointF((($c.x + $c.w) * $s), (($c.y + $c.h) * $s))),
            [System.Drawing.Color]::FromArgb($c.c1[0], $c.c1[1], $c.c1[2], $c.c1[3]), [System.Drawing.Color]::FromArgb($c.c2[0], $c.c2[1], $c.c2[2], $c.c2[3]))
        $g.FillPath($pb, $photo)
        # Clip
        $clip = Rounded (($c.x + $c.w / 2 - 4) * $s) (($c.y - 11) * $s) (8 * $s) (22 * $s) (3 * $s)
        $cb = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.PointF((($c.x + $c.w / 2 - 4) * $s), 0)), (New-Object System.Drawing.PointF((($c.x + $c.w / 2 + 4) * $s), 0)),
            [System.Drawing.Color]::FromArgb(255, 170, 170, 175), [System.Drawing.Color]::FromArgb(255, 235, 235, 238))
        $g.FillPath($cb, $clip)
        $g.DrawPath((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(60, 0, 0, 0)), (0.8 * $s)), $clip)
        $g.Restore($state)
    }
    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = @()
foreach ($sz in $sizes) {
    $bmp = Draw $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,@{ size = $sz; bytes = $ms.ToArray() }
    if ($sz -eq 256) { $bmp.Save($pngOut, [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
}

# ICO container with PNG entries.
$fs = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $sz = if ($img.size -ge 256) { 0 } else { $img.size }
    $w.Write([byte]$sz); $w.Write([byte]$sz); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$img.bytes.Length); $w.Write([uint32]$offset)
    $offset += $img.bytes.Length
}
foreach ($img in $images) { $w.Write($img.bytes) }
$w.Flush(); $fs.Close()
Write-Host "Wrote $out and $pngOut"
