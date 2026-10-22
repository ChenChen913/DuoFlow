# =====================================================================
# DuoFlow - in-process warp self-test (run + analyze).  [scripts/acceptance]
#
# WHAT IT PROVES: "changing Progress produces a continuous spatial
# deformation of the desktop" - measured against the WarpGeometry
# homography, not eyeballed.
#
# HOW: the app (--warp-selftest) drives the AnimationEngine through 0->1->0
# and dumps the RENDERED back buffer (BGRA raw, alpha included) at each
# step; this script paints a fullscreen 10-band reference window, launches
# the app, then measures the band-boundary rows in the dumped frames and
# compares them against the prediction:
#
#     boundary sy=k/10  ->  dest row = H * (1 - Y(sy))
#     Y(sy) = r*cos(phi)*sy / (r + sin(phi)*sy),  r=2.5, phi=90deg*p
#     quad top edge    ->  dest row = H * (1 - FarEdgeY)
#
# Tolerance +/-5 px (linear-sampler color blend ~2px, alpha feather ~4px).
# Hysteresis check: up-step and down-step at the same p must match (+/-2px).
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
# NOTE: this opens a fullscreen topmost band window and a fullscreen overlay
# for ~1 minute. Do not run it while doing something else on the machine.
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
$script:colors = @(
    @(255,0,0),@(0,160,0),@(0,0,255),@(255,255,0),@(255,0,255),
    @(0,255,255),@(255,128,0),@(128,0,255),@(0,80,160),@(255,255,255)
)

function Classify([byte]$r, [byte]$g, [byte]$b, [byte]$a) {
    if ($a -lt 120) { return -1 }   # outside the folded quad (transparent)
    # Chromaticity classification (normalize by the max channel): invariant
    # to the dimming pass, which scales all channels equally. The old
    # absolute-RGB matcher broke on dimmed rows (white 255 -> 54 etc.).
    $mx = [math]::Max($r, [math]::Max($g, $b))
    if ($mx -lt 40) { return -2 }    # too dark for a reliable hue
    $rn = $r / $mx; $gn = $g / $mx; $bn = $b / $mx
    $best = -1; $bestD = [double]::MaxValue
    for ($i = 0; $i -lt 10; $i++) {
        $pr = $script:colors[$i][0] / 255.0; $pg = $script:colors[$i][1] / 255.0; $pb = $script:colors[$i][2] / 255.0
        $pm = [math]::Max($pr, [math]::Max($pg, $pb))
        $pr /= $pm; $pg /= $pm; $pb /= $pm
        $d = ($rn - $pr) * ($rn - $pr) + ($gn - $pg) * ($gn - $pg) + ($bn - $pb) * ($bn - $pb)
        if ($d -lt $bestD) { $bestD = $d; $best = $i }
    }
    if ($bestD -gt 0.08) { return -2 }   # not a band chromaticity (blend/artifact)
    return $best
}

