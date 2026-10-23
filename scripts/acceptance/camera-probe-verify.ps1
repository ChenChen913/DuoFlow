# =====================================================================
# DuoFlow - camera probe acceptance (M3.1) + screen detection (M3.2)
#
# WHAT IT PROVES: this machine can enumerate a camera, open it, negotiate a
# format, receive real decoded frames, and classify health - and that the
# probe leaks no pixels while doing it. With -Detect it also proves the
# screen detector decides on every frame and never throws.
#
#   1.  exit code      the probe reports its own verdict, not just "it ran"
#   2.  枚举           at least one capture device is visible
#   3.  选择           a device is chosen deterministically
#   4.  协商格式       the OPENED device has W/H > 0 and a pixel format -
#                      enumeration reports 0 here on purpose (see below)
#   5.  收帧           FramesReceived >= requested, FramesOffered >= received
#   6.  速率           按"谁是瓶颈"分两种断言（见下方注释）
#   7.  缓冲区结构     AlphaMean = 255 (proves a real NV12->BGRA conversion)
#   8.  健康分类       Health = None on a healthy machine
#   9.  隐私           no pixel-shaped field, no sample array, small JSON
#  10.  检测（-Detect）决定/不抛异常/四边形归一化/不落像素
#
# WHY 5/6 ARE SPLIT: FramesOffered is what the CAMERA delivered before our
# governor; FramesReceived is what we kept. They look identical from outside
# ("few frames arrived") but have opposite fixes. A dim room drops a webcam
# to ~5 fps by lengthening exposure - that is the camera, not a bug, and
# dropping frames because of it would send someone hunting in the wrong file.
#
# WHY CONTRAST IS NOT A GATE: whether the picture has contrast depends on the
# room, not on the code. A locked screen with the lens capped gives a
# perfectly uniform frame from a perfectly working camera (measured on the
# dev laptop: every byte = 15, alpha = 255, 4.9 fps). Gating on it would make
# this script fail for a reason it cannot fix. Pass -RequireContrast when the
# light is on and the shutter is open, to additionally prove a LIVE PICTURE.
#
# WHAT THE NUMBERS LOOK LIKE WHEN IT IS DARK (dev laptop, 2026-10-23):
#   FramesOffered 32 / Received 30 / Dropped 2, CameraFps 4.88,
#   Luminance 15.00 flat, ContrastDelta 0, AlphaMean 255, ByteMin=ByteMax=15.
#   15 is the NV12 limited-range black floor (Y=16) after conversion.
#
# USAGE (Windows PowerShell 5.1, no admin):
#   .\scripts\acceptance\camera-probe-verify.ps1
#   .\scripts\acceptance\camera-probe-verify.ps1 -RequireContrast
#   .\scripts\acceptance\camera-probe-verify.ps1 -Detect              # + M3.2
#   .\scripts\acceptance\camera-probe-verify.ps1 -Lid                 # + M3.3
#   .\scripts\acceptance\camera-probe-verify.ps1 -Detect -Lid -RequireContrast
#   .\scripts\acceptance\camera-probe-verify.ps1 -OutputDir D:\tmp\cam
#
# OUTPUT: camera-probe-verify.txt + probe\camera-probe-result.json
#         (+ detect\camera-detect-result.json when -Detect)
#         (+ lid\camera-lid-result.json when -Lid)
# EXIT CODE: 0 = all checks passed, 1 = at least one did not.
# NOTE: headless - opens no window and does not touch the screen.
#       ~10 s without options, ~20 s with -Detect, ~30 s with both.
# =====================================================================

param(
    [string]$RepoRoot = "",
    [string]$OutputDir = "",
    [string]$Exe = "",
    [int]$TimeoutSeconds = 90,
    [switch]$RequireContrast,
    [switch]$Detect,
    [switch]$Lid
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path }
if (-not $OutputDir) { $OutputDir = Join-Path $env:TEMP "duoflow-camera-probe" }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$log = Join-Path $OutputDir "camera-probe-verify.txt"
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
    Write-Host "ERROR: DuoFlow.App.exe not found - build it first:"
    Write-Host "  cd src\DuoFlow.App; dotnet build -p:Platform=x64"
    exit 1
}

