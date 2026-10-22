# =====================================================================
# DuoFlow - in-process warp self-test (run + analyze).  [scripts/acceptance]
#
# WHAT IT PROVES:
#   1. GEOMETRY  - changing Progress produces a continuous spatial
#      deformation of the desktop, measured against the WarpGeometry
#      homography, not eyeballed.
#   2. LIGHT     - the DD-046 light falloff points the right way: the
#      hinge side is the bright end, the far edge is the dark end, and the
#      profile is monotone between them.
#   3. BLUR      - the DD-046 frost points the right way: high-frequency
#      content survives at the hinge and is gone at the far edge.
#   4. HYSTERESIS- the 0->1 and 1->0 sweeps land on the same numbers.
#
# HOW: the app (--warp-selftest) drives the AnimationEngine through
# 0->1->0 and dumps the RENDERED back buffer (BGRA raw, alpha included) at
# each step; this script paints a fullscreen reference window, launches the
# app, then measures the dumped frames.
#
#     geometry: boundary sy=k/10  ->  dest row = H * (1 - Y(sy))
#               Y(sy) = r*cos(phi)*sy / (r + sin(phi)*sy),  r=2.5, phi=90deg*p
#               quad top edge    ->  dest row = H * (1 - FarEdgeY)
#
# REFERENCE PATTERN: uniform WHITE with BLACK tick lines (8px every 80px).
#   Why not the old 10-band palette? Band luminances span 29 (blue) to 255
#   (white), i.e. a 9x swing that swamps the light falloff completely - the
#   falloff signal is ~4x across the quad, so a coloured reference cannot
#   measure it. A uniform field makes luminance(y) == 255 * light(y)
#   directly, and the ticks are the high-frequency probe for the blur
#   direction (they stay razor sharp at the hinge and dissolve into the
#   frost at the far edge). Band classification is gone with the palette:
#   model v2 (DD-046) deliberately sinks the far bands into shadow, so
#   colour classification there was already meaningless.
#
# Tolerance +/-5 px on quadTop (linear-sampler color blend ~2px, alpha
# feather ~4px). Hysteresis +/-2 px on quadTop, +/-0.05 on falloff depth.
#
# This harness was developed outside the repo during M1.4 (the machine's
# desktop-composition path hid SwapChainPanel content on screen at the time,
# reproduced with the pre-M1.4 baseline - so acceptance ran in-process).
# It lives here now so the next person/agent can re-run the same numbers.
#
# USAGE (Windows PowerShell 5.1, no admin):
#   .\scripts\acceptance\warp-selftest.ps1                     # build + run + analyze
#   .\scripts\acceptance\warp-selftest.ps1 -AnalyzeOnly         # re-analyze existing dumps
#   .\scripts\acceptance\warp-selftest.ps1 -OutputDir D:\tmp\run
#
# EXIT CODE: 0 = all steps within tolerance, 1 = a step failed.
# NOTE: this opens a fullscreen topmost reference window and a fullscreen
# overlay for ~1 minute. Do not run it while doing something else on the
# machine.
# =====================================================================

param(
    [switch]$AnalyzeOnly,
    [string]$RepoRoot = "",
    [string]$OutputDir = "",
    [string]$Exe = "",
    [int]$TimeoutSeconds = 150
)

$ErrorActionPreference = "Stop"

# Default repo root = two levels up from scripts/acceptance/.
if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path }
$repo = $RepoRoot

# Analysis output (JSON + per-step dumps stay here); defaults outside the repo.
if (-not $OutputDir) { $OutputDir = Join-Path $env:TEMP "duoflow-warp-selftest\run" }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$root = $OutputDir

$selftestDir = Join-Path $env:USERPROFILE "duoflow-warp-selftest"
$trace = Join-Path $env:USERPROFILE "duoflow-trace.txt"

if (-not $Exe) {
    $found = Get-ChildItem (Join-Path $repo "src\DuoFlow.App\bin") -Recurse -Filter "DuoFlow.App.exe" -ErrorAction SilentlyContinue |
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
Write-Host "repo : $repo"
Write-Host "exe  : $exe"
Write-Host "out  : $root"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# W: window finding + cursor + DPI
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class W {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int m);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
}
"@
[void][W]::SetProcessDPIAware()

# ---------------------------------------------------------------------
# Analysis
# ---------------------------------------------------------------------

