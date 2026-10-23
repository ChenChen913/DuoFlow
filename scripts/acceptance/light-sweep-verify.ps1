# =====================================================================
# DuoFlow - light sweep acceptance (M2.2).  [scripts/acceptance]
#
# WHAT IT PROVES: the M2.2 highlight is (a) present, (b) where the model says
# it is, (c) bounded by the strength cap, and (d) absent both when disabled
# and below the blur envelope.
#
# WHY A DIFFERENTIAL AND NOT A PEAK: at the default strength (0.08) the band
# adds only ~2 luminance units at mid fold, because the shader scales it by
# (1 - rgb) and mid-fold is still a BRIGHT surface (light ~= 0.88 there). The
# ramp from hinge to far edge is ~190 units across the quad, so a 2-unit bump
# does NOT create a local maximum - searching for "the brightest row" would
# find the hinge every time and prove nothing. Subtracting a sweep-OFF render
# of the SAME binary leaves the band alone, and its peak is then unambiguous.
#
# HOW: three runs of the same binary against the same fullscreen reference
# window, dumps archived between runs:
#   OFF1  no --light-sweep      reference for the differential
#   OFF2  no --light-sweep      run-to-run noise floor (must be ~0)
#   ON    --light-sweep         the band
# Same technique as the M2.1 no-op proof (see README): without a noise floor
# you cannot tell "1 LSB of resampling jitter" from "the pass leaked".
#
#   expected row for a fold height sy:
#     Y = M00*sy / (M11 - M21*sy)      row = h*(1 - Y)
#     M00 = r*cos(phi), M11 = r, M21 = -sin(phi), phi = p*pi/2
#
# USAGE (Windows PowerShell 5.1, no admin, interactive desktop):
#   .\scripts\acceptance\light-sweep-verify.ps1
#   .\scripts\acceptance\light-sweep-verify.ps1 -OutputDir D:\tmp\sweep
#   .\scripts\acceptance\light-sweep-verify.ps1 -AnalyzeOnly     # reuse dumps
#
# OUTPUT: light-sweep-verify.txt (log) + light-sweep-result.json
# EXIT CODE: 0 = every step passed, 1 = at least one did not.
# NOTE: ~3.5 min. Opens a fullscreen topmost reference window and a fullscreen
# overlay for three consecutive runs. Do not use the machine meanwhile.
# =====================================================================

param(
    [switch]$AnalyzeOnly,
    [string]$RepoRoot = "",
    [string]$OutputDir = "",
    [string]$Exe = "",
    [int]$StartupSeconds = 10,
    [int]$TimeoutSeconds = 150,
    # Kept in sync with LightSweepOptions defaults - the panel seeds the
    # renderer from these, so a mismatch here shows up as a position error.
    [double]$SweepStrength = 0.08,
    [double]$SweepWidth = 0.10
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path }
if (-not $OutputDir) { $OutputDir = Join-Path $env:TEMP "duoflow-light-sweep" }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$log = Join-Path $OutputDir "light-sweep-verify.txt"
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

$trace = Join-Path $env:USERPROFILE "duoflow-trace.txt"
$selftestDir = Join-Path $env:USERPROFILE "duoflow-warp-selftest"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace W -Name T -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
'@
[void][W.T]::SetProcessDPIAware()

$tags = @("up0","up1","up2","up3","up4","down1","down2","down3","down4")
$ps   = @(0.0, 0.25, 0.5, 0.75, 1.0, 0.75, 0.5, 0.25, 0.0)

# BlurOptions.StartProgress - below it the envelope is closed, intensity is 0,
# and the sweep is multiplied by it, so the band must be absent there.
$script:BlurStartProgress = 0.30

$script:traceOffset = 0
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

# Dump geometry from the app's own trace (never hardcode it).
function Get-DumpSize([string]$leaf) {
    $text = TraceTail
    if (-not $text) { $text = if (Test-Path $trace) { Get-Content -LiteralPath $trace -Raw } else { "" } }
    $found = $null
    foreach ($line in ($text -split "`r?`n")) {
        if ($line -match 'selftest: dumped .*?\((\d+)\D+?(\d+)\)') {
            # Capture the groups BEFORE any further -match: a second match
            # overwrites $Matches, and the escaped-leaf test below has no
            # groups, so $Matches[1] would silently become $null -> 0.
            $m = $Matches
            if ($line -match [regex]::Escape($leaf)) {
                $found = @{ W = [int]$m[1]; H = [int]$m[2] }
            }
        }
    }
    if ($found) { return $found }
    throw "no 'selftest: dumped $leaf (WxH)' line in this run's trace"
}