Log "DuoFlow camera probe acceptance (M3.1)"
Log ("  exe        : " + $Exe)
Log ("  output     : " + $OutputDir)
$contrastMode = if ($RequireContrast) { "REQUIRED (light on, shutter open)" } else { "informational" }
# The frame budget and the target rate are the PROBE's, and are read back from
# its report. Passing them in here would be a lie: the probe takes them from
# MediaCaptureCamera.DefaultTargetFps and a fixed 30-frame budget, so a script
# parameter would appear to control them while changing nothing.
Log ("  contrast   : " + $contrastMode)
Log ""

$script:steps = @()
function Check([string]$name, [bool]$pass, [string]$detail) {
    $script:steps += [ordered]@{ Name = $name; Pass = $pass; Detail = $detail }
    $tag = if ($pass) { "PASS" } else { "FAIL" }
    Log ("  [{0}] {1} - {2}" -f $tag, $name, $detail)
}

# ---------------------------------------------------------------------
# 1. Run the probe. It is headless: no window, no screen takeover.
# ---------------------------------------------------------------------
$probeDir = Join-Path $OutputDir "probe"
New-Item -ItemType Directory -Force -Path $probeDir | Out-Null
$probeArg = "--camera-probe=" + $probeDir

Log "1. running probe"
$clock = [Diagnostics.Stopwatch]::StartNew()
$proc = Start-Process -FilePath $Exe -ArgumentList $probeArg -PassThru -WindowStyle Hidden
$timedOut = -not $proc.WaitForExit($TimeoutSeconds * 1000)
$clock.Stop()
if ($timedOut) {
    # A probe that hangs is a failure mode in itself (driver stuck behind a
    # privacy prompt); kill it so the script can still report.
    try { $proc.Kill() } catch { }
    Log ("  probe did not exit within " + $TimeoutSeconds + " s - killed")
}
$exitCode = if ($timedOut) { -1 } else { $proc.ExitCode }
Log ("  exit code " + $exitCode + " in " + ("{0:0.0}" -f $clock.Elapsed.TotalSeconds) + " s")

Check "probe exits 0" ($exitCode -eq 0) ("exit=" + $exitCode)

# ---------------------------------------------------------------------
# 2. Read the report.
# ---------------------------------------------------------------------
$jsonPath = Join-Path $probeDir "camera-probe-result.json"
$j = $null
if (Test-Path $jsonPath) {
    try { $j = Get-Content $jsonPath -Raw | ConvertFrom-Json } catch { $j = $null }
}
Check "result JSON written" ($null -ne $j) ($(if ($j) { $jsonPath } else { "missing or unparseable: " + $jsonPath }))

if ($null -eq $j) {
    Log ""
    Log "OVERALL = FAIL (no report to check)"
    [ordered]@{
        GeneratedAt = (Get-Date).ToString("s")
        Exe         = $Exe
        Steps       = $script:steps
        Overall     = $false
    } | ConvertTo-Json -Depth 6 | Out-File (Join-Path $OutputDir "camera-probe-result-summary.json") -Encoding utf8
    exit 1
}

# ---------------------------------------------------------------------
# 3. Devices and format.
# ---------------------------------------------------------------------
$devices = @($j.Devices)
Check "at least one camera enumerated" ($devices.Count -ge 1) ("devices=" + $devices.Count)
if ($devices.Count -gt 0) {
    $names = @($devices | ForEach-Object { "[" + $_.Enclosure + "] " + $_.Name })
    Log ("  devices: " + ($names -join "; "))
}

Check "a device was chosen" ($null -ne $j.ChosenDevice) ("chosen=" + $(if ($j.ChosenDevice) { $j.ChosenDevice.Name } else { "(none)" }))