# Reference window geometry - kept in sync with the paint handler below.
$script:TickThickness = 8
$script:TickSpacing = 80

# BlurOptions.StartProgress. Below this progress the model deliberately has
# NO blur and NO light falloff (the envelope has not opened yet), so a flat,
# fully sharp render there is CORRECT - asserting "far edge is darker" below
# the envelope would be a false failure. Keep in sync with
# src/DuoFlow.Render/BlurOptions.cs (a mismatch shows up here as a spurious
# "falloff present below the envelope").
$script:BlurStartProgress = 0.30

# Dest-row period of the reference window's tick lines, at one destination
# row. The blur cannot erase a periodic pattern whose period is small, so
# the ticks survive as a sinusoidal ripple in the luminance profile - and a
# bin that spans only half a period would report the ripple's phase instead
# of the falloff (measured: +/-20 units of pure artefact). Averaging over
# EXACTLY one local period removes it without touching the smooth ramp.
#   destRow = h*(1 - Y),  Y = M00*sy/(M11 - M21*sy)  =>  dRow/dsy = h*dY/dsy
#   source row = (1 - sy)*h_src  =>  one tick spacing covers
#   TickSpacing * (h*dY/dsy) / h_src destination rows, and h ~= h_src
#   (render 1914x1074 vs capture 1920x1080, a 0.3% difference).
function Get-RipplePeriod([double]$p, [double]$yFrame) {
    $r = 2.5; $phi = $p * [math]::PI / 2.0
    $c = [math]::Cos($phi); $s = [math]::Sin($phi)
    $M00 = $r * $c; $M11 = $r; $M21 = -$s
    $w = $M00 + $M21 * $yFrame
    if ($w -lt 1e-6) { return 2.0 }
    $sy = ($M11 * $yFrame) / $w
    $d = $M11 - $M21 * $sy
    if ($d -lt 1e-6) { return 2.0 }
    $dYdsy = ($M00 * $M11) / ($d * $d)
    return [math]::Max(2.0, $script:TickSpacing * $dYdsy)
}

# Predicted quad-top row (back-buffer pixel row, top-down) for progress p.
# All row predictions are derived from the dump's ACTUAL height (the back
# buffer is the panel's pixel size, which depends on DPI and window size).
function Predict([double]$p, [int]$h) {
    $r = 2.5; $phi = $p * [math]::PI / 2.0
    $c = [math]::Cos($phi); $s = [math]::Sin($phi)
    $Yfar = ($r * $c) / ($r + $s)
    return [math]::Round($h * (1.0 - $Yfar), 1)
}

# Read only the part of the trace written by THIS run (see $script:traceOffset).
function Read-TraceTail {
    if (-not (Test-Path $trace)) { return "" }
    $fs = [IO.File]::Open($trace, 'Open', 'Read', 'ReadWrite')
    try {
        $offset = [long]$script:traceOffset
        if ($offset -gt $fs.Length) { $offset = 0 }
        $fs.Position = $offset
        $sr = New-Object IO.StreamReader($fs)
        return $sr.ReadToEnd()
    } finally {
        $fs.Dispose()
    }
}

# Dump geometry comes from the app's own trace line:
#   selftest: dumped <path> (WxH)
# Never hardcode the width here: it silently corrupts every row index the
# moment the panel or DPI changes (it already went 1914 -> 600 -> 1914).
function Get-DumpSize([string]$path) {
    $leaf = Split-Path $path -Leaf
    $text = Read-TraceTail
    if (-not $text) { $text = if (Test-Path $trace) { Get-Content -LiteralPath $trace -Raw } else { "" } }
    $found = $null
    foreach ($line in ($text -split "`r?`n")) {
        # The separator in the trace is '×' (U+00D7), and reading the file
        # back with a different code page can mangle it - match ANY non-digit
        # run between the two numbers instead of the literal character.
        if ($line -match 'selftest: dumped .*?\((\d+)\D+?(\d+)\)') {
            $m = $Matches
            if ($line -match [regex]::Escape($leaf)) {
                # Keep the LAST match: the newest run wins when the trace has
                # several runs with different geometries.
                $found = @{ W = [int]$m[1]; H = [int]$m[2] }
            }
        }
    }
    if ($found) { return $found }
    throw "no 'selftest: dumped $leaf (WxH)' line in $trace - run the sweep first"
}

