# =====================================================================
# DuoFlow - Hardware probe (M0.4 Hardware Research)
# =====================================================================
# Read this first: docs/SETUP_WINDOWS.md section 7 (how to run, what
# you should see, troubleshooting). Script comments/outputs are in
# English on purpose: Windows PowerShell 5.1 mis-parses UTF-8 without
# BOM, and this script must run cleanly on ANY Windows machine.
#
# WHAT IT DOES (all read-only, nothing is uploaded anywhere):
#   Section 0  System basics          OS / manufacturer / model / CPU
#   Section 1  Windows Sensor API     WinRT: HingeAngleSensor,
#                                      Accelerometer, Gyrometer,
#                                      Inclinometer, SimpleOrientation,
#                                      LightSensor (incl. one sync read)
#   Section 2  HID / PnP sensors      Sensor-class + HID sensor devices
#   Section 3  ACPI layer             lid device (PNP0C0D), ACPI devices,
#                                      sleep button (PNP0C0E)
#   Section 4  Vendor interfaces      Lenovo / HP / Dell / ASUS WMI
#                                      namespaces + vendor PnP devices
#   Section 5  Cameras                PnP camera inventory (M3 ground work)
#   Section 6  GPU / display          Win32_VideoController (M1.4 ground work)
#
# OUTPUT:
#   - Human-readable console report (green = present, yellow = absent)
#   - hardware-probe-result.json  <- paste this file back to the AI Agent,
#     it is used to fill docs/HARDWARE_COMPATIBILITY.md section 5.
#
# REQUIREMENTS:
#   - Windows PowerShell 5.1  (C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe)
#     -> REQUIRED for the Sensor API section (WinRT projection was removed
#        from .NET Core, so pwsh 7 cannot query WinRT sensors).
#     If you run this under pwsh 7, sections 0/2/3/4/5/6 still work and
#     section 1 is reported as skipped - rerun with powershell.exe for it.
#   - No admin needed, but running as admin may reveal a few extra devices.
# =====================================================================

param(
    # Where to write hardware-probe-result.json (default: current directory)
    [string]$OutputDir = (Get-Location).Path
)

$ErrorActionPreference = "Continue"   # sections are isolated, keep going

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    [FOUND] $msg"   -ForegroundColor Green }
function Write-No($msg)   { Write-Host "    [NOT FOUND] $msg" -ForegroundColor Yellow }
function Write-Err2($msg) { Write-Host "    [ERROR] $msg"    -ForegroundColor Red }

$script:Result = [ordered]@{
    meta = [ordered]@{
        script    = "hardware-probe.ps1"
        runAt     = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss K")
        computer  = $env:COMPUTERNAME
        psEdition = $PSVersionTable.PSEdition
        psVersion = $PSVersionTable.PSVersion.ToString()
        isAdmin   = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
                     ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        osIsWin11 = $false
    }
    system   = $null
    sensorApi= [ordered]@{ available = $false; note = $null; sensors = [ordered]@{} }
    hid      = $null
    acpi     = $null
    vendor   = $null
    camera   = $null
    gpu      = $null
    errors   = @()
}

function Add-SectionError($section, $message) {
    $script:Result.errors += [ordered]@{ section = $section; message = "$message" }
    Write-Err2 "$section : $message"
}

# =====================================================================
# Section 0 - System basics
# =====================================================================
Write-Step "Section 0: System basics"
try {
    $os  = Get-CimInstance Win32_OperatingSystem
    $cs  = Get-CimInstance Win32_ComputerSystem
    $bio = Get-CimInstance Win32_BIOS
    $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1

    $script:Result.meta.osIsWin11 = ([int]$os.BuildNumber -ge 22000)

    $script:Result.system = [ordered]@{
        osCaption   = "$($os.Caption)"
        osBuild     = "$($os.Version) (build $($os.BuildNumber))"
        manufacturer= "$($cs.Manufacturer)"
        model       = "$($cs.Model)"
        systemFamily= "$($cs.SystemFamily)"
        biosVersion = "$($bio.SMBIOSBIOSVersion)"
        cpu         = "$($cpu.Name)"
        logicalCores= "$($cpu.NumberOfLogicalProcessors)"
    }
    Write-Host "    OS          : $($os.Caption) (build $($os.BuildNumber))"
    Write-Host "    Machine     : $($cs.Manufacturer) $($cs.Model)"
    Write-Host "    CPU         : $($cpu.Name)"
} catch { Add-SectionError "system" $_.Exception.Message }