# ---- reference window (same pattern as warp-selftest) ----------------
$refForm = New-Object System.Windows.Forms.Form
$refForm.FormBorderStyle = 'None'
$refForm.StartPosition = 'Manual'
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$refForm.SetBounds($bounds.X, $bounds.Y, $bounds.Width, $bounds.Height)
$refForm.TopMost = $true
$refForm.ShowInTaskbar = $false
$refForm.add_Paint({
    param($s, $e)
    $g = $e.Graphics
    $cw = $s.ClientSize.Width; $ch = $s.ClientSize.Height
    $g.FillRectangle([System.Drawing.Brushes]::White, 0, 0, $cw, $ch)
    for ($y = 0; $y -lt $ch; $y += 80) {
        $g.FillRectangle([System.Drawing.Brushes]::Black, 0, $y, $cw, 8)
    }
})

# One full sweep run. $ExtraArgs is what distinguishes OFF from ON.
function Invoke-Run([string]$name, [string[]]$ExtraArgs) {
    $script:traceOffset = 0
    if (Test-Path $trace) { $script:traceOffset = (Get-Item -LiteralPath $trace).Length }
    $startedAt = Get-Date

    $argList = @('--warp-selftest') + $ExtraArgs
    Log "  launching: $Exe $($argList -join ' ')"
    Start-Process -FilePath $Exe -ArgumentList $argList | Out-Null
    Start-Sleep -Seconds $StartupSeconds

    # Move the console off-screen: its dark surface would pollute the capture.
    $proc = Get-Process -Name 'DuoFlow.App' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($proc -and $proc.MainWindowHandle -ne [IntPtr]::Zero) {
        [void][W.T]::SetWindowPos($proc.MainWindowHandle, [IntPtr]::Zero, -2500, -2500, 0, 0, 0x15)
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $done = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        $content = TraceTail
        if ($content -match "selftest: DONE") { $done = $true; break }
        if ($content -match "selftest: FAILED") { break }
    }
    Get-Process -Name 'DuoFlow.App' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
    if (-not $done) { throw "run '$name' did not reach 'selftest: DONE'" }

    # Reject stale dumps: a step that failed to dump would otherwise be
    # analysed against the previous run's file and silently pass.
    $stale = @()
    foreach ($t in $tags) {
        $f = Join-Path $selftestDir "selftest-$t.raw"
        if (-not (Test-Path $f)) { $stale += "$t (missing)" }
        elseif ((Get-Item -LiteralPath $f).LastWriteTime -lt $startedAt) { $stale += "$t (stale)" }
    }
    if ($stale.Count -gt 0) { throw "run '$name' produced no fresh dump for: $($stale -join ', ')" }

    $dest = Join-Path $OutputDir $name
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    $size = Get-DumpSize "selftest-up1.raw"
    foreach ($t in $tags) {
        Copy-Item (Join-Path $selftestDir "selftest-$t.raw") (Join-Path $dest "selftest-$t.raw") -Force
    }
    return @{ Dir = $dest; W = $size.W; H = $size.H }
}

# ---- analysis --------------------------------------------------------

# Per-row luminance, averaged over the horizontal span that is inside the
# quad at every progress, alpha-unpremultiplied (the shader premultiplies).
#
# The span starts at 46% and not 42% (which is what warp-selftest.ps1 uses):
# x ~= 800 carried an intermittent bright UI element in one of three runs, and
# at 42% it landed on the span's left edge - 21 pixels, which is enough to move
# the ROW PROFILE by 1.6 units. That is the same order as the band this script
# exists to measure, so the span is narrowed until the noise floor is an order
# of magnitude below the signal. warp-selftest.ps1 can keep 42% because its
# coarsest assertion is 6 units.
function Get-RowProfile([byte[]]$bytes, [int]$w, [int]$h) {
    $x0 = [int]($w * 0.46); $x1 = [int]($w * 0.58)
    $lum = New-Object 'double[]' $h
    for ($y = 0; $y -lt $h; $y++) {
        $row = $y * $w
        $sum = 0.0; $n = 0
        for ($x = $x0; $x -lt $x1; $x += 2) {
            $o = ($row + $x) * 4
            $a = $bytes[$o + 3]
            if ($a -lt 16) { continue }
            $l = 0.114 * $bytes[$o] + 0.587 * $bytes[$o + 1] + 0.299 * $bytes[$o + 2]
            $sum += $l * 255.0 / $a; $n++
        }
        $lum[$y] = if ($n -gt 0) { $sum / $n } else { -1.0 }
    }
    return $lum
}

function Get-QuadTop([byte[]]$bytes, [int]$w, [int]$h) {
    $cx = [int]($w / 2)
    for ($y = 0; $y -lt $h; $y++) {
        if ($bytes[($y * $w + $cx) * 4 + 3] -ge 120) { return $y }
    }
    return -1
}

