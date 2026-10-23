# =====================================================================
# DuoFlow - demo mode acceptance (M2.4) + the six M2.3 operations.
#
# WHAT IT PROVES: the demo player walks the REAL progress chain end to end -
# provider -> engine -> render - and stops where it says it stops.
#
#   1. 开盖 0->1   reaches 1.0, is monotone, at/under the rate cap, and STAYS
#                  at 1.0 afterwards (auto-stop, no creep, no bounce)
#   2. 合盖 1->0   same, downward
#   3. 循环往返    the direction reverses at BOTH endpoints
#   4. 暂停        the value freezes
#   5. 慢开 3 s    tracked monotonically at ~0.33/s - the regime the fast pass
#                  cannot exercise (it is rate-capped almost the whole way)
#   6. 慢合 3 s    same, downward
#   7. 中途停      pause mid pass: settles BETWEEN the endpoints and stays
#                  dead still (spread <= 0.02 over 1.4 s)
#   8. 中途反向    reverse mid pass: exactly ONE direction change, no jump
#
# "No jump" is measured as progress per SECOND from timestamped samples, not
# as a per-sample delta: RangeValue reads cost 10-40 ms, so a fixed per-sample
# threshold either false-fails on a slow read or hides a real jump. The bound
# is the engine's own MaxProgressPerSecond (2.0/s) plus 30% sampling headroom.
#
# HOW: UIAutomation drives the buttons and reads the Progress slider's live
# RangeValue. That is the same value the provider holds, so the samples prove
# the chain moved - not just that a timer fired.
#
# USAGE (Windows PowerShell 5.1, no admin, interactive desktop):
#   .\scripts\acceptance\demo-mode-verify.ps1
#   .\scripts\acceptance\demo-mode-verify.ps1 -OutputDir D:\tmp\demo
#
# OUTPUT: demo-mode-verify.txt (log incl. every sample) + demo-mode-result.json
# EXIT CODE: 0 = all checks passed, 1 = at least one did not.
# NOTE: ~70 s. Does not move the mouse, but the app takes over the screen.
# =====================================================================

param(
    [string]$RepoRoot = "",
    [string]$OutputDir = "",
    [string]$Exe = "",
    [int]$StartupSeconds = 14,
    [double]$PassDuration = 0.6
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path }
if (-not $OutputDir) { $OutputDir = Join-Path $env:TEMP "duoflow-demo-verify" }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$log = Join-Path $OutputDir "demo-mode-verify.txt"
Remove-Item $log -ErrorAction SilentlyContinue
# Write-Host "" emits NOTHING in Windows PowerShell 5.1 (verified: A/""/B
# prints "AB"), so an empty line has to be written as a single space. Without
# this every Log "" in the script silently collapses the layout.
function Log([string]$m) {
    $line = if ($m.Length -eq 0) { " " } else { $m }
    $line | Out-File -FilePath $log -Append -Encoding UTF8
    Write-Host $line
}