# =====================================================================
# Section 1 - Windows Sensor API (WinRT)  [needs Windows PowerShell 5.1]
# =====================================================================
Write-Step "Section 1: Windows Sensor API (WinRT)"

if ($PSVersionTable.PSEdition -eq "Core") {
    $script:Result.sensorApi.note =
        "SKIPPED: pwsh 7 (.NET Core) cannot project WinRT sensor APIs. " +
        "Rerun this script with Windows PowerShell 5.1 (powershell.exe) to fill this section."
    Write-No "WinRT projection unavailable under pwsh 7 - section skipped"
} else {
    try {
        # System.Runtime.WindowsRuntime provides the AsTask bridge for WinRT IAsyncOperation.
        Add-Type -AssemblyName System.Runtime.WindowsRuntime -ErrorAction Stop
    } catch {
        Add-SectionError "sensorApi" "Cannot load System.Runtime.WindowsRuntime: $($_.Exception.Message)"
    }

    # Await a WinRT IAsyncOperation<T> from PowerShell 5.1.
    function Await-WinRt([object]$AsyncOp, [type]$ResultType) {
        $asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() |
            Where-Object { $_.Name -eq "AsTask" -and
                           $_.GetParameters().Count -eq 1 -and
                           $_.GetParameters()[0].ParameterType.Name -eq "IAsyncOperation``1" })[0]
        if (-not $asTaskGeneric) { throw "AsTask<T> bridge method not found" }
        $asTask  = $asTaskGeneric.MakeGenericMethod($ResultType)
        $netTask = $asTask.Invoke($null, @($AsyncOp))
        if (-not $netTask.Wait(15000)) { throw "WinRT async operation timed out (15s)" }
        return $netTask.Result
    }

    # Probe one WinRT sensor class.
    # IMPORTANT (learned on CI, Windows PowerShell 5.1):
    #   Calling a WinRT static method through a [Type] variable
    #   ($rtType::GetDefaultAsync()) binds unreliably - it either throws
    #   "does not contain a method named 'GetDefaultAsync'" or silently
    #   returns $null. The reliable pattern is the TYPE LITERAL direct
    #   static call, so both $OpFn and $TypeFn below embed the full
    #   literal: [Ns.Type, Ns, ContentType = WindowsRuntime]::Method()
    function Test-WinRtSensor {
        param([string]$Key, [scriptblock]$OpFn, [scriptblock]$TypeFn, [scriptblock]$ReadFn, [scriptblock]$DescFn)
        try {
            $rtType = & $TypeFn    # literal type (loads the projection)
            $op = & $OpFn          # literal static call -> IAsyncOperation

            if ($null -eq $op) {
                # Static call silently returned null: this is a binding
                # anomaly, NOT proof that the hardware is absent.
                $script:Result.sensorApi.sensors[$Key] = [ordered]@{
                    supported = $false
                    note = "binding anomaly: GetDefaultAsync() returned null op (treat as Unknown)"
                }
                Write-No "$Key : null op (binding anomaly, treat as Unknown)"
                return
            }

            $sensor = $null
            $sensor = Await-WinRt $op $rtType

            if ($null -eq $sensor) {
                $script:Result.sensorApi.sensors[$Key] = [ordered]@{
                    supported = $false
                    note = "API available; GetDefaultAsync() completed but no default sensor is present"
                }
                Write-No "$Key : API OK, no default sensor present"
                return
            }

            $rec = [ordered]@{ supported = $true; deviceId = "$($sensor.DeviceId)" }
            $reading = $null
            try { $reading = & $ReadFn $sensor } catch { $rec.readingError = $_.Exception.Message }

            if ($reading) {
                $rec.hasReading = $true
                $rec.sample     = "$(& $DescFn $reading)"
            } else {
                $rec.hasReading = $false
            }
            $script:Result.sensorApi.sensors[$Key] = $rec
            Write-Ok "$Key supported$(if ($rec.sample) { ' | ' + $rec.sample })"
        } catch {
            $err = $_.Exception.Message
            $note = $null
            if ($err -match "does not contain a method named") {
                # CI finding: on windows-latest (Windows SERVER 2025) the sensor
                # projections are incomplete - static methods are missing from
                # the projected types. On desktop Windows 11 the same literal
                # call is the standard, community-proven pattern. So on the
                # real machine this usually means the API is really absent.
                $note = "static method missing on projected type; on Windows Server this is a known SKU projection gap - on desktop Win11 treat this result as definitive"
            }
            $script:Result.sensorApi.sensors[$Key] = [ordered]@{
                supported = $false
                error = $err
                note  = $note
            }
            Add-SectionError "sensorApi/$Key" $_.Exception.Message
        }
    }

    # NOTE: the ", Windows.Devices.Sensors, ContentType = WindowsRuntime"
    # qualifier is PowerShell-specific projection syntax (5.1) and must stay
    # inside the type literals - it is NOT valid for [Type]::GetType().
    Test-WinRtSensor "HingeAngleSensor" `
        { [Windows.Devices.Sensors.HingeAngleSensor, Windows.Devices.Sensors, ContentType = WindowsRuntime]::GetDefaultAsync() } `
        { [Windows.Devices.Sensors.HingeAngleSensor, Windows.Devices.Sensors, ContentType = WindowsRuntime] } `
        { param($s) $s.GetCurrentReading() } `
        { param($r) "angle=$($r.AngleInDegrees) deg" }

    Test-WinRtSensor "Accelerometer" `
        { [Windows.Devices.Sensors.Accelerometer, Windows.Devices.Sensors, ContentType = WindowsRuntime]::GetDefaultAsync() } `
        { [Windows.Devices.Sensors.Accelerometer, Windows.Devices.Sensors, ContentType = WindowsRuntime] } `
        { param($s) $s.GetCurrentReading() } `
        { param($r) "acc=($($r.AccelerationX), $($r.AccelerationY), $($r.AccelerationZ)) g" }

    Test-WinRtSensor "Gyrometer" `
        { [Windows.Devices.Sensors.Gyrometer, Windows.Devices.Sensors, ContentType = WindowsRuntime]::GetDefaultAsync() } `
        { [Windows.Devices.Sensors.Gyrometer, Windows.Devices.Sensors, ContentType = WindowsRuntime] } `
        { param($s) $s.GetCurrentReading() } `
        { param($r) "gyro=($($r.AngularVelocityX), $($r.AngularVelocityY), $($r.AngularVelocityZ)) deg/s" }

    Test-WinRtSensor "Inclinometer" `
        { [Windows.Devices.Sensors.Inclinometer, Windows.Devices.Sensors, ContentType = WindowsRuntime]::GetDefaultAsync() } `
        { [Windows.Devices.Sensors.Inclinometer, Windows.Devices.Sensors, ContentType = WindowsRuntime] } `
        { param($s) $s.GetCurrentReading() } `
        { param($r) "pitch=$($r.PitchDegrees), roll=$($r.RollDegrees), yaw=$($r.YawDegrees)" }

    Test-WinRtSensor "SimpleOrientationSensor" `
        { [Windows.Devices.Sensors.SimpleOrientationSensor, Windows.Devices.Sensors, ContentType = WindowsRuntime]::GetDefaultAsync() } `
        { [Windows.Devices.Sensors.SimpleOrientationSensor, Windows.Devices.Sensors, ContentType = WindowsRuntime] } `
        { param($s) $s.GetCurrentOrientation() } `
        { param($r) "orientation=$($r.Orientation)" }

    Test-WinRtSensor "LightSensor" `
        { [Windows.Devices.Sensors.LightSensor, Windows.Devices.Sensors, ContentType = WindowsRuntime]::GetDefaultAsync() } `
        { [Windows.Devices.Sensors.LightSensor, Windows.Devices.Sensors, ContentType = WindowsRuntime] } `
        { param($s) $s.GetCurrentReading() } `
        { param($r) "lux=$($r.IlluminanceInLux)" }

    if ($script:Result.sensorApi.sensors.Count -gt 0) {
        $script:Result.sensorApi.available = $true
    }
}