# Fold height -> destination row, straight from the homography.
function RowForSy([double]$p, [double]$sy, [int]$h) {
    $r = 2.5
    $phi = $p * [math]::PI / 2.0
    $M00 = $r * [math]::Cos($phi)
    $M11 = $r
    $M21 = -[math]::Sin($phi)
    $den = $M11 - $M21 * $sy
    if ($den -lt 1e-9) { return [double]$h }
    $Y = $M00 * $sy / $den
    return $h * (1.0 - $Y)
}

# Rows per fold-height unit at sy (for turning the FWHM into a row tolerance).
function RowsPerSy([double]$p, [double]$sy, [int]$h) {
    $a = RowForSy $p ($sy - 0.005) $h
    $b = RowForSy $p ($sy + 0.005) $h
    return [math]::Abs($b - $a) / 0.01
}

function Load-Dump([string]$dir, [string]$tag) {
    return [IO.File]::ReadAllBytes((Join-Path $dir "selftest-$tag.raw"))
}

# ---- results ---------------------------------------------------------
$steps = New-Object System.Collections.ArrayList
function Check([string]$name, [bool]$ok, [string]$expected, [string]$actual) {
    [void]$steps.Add([ordered]@{ Step = $name; Pass = $ok; Expected = $expected; Actual = $actual })
    Log ("{0} {1}  expect[{2}]  got[{3}]" -f $(if ($ok) { "PASS" } else { "FAIL" }), $name, $expected, $actual)
}

# ---- run -------------------------------------------------------------
Log "exe = $Exe"
Log "out = $OutputDir"
Log ""

$off1 = $null; $off2 = $null; $on = $null
if ($AnalyzeOnly) {
    # Re-analyse the dumps already on disk. The geometry is not recoverable
    # from a raw file alone (only W*H*4 bytes), so it comes from the previous
    # result JSON - which is exactly why the JSON records it.
    $prevJson = Join-Path $OutputDir "light-sweep-result.json"
    if (-not (Test-Path $prevJson)) { Log "FATAL: -AnalyzeOnly needs $prevJson"; exit 1 }
    $prev = Get-Content -LiteralPath $prevJson -Raw | ConvertFrom-Json
    $w = [int]$prev.Dump.Width
    $h = [int]$prev.Dump.Height
    foreach ($n in @('off1', 'off2', 'on')) {
        $d = Join-Path $OutputDir $n
        if (-not (Test-Path $d)) { Log "FATAL: -AnalyzeOnly: missing dump dir $d"; exit 1 }
    }
    $off1 = @{ Dir = (Join-Path $OutputDir 'off1'); W = $w; H = $h }
    $off2 = @{ Dir = (Join-Path $OutputDir 'off2'); W = $w; H = $h }
    $on = @{ Dir = (Join-Path $OutputDir 'on'); W = $w; H = $h }
    Log "analyze-only: reusing dumps in $OutputDir (${w}x${h})"
} else {
    Log "reference window shown; running three sweeps (this takes ~3 min)"
    $refForm.Show()
    [System.Windows.Forms.Application]::DoEvents()
    [void][W.T]::SetCursorPos(5, 5)
    Start-Sleep -Milliseconds 600
    try {
        Log "[1/3] OFF1 (reference)"
        $off1 = Invoke-Run 'off1' @()
        Log "[2/3] OFF2 (noise floor)"
        $off2 = Invoke-Run 'off2' @()
        Log "[3/3] ON (--light-sweep)"
        $on = Invoke-Run 'on' @('--light-sweep')
    } finally {
        $refForm.Close()
        Get-Process -Name 'DuoFlow.App' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    }
    $w = $off1.W; $h = $off1.H
}

Log ""
Log "dump geometry: ${w}x${h}"

