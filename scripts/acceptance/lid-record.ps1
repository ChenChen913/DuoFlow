# =====================================================================
# DuoFlow 合盖采样（M3.3）
#
# 用途：在你**实际合上/打开盖子**的时候，把摄像头测到的进度序列录下来。
#       之前的探针都是静置的，只能证明"管线跑通"，证明不了"跟着盖子走"。
#
# 用法：
#   .\scripts\acceptance\lid-record.ps1
#   .\scripts\acceptance\lid-record.ps1 -OutputDir D:\tmp\lid
#   .\scripts\acceptance\lid-record.ps1 -WaitSeconds 300 -RecordSeconds 25
#
# 产出：lid\lid-series.jsonl          ← 每帧一行，逐行落盘（睡眠也不丢）
#       lid\lid-series-summary.json   ← 汇总；若机器睡过去导致进程被杀，这个文件可能没有
#       lid-record.txt                ← 本次运行的文字记录
#
# 退出码：0 = 采到样本且无异常；1 = 没采到 / 出错。
#
# 重要：**动作触发**，不是定时。
#   脚本启动后会一直等，直到画面真的开始变化（占比或近平面顶边移动超过阈值），
#   然后从那一刻起录 N 秒，并带上之前 3 秒的预录。
#   所以你没有时间压力——什么时候开始合盖都行。
#
# 注意：
#   1. 完全合上会让 Windows 关屏并通常进入睡眠，进程可能被挂起或杀掉。
#      数据是逐行 flush 的，所以**睡着之前的部分一定在盘上**。
#      想避开睡眠：合到"还差一条缝"就停，不要扣到底。
#   2. 采样期间不要移动笔记本、不要有人从镜头前走过——那都会被算进占比里。
# =====================================================================

param(
    [string]$RepoRoot = "",
    [string]$OutputDir = "",
    [string]$Exe = "",
    [int]$WaitSeconds = 240,
    [int]$RecordSeconds = 20
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path }
if (-not $OutputDir) { $OutputDir = Join-Path $env:TEMP "duoflow-lid-record" }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$log = Join-Path $OutputDir "lid-record.txt"
Remove-Item $log -ErrorAction SilentlyContinue

# Write-Host "" 在 Windows PowerShell 5.1 下什么都不输出（实测 A/""/B 打成 AB），
# 所以空行必须写成一个空格。
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

$lidDir = Join-Path $OutputDir "lid"
New-Item -ItemType Directory -Force -Path $lidDir | Out-Null
$jsonlPath = Join-Path $lidDir "lid-series.jsonl"
$summaryPath = Join-Path $lidDir "lid-series-summary.json"
Remove-Item $jsonlPath -ErrorAction SilentlyContinue
Remove-Item $summaryPath -ErrorAction SilentlyContinue

Log "DuoFlow 合盖采样（M3.3）"
Log ("  exe    : " + $Exe)
Log ("  output : " + $OutputDir)
Log " "
Log "  脚本现在开始等，**没有时间压力**——画面一动它才开始录。"
Log " "
Log "  请这样做："
Log " "
Log "    1) 慢慢合盖 —— 大约 5 秒合到「还差一条缝」，不要扣到底"
Log "    2) 停 2 秒"
Log "    3) 慢慢打开 —— 大约 5 秒回到正常角度"
Log "    4) 之后保持不动，等它自己结束"
Log " "
Log "  为什么不要扣到底：完全合上会关屏并可能让机器睡眠，"
Log "  那样采样进程会被挂起，尾部数据就断了。"
Log " "
Log "  已写入的样本是逐行落盘的，中途睡着也不会丢。"
Log ("  最长等待 " + $WaitSeconds + " 秒，触发后录 " + $RecordSeconds + " 秒（含 3 秒预录）。")
Log " "

