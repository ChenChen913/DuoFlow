# =====================================================================
# DuoFlow - tuning panel acceptance (M2.5).  [scripts/acceptance]
#
# WHAT IT PROVES: the console's parameter panel actually reaches the render
# math - not just the UI. Every step drives a control through UIAutomation
# (the same channel a screen reader uses, so no pixel guessing), then reads
# the app's own trace for the DERIVED geometry that CaptureRenderer logged
# from WarpGeometry.Compute.
#
# The key assertion is M00 at full fold:  M00 = r * cos(MaxFold).
#   MaxFold 90, r 2.5  ->  M00 = 0.0000   (plane edge-on: no image)
#   MaxFold 60, r 2.5  ->  M00 = 1.2500
#   MaxFold 60, r 4.0  ->  M00 = 2.0000
#   MaxFold 60, r 4.0, toward  ->  M21 = +0.8660  (sign flips with direction)
# A UI-only change would leave M00 at 0.0000 and fail every step below.
#
# The gradient steps (M2.1) assert the part that matters more than "the
# checkbox works": with the pass DISABLED the trace must report
# strength=0.000 no matter what the strength slider says. That zero is what
# the pixel shader branches on (`if (GradientParams.x > 0.0)`), so it is the
# mechanism behind "disabled == no-op" - see warp-selftest for the pixel
# level proof.
#
# USAGE (Windows PowerShell 5.1, no admin, interactive desktop):
#   .\scripts/acceptance\tuning-panel-verify.ps1
#   .\scripts\acceptance\tuning-panel-verify.ps1 -OutputDir D:\tmp\tune
#
# OUTPUT: tuning-panel-verify.txt (log) + tuning-panel-result.json
# EXIT CODE: 0 = every step matched, 1 = at least one did not.
# NOTE: ~60 s. It does NOT move the mouse (UIAutomation only), but the app
# takes over the screen with a fullscreen overlay while it runs.
# =====================================================================

param(
    [string]$RepoRoot = "",
    [string]$OutputDir = "",
    [string]$Exe = "",
    [int]$StartupSeconds = 14
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path }
if (-not $OutputDir) { $OutputDir = Join-Path $env:TEMP "duoflow-tuning-verify" }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$log = Join-Path $OutputDir "tuning-panel-verify.txt"
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

$trace = Join-Path $env:USERPROFILE "duoflow-trace.txt"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type -Namespace W -Name T -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
[DllImport("user32.dll")] public static extern IntPtr GetTopWindow(IntPtr h);
[DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
'@
[void][W.T]::SetProcessDPIAware()

$AE = [System.Windows.Automation.AutomationElement]
function Cond([object]$prop, [object]$val) {
    return (New-Object System.Windows.Automation.PropertyCondition($prop, $val))
}

# ---- trace: only the part written by THIS run ------------------------
$script:traceOffset = 0
if (Test-Path $trace) { $script:traceOffset = (Get-Item -LiteralPath $trace).Length }
function TraceTail {
    if (-not (Test-Path $trace)) { return "" }
    $fs = [IO.File]::Open($trace, 'Open', 'Read', 'ReadWrite')
    try {
        $off = [long]$script:traceOffset
        if ($off -gt $fs.Length) { $off = 0 }
        $fs.Position = $off
        return (New-Object IO.StreamReader($fs)).ReadToEnd()
    } finally { $fs.Dispose() }
}

# ---- results ---------------------------------------------------------
$steps = New-Object System.Collections.ArrayList
function Check([string]$name, [bool]$ok, [string]$expected, [string]$actual) {
    [void]$steps.Add([ordered]@{ Step = $name; Pass = $ok; Expected = $expected; Actual = $actual })
    Log ("{0} {1}  expect[{2}]  got[{3}]" -f $(if ($ok) { "PASS" } else { "FAIL" }), $name, $expected, $actual)
}

# Wait until the trace (this run only) contains a pattern; returns the match.
function Wait-Trace([string]$pattern, [int]$timeoutMs = 6000) {
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    while ((Get-Date) -lt $deadline) {
        $t = TraceTail
        $m = [regex]::Match($t, $pattern)
        if ($m.Success) { return $m }
        Start-Sleep -Milliseconds 250
    }
    return [regex]::Match("", $pattern)
}

# ---- launch ----------------------------------------------------------
Get-Process -Name 'DuoFlow.App' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 900
Log "exe = $Exe"
[void](Start-Process -FilePath $Exe -WorkingDirectory (Split-Path $Exe -Parent) -ArgumentList '--overlay-smoke' -PassThru)
Start-Sleep -Seconds $StartupSeconds

$proc = Get-Process -Name 'DuoFlow.App' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Log "FATAL: app not running"; exit 1 }

