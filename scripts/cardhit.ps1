# Checks that the strip takes the pointer over a photo and lets it through beside one.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src\Clothesline\bin\Debug\net8.0-windows10.0.19041.0\Clothesline.exe'
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text;
public static class K {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  public static IntPtr Find(uint pid, string title) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid) { var t = new StringBuilder(256); GetWindowText(h, t, 256); if (t.ToString() == title) { found = h; return false; } } return true; }, IntPtr.Zero);
    return found;
  }
}
"@
[K]::SetProcessDPIAware() | Out-Null
$t = Join-Path $env:LOCALAPPDATA 'Clothesline\Screenshots\Screenshot clicktest.png'
Start-Process $exe; Start-Sleep 3
$proc = Get-Process Clothesline | Select-Object -First 1
Start-Process $exe; Start-Sleep -Milliseconds 1500   # toggle: reveal
# The capture arrives after launch, so the watcher sees it as new.
Copy-Item (Join-Path $root 'docs\samples\sunset.png') $t -Force; Start-Sleep -Milliseconds 2500
$panel = [K]::Find([uint32]$proc.Id, 'Clothesline')
$r = New-Object K+RECT; [K]::GetWindowRect($panel, [ref]$r) | Out-Null
$s = Get-Content (Join-Path $env:LOCALAPPDATA 'Clothesline\settings.json') | ConvertFrom-Json
$prop = $s.spots.PSObject.Properties | Where-Object { $_.Name -like '*clicktest*' } | Select-Object -First 1
$frac = if ($prop) { [double]$prop.Value } else { 0.5 }
"panel $panel rect $($r.L),$($r.T)-$($r.R),$($r.B)  spot fraction $frac"
$scale = ($r.B - $r.T) / 240.0
$cx = [int]($r.L + $frac * ($r.R - $r.L)); $cy = [int]($r.T + 110 * $scale)
$saved = New-Object K+POINT; [K]::GetCursorPos([ref]$saved) | Out-Null
[K]::SetCursorPos($cx, $cy) | Out-Null; Start-Sleep -Milliseconds 300
$pt = New-Object K+POINT; $pt.X = $cx; $pt.Y = $cy
$under = [K]::WindowFromPoint($pt)
"over the card:   is panel? $($under -eq $panel)   (expected True)"
$pt.X = $cx + [int](320 * $scale)
[K]::SetCursorPos($pt.X, $cy) | Out-Null; Start-Sleep -Milliseconds 300
$under2 = [K]::WindowFromPoint($pt)
"beside the card: is panel? $($under2 -eq $panel)   (expected False)"
[K]::SetCursorPos($saved.X, $saved.Y) | Out-Null
Stop-Process -Name Clothesline -Force -ErrorAction SilentlyContinue; Start-Sleep 1
[System.IO.File]::Delete($t); "cleaned"
