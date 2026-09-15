# =====================================================================
# DuoFlow — Windows 11 development environment setup (M0.1)
# =====================================================================
# Usage (on the Windows 11 dev machine, PowerShell as Administrator):
#   Set-ExecutionPolicy -Scope Process Bypass -Force
#   .\scripts\setup-windows.ps1
#
# What it does:
#   1. Verify Windows 11 + PowerShell 5.1+
#   2. Ensure .NET 8 SDK (winget)
#   3. Ensure Visual Studio 2022 Build Tools + managed desktop workload (winget)
#      (full VS 2022 Community is also fine — see notes at the bottom)
#   4. Install Windows App SDK WinUI 3 project templates (dotnet new)
#   5. Scaffold the minimal WinUI 3 project at src/DuoFlow.App
#   6. Build it to verify the toolchain (Hello World level)
#
# After it finishes, continue with M0.1 checklist in EXECUTION_PLAN.md.
# =====================================================================

$ErrorActionPreference = "Stop"

function Write-Step($msg)  { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "    [OK] $msg"  -ForegroundColor Green }
function Write-Warn2($msg) { Write-Host "    [!] $msg"  -ForegroundColor Yellow }

# ---------------------------------------------------------------- 1. OS check
Write-Step "Checking Windows version"
$os = Get-CimInstance Win32_OperatingSystem
Write-Host "    $($os.Caption) | Build $($os.BuildNumber)"
if ($os.BuildNumber -lt 22000) {
    Write-Warn2 "Windows 11 not detected (build < 22000). WinUI 3 requires Windows 10 1809+ but this project targets Windows 11."
}
Write-Ok "OS check done"

# ---------------------------------------------------------------- 2. .NET 8 SDK
Write-Step "Checking .NET SDK"
$sdks = @(dotnet --list-sdks 2>$null)
if ($sdks | Where-Object { $_ -like "8.*" }) {
    Write-Ok ".NET 8 SDK already installed"
} else {
    Write-Warn2 ".NET 8 SDK not found, installing via winget..."
    winget install Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) { throw "winget failed to install .NET 8 SDK. Install manually: https://dotnet.microsoft.com/download/dotnet/8.0" }
    Write-Ok ".NET 8 SDK installed (you may need to reopen the terminal)"
}

# ---------------------------------------------------------------- 3. VS Build Tools
Write-Step "Checking Visual Studio 2022 / Build Tools"
$vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsInstalled = $false
if (Test-Path $vsWhere) {
    $vs = & $vsWhere -latest -products * -requires Microsoft.VisualStudio.Workload.ManagedDesktop -property installationPath
    if ($vs) { $vsInstalled = $true; Write-Ok "Visual Studio with ManagedDesktop workload found: $vs" }
}
if (-not $vsInstalled) {
    Write-Warn2 "VS 2022 (Build Tools or Community) with the managed desktop workload was not detected."
    Write-Host  "    Installing VS 2022 Build Tools via winget (this can take a while)..."
    winget install Microsoft.VisualStudio.2022.BuildTools --accept-package-agreements --accept-source-agreements `
        --override "--quiet --wait --add Microsoft.VisualStudio.Workload.ManagedDesktop --includeRecommended"
    if ($LASTEXITCODE -ne 0) {
        Write-Warn2 "winget install failed or was skipped. Manual path:"
        Write-Host  "    - Install 'Visual Studio 2022 Community' or 'Build Tools for VS 2022'"
        Write-Host  "    - Tick workload: '.NET desktop development'"
        Write-Host  "    - Tick component: 'Windows App SDK C# templates'"
    } else {
        Write-Ok "VS 2022 Build Tools installed"
    }
}

# ---------------------------------------------------------------- 4. WinUI 3 templates
Write-Step "Installing Windows App SDK (WinUI 3) dotnet templates"
dotnet new install Microsoft.WindowsAppSDK.Templates
if ($LASTEXITCODE -eq 0) { Write-Ok "Templates installed (dotnet new winuiapp)" }
else { Write-Warn2 "Template install failed. In Visual Studio, use the 'Blank App, Packaged (WinUI 3 in Desktop)' template instead." }

# ---------------------------------------------------------------- 5. Scaffold minimal project
Write-Step "Scaffolding minimal WinUI 3 project at src/DuoFlow.App"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projDir  = Join-Path $repoRoot "src\DuoFlow.App"
if (Test-Path $projDir) {
    Write-Warn2 "$projDir already exists, skipping scaffold."
} else {
    New-Item -ItemType Directory -Force -Path (Join-Path $repoRoot "src") | Out-Null
    Push-Location (Join-Path $repoRoot "src")
    try {
        dotnet new winuiapp -n DuoFlow.App -o DuoFlow.App
        if ($LASTEXITCODE -ne 0) { throw "dotnet new winuiapp failed. Use the Visual Studio template 'Blank App, Packaged (WinUI 3 in Desktop)' into src/DuoFlow.App." }
        Write-Ok "Created src/DuoFlow.App"
    } finally { Pop-Location }
}

# ---------------------------------------------------------------- 6. Verify build
Write-Step "Building src/DuoFlow.App (toolchain verification)"
Push-Location (Join-Path $repoRoot "src\DuoFlow.App")
try {
    dotnet build -c Debug
    if ($LASTEXITCODE -eq 0) { Write-Ok "Build succeeded - M0.1 toolchain is ready." }
    else { Write-Warn2 "Build failed. Check the Windows App SDK version and VS workload, see EXECUTION_PLAN.md section 6." }
} finally { Pop-Location }

# ---------------------------------------------------------------- Notes
Write-Host ""
Write-Host "Next steps (M0.1 checklist in EXECUTION_PLAN.md):" -ForegroundColor Magenta
Write-Host "  1. Launch the app (dotnet run or F5 in VS) and confirm the window shows."
Write-Host "  2. Tick M0.1 items in EXECUTION_PLAN.md section 4, update the snapshot (section 2),"
Write-Host "     and append a progress log entry (section 5)."
Write-Host "  3. Commit: 'phase(M0.1): dev environment verified + minimal WinUI 3 project'."
