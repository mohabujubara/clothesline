# Assembles every form of the Snapline mark from the design files in design\:
#   src\Snapline\Assets\Snapline.ico   the app icon: the white tile at 32 px and up, the simplified bare mark below
#   src\Snapline\Assets\Mark.png       the bare mark, transparent, for headers and banners
#   src\Snapline\Assets\Lockup.png     mark + wordmark, charcoal
#   src\Snapline\Assets\LockupDark.png mark + wordmark, white
#   docs\icon.png, docs\logo-512.png   for the README and posts
#   docs\social.png                    the social card
#   installer\wizard-small.bmp         55 x 58, the small image in Setup
#   installer\wizard.bmp               164 x 314, the side image in Setup
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$design = Join-Path $root 'design'
$assets = Join-Path $root 'src\Snapline\Assets'
New-Item -ItemType Directory -Force $assets, (Join-Path $root 'docs'), (Join-Path $root 'installer') | Out-Null

function Load($name) { [System.Drawing.Bitmap]::FromFile((Join-Path $design $name)) }
function Scaled([System.Drawing.Bitmap]$src, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.CompositingQuality = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, $size, $size))
    $g.Dispose()
    return $bmp
}

$tile = Load 'mark-tile-1024.png'
$mark = Load 'mark-1024.png'
$small = Load 'mark-small-64.png'

# The icon: the tile where there is room for it, the simplified bare mark where there is not.
$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
$images = @()
foreach ($sz in $sizes) {
    $bmp = if ($sz -le 24) { Scaled $small $sz } else { Scaled $tile $sz }
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,@{ size = $sz; bytes = $ms.ToArray() }
    if ($sz -eq 256) { $bmp.Save((Join-Path $root 'docs\icon.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
}
$icoOut = Join-Path $assets 'Snapline.ico'
$fs = [System.IO.File]::Create($icoOut)
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

(Scaled $tile 512).Save((Join-Path $root 'docs\logo-512.png'), [System.Drawing.Imaging.ImageFormat]::Png)
Copy-Item (Join-Path $design 'mark-1024.png') (Join-Path $assets 'Mark.png') -Force
Copy-Item (Join-Path $design 'lockup-2400.png') (Join-Path $assets 'Lockup.png') -Force
Copy-Item (Join-Path $design 'lockup-dark-2400.png') (Join-Path $assets 'LockupDark.png') -Force
Copy-Item (Join-Path $design 'social-1280x640.png') (Join-Path $root 'docs\social.png') -Force
New-Item -ItemType Directory -Force (Join-Path $assets 'Fonts') | Out-Null
Copy-Item (Join-Path $design 'fonts\Manrope-ExtraBold.ttf') (Join-Path $assets 'Fonts\Manrope-ExtraBold.ttf') -Force
Copy-Item (Join-Path $design 'fonts\OFL.txt') (Join-Path $assets 'Fonts\OFL.txt') -Force

# Setup wizard images: the mark on white, the name in Manrope from the font file.
$fonts = New-Object System.Drawing.Text.PrivateFontCollection
$fonts.AddFontFile((Join-Path $design 'fonts\Manrope-ExtraBold.ttf'))
$family = $fonts.Families[0]
function Canvas24([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::White)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
    return @($bmp, $g)
}
$charcoal = [System.Drawing.Color]::FromArgb(255, 28, 31, 42)
$slate = [System.Drawing.Color]::FromArgb(255, 107, 114, 128)
$s = Canvas24 55 58; $s[1].DrawImage($mark, (New-Object System.Drawing.Rectangle 3, 5, 48, 48)); $s[1].Dispose()
$s[0].Save((Join-Path $root 'installer\wizard-small.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp); $s[0].Dispose()
$side = Canvas24 164 314; $g2 = $side[1]
$g2.DrawImage($mark, (New-Object System.Drawing.Rectangle 32, 40, 100, 100))
$fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'
$g2.DrawString('Snapline', (New-Object System.Drawing.Font $family, 18, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Point)), (New-Object System.Drawing.SolidBrush $charcoal), (New-Object System.Drawing.RectangleF(0, 156, 164, 36)), $fmt)
$g2.DrawString("Screenshots,`nhung out to dry.", (New-Object System.Drawing.Font 'Segoe UI', 8.5), (New-Object System.Drawing.SolidBrush $slate), (New-Object System.Drawing.RectangleF(0, 196, 164, 40)), $fmt)
$g2.Dispose()
$side[0].Save((Join-Path $root 'installer\wizard.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp); $side[0].Dispose()
$tile.Dispose(); $mark.Dispose(); $small.Dispose()
Write-Host "Assembled $icoOut, Mark.png, Lockup.png, docs\icon.png, docs\logo-512.png, docs\social.png and the Setup images"