if (-not $Exe) {
    $found = Get-ChildItem (Join-Path $RepoRoot "src\DuoFlow.App\bin") -Recurse -Filter "DuoFlow.App.exe" -ErrorAction SilentlyContinue |
             Where-Object { $_.FullName -notmatch "\\obj\\" } |
             Sort-Object LastWriteTime -Descending |
             Select-Object -First 1
    if ($found) { $Exe = $found.FullName }
}
if (-not $Exe -or -not (Test-Path $Exe)) {
    Log "DuoFlow.App.exe not found. Build it first: dotnet build src/DuoFlow.App -p:Platform=x64"
    exit 1
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -Namespace W -Name D -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
[DllImport("user32.dll")] public static extern IntPtr GetTopWindow(IntPtr h);
[DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
'@
[void][W.D]::SetProcessDPIAware()

$AE = [System.Windows.Automation.AutomationElement]
function Cond([object]$prop, [object]$val) {
    return (New-Object System.Windows.Automation.PropertyCondition($prop, $val))
}

$steps = New-Object System.Collections.ArrayList
function Check([string]$name, [bool]$ok, [string]$detail) {
    [void]$steps.Add([ordered]@{ Step = $name; Pass = $ok; Detail = $detail })
    Log ("{0} {1}  {2}" -f $(if ($ok) { "PASS" } else { "FAIL" }), $name, $detail)
}

# ---- launch ----------------------------------------------------------
Get-Process -Name 'DuoFlow.App' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 900
Log "exe = $Exe"
[void](Start-Process -FilePath $Exe -WorkingDirectory (Split-Path $Exe -Parent) -ArgumentList '--overlay-smoke' -PassThru)
Start-Sleep -Seconds $StartupSeconds

$proc = Get-Process -Name 'DuoFlow.App' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Log "FATAL: app not running"; exit 1 }

$console = [IntPtr]::Zero
$h = [W.D]::GetTopWindow([IntPtr]::Zero)
while ($h -ne [IntPtr]::Zero) {
    $p = 0; [void][W.D]::GetWindowThreadProcessId($h, [ref]$p)
    if ($p -eq $proc.Id) {
        $sb = New-Object System.Text.StringBuilder 256
        [void][W.D]::GetWindowText($h, $sb, 256)
        if ($sb.ToString() -like 'DuoFlow Console*') { $console = $h }
    }
    $h = [W.D]::GetWindow($h, 2)
}
if ($console -eq [IntPtr]::Zero) { Log "FATAL: console window not found"; exit 1 }
$root = $AE::FromHandle($console)
Log ("console hwnd = 0x{0:X8}" -f [int64]$console)

function Find-ById([string]$id) {
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (Cond $AE::AutomationIdProperty $id))
}
function Read-Progress {
    $el = Find-ById 'ProgressSlider'
    if (-not $el) { return -1.0 }
    $pat = $el.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    return [double]$pat.Current.Value
}
function Set-Progress([double]$v) {
    $el = Find-ById 'ProgressSlider'
    $pat = $el.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    $pat.SetValue($v)
    Start-Sleep -Milliseconds 600
}
function Invoke-Button([string]$id) {
    $el = Find-ById $id
    if (-not $el) { throw "button '$id' not found (is this build M2.4+?)" }
    $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
function Set-Duration([double]$v) {
    $el = Find-ById 'DurationSlider'
    if (-not $el) { throw "DurationSlider not found" }
    $el.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue($v)
    Start-Sleep -Milliseconds 300
}
function Set-Loop([bool]$on) {
    $el = Find-ById 'LoopCheck'
    if (-not $el) { throw "LoopCheck not found" }
    $pat = $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $cur = ($pat.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On)
    if ($cur -ne $on) { $pat.Toggle(); Start-Sleep -Milliseconds 300 }
    return ($pat.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On)
}
# Sample the live progress on a clock, keeping the TIMESTAMP of every sample.
# The timestamps are the whole point for M2.3: "no jump" is a statement about
# progress per SECOND, and a bare value series cannot distinguish "moved 0.2
# in 100 ms" (at the 2.0/s rate cap) from "moved 0.2 in 400 ms" (slower than
# the cap, perfectly fine). Reading RangeValue itself costs ~10-40 ms, so the
# interval is a floor, never an assumption - always divide by the measured dt.
function Sample-Timed([int]$durationMs, [int]$intervalMs) {
    $series = New-Object System.Collections.ArrayList
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $durationMs) {
        $v = Read-Progress
        [void]$series.Add([ordered]@{ T = [int]$sw.ElapsedMilliseconds; V = $v })
        Start-Sleep -Milliseconds $intervalMs
    }
    $sw.Stop()
    return $series
}

# Fastest observed progress rate, in progress units per second.
function Measure-MaxRate($series) {
    $maxRate = 0.0
    for ($i = 1; $i -lt $series.Count; $i++) {
        $dt = ($series[$i].T - $series[$i - 1].T) / 1000.0
        if ($dt -le 0.001) { continue }
        $rate = [math]::Abs($series[$i].V - $series[$i - 1].V) / $dt
        if ($rate -gt $maxRate) { $maxRate = $rate }
    }
    return $maxRate
}

# How many times the direction of travel flips (ignoring sub-tolerance noise).
# startSign seeds the direction of travel from BEFORE the sample window: the
# mid-reverse run is already rising when sampling starts, and without the seed
# a perfectly good single turn reports as "0 changes".
function Measure-DirectionChanges($series, [double]$tolerance, [int]$startSign = 0) {
    $changes = 0
    $lastSign = $startSign
    for ($i = 1; $i -lt $series.Count; $i++) {
        $d = $series[$i].V - $series[$i - 1].V
        if ([math]::Abs($d) -lt $tolerance) { continue }
        $sign = [math]::Sign($d)
        if ($lastSign -ne 0 -and $sign -ne $lastSign) { $changes++ }
        $lastSign = $sign
    }
    return $changes
}