# Per-row luminance of the rendered frame, averaged across a horizontal
# span that is guaranteed INSIDE the projected quad at every progress.
#
# Why a span and not the single center column: the reference field is
# uniform horizontally, so averaging 16% of the width kills sensor/dither
# noise without touching the vertical signal we are after.
# Why un-premultiply: the shader writes rgb*a*fade with a*fade in alpha, so
# the raw RGB is already scaled by coverage - dividing by alpha recovers
# the true surface luminance (and makes the feather rows usable instead of
# looking like a shadow).
function Get-RowProfile([byte[]]$bytes, [int]$w, [int]$h) {
    $x0 = [int]($w * 0.42); $x1 = [int]($w * 0.58)
    $lum = New-Object 'double[]' $h
    for ($y = 0; $y -lt $h; $y++) {
        $row = $y * $w
        $sum = 0.0; $n = 0
        for ($x = $x0; $x -lt $x1; $x += 2) {
            $o = ($row + $x) * 4
            $a = [int]$bytes[$o + 3]
            if ($a -lt 8) { continue }                     # outside the quad
            $l = 0.114 * $bytes[$o] + 0.587 * $bytes[$o + 1] + 0.299 * $bytes[$o + 2]
            $sum += $l * 255.0 / $a; $n++
        }
        $lum[$y] = if ($n -gt 0) { $sum / $n } else { -1.0 }
    }
    return $lum
}