$opened = $j.OpenedDevice
$fmtOk = $false
$fmtText = "(not opened)"
if ($null -ne $opened) {
    $fmtOk = ($opened.Width -gt 0) -and ($opened.Height -gt 0) -and (-not [string]::IsNullOrWhiteSpace($opened.PixelFormat))
    $fmtText = "" + $opened.Width + "x" + $opened.Height + " @" + $opened.FrameRate + "fps " + $opened.PixelFormat
}
# Enumeration reports 0x0 on purpose: the real size is a property of the
# negotiated source, and reporting the advertised one would make the trace lie.
Check "opened with a negotiated format" $fmtOk $fmtText

# ---------------------------------------------------------------------
# 4. Frames: received vs offered, and the two rates.
# ---------------------------------------------------------------------
$received = [int]$j.FramesReceived
$offered  = [int]$j.FramesOffered
$dropped  = [int]$j.FramesDropped
$requested = [int]$j.RequestedFrames
$targetFps = [double]$j.TargetFps
$measured = [double]$j.MeasuredFps
$cameraFps = [double]$j.CameraFps

# RequestedFrames is the probe's own contract, read from the report rather
# than passed in: a script parameter that the probe ignores would look like it
# controlled the frame budget while doing nothing (it did, briefly).
Check "probe declared a frame budget" ($requested -gt 0) ("requested=" + $requested + " target=" + $targetFps + "/s")
Check "received the requested frames" ($received -ge $requested) ("received=" + $received + " requested=" + $requested)
# Offered must be >= received: the governor can only ever remove frames.
Check "offered >= received" ($offered -ge $received) ("offered=" + $offered + " received=" + $received + " dropped=" + $dropped)
Check "measured rate is positive" ($measured -gt 0) ("measured=" + ("{0:0.00}" -f $measured) + "/s")
Check "camera-side rate is positive" ($cameraFps -gt 0) ("camera=" + ("{0:0.00}" -f $cameraFps) + "/s")

# MeasuredFps is (frames-1)/span over the frames we KEPT; CameraFps is over
# what the camera OFFERED. They can only differ by our own dropping - so the
# gap means opposite things depending on who is the limit:
#
#   camera slower than target -> we must track it (a gap means we are losing
#                                frames we wanted, which is a bug)
#   camera faster than target -> dropping is the governor DOING ITS JOB, and
#                                the right assertion is that we kept ~target
#
# Asserting "gap <= 35%" unconditionally was wrong: it failed on a perfectly
# healthy 30 fps camera throttled to 10. Measured, not theorised.
if ($cameraFps -le $targetFps * 1.05) {
    $rateGap = if ($cameraFps -gt 0) { [Math]::Abs($measured - $cameraFps) / $cameraFps } else { 1.0 }
    Check "measured rate tracks the camera" ($rateGap -le 0.35) ("gap=" + ("{0:0.0}" -f ($rateGap * 100)) + "% (camera is the limit, so we must keep what it gives)")
} else {
    $targetGap = if ($targetFps -gt 0) { [Math]::Abs($measured - $targetFps) / $targetFps } else { 1.0 }
    Check "measured rate tracks the target" ($targetGap -le 0.30) ("measured=" + ("{0:0.00}" -f $measured) + "/s target=" + ("{0:0.00}" -f $targetFps) + "/s gap=" + ("{0:0.0}" -f ($targetGap * 100)) + "%")
    Check "governor drops the surplus" ($dropped -gt 0) ("offered=" + $offered + " kept=" + $received + " dropped=" + $dropped)
}

# ---------------------------------------------------------------------
# 5. Buffer structure - the load-bearing check.
#    NV12 carries no alpha, so a correct BGRA conversion synthesises 255.
#    Anything else means we are reading bytes that are not BGRA, and every
#    statistic above would be meaningless.
# ---------------------------------------------------------------------
$alpha = [double]$j.AlphaMean
$byteMin = [int]$j.ByteMin
$byteMax = [int]$j.ByteMax