function Format-Series($series) {
    return (($series | ForEach-Object { [math]::Round($_.V, 2) }) -join ' ')
}

# The engine's anti-jump guarantee (AnimationEngineOptions.MaxProgressPerSecond).
# Sampled rates are compared against it with 30% headroom for the sampling
# itself: the slider value is read from the UI thread, so a sample can land
# either side of a tick and report a rate slightly above the true one.
$script:RateCap = 2.0
$script:RateSlack = 1.30

# ---------------------------------------------------------------------
# Prepare: fast pass, no loop, start from fully open
# ---------------------------------------------------------------------
try {
    [void](Set-Duration $PassDuration)
    [void](Set-Loop $false)
    Set-Progress 0
    Log ("prepared: duration={0}s loop=off progress={1}" -f $PassDuration, (Read-Progress))
} catch {
    Log ("FATAL while preparing: " + $_.Exception.Message)
    exit 1
}

# ---------------------------------------------------------------------
# 1. 开盖 0 -> 1 (fast) : reaches 1.0, monotone, at or under the rate cap,
#    and stays
# ---------------------------------------------------------------------
$s1 = New-Object System.Collections.ArrayList
try {
    Invoke-Button 'PlayOpeningButton'
    $s1 = Sample-Timed 1600 120
} catch {
    Log ("  play opening failed: " + $_.Exception.Message)
}
$v1 = @($s1 | ForEach-Object { $_.V })
$peak = if ($v1.Count -gt 0) { ($v1 | Measure-Object -Maximum).Maximum } else { -1 }
$reached = ($peak -ge 0.995)
$mono = $true
for ($i = 1; $i -lt $v1.Count; $i++) {
    if ($v1[$i] -lt $v1[$i - 1] - 0.02) { $mono = $false }
}
$rate1 = Measure-MaxRate $s1
Check "opening reaches 1.0" $reached ("peak={0} samples={1}" -f $peak, $v1.Count)
Check "opening monotone" $mono ("{0} samples: {1}" -f $v1.Count, (($v1 | ForEach-Object { [math]::Round($_, 2) }) -join ' '))
Check "opening has no jump" ($rate1 -le ($script:RateCap * $script:RateSlack)) `
    ("max rate={0:0.00}/s (cap {1}/s)" -f $rate1, $script:RateCap)

# auto-stop: after reaching the endpoint the value must not creep or bounce
Start-Sleep -Milliseconds 900
$after = New-Object System.Collections.ArrayList
for ($i = 0; $i -lt 5; $i++) { [void]$after.Add((Read-Progress)); Start-Sleep -Milliseconds 150 }
$stillAtEnd = (@($after | Where-Object { $_ -lt 0.99 }).Count -eq 0)
Check "opening auto-stops at 1.0" $stillAtEnd ("after 0.9s: " + (($after | ForEach-Object { [math]::Round($_, 2) }) -join ' '))

# ---------------------------------------------------------------------
# 2. 合盖 1 -> 0 (fast) : reaches 0.0, monotone, at or under the cap, stays
# ---------------------------------------------------------------------
$s2 = New-Object System.Collections.ArrayList
try {
    Invoke-Button 'PlayClosingButton'
    $s2 = Sample-Timed 1600 120
} catch {
    Log ("  play closing failed: " + $_.Exception.Message)
}
$v2 = @($s2 | ForEach-Object { $_.V })
$low2 = if ($v2.Count -gt 0) { ($v2 | Measure-Object -Minimum).Minimum } else { 1.0 }
$reached0 = ($low2 -le 0.005)
$mono2 = $true
for ($i = 1; $i -lt $v2.Count; $i++) {
    if ($v2[$i] -gt $v2[$i - 1] + 0.02) { $mono2 = $false }
}
$rate2 = Measure-MaxRate $s2
Check "closing reaches 0.0" $reached0 ("min={0} samples={1}" -f $low2, $v2.Count)
Check "closing monotone" $mono2 (($v2 | ForEach-Object { [math]::Round($_, 2) }) -join ' ')
Check "closing has no jump" ($rate2 -le ($script:RateCap * $script:RateSlack)) `
    ("max rate={0:0.00}/s (cap {1}/s)" -f $rate2, $script:RateCap)
Start-Sleep -Milliseconds 700
$low = Read-Progress
Check "closing auto-stops at 0.0" ($low -le 0.01) ("value={0}" -f $low)