# Find the console window (exact-ish match: the overlay's title also starts
# with "DuoFlow", and the console's own title contains the word "Overlay").
$console = [IntPtr]::Zero
$h = [W.T]::GetTopWindow([IntPtr]::Zero)
while ($h -ne [IntPtr]::Zero) {
    $p = 0; [void][W.T]::GetWindowThreadProcessId($h, [ref]$p)
    if ($p -eq $proc.Id) {
        $sb = New-Object System.Text.StringBuilder 256
        [void][W.T]::GetWindowText($h, $sb, 256)
        if ($sb.ToString() -like 'DuoFlow Console*') { $console = $h }
    }
    $h = [W.T]::GetWindow($h, 2)
}
if ($console -eq [IntPtr]::Zero) { Log "FATAL: console window not found"; exit 1 }
$root = $AE::FromHandle($console)
Log ("console hwnd = 0x{0:X8}" -f [int64]$console)

function Find-ById([string]$id) {
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (Cond $AE::AutomationIdProperty $id))
}
function Set-Slider([string]$id, [double]$v) {
    $el = Find-ById $id
    if (-not $el) { throw "control '$id' not found (is this build M2.5+?)" }
    $pat = $el.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    $pat.SetValue($v)
    Start-Sleep -Milliseconds 700
    return [double]$pat.Current.Value
}
function Set-Check([string]$id, [bool]$on) {
    $el = Find-ById $id
    if (-not $el) { throw "checkbox '$id' not found (is this build M2.1+?)" }
    $pat = $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $before = $pat.Current.ToggleState.ToString()
    $want = if ($on) { 'On' } else { 'Off' }
    if ($before -ne $want) { $pat.Toggle() }
    Start-Sleep -Milliseconds 700
    $after = $pat.Current.ToggleState.ToString()
    Log ("  [$id] toggle $before -> $after (want $want)")
    return $after
}
function Select-Combo([string]$id, [string]$namePrefix) {
    $cb = Find-ById $id
    if (-not $cb) { throw "combobox '$id' not found" }

    # Record the current selection so we can tell "the click did nothing" from
    # "the trace line is missing".
    $selPat = $null
    try { $selPat = $cb.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern) } catch { }
    $before = ""
    if ($selPat) {
        $cur = $selPat.Current.GetSelection()
        if ($cur.Count -gt 0) { $before = $cur[0].Current.Name }
    }
    Log ("  [$id] selection before = '$before'")

    $ec = $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $ec.Expand()
    Start-Sleep -Milliseconds 700

    # The popup is a SEPARATE UIA subtree, so searching under the ComboBox
    # itself can come back empty - search from the desktop root as well.
    $roots = @($cb, $AE::RootElement)
    $items = @()
    foreach ($r in $roots) {
        $found = $r.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            (Cond $AE::ControlTypeProperty ([System.Windows.Automation.ControlType]::ListItem)))
        foreach ($f in $found) { $items += $f }
        if ($items.Count -gt 0) { break }
    }
    $names = ($items | ForEach-Object { $_.Current.Name }) -join ' | '
    Log ("  [$id] list items = $($items.Count): $names")

    $target = $null
    foreach ($it in $items) {
        if ($it.Current.Name -like "$namePrefix*") { $target = $it; break }
    }
    if (-not $target) {
        $ec.Collapse()
        throw "no list item starting with '$namePrefix' (saw: $names)"
    }

    $target.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 500
    $ec.Collapse()
    Start-Sleep -Milliseconds 400

    if ($selPat) {
        $after = ""
        $cur = $selPat.Current.GetSelection()
        if ($cur.Count -gt 0) { $after = $cur[0].Current.Name }
        Log ("  [$id] selection after  = '$after'")
        if ($after -eq $before) { throw "selection did not change (still '$after')" }
    }
}

