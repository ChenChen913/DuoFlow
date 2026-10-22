# =====================================================================
# DuoFlow - P0 real-machine verification (transparency / click-through /
# live preview).  [scripts/acceptance]
#
# WHAT IT PROVES (the three things that made the M0 overlay useless when
# they were wrong, and that no unit test can see):
#
#   P0-1 TRANSPARENCY  the overlay really lets the desktop through.
#        Method: sample a WHITE reference window through the overlay (S1)
#        and again with the overlay moved off-screen (S2). Both must be
#        bright and close. A black/opaque overlay gives S1 ~ 0.
#        Includes a causal A/B (LAYERED cleared / LWA_ALPHA 0 / restored)
#        so a future regression can be attributed to a specific style bit
#        instead of guessed at.
#
#   P0-2 CLICK-THROUGH  real mouse input reaches the window UNDER the
#        overlay. Method: real cursor clicks and a real 16-step drag on the
#        console's slider, with the overlay forced to the top of the
#        topmost band. Asserted through UIAutomation's RangeValuePattern,
#        not by looking at pixels.
#        A CONTROL step runs first with the console on top - if that fails,
#        the harness itself is broken and every result below is void.
#
#   P1-a LIVE PREVIEW  the console's preview panel shows live desktop
#        content. Method: a white reference window is placed inside the
#        preview's mapped region; the panel's interior must be non-black
#        and/or change when the reference disappears.
#
# WHY A REFERENCE WINDOW AND NOT A SCREENSHOT DIFF: the desktop wallpaper
# is arbitrary. A known white patch turns "is it transparent" into a
# number, and the off-screen A/B removes the wallpaper from the equation.
# (Known trap: a white reference on a white desktop proves nothing. Run
# this with a non-white wallpaper, or accept that P0-1 is only weakly
# covered - the A/B still catches an opaque overlay.)
#
# USAGE (Windows PowerShell 5.1, no admin, INTERACTIVE desktop required -
# it moves the real mouse and needs a real compositor):
#   .\scripts\acceptance\overlay-p0-verify.ps1
#   .\scripts\acceptance\overlay-p0-verify.ps1 -OutputDir D:\tmp\p0
#   .\scripts\acceptance\overlay-p0-verify.ps1 -Exe <path to DuoFlow.App.exe>
#
# OUTPUT: p0-verify.txt (human log) + p0-*.png (evidence) +
#         overlay-p0-result.json (machine verdict) in -OutputDir.
# EXIT CODE: 0 = all four verdicts PASS, 1 = at least one FAILED.
# NOTE: takes ~90 s and steals the mouse for part of it. Do not run it
# while you are using the machine.
# =====================================================================

param(
    [string]$RepoRoot = "",
    [string]$OutputDir = "",
    [string]$Exe = "",
    [int]$StartupSeconds = 14
)

$ErrorActionPreference = 'Continue'

if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path }
if (-not $OutputDir) { $OutputDir = Join-Path $env:TEMP "duoflow-p0-verify" }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$out = $OutputDir
$log = Join-Path $out 'p0-verify.txt'
Remove-Item $log -ErrorAction SilentlyContinue
function DfLog([string]$m) { $m | Out-File -FilePath $log -Append -Encoding UTF8 }