# Light-falloff + blur-direction metrics over the projected quad.
#
# The quad occupies rows quadTop..h-1. In the destination the HINGE is the
# BOTTOM row (HingeAtTop=false: dest y = 1 - v) and the FAR EDGE is quadTop.
# Expected luminance with a white reference: L(y) = 255 * light(y), and
# light = saturate(1 - 0.006 * coc*MaxBlurPixels) grows monotonically as y
# approaches the hinge.
function Measure-Physics([double[]]$lum, [int]$quadTop, [int]$h, [double]$p) {
    if ($quadTop -lt 0) { return $null }
    $bot = $h - 1
    $qh = $bot - $quadTop + 1
    # Trim the alpha feather rows at both quad edges (EdgeFeather=0.004 plus
    # the 1-texel source border) - they are legitimately dark and would
    # otherwise be read as "extra falloff".
    $pad = [math]::Max(4, [int]($qh * 0.03))
    $a0 = $quadTop + $pad           # first analyzed row (far-edge side)
    $a1 = $bot - $pad               # last analyzed row  (hinge side)
    $n = $a1 - $a0 + 1
    if ($n -lt 40) { return $null }

    # ---- step 1: remove the tick ripple with a local-period box filter.
    # The filter window may read BEYOND the binned range (f0..f1 is the whole
    # quad minus 1 row): clipping it at a0/a1 would make the first and last
    # few rows a partial average, biasing the far end bright and the hinge
    # end dark - measured as a systematic ~0.09 under-report of the depth.
    $f0 = $quadTop + 1; $f1 = $bot - 1
    $sm = New-Object 'double[]' $h
    for ($y = $a0; $y -le $a1; $y++) {
        $pw = [int][math]::Round((Get-RipplePeriod $p (1.0 - $y / [double]$h)))
        if ($pw -lt 2) { $pw = 2 }
        $lo = $y - [int][math]::Floor($pw / 2)
        $acc = 0.0; $cnt = 0
        for ($k = $lo; $k -lt ($lo + $pw); $k++) {
            if ($k -lt $f0 -or $k -gt $f1) { continue }
            if ($lum[$k] -lt 0) { continue }
            $acc += $lum[$k]; $cnt++
        }
        $sm[$y] = if ($cnt -gt 0) { $acc / $cnt } else { -1.0 }
    }

    # ---- step 2: 10-bin vertical profile. bin 0 = far edge, bin 9 = hinge.
    # 10 bins (not 20): each bin must span at least one ripple period, or
    # the profile reports the ripple instead of the ramp (that is exactly
    # what the 20-bin first version did - 1 spurious "drop" of 9-11 units on
    # every step above the envelope). The filter in step 1 does the heavy
    # lifting; the bin size is the belt to that braces.
    $NB = 10
    $bins = New-Object 'double[]' $NB
    for ($i = 0; $i -lt $NB; $i++) {
        $s = $a0 + [int][math]::Floor($n * $i / $NB)
        $e = $a0 + [int][math]::Floor($n * ($i + 1) / $NB) - 1
        if ($e -lt $s) { $e = $s }
        $acc = 0.0; $cnt = 0
        for ($y = $s; $y -le $e; $y++) { if ($sm[$y] -ge 0) { $acc += $sm[$y]; $cnt++ } }
        $bins[$i] = if ($cnt -gt 0) { $acc / $cnt } else { -1.0 }
    }

    $hingeL = 0.0; $c = 0
    for ($i = $NB - 2; $i -lt $NB; $i++) { if ($bins[$i] -ge 0) { $hingeL += $bins[$i]; $c++ } }
    if ($c -gt 0) { $hingeL = $hingeL / $c }
    $farL = 0.0; $c = 0
    for ($i = 0; $i -le 1; $i++) { if ($bins[$i] -ge 0) { $farL += $bins[$i]; $c++ } }
    if ($c -gt 0) { $farL = $farL / $c }

    # Monotonicity: luminance must NOT fall as we walk toward the hinge.
    # 6 luminance units of slack absorbs dithering/rounding; a real
    # direction inversion in this profile is tens of units.
    $violations = 0; $worstDrop = 0.0
    for ($i = 0; $i -lt $NB - 1; $i++) {
        if ($bins[$i] -lt 0 -or $bins[$i + 1] -lt 0) { continue }
        $drop = $bins[$i] - $bins[$i + 1]
        if ($drop -gt 6) { $violations++; if ($drop -gt $worstDrop) { $worstDrop = $drop } }
    }

    # Blur direction: the biggest single-row luminance step inside a window.
    # Sharp tick edge at the hinge -> ~85-255; fully frosted far edge -> a
    # few units (only the smooth falloff gradient remains). Measured on the
    # RAW profile, not the smoothed one - smoothing would erase the very
    # high-frequency content this metric is about.
    $win = [math]::Max(8, [int]($n * 0.15))
    $hfFar = 0.0; $hfHinge = 0.0
    for ($y = $a0 + 1; $y -lt $a0 + $win; $y++) {
        if ($lum[$y] -lt 0 -or $lum[$y - 1] -lt 0) { continue }
        $d = [math]::Abs($lum[$y] - $lum[$y - 1]); if ($d -gt $hfFar) { $hfFar = $d }
    }
    for ($y = $a1 - $win + 1; $y -le $a1; $y++) {
        if ($lum[$y] -lt 0 -or $lum[$y - 1] -lt 0) { continue }
        $d = [math]::Abs($lum[$y] - $lum[$y - 1]); if ($d -gt $hfHinge) { $hfHinge = $d }
    }

    $depth = if ($hingeL -gt 1.0) { 1.0 - ($farL / $hingeL) } else { -1.0 }
    return @{
        HingeL = [math]::Round($hingeL, 2)
        FarL   = [math]::Round($farL, 2)
        Depth  = [math]::Round($depth, 4)
        MonoViolations = $violations
        WorstDrop = [math]::Round($worstDrop, 2)
        HfHinge = [math]::Round($hfHinge, 2)
        HfFar   = [math]::Round($hfFar, 2)
        Bins    = ($bins | ForEach-Object { [math]::Round($_, 1) })
    }
}

