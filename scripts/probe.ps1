# Checks that the strip is really transparent and draws the line when revealed.
# A magenta window of our own is placed under the strip, the strip is re-asserted
# on top, and only that region (covered entirely by our two windows) is captured.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src\Clothesline\bin\Debug\net8.0-windows\Clothesline.exe'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing @"
using System; using System.Runtime.InteropServices; using System.Text; using System.Collections.Generic;
public static class P {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
  [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
  [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public static IntPtr Find(uint pid, string title) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid) { var t = new StringBuilder(256); GetWindowText(h, t, 256); if (t.ToString() == title) { found = h; return false; } } return true; }, IntPtr.Zero);
    return found;
  }
  public static System.Drawing.Bitmap Grab(int x, int y, int w, int h) {
    var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    using (var g = System.Drawing.Graphics.FromImage(bmp)) { var dst = g.GetHdc(); var src = GetDC(IntPtr.Zero); BitBlt(dst, 0, 0, w, h, src, x, y, 0x00CC0020); ReleaseDC(IntPtr.Zero, src); g.ReleaseHdc(dst); }
    return bmp;
  }
}
"@
[P]::SetProcessDPIAware() | Out-Null

$proc = Get-Process Clothesline -ErrorAction SilentlyContinue
if (-not $proc) { Start-Process $exe; Start-Sleep 3; $proc = Get-Process Clothesline }
$panel = [P]::Find([uint32]$proc.Id, 'Clothesline')
"panel hwnd: $panel"
$r = New-Object P+RECT; [P]::GetWindowRect($panel, [ref]$r) | Out-Null
"panel rect: $($r.L),$($r.T)-$($r.R),$($r.B)"

# Our backdrop, under the middle of the strip.
$w = 700; $h = $r.B - $r.T
$x = [int](($r.L + $r.R) / 2 - $w / 2); $y = $r.T
$form = New-Object System.Windows.Forms.Form
$form.FormBorderStyle = 'None'; $form.StartPosition = 'Manual'; $form.ShowInTaskbar = $false
$form.BackColor = [System.Drawing.Color]::Magenta; $form.TopMost = $true
$form.Location = New-Object System.Drawing.Point($x, $y); $form.Size = New-Object System.Drawing.Size($w, $h)
$form.Show(); [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 300
[P]::SetWindowPos($form.Handle, [IntPtr](-1), $x, $y, $w, $h, 0x10) | Out-Null
# The strip back on top of our backdrop.
[P]::SetWindowPos($panel, [IntPtr](-1), 0, 0, 0, 0, 0x13) | Out-Null
[System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 600

function Stats($bmp) {
  $mag = 0; $other = 0
  for ($yy = 0; $yy -lt $bmp.Height; $yy += 3) { for ($xx = 0; $xx -lt $bmp.Width; $xx += 3) {
    $c = $bmp.GetPixel($xx, $yy); if ($c.R -gt 240 -and $c.G -lt 15 -and $c.B -gt 240) { $mag++ } else { $other++ } } }
  "magenta=$mag other=$other"
}

$a = [P]::Grab($x, $y, $w, $h)
$a.Save((Join-Path $root 'docs\probe-tucked.png')); "tucked: $(Stats $a)"; $a.Dispose()

# Launching a second copy asks the running one to toggle the line.
Start-Process $exe; Start-Sleep -Milliseconds 1500
[P]::SetWindowPos($panel, [IntPtr](-1), 0, 0, 0, 0, 0x13) | Out-Null
[System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 300
$b = [P]::Grab($x, $y, $w, $h)
$b.Save((Join-Path $root 'docs\probe-revealed.png')); "revealed: $(Stats $b)"; $b.Dispose()

Start-Process $exe; Start-Sleep -Milliseconds 800   # toggle back
$form.Close()
