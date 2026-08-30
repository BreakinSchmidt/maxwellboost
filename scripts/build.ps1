<#
.SYNOPSIS
    Builds and publishes MaxwellBoost for deployment.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "$PSScriptRoot\..\publish"
)

$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "         Building & Publishing MaxwellBoost       " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# Stop running instances to release file locks on publish directory
$running = Get-Process -Name "MaxwellBoost" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping running instance of MaxwellBoost to release file lock..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

$projectPath = "$PSScriptRoot\..\src\MaxwellBoost.csproj"

Write-Host "Publishing MaxwellBoost ($Configuration) to $OutputDir..." -ForegroundColor Yellow
dotnet publish $projectPath -c $Configuration -o $OutputDir --nologo

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n[SUCCESS] Published to: $OutputDir\MaxwellBoost.exe" -ForegroundColor Green
} else {
    Write-Host "`n[ERROR] Build/Publish failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}
