# Parental Control Windows Client - Installation Script
# Run as Administrator

param(
    [string]$ServerUrl = "http://localhost:8080"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Parental Control Windows Client Installation ===" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator" -ForegroundColor Red
    exit 1
}

# Define paths
$installPath = "C:\Program Files\ParentalControl"
$dataPath = "C:\ProgramData\ParentalControl"
$serviceName = "ParentalControlClient"

# Create directories
Write-Host "Creating directories..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $installPath | Out-Null
New-Item -ItemType Directory -Force -Path $dataPath | Out-Null
New-Item -ItemType Directory -Force -Path "$dataPath\Logs" | Out-Null

# Restrict write access to the service (SYSTEM) and admins only, so a local user
# (including the child the service is enforcing limits on) can't tamper with
# server-url.txt, computer-id.txt, api-key.txt, or cache.json to fake their own limits.
# Users keep read access because ParentalControl.TrayIcon.Windows runs in the user's own
# session and reads server-url.txt/computer-id.txt/proxy-user.txt/proxy-pass.txt/
# appsettings.json from this same directory to show remaining time -- api-key.txt and
# cache.json are therefore still user-readable (unchanged from before this restriction),
# just no longer user-writable.
#
# S-1-5-18 = SYSTEM, S-1-5-32-544 = Administrators, S-1-5-32-545 = Users.
# SIDs, not names: group display names are localized (Czech "Administrátoři"/"Uživatelé"),
# and a failed name lookup after /inheritance:r would leave an EMPTY DACL -- a directory
# nobody, including the service, can access. One combined invocation also avoids the
# window where inheritance is stripped but the grants haven't been applied yet.
Write-Host "Restricting data directory permissions..." -ForegroundColor Yellow
icacls $dataPath /inheritance:r /grant "*S-1-5-18:(OI)(CI)F" /grant "*S-1-5-32-544:(OI)(CI)F" /grant "*S-1-5-32-545:(OI)(CI)RX" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "WARNING: could not restrict permissions on $dataPath (icacls exit code $LASTEXITCODE)." -ForegroundColor Red
    Write-Host "The service will still work, but a local user may be able to tamper with its data files." -ForegroundColor Red
}

# Copy binaries
Write-Host "Copying binaries..." -ForegroundColor Yellow
Copy-Item -Path ".\*" -Destination $installPath -Recurse -Force

# Set server URL using command-line
Write-Host "Configuring server URL..." -ForegroundColor Yellow
$exePath = Join-Path $installPath "ParentalControl.Client.Windows.exe"
& $exePath set server-url $ServerUrl

# Stop service if exists
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Stopping existing service..." -ForegroundColor Yellow
    Stop-Service -Name $serviceName -Force
    Start-Sleep -Seconds 2
}

# Create/Update Windows Service
Write-Host "Registering Windows Service..." -ForegroundColor Yellow
$exePath = Join-Path $installPath "ParentalControl.Client.Windows.exe"

if ($existingService) {
    sc.exe config $serviceName binPath= $exePath start= auto
} else {
    sc.exe create $serviceName binPath= $exePath start= auto DisplayName= "Parental Control Client"
    sc.exe description $serviceName "Monitors and enforces parental control time limits"
}

# Configure service recovery
Write-Host "Configuring service recovery..." -ForegroundColor Yellow
sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/60000

# Start service
Write-Host "Starting service..." -ForegroundColor Yellow
Start-Service -Name $serviceName

# Verify service is running
Start-Sleep -Seconds 2
$service = Get-Service -Name $serviceName
if ($service.Status -eq "Running") {
    Write-Host ""
    Write-Host "=== Installation Complete ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "Service Status: Running" -ForegroundColor Green
    Write-Host "Install Path: $installPath" -ForegroundColor Cyan
    Write-Host "Data Path: $dataPath" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To change server URL, run:" -ForegroundColor Yellow
    Write-Host "  $exePath set server-url <new-url>" -ForegroundColor Cyan
    Write-Host "  Restart-Service $serviceName" -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "WARNING: Service installed but not running" -ForegroundColor Yellow
    Write-Host "Check logs at: $dataPath\Logs" -ForegroundColor Yellow
}
