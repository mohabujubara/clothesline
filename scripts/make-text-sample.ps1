# A sample screenshot with text in it, for trying "Copy text".
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$bmp = New-Object System.Drawing.Bitmap 900, 300
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::White)
$g.TextRenderingHint = 'AntiAliasGridFit'
$font = New-Object System.Drawing.Font 'Segoe UI', 28
$g.DrawString("Screenshots, hung out to dry.`nClick to copy. Hold to edit. Drag to share.", $font, [System.Drawing.Brushes]::Black, 40, 60)
$out = Join-Path $root 'docs\samples\text.png'
$bmp.Save($out); $g.Dispose(); $bmp.Dispose(); "Wrote $out"
