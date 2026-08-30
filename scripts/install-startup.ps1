<#
.SYNOPSIS
    Installs MaxwellBoost to Windows Startup and launches the System Tray application.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "         Installing MaxwellBoost Startup          " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Build and Publish
& "$PSScriptRoot\build.ps1"

$publishExe = "$PSScriptRoot\..\publish\MaxwellBoost.exe"
if (-not (Test-Path $publishExe)) {
    Write-Error "Published executable not found at $publishExe"
}

$fullExePath = (Resolve-Path $publishExe).Path

# 2. Stop running instances if any
$running = Get-Process -Name "MaxwellBoost" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping running instance of MaxwellBoost..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

# 3. Add to CurrentUser Run Registry Key
$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$appName = "MaxwellBoost"

Set-ItemProperty -Path $regPath -Name $appName -Value "`"$fullExePath`"" -Force
Write-Host "[OK] Added $appName to Windows Startup: $fullExePath" -ForegroundColor Green

# 4. Launch Application (Detached from script runner)
Write-Host "Starting MaxwellBoost System Tray monitor..." -ForegroundColor Cyan
$spawnResult = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = "`"$fullExePath`"" }

Start-Sleep -Seconds 1
$runningCheck = Get-Process -Name "MaxwellBoost" -ErrorAction SilentlyContinue
if ($runningCheck) {
    Write-Host "`n[SUCCESS] MaxwellBoost is now active in your System Tray (PID: $($runningCheck.Id))!" -ForegroundColor Green
    Write-Host "Look for the green microphone icon near your Windows clock." -ForegroundColor Gray
} else {
    Write-Host "`n[WARNING] Process did not stay running. Please check C:\logs\maxwell.log" -ForegroundColor Yellow
}