# =====================================================================
# Section 2 - HID / PnP sensor devices
# =====================================================================
Write-Step "Section 2: HID / PnP sensor devices"
try {
    $sensorClass = @(Get-PnpDevice -PresentOnly -Class Sensor -ErrorAction SilentlyContinue)
    $hidSensor   = @(Get-PnpDevice -PresentOnly -Class HIDClass -ErrorAction SilentlyContinue |
                     Where-Object { $_.FriendlyName -match "sensor|orientation|accel|gyro|incline|hinge" })

    $mapFn = { param($d) [ordered]@{
        friendlyName = "$($d.FriendlyName)"
        instanceId   = "$($d.InstanceId)"
        status       = "$($d.Status)"
    } }

    $script:Result.hid = [ordered]@{
        sensorClassDeviceCount = $sensorClass.Count
        sensorClassDevices     = @($sensorClass | ForEach-Object $mapFn)
        hidSensorDevices       = @($hidSensor   | ForEach-Object $mapFn)
    }

    if ($sensorClass.Count -gt 0) {
        Write-Ok "$($sensorClass.Count) device(s) in PnP class 'Sensor':"
        $sensorClass | ForEach-Object { Write-Host "      - $($_.FriendlyName)  [$($_.InstanceId)]" }
    } else { Write-No "No PnP 'Sensor' class devices" }
    if ($hidSensor.Count -gt 0) {
        Write-Ok "$($hidSensor.Count) HID device(s) with sensor-related friendly name"
    } else { Write-No "No HID devices with sensor-related names" }
} catch { Add-SectionError "hid" $_.Exception.Message }