Check "buffer is BGRA8 (alpha 255)" ($alpha -ge 250) ("alpha mean=" + ("{0:0.0}" -f $alpha))
Check "buffer is not all zeros" ($byteMax -gt 0) ("byte range=" + $byteMin + ".." + $byteMax)

# ---------------------------------------------------------------------
# 6. Health.
# ---------------------------------------------------------------------
$health = [string]$j.Health
Check "health classified as None" ($health -eq "None") ("health=" + $health + " hint=" + $j.HealthHint)

# ---------------------------------------------------------------------
# 7. Contrast: informational by default, gated on request.
# ---------------------------------------------------------------------
$sceneFlat = [bool]$j.SceneFlat
$contrastDelta = [double]$j.ContrastDelta
if ($RequireContrast) {
    Check "scene has contrast (live picture)" (-not $sceneFlat) ("contrast delta=" + ("{0:0.000000}" -f $contrastDelta) + " lum=" + $j.LuminanceMean)
} else {
    Log ("  [INFO] scene flat=" + $sceneFlat + " contrast delta=" + ("{0:0.000000}" -f $contrastDelta) + " lum=" + $j.LuminanceMean)
    if ($sceneFlat) {
        Log "         uniform frame: lens capped, privacy shutter closed, or a dark room."
        Log "         The pipeline is fine - rerun with -RequireContrast once there is light."
    }
}

# ---------------------------------------------------------------------
# 8. Privacy: no pixel ever leaves. The probe sets StoredPixels=false; this
#    also proves no field NAME hints at image data and that nothing grew into
#    a sample array. Cheap, and it is the one rule that must never regress.
# ---------------------------------------------------------------------
Check "StoredPixels is false" ($j.StoredPixels -eq $false) ("StoredPixels=" + $j.StoredPixels)

$leaky = @($j.PSObject.Properties | Where-Object { $_.Name -match '(?i)pixel' -and $_.Name -ne 'StoredPixels' })
Check "no pixel-shaped field" ($leaky.Count -eq 0) ($(if ($leaky.Count -eq 0) { "no key matches /pixel/ except StoredPixels" } else { "leaky keys: " + (($leaky | ForEach-Object { $_.Name }) -join ", ") }))

# A per-frame series would be an array of numbers; nothing legitimate here is.
$series = @($j.PSObject.Properties | Where-Object { $_.Value -is [array] -and @($_.Value).Count -gt 8 -and @($_.Value)[0] -is [double] })
Check "no per-frame sample arrays" ($series.Count -eq 0) ($(if ($series.Count -eq 0) { "report is scalars only" } else { "arrays: " + (($series | ForEach-Object { $_.Name }) -join ", ") }))

$jsonKb = [Math]::Round((Get-Item $jsonPath).Length / 1024.0, 1)
Check "report stays small" ($jsonKb -lt 64) ($jsonKb.ToString() + " KB (< 64 KB cannot hold a frame)")

