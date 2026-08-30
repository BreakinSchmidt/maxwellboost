<#
.SYNOPSIS
    Runs diagnostic test and displays live status and recent logs for MaxwellBoost.
#>
[CmdletBinding()]
param()

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "         MaxwellBoost Diagnostic Test Utility      " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$exePath = "$PSScriptRoot\..\publish\MaxwellBoost.exe"
if (-not (Test-Path $exePath)) {
    $exePath = "$PSScriptRoot\..\src\bin\Release\net8.0-windows\MaxwellBoost.exe"
}

if (-not (Test-Path $exePath)) {
    Write-Host "Building project first..." -ForegroundColor Yellow
    & "$PSScriptRoot\build.ps1"
    $exePath = "$PSScriptRoot\..\publish\MaxwellBoost.exe"
}

Write-Host "`nExecuting diagnosis using: $exePath..." -ForegroundColor Yellow
$process = Start-Process -FilePath $exePath -ArgumentList "--test" -Wait -PassThru -NoNewWindow

Write-Host "`n[Recent Entries in C:\logs\maxwell.log]:" -ForegroundColor Cyan
if (Test-Path "C:\logs\maxwell.log") {
    Get-Content "C:\logs\maxwell.log" -Tail 15 | ForEach-Object {
        if ($_ -match "\[ERROR\]") {
            Write-Host $_ -ForegroundColor Red
        } elseif ($_ -match "\[WARN \]") {
            Write-Host $_ -ForegroundColor Yellow
        } elseif ($_ -match "Connected & Boosted") {
            Write-Host $_ -ForegroundColor Green
        } else {
            Write-Host $_ -ForegroundColor Gray
        }
    }
} else {
    Write-Host "No log file found at C:\logs\maxwell.log" -ForegroundColor Yellow
}

Write-Host "`n[Current Equalizer APO config.txt]:" -ForegroundColor Cyan
if (Test-Path "C:\Program Files\EqualizerAPO\config\config.txt") {
    Get-Content "C:\Program Files\EqualizerAPO\config\config.txt" | ForEach-Object {
        Write-Host "  $_" -ForegroundColor White
    }
}

Write-Host "`n==================================================" -ForegroundColor Cyan