# ---- per-step differential -------------------------------------------
$report = @()
for ($i = 0; $i -lt 9; $i++) {
    $tag = $tags[$i]; $p = $ps[$i]

    $b1 = Load-Dump $off1.Dir $tag
    $b2 = Load-Dump $off2.Dir $tag
    $b3 = Load-Dump $on.Dir   $tag

    $qt = Get-QuadTop $b1 $w $h
    if ($qt -lt 0) {
        Log "  $tag p=$p : no opaque rows (endpoint) - nothing to measure"
        $report += [ordered]@{ Tag = $tag; Progress = $p; QuadTop = -1; Note = 'endpoint' }
        continue
    }

    $l1 = Get-RowProfile $b1 $w $h
    $l2 = Get-RowProfile $b2 $w $h
    $l3 = Get-RowProfile $b3 $w $h

    $pad = [math]::Max(4, [int](($h - $qt) * 0.03))
    $a0 = $qt + $pad
    $a1 = $h - 1 - $pad

    $noise = 0.0; $peak = -1e9; $peakRow = -1
    for ($y = $a0; $y -le $a1; $y++) {
        if ($l1[$y] -lt 0) { continue }
        $dNoise = [math]::Abs($l2[$y] - $l1[$y])
        if ($dNoise -gt $noise) { $noise = $dNoise }
        if ($l3[$y] -lt 0) { continue }
        $d = $l3[$y] - $l1[$y]
        if ($d -gt $peak) { $peak = $d; $peakRow = $y }
    }

    $centre = $p            # Position 0, HingeToFar -> centre == progress
    $expRow = RowForSy $p $centre $h
    $fwhmRows = (RowsPerSy $p $centre $h) * $SweepWidth
    $rowTol = [math]::Max(8.0, $fwhmRows * 0.75)
    $rowErr = if ($peakRow -ge 0) { [math]::Abs($peakRow - $expRow) } else { 9999.0 }
    $cap = $SweepStrength * 255.0

    Log ("  {0} p={1}  quadTop={2}  noise={3:0.00}  peak={4:0.00} @row {5} (expect {6:0.0}, FWHM {7:0.0} rows)" -f `
        $tag, $p, $qt, $noise, $peak, $peakRow, $expRow, $fwhmRows)
    $report += [ordered]@{
        Tag = $tag; Progress = $p; QuadTop = $qt
        NoisePeak = [math]::Round($noise, 3)
        PeakDelta = [math]::Round($peak, 3)
        PeakRow = $peakRow; ExpectedRow = [math]::Round($expRow, 1)
        RowError = [math]::Round($rowErr, 1); RowTolerance = [math]::Round($rowTol, 1)
        FwhmRows = [math]::Round($fwhmRows, 1); StrengthCap = [math]::Round($cap, 1)
    }

    # -- assertions --
    if ($p -le $script:BlurStartProgress + 0.01) {
        Check "$tag below envelope: no band" ($peak -lt 1.5) "peak < 1.5" "peak=$([math]::Round($peak,2))"
    } else {
        Check "$tag band present" ($peak -ge 1.0) "peak >= 1.0" "peak=$([math]::Round($peak,2))"
        Check "$tag band bounded by strength cap" ($peak -le $cap + 3.0) "peak <= $([math]::Round($cap,1))+3" "peak=$([math]::Round($peak,2))"
        Check "$tag band at the predicted row" ($rowErr -le $rowTol) "|row-$([math]::Round($expRow,0))| <= $([math]::Round($rowTol,0))" "row=$peakRow (err $([math]::Round($rowErr,1)))"
    }
}

# ---- summary ---------------------------------------------------------
# A run that measured NOTHING must never report PASS. Without this guard a
# broken geometry read (or a dump the analyser could not open) reports
# "PASS (0/0)" - exactly the false positive class this project keeps hitting.
if ($steps.Count -eq 0) {
    Log ""
    Log "FATAL: no step produced a measurable quad - the run is invalid, not passing."
    Log ("OVERALL = FAIL (0 checks executed, dump geometry {0}x{1})" -f $w, $h)
    [ordered]@{
        GeneratedAt = (Get-Date).ToString("s"); Exe = $Exe
        Dump = @{ Width = $w; Height = $h }; Steps = @(); Overall = $false
        Fatal = 'no measurable quad'
    } | ConvertTo-Json -Depth 6 | Out-File (Join-Path $OutputDir "light-sweep-result.json") -Encoding utf8
    exit 1
}

# Wrap in @(): with exactly one match, Where-Object returns the object
# itself, and .Count on an ordered dictionary is the number of KEYS (4) -
# which is how "1 failed" once printed as "4 of 20 failed".
$failed = @($steps | Where-Object { -not $_.Pass }).Count
$overall = ($failed -eq 0)
$maxNoise = ($report | Where-Object { $_.Contains('NoisePeak') } | ForEach-Object { $_.NoisePeak } | Measure-Object -Maximum).Maximum

Log ""
Log ("noise floor (OFF1 vs OFF2, max row delta) = {0}" -f $maxNoise)
Log ("OVERALL = " + $(if ($overall) { "PASS ($($steps.Count)/$($steps.Count))" } else { "FAIL ($failed of $($steps.Count) failed)" }))

[ordered]@{
    GeneratedAt = (Get-Date).ToString("s")
    Exe         = $Exe
    Dump        = @{ Width = $w; Height = $h }
    Sweep       = @{ Strength = $SweepStrength; Width = $SweepWidth; Position = 0.0; Direction = 'HingeToFar' }
    Steps       = $steps
    PerProgress = $report
    NoiseFloor  = $maxNoise
    Overall     = $overall
} | ConvertTo-Json -Depth 6 | Out-File (Join-Path $OutputDir "light-sweep-result.json") -Encoding utf8

if (-not $overall) { exit 1 }
exit 0