# ---------------------------------------------------------------------
# 9. M3.2 screen detection (optional, -Detect).
#
#    What is gated: the detector DECIDES on every frame and never throws.
#    What is NOT gated: that a screen was found. On a capped lens or a dark
#    room there is nothing to find, and a detector that invented a quad there
#    would be the bug. Detection quality is reported instead.
# ---------------------------------------------------------------------
$detectInfo = $null
if ($Detect) {
    Log ""
    Log "9. running screen detection (M3.2)"

    $detectDir = Join-Path $OutputDir "detect"
    New-Item -ItemType Directory -Force -Path $detectDir | Out-Null
    $detectProc = Start-Process -FilePath $Exe -ArgumentList ("--camera-detect=" + $detectDir) -PassThru -WindowStyle Hidden
    $detectTimedOut = -not $detectProc.WaitForExit($TimeoutSeconds * 1000)
    if ($detectTimedOut) {
        try { $detectProc.Kill() } catch { }
    }

    $detectExit = if ($detectTimedOut) { -1 } else { $detectProc.ExitCode }
    Check "detect probe exits 0" ($detectExit -eq 0) ("exit=" + $detectExit)

    $detectJsonPath = Join-Path $detectDir "camera-detect-result.json"
    $d = $null
    if (Test-Path $detectJsonPath) {
        try { $d = Get-Content $detectJsonPath -Raw | ConvertFrom-Json } catch { $d = $null }
    }
    Check "detect report written" ($null -ne $d) ($detectJsonPath)

    if ($null -ne $d) {
        $framesDetected = [int]$d.FramesDetected
        $throws = [int]$d.DetectorThrows
        $usableFrames = [int]$d.UsableFrames
        $bestConfidence = [double]$d.BestConfidence

        Check "detector decided on every frame" ($framesDetected -ge [int]$d.RequestedFrames) ("decided=" + $framesDetected + "/" + $d.RequestedFrames)
        Check "detector never threw" ($throws -eq 0) ("throws=" + $throws + $(if ($d.LastThrow) { " last=" + $d.LastThrow } else { "" }))
        Check "detect report stores no pixels" ($d.StoredPixels -eq $false) ("StoredPixels=" + $d.StoredPixels)

        # A quad is 8 normalized numbers. Anything outside 0..1 means the
        # normalization door leaked a pixel coordinate - the one thing DD-053
        # forbids - so it is worth asserting rather than trusting.
        $quadOk = $true
        $quadText = "(no quad)"
        if ($null -ne $d.BestQuad) {
            $flat = @($d.BestQuad)
            $quadOk = ($flat.Count -eq 8)
            foreach ($v in $flat) {
                if ([double]$v -lt 0.0 -or [double]$v -gt 1.0) { $quadOk = $false }
            }
            $quadText = ($flat -join ",")
        }
        Check "best quad is normalized 0..1" $quadOk $quadText

        Log ("  [INFO] usable frames " + $usableFrames + "/" + $framesDetected +
             "  best confidence " + ("{0:0.000}" -f $bestConfidence) +
             "  reason " + $d.BestReason + "  std " + $d.BestSceneStd)
        $reasonText = ($d.Reasons.PSObject.Properties | ForEach-Object { $_.Name + "=" + $_.Value }) -join " "
        Log ("         reasons: " + $reasonText)

        if ($RequireContrast) {
            Check "a screen is actually detected" ($usableFrames -gt 0) ("usable=" + $usableFrames)
        }

        $detectInfo = [ordered]@{
            FramesDetected = $framesDetected
            DetectorThrows = $throws
            UsableFrames   = $usableFrames
            BestConfidence = $bestConfidence
            BestReason     = $d.BestReason
            BestSceneStd   = $d.BestSceneStd
        }
    }
}