# ---------------------------------------------------------------------
# 3. loop: direction must reverse at BOTH endpoints
# ---------------------------------------------------------------------
$s3 = New-Object System.Collections.ArrayList
try {
    [void](Set-Loop $true)
    Invoke-Button 'PlayOpeningButton'
    $deadline = (Get-Date).AddMilliseconds(3200)   # ~2.5 passes at 0.6s
    while ((Get-Date) -lt $deadline) {
        [void]$s3.Add((Read-Progress))
        Start-Sleep -Milliseconds 120
    }
} catch {
    Log ("  loop run failed: " + $_.Exception.Message)
}
$max3 = if ($s3.Count -gt 0) { ($s3 | Measure-Object -Maximum).Maximum } else { -1 }
$min3 = if ($s3.Count -gt 0) { ($s3 | Measure-Object -Minimum).Minimum } else { -1 }
$rose = $false; $fell = $false
for ($i = 1; $i -lt $s3.Count; $i++) {
    if ($s3[$i] -gt $s3[$i - 1] + 0.03) { $rose = $true }
    if ($s3[$i] -lt $s3[$i - 1] - 0.03) { $fell = $true }
}
Check "loop touches both endpoints" ($max3 -ge 0.97 -and $min3 -le 0.03) ("max={0} min={1}" -f $max3, $min3)
Check "loop reverses direction" ($rose -and $fell) ("rose={0} fell={1}" -f $rose, $fell)
Log ("  loop samples: " + (($s3 | ForEach-Object { [math]::Round($_, 2) }) -join ' '))

# ---------------------------------------------------------------------
# 4. pause freezes the value
# ---------------------------------------------------------------------
try { Invoke-Button 'PauseDemoButton' } catch { Log ("  pause failed: " + $_.Exception.Message) }
Start-Sleep -Milliseconds 500
$p1 = Read-Progress
Start-Sleep -Milliseconds 800
$p2 = Read-Progress
Check "pause freezes progress" ([math]::Abs($p2 - $p1) -le 0.02) ("{0} -> {1}" -f $p1, $p2)
[void](Set-Loop $false)

# ---------------------------------------------------------------------
# 5/6. 慢开 / 慢合 (3 s per pass) - the "慢" half of M2.3's six operations.
# A slow pass is where a lag or a stutter is most visible, and it is the
# regime the fast pass cannot exercise (the fast pass is rate-capped almost
# the whole way, so it says nothing about tracking).
# ---------------------------------------------------------------------
try { [void](Set-Duration 3.0) } catch { Log ("  set duration failed: " + $_.Exception.Message) }
Set-Progress 0
Start-Sleep -Milliseconds 400
try { Invoke-Button 'PlayOpeningButton' } catch { Log ("  slow open failed: " + $_.Exception.Message) }
$s5 = Sample-Timed 3400 120
$v5 = @($s5 | ForEach-Object { $_.V })
$mono5 = $true
for ($i = 1; $i -lt $v5.Count; $i++) {
    if ($v5[$i] -lt $v5[$i - 1] - 0.02) { $mono5 = $false }
}
$peak5 = if ($v5.Count -gt 0) { ($v5 | Measure-Object -Maximum).Maximum } else { -1 }
$rate5 = Measure-MaxRate $s5
Check "slow opening reaches 1.0" ($peak5 -ge 0.995) ("peak={0} samples={1}" -f $peak5, $v5.Count)
Check "slow opening monotone" $mono5 (Format-Series $s5)
Check "slow opening tracks at ~0.33/s" ($rate5 -le ($script:RateCap * $script:RateSlack) -and $rate5 -le 0.75) `
    ("max rate={0:0.00}/s (expected ~0.33)" -f $rate5)

Start-Sleep -Milliseconds 500
try { Invoke-Button 'PlayClosingButton' } catch { Log ("  slow close failed: " + $_.Exception.Message) }
$s6 = Sample-Timed 3400 120
$v6 = @($s6 | ForEach-Object { $_.V })
$mono6 = $true
for ($i = 1; $i -lt $v6.Count; $i++) {
    if ($v6[$i] -gt $v6[$i - 1] + 0.02) { $mono6 = $false }
}
$low6 = if ($v6.Count -gt 0) { ($v6 | Measure-Object -Minimum).Minimum } else { 1.0 }
$rate6 = Measure-MaxRate $s6
Check "slow closing reaches 0.0" ($low6 -le 0.005) ("min={0} samples={1}" -f $low6, $v6.Count)
Check "slow closing monotone" $mono6 (Format-Series $s6)
Check "slow closing has no jump" ($rate6 -le ($script:RateCap * $script:RateSlack)) `
    ("max rate={0:0.00}/s" -f $rate6)

