# Runs the Debug build, drops a sample capture into the caught-captures folder,
# and reports what the app did: window placement, styles, hung files and the log.
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src\Clothesline\bin\Debug\net8.0-windows\Clothesline.exe'
$inbox = Join-Path $env:LOCALAPPDATA 'Clothesline\Screenshots'
$settings = Join-Path $env:LOCALAPPDATA 'Clothesline\settings.json'
$log = Join-Path $env:LOCALAPPDATA 'Clothesline\log.txt'

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class W {
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@

Stop-Process -Name Clothesline -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
if (Test-Path $log) { Remove-Item $log }
Start-Process $exe
Start-Sleep 3

$h = [W]::FindWindow($null, 'Clothesline')
"hwnd: $h"
if ($h -ne [IntPtr]::Zero) {
  $r = New-Object W+RECT; [W]::GetWindowRect($h, [ref]$r) | Out-Null
  "rect: $($r.L),$($r.T) - $($r.R),$($r.B)  visible: $([W]::IsWindowVisible($h))"
  $ex = [W]::GetWindowLongPtr($h, -20).ToInt64()
  "exstyle: 0x{0:X}  toolwindow={1} noactivate={2} topmost={3} transparent={4}" -f $ex, (($ex -band 0x80) -ne 0), (($ex -band 0x08000000) -ne 0), (($ex -band 8) -ne 0), (($ex -band 0x20) -ne 0)
  # Hit test at the middle of the strip (no photo there) and at the top-left corner.
  $midX = [int](($r.L + $r.R) / 2); $midY = $r.T + 100
  $ht = [W]::SendMessage($h, 0x84, [IntPtr]::Zero, [IntPtr]((($midY -band 0xFFFF) -shl 16) -bor ($midX -band 0xFFFF)))
  "nchittest at empty strip: $ht  (expected -1 = HTTRANSPARENT)"
}

# Drop a capture in.
$sample = Join-Path $root 'docs\samples\sunset.png'
$target = Join-Path $inbox ("Screenshot test " + (Get-Date -Format 'HHmmss') + ".png")
Copy-Item $sample $target
Start-Sleep 4
"pegged:"; (Get-Content $settings | ConvertFrom-Json).pegged
"window visible now: $([W]::IsWindowVisible($h))"
$r = New-Object W+RECT; [W]::GetWindowRect($h, [ref]$r) | Out-Null
"rect now: $($r.L),$($r.T) - $($r.R),$($r.B)"

# A hit test where the single card should hang: centre of the strip, 60px below the rope.
$midX = [int](($r.L + $r.R) / 2); $cardY = $r.T + 60
$ht = [W]::SendMessage($h, 0x84, [IntPtr]::Zero, [IntPtr]((($cardY -band 0xFFFF) -shl 16) -bor ($midX -band 0xFFFF)))
"nchittest over the card: $ht  (expected 1 = HTCLIENT while revealed)"

# Render our own window's content, as composed, into a PNG next to the samples.
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [W]::PrintWindow($h, $hdc, 2)
$g.ReleaseHdc($hdc); $g.Dispose()
$out = Join-Path $root 'docs\selftest-window.png'
$bmp.Save($out); $bmp.Dispose()
"printwindow: $ok -> $out"

"log:"; Get-Content $log