# ---------------------------------------------------------------------
# Step 0: the panel pushed its initial state (defaults) into the renderer
# ---------------------------------------------------------------------
$m = Wait-Trace 'tuning: warp maxFold=([\d.]+) r=([\d.]+) tiltAway=(\w+) hingeTop=(\w+).*M00=([-\d.]+)'
Check "baseline pushed" $m.Success "maxFold=90.0 r=2.50 away bottom M00=0.0000" $(if ($m.Success) { $m.Value } else { "<no tuning line in trace>" })

# ---------------------------------------------------------------------
# Step 1: MaxFold 90 -> 60  =>  M00 = 2.5*cos(60) = 1.2500
# ---------------------------------------------------------------------
$v = Set-Slider 'MaxFoldSlider' 60
$m = Wait-Trace 'tuning: warp maxFold=60\.0 r=2\.50 .*M00=([-\d.]+)'
Check "MaxFold 60 -> M00" ($m.Success -and $m.Groups[1].Value -eq '1.2500') "M00=1.2500" $(if ($m.Success) { $m.Value } else { "<timeout>" })

# ---------------------------------------------------------------------
# Step 2: camera r 2.5 -> 4.0  =>  M00 = 4.0*cos(60) = 2.0000
# ---------------------------------------------------------------------
[void](Set-Slider 'CameraSlider' 4.0)
$m = Wait-Trace 'tuning: warp maxFold=60\.0 r=4\.00 .*M00=([-\d.]+)'
Check "r 4.0 -> M00" ($m.Success -and $m.Groups[1].Value -eq '2.0000') "M00=2.0000" $(if ($m.Success) { $m.Value } else { "<timeout>" })