Log "  [等待中] 请开始合盖 ..."
$proc = Start-Process -FilePath $Exe -ArgumentList ("--lid-record=" + $lidDir) -PassThru -WindowStyle Hidden
$timeoutMs = ($WaitSeconds + $RecordSeconds + 40) * 1000
$timedOut = -not $proc.WaitForExit($timeoutMs)
if ($timedOut) {
    try { $proc.Kill() } catch { }
}
$exitCode = if ($timedOut) { -1 } else { $proc.ExitCode }
Log ("  [结束] exit=" + $exitCode + $(if ($timedOut) { "（超时）" } else { "" }))
Log " "

# ---- 读结果 ---------------------------------------------------------
$summary = $null
if (Test-Path $summaryPath) {
    try { $summary = Get-Content $summaryPath -Raw | ConvertFrom-Json } catch { $summary = $null }
}

# 机器睡过去时进程会被杀掉，summary 就不会生成。这时直接从 JSONL 里取。
$samples = @()
if (Test-Path $jsonlPath) {
    $samples = @(Get-Content $jsonlPath | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object {
        try { $_ | ConvertFrom-Json } catch { $null }
    } | Where-Object { $null -ne $_ })
}

$triggered = $null -ne $summary -and $null -ne $summary.TriggerAtSeconds

if ($null -ne $summary) {
    if ($triggered) {
        Log ("  触发时刻      : t=" + $summary.TriggerAtSeconds + "s（画面开始变化）")
    } else {
        Log "  [!] 没有检测到画面变化 —— 采样窗口内盖子可能没动。"
    }
    Log "  样本数        : $($summary.Samples)（录了 $($summary.DurationSeconds) 秒）"
    Log "  进度范围      : $($summary.ProgressMin) .. $($summary.ProgressMax)（首 $($summary.ProgressFirst) / 末 $($summary.ProgressLast)）"
    Log "  占比范围      : $($summary.CoverageMin) .. $($summary.CoverageMax)"
    Log "  有测量的样本  : $($summary.MeasuredSamples)（占比 $($summary.MeasuredRate)）"
    Log "  触顶 / 填满   : $($summary.TouchesTopSamples) / $($summary.FillsFrameSamples)"
    Log "  前进 / 回退步 : $($summary.ForwardSteps) / $($summary.BackwardSteps)（最大 $($summary.MaxForwardStep) / $($summary.MaxBackwardStep)）"
    $pr = ($summary.ProgressReasons.PSObject.Properties | ForEach-Object { $_.Name + "=" + $_.Value }) -join " "
    $dr = ($summary.DeckReasons.PSObject.Properties | ForEach-Object { $_.Name + "=" + $_.Value }) -join " "
    Log ("  进度原因      : " + $pr)
    Log ("  占比原因      : " + $dr)
    Log " "
    Log ("  进度曲线      : |" + $summary.Sparkline + "|")
    Log " "
    Log "  （曲线：空格=0，@=1；每格取该段峰值）"
    if (@($summary.Failures).Count -gt 0) {
        Log ("  失败项        : " + (@($summary.Failures) -join " | "))
    }
} elseif ($samples.Count -gt 0) {
    $last = $samples[-1]
    $prog = ($samples | ForEach-Object { [double]$_.progress })
    $pmin = ($prog | Measure-Object -Minimum).Minimum
    $pmax = ($prog | Measure-Object -Maximum).Maximum
    Log "  [!] 没有 summary —— 进程很可能在采样途中被睡眠杀掉了。"
    Log "      但 JSONL 里有 $($samples.Count) 个样本，数据没丢。"
    Log "  进度范围      : $pmin .. $pmax"
    Log "  最后一条      : t=$($last.t) progress=$($last.progress) reason=$($last.reason) deck=$($last.deck)"
} else {
    Log "  [!] 没有采到任何样本。"
}

$ok = $false
if ($null -ne $summary) {
    $ok = $triggered -and ([int]$summary.Samples -ge 10) -and (@($summary.Failures).Count -eq 0)
} elseif ($samples.Count -gt 0) {
    # 睡过去导致没有 summary，但数据在——这不算失败。
    $ok = $samples.Count -ge 10
}

Log " "
Log ("OVERALL = " + $(if ($ok) { "PASS" } else { "FAIL" }))
if (-not $ok) { exit 1 }
exit 0