# ---------------------------------------------------------------------
# 7. 中途停: stop the pass half way - the value must settle and stay dead
#    still. A springy settle (overshoot then return) or a jitter at rest is
#    a visible flicker at a fixed progress, which is exactly what M2.3's
#    "中途停" check is about.
# ---------------------------------------------------------------------
try { [void](Set-Duration 2.0) } catch { }
Set-Progress 0
Start-Sleep -Milliseconds 400
try { Invoke-Button 'PlayOpeningButton' } catch { Log ("  mid-stop run failed: " + $_.Exception.Message) }
$stopAt = 0.0
$deadline = (Get-Date).AddMilliseconds(2500)
while ((Get-Date) -lt $deadline) {
    $stopAt = Read-Progress
    if ($stopAt -ge 0.40) { break }
    Start-Sleep -Milliseconds 60
}
try { Invoke-Button 'PauseDemoButton' } catch { Log ("  pause failed: " + $_.Exception.Message) }
$s7 = Sample-Timed 1400 100
$v7 = @($s7 | ForEach-Object { $_.V })
$spread7 = if ($v7.Count -gt 1) { ($v7 | Measure-Object -Maximum).Maximum - ($v7 | Measure-Object -Minimum).Minimum } else { 1.0 }
$held7 = if ($v7.Count -gt 0) { $v7[$v7.Count - 1] } else { -1 }
Check "mid-stop stops between the endpoints" ($held7 -gt 0.10 -and $held7 -lt 0.95) ("paused at {0:0.000}" -f $held7)
Check "mid-stop is dead still" ($spread7 -le 0.02) ("spread over 1.4 s = {0:0.0000}" -f $spread7)

# ---------------------------------------------------------------------
# 8. 中途反向: reverse mid pass - exactly ONE direction change, no jump.
#    Two changes would be a shudder; zero would mean the reverse was ignored.
# ---------------------------------------------------------------------
Set-Progress 0
Start-Sleep -Milliseconds 400
try { Invoke-Button 'PlayOpeningButton' } catch { Log ("  mid-reverse run failed: " + $_.Exception.Message) }
$deadline = (Get-Date).AddMilliseconds(2500)
while ((Get-Date) -lt $deadline) {
    if ((Read-Progress) -ge 0.40) { break }
    Start-Sleep -Milliseconds 60
}
$turnAt = Read-Progress
try { Invoke-Button 'PlayClosingButton' } catch { Log ("  reverse failed: " + $_.Exception.Message) }
$s8 = Sample-Timed 2400 120
$v8 = @($s8 | ForEach-Object { $_.V })
$changes8 = Measure-DirectionChanges $s8 0.01 1   # the rise preceded the window
$rate8 = Measure-MaxRate $s8
$end8 = if ($v8.Count -gt 0) { $v8[$v8.Count - 1] } else { 1.0 }
Check "mid-reverse turns exactly once" ($changes8 -eq 1) ("turned at p~{0:0.00}, direction changes={1}" -f $turnAt, $changes8)
Check "mid-reverse has no jump" ($rate8 -le ($script:RateCap * $script:RateSlack)) ("max rate={0:0.00}/s" -f $rate8)
Check "mid-reverse finishes at 0.0" ($end8 -le 0.03) ("final={0:0.000}" -f $end8)
Log ("  mid-reverse samples: " + (Format-Series $s8))

# ---------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------
# Wrap in @(): with exactly one match, Where-Object returns the object
# itself, and .Count on an ordered dictionary is the number of KEYS (4) -
# which is how "1 failed" once printed as "4 of 20 failed".
$failed = @($steps | Where-Object { -not $_.Pass }).Count
$overall = ($failed -eq 0)
Log ""
Log ("OVERALL = " + $(if ($overall) { "PASS ($($steps.Count)/$($steps.Count))" } else { "FAIL ($failed of $($steps.Count) failed)" }))

[ordered]@{
    GeneratedAt = (Get-Date).ToString("s")
    Exe         = $Exe
    PassDuration = $PassDuration
    Steps       = $steps
    Overall     = $overall
} | ConvertTo-Json -Depth 6 | Out-File (Join-Path $OutputDir "demo-mode-result.json") -Encoding utf8

Get-Process -Name 'DuoFlow.App' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if (-not $overall) { exit 1 }
exit 0