# ---------------------------------------------------------------------
# Step 3: direction away -> toward  =>  M21 sign flips to +0.8660
# ---------------------------------------------------------------------
try {
    Select-Combo 'TiltCombo' '向前倾'
    $m = Wait-Trace 'tuning: warp maxFold=60\.0 r=4\.00 tiltAway=False .*M21=([-\d.]+)'
    Check "tilt toward -> M21" ($m.Success -and $m.Groups[1].Value -eq '0.8660') "M21=0.8660" $(if ($m.Success) { $m.Value } else { "<timeout>" })
} catch {
    Check "tilt toward -> M21" $false "M21=0.8660" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Step 4: hinge bottom -> top
# ---------------------------------------------------------------------
try {
    Select-Combo 'HingeCombo' '顶边'
    $m = Wait-Trace 'tuning: warp .*hingeTop=True'
    Check "hinge top" $m.Success "hingeTop=True" $(if ($m.Success) { $m.Value } else { "<timeout>" })
} catch {
    Check "hinge top" $false "hingeTop=True" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Step 5: max blur 160 -> 80
# ---------------------------------------------------------------------
[void](Set-Slider 'MaxBlurSlider' 80)
$m = Wait-Trace 'tuning: blur max=80\.0'
Check "maxBlur 80" $m.Success "blur max=80.0" $(if ($m.Success) { $m.Value } else { "<timeout>" })

# ---------------------------------------------------------------------
# Step 6: envelope coherence. Start 0.80 while Full is 0.60 must clamp
# Full up to 0.81 (a start >= full is a UI state, never a render state).
# ---------------------------------------------------------------------
[void](Set-Slider 'EnvelopeStartSlider' 0.80)
$m = Wait-Trace 'tuning: blur max=80\.0 start=0\.80 full=([\d.]+)'
Check "envelope clamp" ($m.Success -and $m.Groups[1].Value -eq '0.81') "start=0.80 full=0.81" $(if ($m.Success) { $m.Value } else { "<timeout>" })

# ---------------------------------------------------------------------
# Step 7: gradient pass (M2.1). Strength is set FIRST while the pass is
# still off - that is the interesting half of the contract: the trace must
# report strength=0.000 because GradientOptions.EffectiveStrength collapses
# to exactly 0 when disabled (the shader branches on > 0.0, so anything
# non-zero here would mean "off" is not a no-op).
# ---------------------------------------------------------------------
[void](Set-Slider 'GradientStrengthSlider' 0.15)
$m = Wait-Trace 'tuning: gradient enabled=False strength=([\d.]+)'
Check "gradient off ignores strength" ($m.Success -and $m.Groups[1].Value -eq '0.000') "enabled=False strength=0.000" $(if ($m.Success) { $m.Value } else { "<timeout>" })

# ---------------------------------------------------------------------
# Step 8: enable it -> the same 0.15 now reaches the shader
# ---------------------------------------------------------------------
try {
    [void](Set-Check 'GradientCheck' $true)
    $m = Wait-Trace 'tuning: gradient enabled=True strength=([\d.]+) hue=(\d+)->(\d+)'
    Check "gradient on -> strength" ($m.Success -and $m.Groups[1].Value -eq '0.150' -and $m.Groups[2].Value -eq '205' -and $m.Groups[3].Value -eq '275') "enabled=True strength=0.150 hue=205->275" $(if ($m.Success) { $m.Value } else { "<timeout>" })
} catch {
    Check "gradient on -> strength" $false "enabled=True strength=0.150 hue=205->275" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Step 9: hue start 205 -> 300 (hue is a ramp, so only the start moves)
# ---------------------------------------------------------------------
[void](Set-Slider 'HueStartSlider' 300)
$m = Wait-Trace 'tuning: gradient enabled=True strength=0\.150 hue=300->275'
Check "hue start 300" $m.Success "hue=300->275" $(if ($m.Success) { $m.Value } else { "<timeout>" })

# ---------------------------------------------------------------------
# Step 10: disable again -> back to a bit-exact no-op
# ---------------------------------------------------------------------
try {
    [void](Set-Check 'GradientCheck' $false)
    $m = Wait-Trace 'tuning: gradient enabled=False strength=0\.000 hue=300->275'
    Check "gradient off again" $m.Success "enabled=False strength=0.000" $(if ($m.Success) { $m.Value } else { "<timeout>" })
} catch {
    Check "gradient off again" $false "enabled=False strength=0.000" $_.Exception.Message
}

# ---------------------------------------------------------------------
# Step 11: 恢复默认 puts every knob back
# ---------------------------------------------------------------------
$btn = Find-ById 'ResetTuningButton'
if ($btn) {
    $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 900
} else {
    Log "WARN: ResetTuningButton not found"
}
$m = Wait-Trace 'tuning: warp maxFold=90\.0 r=2\.50 tiltAway=True hingeTop=False .*M00=([-\d.]+)'
Check "reset -> defaults" ($m.Success -and $m.Groups[1].Value -eq '0.0000') "maxFold=90 r=2.50 away bottom M00=0.0000" $(if ($m.Success) { $m.Value } else { "<timeout>" })
$m = Wait-Trace 'tuning: blur max=160\.0 start=0\.30 full=0\.60'
Check "reset -> blur defaults" $m.Success "max=160 start=0.30 full=0.60" $(if ($m.Success) { $m.Value } else { "<timeout>" })
$m = Wait-Trace 'tuning: gradient enabled=False strength=0\.000 hue=205->275'
Check "reset -> gradient defaults" $m.Success "enabled=False strength=0.000 hue=205->275" $(if ($m.Success) { $m.Value } else { "<timeout>" })

# ---------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------
$failed = ($steps | Where-Object { -not $_.Pass }).Count
$overall = ($failed -eq 0)
Log ""
Log ("OVERALL = " + $(if ($overall) { "PASS ($($steps.Count)/$($steps.Count))" } else { "FAIL ($failed of $($steps.Count) failed)" }))

[ordered]@{
    GeneratedAt = (Get-Date).ToString("s")
    Exe         = $Exe
    Steps       = $steps
    Overall     = $overall
} | ConvertTo-Json -Depth 6 | Out-File (Join-Path $OutputDir "tuning-panel-result.json") -Encoding utf8

Get-Process -Name 'DuoFlow.App' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
if (-not $overall) { exit 1 }
exit 0