# =====================================================================
# Section 3 - ACPI layer (lid / sleep-button / vendor ACPI devices)
# =====================================================================
Write-Step "Section 3: ACPI layer"
try {
    $acpiDevices = @(Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
                     Where-Object { $_.InstanceId -like "ACPI\*" })

    # PNP0C0D = lid device, PNP0C0E = sleep button (classic ACPI hardware IDs)
    $lid = @($acpiDevices | Where-Object { $_.InstanceId -match "ACPI\\PNP0C0D" })
    $slp = @($acpiDevices | Where-Object { $_.InstanceId -match "ACPI\\PNP0C0E" })
    $vendorAcpi = @($acpiDevices | Where-Object {
        $_.InstanceId -match "ACPI\\VEN_(LNO|LEN|AMDI|HPQ|ASUS)" -or $_.InstanceId -match "ACPI\\(LEN|LNO|ATK|HPQ|ASUS)" })

    $mapFn3 = { param($d) [ordered]@{
        friendlyName = "$($d.FriendlyName)"
        instanceId   = "$($d.InstanceId)"
        status       = "$($d.Status)"
    } }

    $script:Result.acpi = [ordered]@{
        acpiDeviceCount   = $acpiDevices.Count
        lidDevice         = @($lid | ForEach-Object $mapFn3)
        sleepButtonDevice = @($slp | ForEach-Object $mapFn3)
        vendorAcpiDevices = @($vendorAcpi | ForEach-Object $mapFn3)
    }

    if ($lid.Count -gt 0)    { Write-Ok "ACPI lid device present: $($lid[0].FriendlyName) [$($lid[0].InstanceId)]" }
    else                     { Write-No "ACPI lid device (PNP0C0D) not present" }
    if ($slp.Count -gt 0)    { Write-Ok "ACPI sleep button present" }
    else                     { Write-No "ACPI sleep button (PNP0C0E) not present" }
    Write-Host "    Total present ACPI devices: $($acpiDevices.Count)"
    if ($vendorAcpi.Count -gt 0) {
        $vendorAcpi | Select-Object -First 8 | ForEach-Object { Write-Host "      - $($_.FriendlyName)  [$($_.InstanceId)]" }
    }
} catch { Add-SectionError "acpi" $_.Exception.Message }

