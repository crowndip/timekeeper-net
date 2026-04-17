# Parental Control Windows Client - Uninstallation Script
# Run as Administrator

$ErrorActionPreference = "Stop"

Write-Host "=== Parental Control Windows Client Uninstallation ===" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator" -ForegroundColor Red
    exit 1
}

$serviceName = "ParentalControlClient"
$installPath = "C:\Program Files\ParentalControl"
$dataPath = "C:\ProgramData\ParentalControl"

# Stop service
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping service..." -ForegroundColor Yellow
    Stop-Service -Name $serviceName -Force
    Start-Sleep -Seconds 2
    
    Write-Host "Removing service..." -ForegroundColor Yellow
    sc.exe delete $serviceName
}

# Remove binaries
if (Test-Path $installPath) {
    Write-Host "Removing binaries..." -ForegroundColor Yellow
    Remove-Item -Path $installPath -Recurse -Force
}

# Ask about data removal
Write-Host ""
$removeData = Read-Host "Remove configuration and logs? (y/N)"
if ($removeData -eq "y" -or $removeData -eq "Y") {
    if (Test-Path $dataPath) {
        Write-Host "Removing data..." -ForegroundColor Yellow
        Remove-Item -Path $dataPath -Recurse -Force
    }
}

Write-Host ""
Write-Host "=== Uninstallation Complete ===" -ForegroundColor Green
