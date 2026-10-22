# =====================================================================
# DuoFlow - one-click build + launch (developer loop).  [scripts/]
#
# Builds DuoFlow.App for x64 and starts it. -Platform x64 is NOT optional:
# WindowsAppSDKSelfContained refuses to resolve without it and the build
# fails with "requires a supported Windows architecture".
#
# USAGE (Windows PowerShell 5.1, no admin):
#   .\scripts\run.ps1                  # build + launch
#   .\scripts\run.ps1 -NoBuild         # launch the existing binary
#   .\scripts\run.ps1 -Smoke           # build + launch with --overlay-smoke
#                                      #   (writes %USERPROFILE%\duoflow-overlay-smoke.json)
#   .\scripts\run.ps1 -SelfTest        # build + launch with --warp-selftest
#                                      #   (needs scripts/acceptance/warp-selftest.ps1
#                                      #    to paint the reference window first)
#   .\scripts\run.ps1 -Configuration Release
#
# NOTES
#  - Do not use Start-Process to detach and return immediately: the console
#    window IS the app's control surface and the overlay lives as long as it
#    does. This script waits, so Ctrl+C in this window ends the session.
#  - Closing the console window takes the overlay down with it (M1 fix).
# =====================================================================

param(
    [switch]$NoBuild,
    [switch]$Smoke,
    [switch]$SelfTest,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"

if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path }
Set-Location $RepoRoot

$tfm = "net8.0-windows10.0.22621.0"
$exe = Join-Path $RepoRoot "src\DuoFlow.App\bin\x64\$Configuration\$tfm\DuoFlow.App.exe"

if (-not $NoBuild) {
    Write-Host "==> dotnet build src/DuoFlow.App -p:Platform=x64 -c $Configuration"
    & dotnet build "src/DuoFlow.App" -p:Platform=x64 -c $Configuration -v:m
    if ($LASTEXITCODE -ne 0) {
        Write-Error "build failed (exit $LASTEXITCODE)"
        exit 1
    }
}

if (-not (Test-Path $exe)) {
    Write-Error "not found: $exe`nBuild it first: .\scripts\run.ps1"
    exit 1
}

$appArgs = @()
if ($Smoke)    { $appArgs += "--overlay-smoke" }
if ($SelfTest) { $appArgs += "--warp-selftest" }

Write-Host "==> $exe $($appArgs -join ' ')"
$proc = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent) -ArgumentList $appArgs -PassThru
Write-Host "pid $($proc.Id) - close the console window to stop (it takes the overlay with it)"
$proc.WaitForExit()
Write-Host "==> exited with code $($proc.ExitCode)"
