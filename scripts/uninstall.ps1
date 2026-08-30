<#
.SYNOPSIS
    Uninstalls MaxwellBoost from Windows Startup and stops the application.
#>
[CmdletBinding()]
param()

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "         Uninstalling MaxwellBoost                " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Stop running processes
$running = Get-Process -Name "MaxwellBoost" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping MaxwellBoost process..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Write-Host "[OK] Process stopped." -ForegroundColor Green
}

# 2. Remove Startup Registry Key
$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$appName = "MaxwellBoost"

if (Get-ItemProperty -Path $regPath -Name $appName -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $regPath -Name $appName -Force
    Write-Host "[OK] Removed $appName from Windows Startup." -ForegroundColor Green
} else {
    Write-Host "[INFO] $appName was not found in Windows Startup registry." -ForegroundColor Gray
}

Write-Host "`n[SUCCESS] MaxwellBoost uninstalled successfully." -ForegroundColor Green