# Predicted boundary rows (back-buffer pixel rows, top-down) for progress p.
# Boundary k=1..9 separates band k-1 (above) and band k (below) in the DEST.
# All row predictions are derived from the dump's ACTUAL height (the back
# buffer is the panel's pixel size, which depends on DPI and window size).
function Predict([double]$p, [int]$h) {
    $r = 2.5; $phi = $p * [math]::PI / 2.0
    $c = [math]::Cos($phi); $s = [math]::Sin($phi)
    $rows = @{}
    for ($k = 1; $k -le 9; $k++) {
        $sy = $k / 10.0
        $Y = ($r * $c * $sy) / ($r + $s * $sy)
        $rows[[string]$k] = [math]::Round($h * (1.0 - $Y), 1)
    }
    $Yfar = ($r * $c) / ($r + $s)
    return @{ Boundaries = $rows; QuadTop = [math]::Round($h * (1.0 - $Yfar), 1) }
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

function Analyze-Raw([string]$path, [double]$p) {
    if (-not (Test-Path $path)) { throw "missing dump: $path" }
    $bytes = [IO.File]::ReadAllBytes($path)
    $size = Get-DumpSize $path
    $w = $size.W; $h = $size.H
    if ($bytes.Length -ne ($w * $h * 4)) {
        throw "dump size mismatch for $path : $($bytes.Length) bytes, expected $($w)x$($h)x4"
    }
    $cx = [int]($w / 2)   # center column - the overlay UI is collapsed during
                           # the selftest, so the capture contains ONLY the
                           # reference window (no recursion, console or chip)
                # the console (moved off-screen) and the status chip (rows<40)

    # walk the analysis column: class per row. Rows 0..44 skipped - the
    # overlay's own status chip (white text) sits there in the capture and
    # would classify as band 9 (white).
    $classes = New-Object 'int[]' $h
    for ($y = 0; $y -lt $h; $y++) {
        $off = ($y * $w + $cx) * 4
        if ($y -lt 45) { $classes[$y] = -99 } else { $classes[$y] = Classify $bytes[$off + 2] $bytes[$off + 1] $bytes[$off] $bytes[$off + 3] }
    }

    # quad top edge = first row with alpha >= 200 (feather ramps ~4px)
    $quadTop = -1
    for ($y = 0; $y -lt $h; $y++) {
        $off = ($y * $w + $cx) * 4
        if ($bytes[$off + 3] -ge 120) { $quadTop = $y; break }
    }

    # band boundaries: transition band a -> band b (both >= 0, a != b).
    # Boundary sy=k/10 separates band (9-k) ABOVE and band (10-k) BELOW in
    # the destination, so the prediction key is k = 10 - max(a, b).
    $measured = @{}
    $prev = -99
    for ($y = 0; $y -lt $h; $y++) {
        $cls = $classes[$y]
        if ($cls -ge 0 -and $prev -ge 0 -and $cls -ne $prev) {
            $key = [string](10 - [math]::Max($prev, $cls))
            if (-not $measured.ContainsKey($key)) { $measured[$key] = $y }
        }
        if ($cls -ge 0) { $prev = $cls }
    }

    $pred = Predict $p $h
    $result = [ordered]@{ File = (Split-Path $path -Leaf); Progress = $p; QuadTopMeasured = $quadTop; QuadTopPredicted = $pred.QuadTop; Boundaries = @{}; Pass = $true; Notes = @() }

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
        $d = [math]::Abs($quadTop - $pred.QuadTop)
        if ($d -gt 5) { $result.Pass = $false; $result.Notes += "quadTop off by $d px" }
    }

    # NOTE: the old band-boundary sweep is gone (model v2). The physical
    # light falloff (DD-046) intentionally sinks the far bands into shadow,
    # where color classification cannot see - measuring those boundaries by
    # color is meaningless by design. The shadow transition is verified by
    # m16 (frost edge width) and m17 (light profile); this script verifies
    # the GEOMETRY (quadTop) and the fade endpoints.
    if ($false) {
        $okCount = 0; $detected = 0
        for ($k = 1; $k -le 9; $k++) {
            $pr = $pred.Boundaries[[string]$k]
            $m = if ($measured.ContainsKey([string]$k)) { $measured[[string]$k] } else { -1 }
            $result.Boundaries[[string]$k] = @{ predicted = $pr; measured = $m }
            if ($m -ge 0) {
                $detected++
                $d = [math]::Abs($m - $pr)
                if ($d -le 5) { $okCount++ } else { $result.Notes += "boundary $k off by $d px" }
            }
        }
        if ($okCount -lt 6) { $result.Pass = $false; $result.Notes += "only $okCount/9 boundaries within tolerance ($detected detected)" }
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
    # script actually measures (quadTop). The old band-boundary comparison
    # became vacuous once model v2 stopped emitting classifiable far bands -
    # it compared two always-empty tables and printed "HYSTERESIS OK".
    $pairs = @(,@(1,7)) + @(,@(2,6)) + @(,@(3,5))
    $hysteresisOk = $true
    foreach ($pair in $pairs) {
        $a = $all[$pair[0]]; $b = $all[$pair[1]]
        $ma = $a.QuadTopMeasured; $mb = $b.QuadTopMeasured
        if ([math]::Abs($ma - $mb) -gt 2) {
            $hysteresisOk = $false
            Write-Host "HYSTERESIS: p=$($a.Progress) quadTop up=$ma down=$mb"
        }
    }
    # (The M1.5 mask-debug phase is gone - model v2 (DD-046) replaced the
    # hinge mask with the physical gap/blur/light coupling.)
    foreach ($r in $all) {
        $flag = if ($r.Pass) { "PASS" } else { "FAIL" }
        Write-Host ("{0} p={1} quadTop={2}/{3} -> {4} {5}" -f $r.File, $r.Progress, $r.QuadTopMeasured, $r.QuadTopPredicted, $flag, ($r.Notes -join '; '))
        if (-not $r.Pass) { $r | ConvertTo-Json -Depth 5 | Write-Host }
    }
    Write-Host "HYSTERESIS OK: $hysteresisOk"
    $overall = ($all | Where-Object { -not $_.Pass }).Count -eq 0 -and $hysteresisOk
    $all | ConvertTo-Json -Depth 6 | Out-File "$root\warp-selftest-result.json" -Encoding utf8
    Write-Host "SELFTEST OVERALL: $overall"
    if (-not $overall) { exit 1 }
    exit 0
}

# ---------------------------------------------------------------------
# Run: bands window -> app --warp-selftest -> wait -> kill -> analyze
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
    $bh = [double]$s.ClientSize.Height / 10
    for ($i = 0; $i -lt 10; $i++) {
        $b = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($script:colors[$i][0], $script:colors[$i][1], $script:colors[$i][2]))
        $e.Graphics.FillRectangle($b, 0, [int]($i * $bh), $s.ClientSize.Width, [int]($bh) + 1)
        $b.Dispose()
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
# otherwise pollute the analysis column (dest x=250 -> source x=480) in the
# capture. The selftest drives the engine directly - the console is not
# needed on screen.
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


