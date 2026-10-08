# Proves that clicks go through the strip into another program's window.
# A magenta window of our own goes under the strip, the strip is re-asserted on
# top, and a synthetic click lands on a bare part of the strip. The magenta
# window reports whether it received the click. The pointer is put back after.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src\Clothesline\bin\Debug\net8.0-windows10.0.19041.0\Clothesline.exe'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing @"
using System; using System.Runtime.InteropServices; using System.Text;
public static class Q {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, IntPtr extra);
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
[Q]::SetProcessDPIAware() | Out-Null

$proc = Get-Process Clothesline -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*bin\Debug*' } | Select-Object -First 1
if (-not $proc) { Start-Process $exe; Start-Sleep 3; $proc = Get-Process Clothesline | Where-Object { $_.Path -like '*bin\Debug*' } | Select-Object -First 1 }
$panel = [Q]::Find([uint32]$proc.Id, 'Clothesline')
$r = New-Object Q+RECT; [Q]::GetWindowRect($panel, [ref]$r) | Out-Null
"panel: $panel rect $($r.L),$($r.T)-$($r.R),$($r.B)"

$w = 500; $h = $r.B - $r.T
$x = [int](($r.L + $r.R) / 2 - $w / 2) - 400; $y = $r.T   # left of the middle, away from any card
$form = New-Object System.Windows.Forms.Form
$form.FormBorderStyle = 'None'; $form.StartPosition = 'Manual'; $form.ShowInTaskbar = $false
$form.BackColor = [System.Drawing.Color]::Magenta; $form.TopMost = $true
$form.Location = New-Object System.Drawing.Point($x, $y); $form.Size = New-Object System.Drawing.Size($w, $h)
$script:clicks = 0
$form.Add_MouseDown({ $script:clicks++ })
$form.Show(); [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 300
[Q]::SetWindowPos($panel, [IntPtr](-1), 0, 0, 0, 0, 0x13) | Out-Null
[System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 300

$saved = New-Object Q+POINT; [Q]::GetCursorPos([ref]$saved) | Out-Null
function ClickAt($px, $py) {
  [Q]::SetCursorPos($px, $py) | Out-Null; Start-Sleep -Milliseconds 120
  [System.Windows.Forms.Application]::DoEvents()
  [Q]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60
  [Q]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 200
  [System.Windows.Forms.Application]::DoEvents()
}
$cx = $x + 120; $cy = $y + [int]($h * 0.6)
$pt = New-Object Q+POINT; $pt.X = $cx; $pt.Y = $cy
"window under point before click: $([Q]::WindowFromPoint($pt)) (magenta form is $($form.Handle))"
ClickAt $cx $cy
"tucked: clicks received by the magenta window = $script:clicks  (expected 1)"

# Reveal the line (second launch toggles it) and click a bare spot again.
Start-Process $exe; Start-Sleep -Milliseconds 1500
[Q]::SetWindowPos($panel, [IntPtr](-1), 0, 0, 0, 0, 0x13) | Out-Null
[System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 400
ClickAt $cx $cy
"revealed: clicks received by the magenta window = $script:clicks  (expected 2)"
Start-Process $exe; Start-Sleep -Milliseconds 600   # toggle back

[Q]::SetCursorPos($saved.X, $saved.Y) | Out-Null
$form.Close()