# =====================================================================
# Section 4 - Vendor WMI interfaces (Lenovo / HP / Dell / ASUS)
# =====================================================================
Write-Step "Section 4: Vendor WMI interfaces"
try {
    $vendorClasses = @(Get-CimClass -Namespace root/wmi -ErrorAction Stop |
        Where-Object { $_.CimClassName -match "Lenovo|ThinkPad|ThinkVantage|Asus|ATK|Hp|Dell" } |
        Select-Object -ExpandProperty CimClassName)

    $nsProbe = [ordered]@{}
    foreach ($ns in @("root/HP/InstrumentedBIOS", "root/dcim/sysman", "root/Lenovo")) {
        try {
            $null = Get-CimClass -Namespace $ns -ErrorAction Stop
            $nsProbe[$ns] = $true
            Write-Ok "WMI namespace present: $ns"
        } catch {
            $nsProbe[$ns] = $false
        }
    }

    $machine = "$($script:Result.system.manufacturer) $($script:Result.system.model)".ToLower()
    $detectedVendor = "unknown"
    if ($machine -match "lenovo|thinkpad|ideapad|yoga|legion")      { $detectedVendor = "LENOVO" }
    elseif ($machine -match "dell")                                 { $detectedVendor = "DELL" }
    elseif ($machine -match "hp|pavilion|victus|omen")              { $detectedVendor = "HP" }
    elseif ($machine -match "asus|vivobook|zenbook|tuf|rog")        { $detectedVendor = "ASUS" }

    $script:Result.vendor = [ordered]@{
        detectedVendor    = $detectedVendor
        rootWmiVendorClassCount = $vendorClasses.Count
        rootWmiVendorClasses    = @($vendorClasses | Select-Object -First 60)
        namespaces         = $nsProbe
    }

    if ($vendorClasses.Count -gt 0) {
        Write-Ok "$($vendorClasses.Count) vendor-related class(es) in root/wmi (first 10):"
        $vendorClasses | Select-Object -First 10 | ForEach-Object { Write-Host "      - $_" }
    } else { Write-No "No Lenovo/HP/Dell/ASUS classes found in root/wmi" }
    if ($nsProbe.Values -notcontains $true) { Write-No "No vendor-specific WMI namespaces" }
} catch { Add-SectionError "vendor" $_.Exception.Message }

# =====================================================================
# Section 5 - Camera inventory (ground work for M3)
# =====================================================================
Write-Step "Section 5: Camera inventory"
try {
    $cams = @(Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue |
              Where-Object { $_.Class -in @("Camera", "Image") })

    $mapFn5 = { param($d) [ordered]@{
        friendlyName = "$($d.FriendlyName)"
        class        = "$($d.Class)"
        instanceId   = "$($d.InstanceId)"
        status       = "$($d.Status)"
    } }

    $script:Result.camera = @($cams | ForEach-Object $mapFn5)
    if ($cams.Count -gt 0) {
        Write-Ok "$($cams.Count) camera/imaging device(s):"
        $cams | ForEach-Object { Write-Host "      - $($_.FriendlyName)  [$($_.Class)]" }
    } else { Write-No "No camera devices found" }
} catch { Add-SectionError "camera" $_.Exception.Message }

# =====================================================================
# Section 6 - GPU / display (ground work for M1.4 D3D11)
# =====================================================================
Write-Step "Section 6: GPU / display"
try {
    $gpus = @(Get-CimInstance Win32_VideoController)
    $script:Result.gpu = @($gpus | ForEach-Object {
        [ordered]@{
            name         = "$($_.Name)"
            adapterRamMb = [math]::Round($_.AdapterRAM / 1MB)   # note: caps at 4095 on WMI for >4GB cards
            currentMode  = if ($_.CurrentHorizontalResolution) {
                               "$($_.CurrentHorizontalResolution)x$($_.CurrentVerticalResolution)@$($_.CurrentRefreshRate)Hz"
                           } else { "n/a" }
            driverVer    = "$($_.DriverVersion)"
            status       = "$($_.Status)"
        }
    })
    $gpus | ForEach-Object {
        Write-Ok "GPU: $($_.Name) | $($_.DriverVersion)"
    }
} catch { Add-SectionError "gpu" $_.Exception.Message }

# =====================================================================
# Write JSON + final summary
# =====================================================================
Write-Step "Writing result JSON"
try {
    $jsonPath = Join-Path $OutputDir "hardware-probe-result.json"
    $script:Result | ConvertTo-Json -Depth 6 | Out-File -FilePath $jsonPath -Encoding utf8 -Force
    Write-Ok "JSON written: $jsonPath"
} catch { Add-SectionError "json" $_.Exception.Message }

Write-Step "SUMMARY"
$errCount = $script:Result.errors.Count
Write-Host ("    Sections executed: 7 | errors: {0}" -f $errCount) -ForegroundColor $(if ($errCount -gt 0) { "Yellow" } else { "Green" })
Write-Host "    NEXT STEP: send hardware-probe-result.json back to the AI Agent."
Write-Host "    It will be used to fill the measured-device row in"
Write-Host "    docs/HARDWARE_COMPATIBILITY.md (section 5) - nothing was uploaded."