function Analyze-Raw([string]$path, [double]$p) {
    if (-not (Test-Path $path)) { throw "missing dump: $path" }
    $bytes = [IO.File]::ReadAllBytes($path)
    $size = Get-DumpSize $path
    $w = $size.W; $h = $size.H
    if ($bytes.Length -ne ($w * $h * 4)) {
        throw "dump size mismatch for $path : $($bytes.Length) bytes, expected $($w)x$($h)x4"
    }
    $cx = [int]($w / 2)

    # quad top edge = first row with alpha >= 200 (feather ramps ~4px)
    $quadTop = -1
    for ($y = 0; $y -lt $h; $y++) {
        $off = ($y * $w + $cx) * 4
        if ($bytes[$off + 3] -ge 120) { $quadTop = $y; break }
    }

    $predTop = Predict $p $h
    $result = [ordered]@{
        File = (Split-Path $path -Leaf); Progress = $p
        QuadTopMeasured = $quadTop; QuadTopPredicted = $predTop
        Light = $null; Blur = $null; Pass = $true; Notes = @()
    }

    # quad top comparison. Two special cases at the sweep endpoints:
    #   p=1: at MaxFold=90 the plane is edge-on - the quad collapses to zero
    #        height, no opaque rows (fully folded).
    #   p=0: the M1.8 global fade keeps the fully-open desktop INVISIBLE -
    #        opacity 0 -> no opaque rows either. Both are correct renders.
    if ($p -ge 0.99) {
        if ($quadTop -ne -1) { $result.Pass = $false; $result.Notes += "expected full collapse (no opaque rows), got quadTop=$quadTop" }
        else { $result.Notes += "full fold: quad collapsed (no opaque rows) - expected at MaxFold=90" }
    } elseif ($p -le 0.01) {
        if ($quadTop -ne -1) { $result.Pass = $false; $result.Notes += "expected fade-in invisible (no opaque rows), got quadTop=$quadTop" }
        else { $result.Notes += "fully open: effect faded out (no opaque rows) - expected" }
    } else {
        $d = [math]::Abs($quadTop - $predTop)
        if ($d -gt 5) { $result.Pass = $false; $result.Notes += "quadTop off by $d px" }
    }

    # ---- physics (DD-046): light-falloff direction + blur direction ----
    # Only meaningful while the quad has opaque area (0 < p < 1).
    if ($quadTop -ge 0) {
        $lum = Get-RowProfile $bytes $w $h
        $phy = Measure-Physics $lum $quadTop $h $p
        if ($phy) {
            $result.Light = [ordered]@{
                HingeL = $phy.HingeL; FarL = $phy.FarL; Depth = $phy.Depth
                MonoViolations = $phy.MonoViolations; WorstDrop = $phy.WorstDrop
                Profile = $phy.Bins
            }
            $result.Blur = [ordered]@{
                HingeMaxStep = $phy.HfHinge; FarMaxStep = $phy.HfFar
                Ratio = $(if ($phy.HfFar -gt 0.01) { [math]::Round($phy.HfHinge / $phy.HfFar, 2) } else { 999.0 })
            }

            # The reference ticks must be visible SOMEWHERE, at every
            # progress. If the reference window failed to paint (or was
            # covered), every physics number below becomes meaningless.
            if ($phy.HfHinge -lt 20.0) {
                $result.Pass = $false
                $result.Notes += "reference ticks not visible (max row step $($phy.HfHinge)) - did the reference window paint?"
            } elseif ($p -le $script:BlurStartProgress + 0.01) {
                # Below the blur envelope: no frost, no falloff, everything
                # sharp. A flat profile here is the CORRECT render - this is
                # an assertion too (it catches an envelope that opens too
                # early), just the mirror image of the one below.
                if ($phy.Depth -gt 0.05) {
                    $result.Pass = $false
                    $result.Notes += "falloff present below the blur envelope (p=$p depth=$($phy.Depth))"
                } else {
                    $result.Notes += "below the blur envelope (p <= $($script:BlurStartProgress)): flat + sharp - expected"
                }
            } else {
                # 1) direction: the hinge end must be the bright end.
                if (-not ($phy.FarL -lt $phy.HingeL)) {
                    $result.Pass = $false
                    $result.Notes += "light falloff direction inverted: hinge=$($phy.HingeL) far=$($phy.FarL)"
                }
                # 2) shape: no shadow band in the middle of the ramp.
                if ($phy.MonoViolations -gt 0) {
                    $result.Pass = $false
                    $result.Notes += "light profile not monotone ($($phy.MonoViolations) drops, worst $($phy.WorstDrop))"
                }
                # 3) direction: the hinge must be the sharp end.
                if ($phy.HfHinge -lt 4.0 * $phy.HfFar) {
                    $result.Pass = $false
                    $result.Notes += "blur direction weak: hinge step $($phy.HfHinge) vs far step $($phy.HfFar)"
                }
            }
        } else {
            $result.Notes += "quad too short for the physics profile ($($h - $quadTop) rows)"
        }
    }
    return $result
}