# ---------------------------------------------------------------------
# 10. M3.3 lid progress (optional, -Lid).
#
#     What is gated: the provider STARTS, delivers every state, the vision
#     chain never throws, and a lid that is not moving does not report
#     motion. What is NOT gated: that progress can reach 1 - the probe runs
#     with the laptop sitting still and fully open, so there is nothing to
#     track.
#
#     History worth keeping: the first build of this stage gated nothing on
#     motion and reported 30/30 Ok with progress ramping 0 -> 0.13 while the
#     lid was stationary. It was green and it was wrong. That is why the
#     drift check below is a hard check and not a warning.
# ---------------------------------------------------------------------
$lidInfo = $null
if ($Lid) {
    Log ""
    Log "10. running lid progress (M3.3)"

    $lidDir = Join-Path $OutputDir "lid"
    New-Item -ItemType Directory -Force -Path $lidDir | Out-Null
    $lidProc = Start-Process -FilePath $Exe -ArgumentList ("--camera-lid=" + $lidDir) -PassThru -WindowStyle Hidden
    $lidTimedOut = -not $lidProc.WaitForExit($TimeoutSeconds * 1000)
    if ($lidTimedOut) {
        try { $lidProc.Kill() } catch { }
    }

    $lidExit = if ($lidTimedOut) { -1 } else { $lidProc.ExitCode }
    Check "lid probe exits 0" ($lidExit -eq 0) ("exit=" + $lidExit)

    $lidJsonPath = Join-Path $lidDir "camera-lid-result.json"
    $l = $null
    if (Test-Path $lidJsonPath) {
        try { $l = Get-Content $lidJsonPath -Raw | ConvertFrom-Json } catch { $l = $null }
    }
    Check "lid report written" ($null -ne $l) ($lidJsonPath)

    if ($null -ne $l) {
        $states = [int]$l.States
        $failCount = @($l.Failures).Count
        $progMin = [double]$l.ProgressMin
        $progMax = [double]$l.ProgressMax
        $covMin = [double]$l.CoverageMin
        $covMax = [double]$l.CoverageMax
        $confMax = [double]$l.ConfidenceMax
        $topFrames = [int]$l.TouchesTopFrames
        $fillsFrames = [int]$l.FillsFrameFrames

        Check "provider delivered every state" ($states -ge [int]$l.RequestedFrames) ("states=" + $states + "/" + $l.RequestedFrames)
        Check "vision chain never threw" ($failCount -eq 0) ("failures=" + $failCount + $(if ($l.Error) { " error=" + $l.Error } else { "" }))
        Check "lid report stores no pixels" ($l.StoredPixels -eq $false) ("StoredPixels=" + $l.StoredPixels)
        Check "progress stays in 0..1" (($progMin -ge 0.0) -and ($progMax -le 1.0)) ("progress=" + $progMin + ".." + $progMax)
        Check "coverage stays in 0..1" (($covMin -ge 0.0) -and ($covMax -le 1.0)) ("coverage=" + $covMin + ".." + $covMax)

        # The invariant that catches "a frozen progress leaking out as a
        # measurement": if nothing was measurable, progress must not have moved
        # off its start value. A tracker that reports progress without a
        # measurement is worse than one that reports nothing.
        $noMeasure = ($confMax -le 0.0)
        Check "no measurement means no progress" ((-not $noMeasure) -or ($progMax -le 0.0)) ("confMax=" + $confMax + " progMax=" + $progMax)

        # A STATIONARY lid must not produce a visible fold.
        #
        # Deleting the NoFarBoundary veto (DD-055 §3, third correction) bought
        # back the last third of the travel, and the price is this: with no far
        # boundary in the frame, room brightness drift now reaches progress at
        # full confidence. The machine is sitting still for this whole run, so
        # ANY progress here is drift, not motion.
        #
        # 0.05 is the point below which the fold is not perceptible - it is a
        # statement about what the user can see, not about what the estimator
        # can resolve. M3.4's smoothing is what has to bring this to zero; until
        # it does, this check fails on purpose.
        Check "stationary lid does not report motion" ($progMax -le 0.05) ("progMax=" + $progMax + " (drift - owner: M3.4 smoothing)")

        $deckText = ($l.DeckReasons.PSObject.Properties | ForEach-Object { $_.Name + "=" + $_.Value }) -join " "
        $progText = ($l.ProgressReasons.PSObject.Properties | ForEach-Object { $_.Name + "=" + $_.Value }) -join " "
        Log ("  [INFO] open baseline " + $l.OpenBaseline + " (learned=" + $l.HasLearnedBaseline + ")")
        Log ("         coverage " + $l.CoverageMin + ".." + $l.CoverageMax + " (std " + $l.CoverageStd + ")" +
             "  nearTop " + $l.NearEdgeTopMin + ".." + $l.NearEdgeTopMax)
        Log ("         progress " + $l.ProgressMin + ".." + $l.ProgressMax + " (std " + $l.ProgressStd + ")" +
             "  confidence " + $l.ConfidenceMin + ".." + $l.ConfidenceMax)
        Log ("         deck: " + $deckText + "  |  progress: " + $progText)
        Log ("         touchesTop " + $topFrames + "/" + $states + "  fillsFrame " + $fillsFrames + "/" + $states)

        if ($topFrames -ge $states -and $states -gt 0) {
            # Not a failure: with the lid open the deck is not in the frame, and
            # the measured region is some other surface spanning the full
            # height. TouchesTop reports exactly that. It used to be a veto
            # (NoFarBoundary); it no longer is, because the same signature also
            # appears - with a monotone rising area - through the last third of
            # a real close, and vetoing it threw that away (DD-055 §3).
            Log "         [WARN] every region touched the top edge - the deck is not in view."
            Log "                Expected with the lid fully open, but it also happens mid-close,"
            Log "                so TouchesTop alone no longer means 'nothing to measure'."
        }
        if ($noMeasure) {
            Log "         [WARN] no usable measurement this run - progress is frozen at 0,"
            Log "                which is the honest answer, not a pass for tracking."
        }

        $lidInfo = [ordered]@{
            States           = $states
            Failures         = $failCount
            OpenBaseline     = $l.OpenBaseline
            HasLearnedBaseline = $l.HasLearnedBaseline
            CoverageMin      = $covMin
            CoverageMax      = $covMax
            CoverageStd      = $l.CoverageStd
            ProgressMin      = $progMin
            ProgressMax      = $progMax
            ProgressStd      = $l.ProgressStd
            ConfidenceMax    = $confMax
            TouchesTopFrames = $topFrames
            FillsFrameFrames = $fillsFrames
            DeckReasons      = $deckText
        }
    }
}