if (-not $Exe) {
    $found = Get-ChildItem (Join-Path $RepoRoot "src\DuoFlow.App\bin") -Recurse -Filter "DuoFlow.App.exe" -ErrorAction SilentlyContinue |
             Where-Object { $_.FullName -notmatch "\\obj\\" } |
             Sort-Object LastWriteTime -Descending |
             Select-Object -First 1
    if ($found) { $Exe = $found.FullName }
}
if (-not $Exe -or -not (Test-Path $Exe)) {
    Write-Error "DuoFlow.App.exe not found. Build it first: dotnet build src/DuoFlow.App -p:Platform=x64"
    exit 1
}
$exe = $Exe
$dir = Split-Path $exe -Parent
$smokeJson = Join-Path $env:USERPROFILE 'duoflow-overlay-smoke.json'

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type -Namespace W -Name M -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
[DllImport("user32.dll", EntryPoint="SetWindowLongPtrW", SetLastError=true)] public static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
[DllImport("user32.dll")] public static extern IntPtr GetTopWindow(IntPtr h);
[DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
[DllImport("user32.dll", SetLastError=true)] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint data, UIntPtr extra);
[DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
public struct RECT { public int Left, Top, Right, Bottom; }
public struct POINT { public int X, Y; }
'@
[void][W.M]::SetProcessDPIAware()

function DfStats([int]$x,[int]$y,[int]$w,[int]$h,[string]$png) {
  try {
    $bmp = New-Object System.Drawing.Bitmap($w,$h,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x,$y,0,0,(New-Object System.Drawing.Size($w,$h)))
    $g.Dispose()
    if ($png) { $bmp.Save($png,[System.Drawing.Imaging.ImageFormat]::Png) }
    $rect = New-Object System.Drawing.Rectangle(0,0,$w,$h)
    $data = $bmp.LockBits($rect,[System.Drawing.Imaging.ImageLockMode]::ReadOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $bytes = New-Object byte[] ($stride*$h)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0,$bytes,0,$bytes.Length)
    $bmp.UnlockBits($data); $bmp.Dispose()
    $sum=0.0; $sum2=0.0; $mn=255.0; $mx=0.0; $n=0
    for ($yy=0;$yy -lt $h;$yy++){
      $row=$yy*$stride
      for ($xx=0;$xx -lt $w;$xx++){
        $i=$row+$xx*4
        $l=0.114*[double]$bytes[$i+0]+0.587*[double]$bytes[$i+1]+0.299*[double]$bytes[$i+2]
        $sum+=$l; $sum2+=$l*$l
        if($l -lt $mn){$mn=$l}; if($l -gt $mx){$mx=$l}; $n++
      }
    }
    $mean=$sum/$n
    $var=($sum2/$n)-($mean*$mean); if($var -lt 0){$var=0}
    return [pscustomobject]@{Mean=[math]::Round($mean,2);Std=[math]::Round([math]::Sqrt($var),2);Min=[math]::Round($mn,1);Max=[math]::Round($mx,1)}
  } catch {
    DfLog ("    DfStats ERROR at ${x},${y} - " + $_.Exception.Message)
    return [pscustomobject]@{Mean=-1;Std=-1;Min=-1;Max=-1}
  }
}
function DfFmt($s) { return ("mean={0} std={1} min={2} max={3}" -f $s.Mean,$s.Std,$s.Min,$s.Max) }

$vx=[W.M]::GetSystemMetrics(76); $vy=[W.M]::GetSystemMetrics(77)
$vw=[W.M]::GetSystemMetrics(78); $vh=[W.M]::GetSystemMetrics(79)
function DfAbs([int]$x,[int]$y) {
  # MOUSEEVENTF_VIRTUALDESK (0x4000) is mandatory: without it the absolute
  # coordinates are normalized against the PRIMARY monitor only, and on a
  # multi-monitor desktop every injected point lands compressed toward the
  # top-left (this already produced a bogus "drag FAILED" verdict once).
  $nx=[int](($x-$vx)*65535.0/($vw-1)); $ny=[int](($y-$vy)*65535.0/($vh-1))
  [W.M]::mouse_event(0x0001 -bor 0x8000 -bor 0x4000,[uint32]$nx,[uint32]$ny,0,[UIntPtr]::Zero)
}
function DfClick([int]$x,[int]$y) {
  [void][W.M]::SetCursorPos($x,$y); Start-Sleep -Milliseconds 320
  [W.M]::mouse_event(0x0002,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 160
  [W.M]::mouse_event(0x0004,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 800
}
function DfDrag([int]$xs,[int]$xe,[int]$y) {
  [void][W.M]::SetCursorPos($xs,$y); Start-Sleep -Milliseconds 350
  [W.M]::mouse_event(0x0002,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 350
  for ($k=1; $k -le 16; $k++) { DfAbs ([int]($xs+($xe-$xs)*$k/16.0)) $y; Start-Sleep -Milliseconds 55 }
  Start-Sleep -Milliseconds 250
  [W.M]::mouse_event(0x0004,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 850
}

# verdicts (initialised so the summary never prints blanks on an early bail)
$a = 'N/A'; $b = 'N/A'; $bOk = $false
$s1 = [pscustomobject]@{Mean=-1}; $s2 = [pscustomobject]@{Mean=-1}
$s3 = [pscustomobject]@{Mean=-1}; $s4 = [pscustomobject]@{Mean=-1}; $s5 = [pscustomobject]@{Mean=-1}
$q1 = [pscustomobject]@{Mean=-1;Std=-1}
$pDelta = -1.0; $dMean = -1.0; $pOk = $false

# ================= launch =================
Get-Process -Name 'DuoFlow.App' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 900
$null = Start-Process -FilePath $exe -WorkingDirectory $dir -ArgumentList '--overlay-smoke' -PassThru
Start-Sleep -Seconds $StartupSeconds
$proc = Get-Process -Name 'DuoFlow.App' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { DfLog 'FATAL: app not running'; exit 1 }
$mypid = $proc.Id

$overlay = [IntPtr]::Zero; $console = [IntPtr]::Zero
$allTitles = @()
$h = [W.M]::GetTopWindow([IntPtr]::Zero)
while ($h -ne [IntPtr]::Zero) {
  $p2=0; [void][W.M]::GetWindowThreadProcessId($h,[ref]$p2)
  if ($p2 -eq $mypid) {
    $sb=New-Object System.Text.StringBuilder 256; [void][W.M]::GetWindowText($h,$sb,256)
    $t=$sb.ToString()
    $allTitles += ("0x{0:X8}='{1}'" -f [int64]$h,$t)
    # Exact match for the overlay: the console's title ALSO contains
    # "Overlay" ("DuoFlow Console - M0.3 Overlay / M1.2 Manual Progress"), so a -like
    # '*Overlay*' match silently grabs the wrong window.
    if ($t -eq 'DuoFlow Overlay') { $overlay=$h }
    elseif ($t -like 'DuoFlow Console*') { $console=$h }
  }
  $h = [W.M]::GetWindow($h,2)
}
DfLog '=== DuoFlow P0 real-machine verification ==='
DfLog ("exe  = " + $exe)
DfLog ("pid={0}  overlay=0x{1:X8}  console=0x{2:X8}" -f $mypid,[int64]$overlay,[int64]$console)
DfLog ("virtual screen = {0}x{1} at {2},{3}   remote session = {4}" -f $vw,$vh,$vx,$vy,([W.M]::GetSystemMetrics(0x1000)))
DfLog ("top-level windows of pid: " + ($allTitles -join ' ; '))
if ($overlay -eq [IntPtr]::Zero) { DfLog 'FATAL: overlay window not found'; exit 1 }

$exOv = [W.M]::GetWindowLongPtr($overlay,-20).ToInt64()
DfLog ("overlay exstyle = 0x{0:X8}  LAYERED={1} TRANSPARENT={2} NOACTIVATE={3} TOOLWINDOW={4} TOPMOST={5}" -f `
  $exOv, (($exOv -band 0x80000) -ne 0), (($exOv -band 0x20) -ne 0), (($exOv -band 0x8000000) -ne 0), (($exOv -band 0x80) -ne 0), (($exOv -band 0x8) -ne 0))
$r=New-Object W.M+RECT; [void][W.M]::GetWindowRect($overlay,[ref]$r)
$W=$r.Right-$r.Left; $H=$r.Bottom-$r.Top
DfLog ("overlay rect = {0},{1} {2}x{3}   (primary monitor; all layout math uses THIS)" -f $r.Left,$r.Top,$W,$H)

if (Test-Path $smokeJson) {
  try {
    $j = Get-Content $smokeJson -Raw | ConvertFrom-Json
    DfLog '--- app self-probe (duoflow-overlay-smoke.json) ---'
    DfLog ("  OverlayCreated={0} Fullscreen={1} Topmost={2} ClickThrough={3} NoActivate={4} ToolWindow={5} Dwm={6}" -f `
      $j.OverlayCreated,$j.Fullscreen,$j.Topmost,$j.ClickThrough,$j.NoActivate,$j.ToolWindow,$j.TransparentDwm)
    DfLog ("  LayeredApplied={0} BackdropApplied={1} CaptureState={2} frames={3} WarpState={4}" -f $j.LayeredApplied,$j.BackdropApplied,$j.CaptureState,$j.CaptureFrames,$j.WarpState)
    DfLog ("  Transparency.Pass={0} lum={1} (thr {2}) note='{3}'" -f $j.Transparency.Pass,$j.Transparency.ReferenceLuminance,$j.Transparency.PassThreshold,$j.Transparency.Note)
    DfLog ("  HitTest.Pass={0} class='{1}' pointsToOverlay={2}" -f $j.HitTest.Pass,$j.HitTest.WindowFromPointClass,$j.HitTest.PointsToOverlayChild)
    if ($j.Warnings -and $j.Warnings.Count -gt 0) { DfLog ("  Warnings: " + ($j.Warnings -join ' | ')) }
  } catch { DfLog ("  smoke json parse FAILED: " + $_.Exception.Message) }
} else { DfLog '--- app self-probe: duoflow-overlay-smoke.json NOT FOUND ---' }

$SWP = 0x1 -bor 0x2 -bor 0x10   # NOSIZE|NOMOVE|NOACTIVATE
$FRM = 0x1 -bor 0x2 -bor 0x10 -bor 0x20  # + FRAMECHANGED

# =========================================================
DfLog ''
DfLog '########## TEST 1 : P0-2 real input through the overlay ##########'
if ($console -ne [IntPtr]::Zero) {
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($console)
  $slCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Slider)
  # M2.5 added six more sliders (the tuning panel), so "first Slider in the
  # subtree" is no longer a safe way to find the Progress one - target its
  # AutomationId, with the old lookup as a fallback for pre-M2.5 binaries.
  $idCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'ProgressSlider')
  function DfFindSlider {
    $s = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $idCond)
    if (-not $s) { $s = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $slCond) }
    return $s
  }
  function DfVal {
    $sl = DfFindSlider
    if (-not $sl) { return 'NOSLIDER' }
    $pat = $sl.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    return ([string][math]::Round([double]$pat.Current.Value,4))
  }
  function DfSet([double]$v) {
    $sl = DfFindSlider
    if (-not $sl) { return }
    $pat = $sl.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    $pat.SetValue($v); Start-Sleep -Milliseconds 700
  }
  $sl = DfFindSlider
  if ($sl) {
    $sr = $sl.Current.BoundingRectangle
    $midY = [int]($sr.Y + $sr.Height/2)
    $TX0 = $sr.X + 10; $TW = $sr.Width - 20
    function DfThX([double]$v) { return [int]($TX0 + $TW*$v) }
    DfLog ("slider rect = {0},{1} {2}x{3}  midY={4}  x(0.30)={5} x(0.40)={6} x(0.70)={7}" -f `
      [int]$sr.X,[int]$sr.Y,[int]$sr.Width,[int]$sr.Height,$midY,(DfThX 0.30),(DfThX 0.40),(DfThX 0.70))

    DfLog ''
    DfLog '--- 1-CONTROL: console above overlay (harness sanity gate) ---'
    [void][W.M]::ShowWindow($console,9)
    [void][W.M]::SetWindowPos($console,[IntPtr](-1),0,0,0,0,$SWP)
    [void][W.M]::SetForegroundWindow($console); Start-Sleep -Milliseconds 900
    DfSet 0.30
    DfClick (DfThX 0.70) $midY
    $ctrl = DfVal
    DfLog ("  click at x(0.70) -> slider = {0}   {1}" -f $ctrl, $(if($ctrl -eq '0.7'){'PASS (injection works)'}else{'FAIL - results below unreliable'}))

    DfLog ''
    DfLog '--- 1-A: overlay forced to top of the TOPMOST band (production posture) ---'
    [void][W.M]::SetWindowPos($overlay,[IntPtr](-1),0,0,0,0,$SWP); Start-Sleep -Milliseconds 800
    $z=@(); $hh=[W.M]::GetTopWindow([IntPtr]::Zero); $c=0
    while ($hh -ne [IntPtr]::Zero -and $c -lt 12) {
      $p3=0; [void][W.M]::GetWindowThreadProcessId($hh,[ref]$p3)
      if ($p3 -eq $mypid -and [W.M]::IsWindowVisible($hh)) {
        $sb2=New-Object System.Text.StringBuilder 128; [void][W.M]::GetWindowText($hh,$sb2,128)
        $z += ("{0}" -f $(if($sb2.ToString() -like 'DuoFlow Console*'){'CONSOLE'}elseif($sb2.ToString() -eq 'DuoFlow Overlay'){'OVERLAY'}else{'other'})); $c++ }
      $hh=[W.M]::GetWindow($hh,2) }
    DfLog ("  z-order (top->bottom): " + ($z -join ' > '))
    DfSet 0.30
    DfClick (DfThX 0.40) $midY
    $a = DfVal
    if ($a -eq '0.3') { DfClick (DfThX 0.40) $midY; $a = DfVal }
    DfLog ("  click at x(0.40) THROUGH overlay -> slider = {0}   {1}" -f $a, $(if($a -eq '0.4'){'PASS'}else{'FAIL'}))

    DfLog ''
    DfLog '--- 1-B: real DRAG through the overlay (0.70 -> 0.30) ---'
    DfSet 0.70; Start-Sleep -Milliseconds 500
    DfLog ("  pre-drag slider = " + (DfVal))
    DfDrag (DfThX 0.70) (DfThX 0.30) $midY
    $b = DfVal
    $bOk = $false
    if ($b -ne 'NOSLIDER') { $bv=[double]$b; if ($bv -ge 0.27 -and $bv -le 0.35) { $bOk=$true } }
    DfLog ("  drag 0.70->0.30 THROUGH overlay -> slider = {0}   {1}" -f $b, $(if($bOk){'PASS'}else{'FAIL'}))
  } else { DfLog 'WARN: slider not found - TEST 1 skipped' }
} else { DfLog 'WARN: console window not found - TEST 1 skipped' }

# =========================================================
DfLog ''
DfLog '########## TEST 3 : capture preview content (P1-a) ##########'
$panelL = $r.Left + $W - 24 - 480; $panelT = $r.Top + $H - 24 - 270
$rx = $r.Left + [int]($W*0.35); $ry = $r.Top + [int]($H*0.28); $rw=320; $rh=220
$sx = $rx + [int]($rw/2) - 50; $sy = $ry + [int]($rh/2) - 30
$px = $panelL + [int](($rx-$r.Left)*480.0/$W) + 6
$py = $panelT + [int](($ry-$r.Top)*270.0/$H) + 6

$form = New-Object System.Windows.Forms.Form
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
$form.BackColor = [System.Drawing.Color]::White
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
$form.Location = New-Object System.Drawing.Point($rx,$ry)
$form.Size = New-Object System.Drawing.Size($rw,$rh)
$form.ShowInTaskbar = $false
$form.TopMost = $false
$form.Text = 'DuoFlowLumaRefForm'
[void]$form.Show()
Start-Sleep -Milliseconds 1000

[void][W.M]::SetWindowPos($overlay,[IntPtr](-1),0,0,0,0,$SWP); Start-Sleep -Milliseconds 900
DfLog ("white ref form at {0},{1} {2}x{3} ; sample {4},{5} 100x60 ; preview panel {6},{7}" -f $rx,$ry,$rw,$rh,$sx,$sy,$panelL,$panelT)
$p1 = DfStats $px $py 68 43 (Join-Path $out 'p0-P1-preview-with-ref.png')
DfLog ("  P1 preview @ mapped ref spot (ref ON) : " + (DfFmt $p1))
$q1 = DfStats ($panelL+20) ($panelT+20) 440 230 (Join-Path $out 'p0-P2-preview-interior.png')
DfLog ("  P2 preview interior (440x230)         : " + (DfFmt $q1))
$form.Close(); $form.Dispose(); Start-Sleep -Milliseconds 1000
$p3 = DfStats $px $py 68 43 (Join-Path $out 'p0-P3-preview-without-ref.png')
DfLog ("  P3 preview @ same spot (ref OFF)      : " + (DfFmt $p3))
$pDelta = [math]::Abs($p1.Mean - $p3.Mean)
$pOk = ($q1.Mean -ge 8) -or ($q1.Std -ge 8) -or ($pDelta -ge 20)
DfLog ("  >>> preview differential |P1-P3| = {0}   P1-a VERDICT = {1}" -f ([math]::Round($pDelta,2)), $(if($pOk){'PASS (preview shows live content)'}else{'FAIL (preview interior blank/black)'}))

# =========================================================
DfLog ''
DfLog '########## TEST 2 : P0-1 transparency + causal A/B ##########'
# M1.8: the fullscreen composite renders the fold whenever progress > 0 -
# the transparency test is only meaningful at REST. TEST 1 left the slider
# at ~0.3, so reset it to 0 and let the engine slew back before sampling.
try {
  DfSet 0
  Start-Sleep -Milliseconds 2500
  DfLog ("  slider reset to 0 for the rest-state transparency sample (value now {0})" -f (DfVal))
} catch { DfLog ("  slider reset skipped: " + $_.Exception.Message) }

$form = New-Object System.Windows.Forms.Form
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
$form.BackColor = [System.Drawing.Color]::White
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
$form.Location = New-Object System.Drawing.Point($rx,$ry)
$form.Size = New-Object System.Drawing.Size($rw,$rh)
$form.ShowInTaskbar = $false
$form.TopMost = $false
[void]$form.Show()
Start-Sleep -Milliseconds 1000

$chipX = $r.Left + $W - 320; $chipY = $r.Top + 30

[void][W.M]::SetWindowPos($overlay,[IntPtr](-1),0,0,0,0,$SWP); Start-Sleep -Milliseconds 900
$s0 = DfStats $sx $sy 100 60 (Join-Path $out 'p0-S0-noref-overlay-on-top.png')
DfLog ("  S0  ref region, overlay on top   : " + (DfFmt $s0))
$s1 = DfStats $sx $sy 100 60 (Join-Path $out 'p0-S1-ref-overlay-on-top.png')
DfLog ("  S1  ref visible, overlay ON TOP  : " + (DfFmt $s1) + "   <== P0-1 as-shipped")
$c1 = DfStats $chipX $chipY 120 24 (Join-Path $out 'p0-C1-chip-overlay-on-top.png')
DfLog ("  C1  overlay's own chip, on top   : " + (DfFmt $c1))

[void][W.M]::SetWindowPos($overlay,[IntPtr](-1),-3200,0,$W,$H,0x10); Start-Sleep -Milliseconds 1300
$s2 = DfStats $sx $sy 100 60 (Join-Path $out 'p0-S2-ref-overlay-offscreen.png')
DfLog ("  S2  ref visible, overlay OFFSCR  : " + (DfFmt $s2) + "   <== ground truth")
$c2 = DfStats $chipX $chipY 120 24 (Join-Path $out 'p0-C2-chip-overlay-offscreen.png')
DfLog ("  C2  overlay's own chip, offscr   : " + (DfFmt $c2))
[void][W.M]::SetWindowPos($overlay,[IntPtr](-1),$r.Left,$r.Top,$W,$H,0x10); Start-Sleep -Milliseconds 1000

$dMean = [math]::Abs($s1.Mean - $s2.Mean)
$tOk = ($s1.Mean -ge 150 -and $dMean -le 40)
DfLog ("  delta(S1,S2) = {0}   P0-1 VERDICT = {1}" -f ([math]::Round($dMean,2)), $(if($tOk){'PASS (overlay is transparent)'}else{'FAIL (overlay still occludes)'}))

DfLog ''
DfLog '--- A/B causal probe: which bit kills the transparency? ---'

# B1: keep everything, but clear WS_EX_LAYERED (drop LWA_ALPHA semantics)
[void][W.M]::SetWindowLongPtr($overlay,-20,[IntPtr]($exOv -band (-bnot 0x80000)))
[void][W.M]::SetWindowPos($overlay,[IntPtr](-1),0,0,0,0,$FRM); Start-Sleep -Milliseconds 1300
$exB = [W.M]::GetWindowLongPtr($overlay,-20).ToInt64()
$s3 = DfStats $sx $sy 100 60 (Join-Path $out 'p0-S3-ref-LAYERED-cleared.png')
DfLog (("  B1  LAYERED cleared (ex=0x{0:X8})  : " -f $exB) + (DfFmt $s3))
$c3 = DfStats $chipX $chipY 120 24 (Join-Path $out 'p0-C3-chip-LAYERED-cleared.png')
DfLog ("      chip sample                      : " + (DfFmt $c3))

# B2: LAYERED back, LWA_ALPHA = 0 (uniform alpha 0)
[void][W.M]::SetWindowLongPtr($overlay,-20,[IntPtr]$exOv)
[void][W.M]::SetWindowPos($overlay,[IntPtr](-1),0,0,0,0,$FRM); Start-Sleep -Milliseconds 700
$ok2 = [W.M]::SetLayeredWindowAttributes($overlay,0,0,0x2)
Start-Sleep -Milliseconds 1300
$s4 = DfStats $sx $sy 100 60 (Join-Path $out 'p0-S4-ref-LWA_ALPHA0.png')
DfLog (("  B2  LAYERED + LWA_ALPHA(0) ok={0}  : " -f $ok2) + (DfFmt $s4))
$c4 = DfStats $chipX $chipY 120 24 (Join-Path $out 'p0-C4-chip-LWA_ALPHA0.png')
DfLog ("      chip sample (should vanish too)  : " + (DfFmt $c4))

# B3: LWA_ALPHA back to 255 (as-shipped)
$ok3 = [W.M]::SetLayeredWindowAttributes($overlay,0,255,0x2)
Start-Sleep -Milliseconds 1300
$s5 = DfStats $sx $sy 100 60 (Join-Path $out 'p0-S5-ref-LWA_ALPHA255-restored.png')
DfLog ("  B3  LAYERED + LWA_ALPHA(255) ok={0}: " -f $ok3) + (DfFmt $s5)
$c5 = DfStats $chipX $chipY 120 24 (Join-Path $out 'p0-C5-chip-LWA_ALPHA255-restored.png')
DfLog ("      chip sample                      : " + (DfFmt $c5))

$form.Close(); $form.Dispose()

# =========================================================
DfLog ''
DfLog '########## SUMMARY ##########'
DfLog ("  P0-2 click-through (real input) : click={0}  drag={1}" -f $a, $b)
DfLog ("  P0-1 transparency  as-shipped S1={0}  ground-truth S2={1}  delta={2}" -f $s1.Mean,$s2.Mean,([math]::Round($dMean,2)))
DfLog ("  A/B  LAYERED cleared S3={0}   LWA_ALPHA(0) S4={1}   LWA_ALPHA(255) S5={2}" -f $s3.Mean,$s4.Mean,$s5.Mean)
DfLog ("  P1-a preview: interior={0} std={1}  differential={2}" -f $q1.Mean,$q1.Std,([math]::Round($pDelta,2)))

$clickOk = ($a -eq '0.4')
$verdicts = [ordered]@{
    GeneratedAt          = (Get-Date).ToString("s")
    Exe                  = $exe
    OverlayExStyle       = ("0x{0:X8}" -f $exOv)
    ClickThroughClick    = [ordered]@{ Pass = $clickOk; SliderValue = $a }
    ClickThroughDrag     = [ordered]@{ Pass = $bOk;     SliderValue = $b }
    Transparency         = [ordered]@{ Pass = $tOk;     S1 = $s1.Mean; S2 = $s2.Mean; Delta = [math]::Round($dMean,2) }
    PreviewLive          = [ordered]@{ Pass = $pOk;     Interior = $q1.Mean; Std = $q1.Std; Differential = [math]::Round($pDelta,2) }
    CausalAB             = [ordered]@{ LayeredCleared = $s3.Mean; AlphaZero = $s4.Mean; AlphaRestored = $s5.Mean }
}
$allOk = $clickOk -and $bOk -and $tOk -and $pOk
$verdicts['Overall'] = $allOk
$verdicts | ConvertTo-Json -Depth 6 | Out-File (Join-Path $out 'overlay-p0-result.json') -Encoding utf8
DfLog ''
DfLog ("  OVERALL = " + $(if($allOk){'PASS (4/4)'}else{'FAIL - see the per-test verdicts above'}))

DfLog ''
DfLog '########## CLEANUP ##########'
[void][W.M]::SetCursorPos(960,540)
Get-Process -Name 'DuoFlow.App' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 1500
$left = (Get-Process -Name 'DuoFlow.App' -ErrorAction SilentlyContinue | Measure-Object).Count
DfLog ("  DuoFlow.App processes left: {0}" -f $left)
DfLog '=== DONE ==='

if (-not $allOk) { exit 1 }
exit 0