if ($AnalyzeOnly) {
    $tags = @("up0","up1","up2","up3","up4","down1","down2","down3","down4")
    $ps   = @(0.0,0.25,0.5,0.75,1.0,0.75,0.5,0.25,0.0)
    $all = @()
    for ($i = 0; $i -lt 9; $i++) {
        $all += Analyze-Raw "$selftestDir\selftest-$($tags[$i]).raw" $ps[$i]
    }
    # hysteresis: up vs down at the same p, measured on the metric this
    # script actually measures (quadTop) plus the falloff depth.
    $pairs = @(,@(1,7)) + @(,@(2,6)) + @(,@(3,5))
    $hysteresisOk = $true
    foreach ($pair in $pairs) {
        $a = $all[$pair[0]]; $b = $all[$pair[1]]
        $ma = $a.QuadTopMeasured; $mb = $b.QuadTopMeasured
        if ([math]::Abs($ma - $mb) -gt 2) {
            $hysteresisOk = $false
            Write-Host "HYSTERESIS: p=$($a.Progress) quadTop up=$ma down=$mb"
        }
        if ($a.Light -and $b.Light -and [math]::Abs($a.Light.Depth - $b.Light.Depth) -gt 0.05) {
            $hysteresisOk = $false
            Write-Host "HYSTERESIS: p=$($a.Progress) falloffDepth up=$($a.Light.Depth) down=$($b.Light.Depth)"
        }
    }

    # ---- cross-progress: the falloff must DEEPEN as the fold closes ----
    # depth(p) = 1 - farL/hingeL, sampled only ABOVE the blur envelope
    # (BlurOptions.StartProgress = 0.30) - below it the model has no frost
    # at all, so p=0.25 is legitimately flat and cannot be part of a
    # "growing" series.
    # Reference expectation at qualityScale=1:
    #   p=.50 -> .35   p=.75 -> .85     (measured 2026-10-22, 1914x1074)
    # Note the depth is computed from the mean over the top/bottom two bins,
    # so it is the depth of the band AROUND sy=0.9 / sy=0.1, not of the exact
    # edge (which is feathered). It is therefore a few percent below the
    # value at sy=1; that is fine - this is a direction/shape check.
    # If the frame budget degrades the blur (qualityScale=0.5) the radii
    # halve: roughly .20 / .55 - still strictly growing, so the assertion is
    # deliberately about ORDER and a loose floor, not about exact values.
    $physicsOk = $true
    $d50 = if ($all[2].Light) { $all[2].Light.Depth } else { $null }
    $d75 = if ($all[3].Light) { $all[3].Light.Depth } else { $null }
    if ($null -eq $d50 -or $null -eq $d75) {
        $physicsOk = $false
        Write-Host "PHYSICS: falloff depth missing at one or more up-sweep steps"
    } else {
        if (-not ($d75 -gt $d50 + 0.05)) { $physicsOk = $false; Write-Host "PHYSICS: depth(.75)=$d75 not above depth(.50)=$d50" }
        if ($d75 -lt 0.20)               { $physicsOk = $false; Write-Host "PHYSICS: depth(.75)=$d75 below the 0.20 floor" }
    }

    foreach ($r in $all) {
        $flag = if ($r.Pass) { "PASS" } else { "FAIL" }
        Write-Host ("{0} p={1} quadTop={2}/{3} -> {4} {5}" -f $r.File, $r.Progress, $r.QuadTopMeasured, $r.QuadTopPredicted, $flag, ($r.Notes -join '; '))
        if ($r.Light) {
            Write-Host ("     light hinge={0} far={1} depth={2} drops={3} | blur hingeStep={4} farStep={5}" -f `
                $r.Light.HingeL, $r.Light.FarL, $r.Light.Depth, $r.Light.MonoViolations, $r.Blur.HingeMaxStep, $r.Blur.FarMaxStep)
        }
        if (-not $r.Pass) { $r | ConvertTo-Json -Depth 6 | Write-Host }
    }
    Write-Host "HYSTERESIS OK: $hysteresisOk"
    Write-Host "PHYSICS OK: $physicsOk"

    $overall = ($all | Where-Object { -not $_.Pass }).Count -eq 0 -and $hysteresisOk -and $physicsOk
    [ordered]@{
        GeneratedAt  = (Get-Date).ToString("s")
        Steps        = $all
        HysteresisOk = $hysteresisOk
        PhysicsOk    = $physicsOk
        Overall      = $overall
    } | ConvertTo-Json -Depth 7 | Out-File "$root\warp-selftest-result.json" -Encoding utf8
    Write-Host "SELFTEST OVERALL: $overall"
    if (-not $overall) { exit 1 }
    exit 0
}

# ---------------------------------------------------------------------
# Run: reference window -> app --warp-selftest -> wait -> kill -> analyze
# ---------------------------------------------------------------------
$bands = New-Object System.Windows.Forms.Form
$bands.FormBorderStyle = 'None'
$bands.StartPosition = 'Manual'
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bands.SetBounds($bounds.X, $bounds.Y, $bounds.Width, $bounds.Height)
$bands.TopMost = $true
$bands.ShowInTaskbar = $false
$bands.add_Paint({
    param($s, $e)
    $g = $e.Graphics
    $cw = $s.ClientSize.Width; $ch = $s.ClientSize.Height
    # Uniform field: luminance(y) == 255 * light(y), so the falloff profile
    # is readable directly (a coloured reference cannot do this).
    $g.FillRectangle([System.Drawing.Brushes]::White, 0, 0, $cw, $ch)
    # Tick lines: the high-frequency probe for the blur direction. Sharp at
    # the hinge, dissolved into the frost at the far edge.
    for ($y = 0; $y -lt $ch; $y += $script:TickSpacing) {
        $g.FillRectangle([System.Drawing.Brushes]::Black, 0, $y, $cw, $script:TickThickness)
    }
})
$bands.Show()
[System.Windows.Forms.Application]::DoEvents()
[void][W]::SetCursorPos(5, 5)   # cursor away from the analyzed column
Start-Sleep -Milliseconds 500

# This run's trace starts at the CURRENT end of the log. We never delete the
# trace or the old dumps: some shells guard deletes (safe-delete fail-closed),
# and a timestamp/offset check is strictly better evidence than a successful
# delete anyway.
$script:traceOffset = 0
if (Test-Path $trace) { $script:traceOffset = (Get-Item -LiteralPath $trace).Length }
$script:runStartedAt = Get-Date
Write-Host "trace offset: $($script:traceOffset)"

Start-Process -FilePath $exe -ArgumentList "--warp-selftest"
Start-Sleep -Seconds 10
# Move the console OFF-SCREEN during the sweep: its dark surface would
# otherwise pollute the capture. The selftest drives the engine directly -
# the console is not needed on screen.
$consoleHwnd = (Get-Process DuoFlow.App -ErrorAction SilentlyContinue | Select-Object -First 1).MainWindowHandle
if ($consoleHwnd -ne [IntPtr]::Zero) {
    [void][W]::SetWindowPos($consoleHwnd, [IntPtr]::Zero, -2500, -2500, 0, 0, 0x15)
    Write-Host "console moved off-screen"
}
Write-Host "app launched (--warp-selftest); waiting for 'selftest: DONE' in trace..."

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$done = $false
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    $content = Read-TraceTail
    if ($content -match "selftest: DONE") { $done = $true; break }
    if ($content -match "selftest: FAILED") { break }
}
$proc = Get-Process DuoFlow.App -ErrorAction SilentlyContinue
if ($proc) { Stop-Process -Name DuoFlow.App -Force }
$bands.Close()
Write-Host "selftest done=$done (this run's trace below)"
$tail = Read-TraceTail
if ($tail) { ($tail -split "`r?`n") | Select-String "warp|selftest|capture renderer" | ForEach-Object { Write-Host $_.Line } }
if (-not $done) { Write-Error "selftest did not complete - see trace above"; exit 1 }

# Reject stale dumps: a step that failed to dump would otherwise be analyzed
# against the previous run's file and silently "pass".
$tags = @("up0","up1","up2","up3","up4","down1","down2","down3","down4")
$stale = @()
foreach ($t in $tags) {
    $f = Join-Path $selftestDir "selftest-$t.raw"
    if (-not (Test-Path $f)) { $stale += "$t (missing)" }
    elseif ((Get-Item -LiteralPath $f).LastWriteTime -lt $script:runStartedAt) { $stale += "$t (stale)" }
}
if ($stale.Count -gt 0) {
    Write-Error "dumps not produced by this run: $($stale -join ', ')"
    exit 1
}

& $MyInvocation.MyCommand.Path -AnalyzeOnly -RepoRoot $repo -OutputDir $root -Exe $exe