# ---------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------
# Wrap in @(): with exactly one match, Where-Object returns the object itself,
# and .Count on an ordered dictionary is the number of KEYS.
$failed = @($script:steps | Where-Object { -not $_.Pass }).Count
$overall = ($failed -eq 0)

Log ""
Log ("  " + ("{0,-38}" -f "measure") + " value")
Log ("  " + ("-" * 38) + " -----")
Log ("  " + ("{0,-38}" -f "target rate") + " " + ("{0:0.00}" -f $targetFps) + " fps")
Log ("  " + ("{0,-38}" -f "camera rate (offered)") + " " + ("{0:0.00}" -f $cameraFps) + " fps")
Log ("  " + ("{0,-38}" -f "measured rate (kept)") + " " + ("{0:0.00}" -f $measured) + " fps")
Log ("  " + ("{0,-38}" -f "frames offered/received/dropped") + " " + $offered + "/" + $received + "/" + $dropped)
Log ("  " + ("{0,-38}" -f "luminance mean") + " " + $j.LuminanceMean)
Log ("  " + ("{0,-38}" -f "alpha mean") + " " + $alpha)
Log ("  " + ("{0,-38}" -f "byte range") + " " + $byteMin + ".." + $byteMax)
Log ""
Log ("OVERALL = " + $(if ($overall) { "PASS (" + $script:steps.Count + "/" + $script:steps.Count + ")" } else { "FAIL (" + $failed + " of " + $script:steps.Count + " failed)" }))

[ordered]@{
    GeneratedAt     = (Get-Date).ToString("s")
    Exe             = $Exe
    RequireContrast = [bool]$RequireContrast
    Detect          = [bool]$Detect
    Lid             = [bool]$Lid
    ExitCode        = $exitCode
    TargetFps       = $targetFps
    CameraFps       = $cameraFps
    MeasuredFps     = $measured
    FramesOffered   = $offered
    FramesReceived  = $received
    FramesDropped   = $dropped
    AlphaMean       = $alpha
    SceneFlat       = $sceneFlat
    ContrastDelta   = $contrastDelta
    Detection       = $detectInfo
    LidProgress     = $lidInfo
    Steps           = $script:steps
    Overall         = $overall
} | ConvertTo-Json -Depth 6 | Out-File (Join-Path $OutputDir "camera-probe-verify-result.json") -Encoding utf8

if (-not $overall) { exit 1 }
exit 0
