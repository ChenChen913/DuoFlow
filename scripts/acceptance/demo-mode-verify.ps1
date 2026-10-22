# =====================================================================
# DuoFlow - demo mode acceptance (M2.4).  [scripts/acceptance]
#
# WHAT IT PROVES: the demo player walks the REAL progress chain end to end -
# provider -> engine -> render - and stops where it says it stops.
#
#   1. 开盖 0->1   reaches 1.0, is monotone, and STAYS at 1.0 afterwards
#                  (auto-stop at the endpoint, no creep, no bounce)
#   2. 合盖 1->0   reaches 0.0 and stays
#   3. 循环往返    the direction reverses at BOTH endpoints (observed rising
#                  AND falling phases with max ~1 and min ~0) - a loop that
#                  only goes one way, or that jumps, fails here
#   4. 暂停        the value freezes
#
# HOW: UIAutomation drives the buttons and reads the Progress slider's live
# RangeValue every ~120 ms. That is the same value the provider holds, so the
# samples prove the chain moved - not just that a timer fired.
#
# USAGE (Windows PowerShell 5.1, no admin, interactive desktop):
#   .\scripts\acceptance\demo-mode-verify.ps1
#   .\scripts\acceptance\demo-mode-verify.ps1 -OutputDir D:\tmp\demo
#
# OUTPUT: demo-mode-verify.txt (log incl. every sample) + demo-mode-result.json
# EXIT CODE: 0 = all four checks passed, 1 = at least one did not.
# NOTE: ~35 s. Does not move the mouse, but the app takes over the screen.
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
function Log([string]$m) { $m | Out-File -FilePath $log -Append -Encoding UTF8; Write-Host $m }

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
# Sample the live progress until the predicate holds or the deadline passes.
function Sample-Until([scriptblock]$pred, [int]$timeoutMs, [System.Collections.ArrayList]$into) {
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    while ((Get-Date) -lt $deadline) {
        $v = Read-Progress
        [void]$into.Add($v)
        if (& $pred $v) { return $true }
        Start-Sleep -Milliseconds 120
    }
    return $false
}

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
# 1. 开盖 0 -> 1 : reaches 1.0, monotone, and stays
# ---------------------------------------------------------------------
$s1 = New-Object System.Collections.ArrayList
$reached = $false
try {
    Invoke-Button 'PlayOpeningButton'
    $reached = Sample-Until { param($v) $v -ge 0.995 } 5000 $s1
} catch {
    Log ("  play opening failed: " + $_.Exception.Message)
}
$mono = $true
for ($i = 1; $i -lt $s1.Count; $i++) {
    if ($s1[$i] -lt $s1[$i - 1] - 0.02) { $mono = $false }
}
$peak = if ($s1.Count -gt 0) { ($s1 | Measure-Object -Maximum).Maximum } else { -1 }
Check "opening reaches 1.0" $reached ("peak={0} samples={1}" -f $peak, $s1.Count)
Check "opening monotone" $mono ("{0} samples: {1}" -f $s1.Count, (($s1 | ForEach-Object { [math]::Round($_, 2) }) -join ' '))

# auto-stop: after reaching the endpoint the value must not creep or bounce
Start-Sleep -Milliseconds 900
$after = New-Object System.Collections.ArrayList
for ($i = 0; $i -lt 5; $i++) { [void]$after.Add((Read-Progress)); Start-Sleep -Milliseconds 150 }
$stillAtEnd = (($after | Where-Object { $_ -lt 0.99 }).Count -eq 0)
Check "opening auto-stops at 1.0" $stillAtEnd ("after 0.9s: " + (($after | ForEach-Object { [math]::Round($_, 2) }) -join ' '))

# ---------------------------------------------------------------------
# 2. 合盖 1 -> 0 : reaches 0.0 and stays
# ---------------------------------------------------------------------
$s2 = New-Object System.Collections.ArrayList
$reached0 = $false
try {
    Invoke-Button 'PlayClosingButton'
    $reached0 = Sample-Until { param($v) $v -le 0.005 } 5000 $s2
} catch {
    Log ("  play closing failed: " + $_.Exception.Message)
}
$mono2 = $true
for ($i = 1; $i -lt $s2.Count; $i++) {
    if ($s2[$i] -gt $s2[$i - 1] + 0.02) { $mono2 = $false }
}
Check "closing reaches 0.0" $reached0 ("samples={0}" -f $s2.Count)
Check "closing monotone" $mono2 ((($s2 | ForEach-Object { [math]::Round($_, 2) }) -join ' '))
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
