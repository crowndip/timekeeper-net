# Windows Client Installation Guide

## Overview

The Windows client provides identical functionality to the Linux client and connects to the same centralized server. Time limits are shared across all platforms.

## System Requirements

- Windows 11 (or Windows 10)
- .NET 8 Runtime
- Administrator privileges for installation

## Installation

### Option 1: Pre-built Binaries (Recommended)

1. Download the latest Windows client release from [Releases](https://github.com/crowndip/timekeeper-net/releases)
2. Extract `client-windows-x64.zip` to a temporary folder
3. Open PowerShell as Administrator
4. Navigate to the extracted folder
5. Run the installation script:

```powershell
.\install-windows-client.ps1 -ServerUrl "http://your-server-ip:8080"
```

The installer will:
- Copy binaries to `C:\Program Files\ParentalControl`
- Create configuration in `C:\ProgramData\ParentalControl`
- Register and start the Windows Service
- Configure automatic startup

### Option 2: Build from Source

**Prerequisites:**
- .NET 8 SDK
- Windows 11 SDK (for WPF)

**Build Steps:**

```powershell
# Clone repository
git clone https://github.com/crowndip/timekeeper-net.git
cd timekeeper-net

# Build Windows client
dotnet publish src/ParentalControl.Client.Windows/ParentalControl.Client.Windows.csproj `
  -c Release `
  -r win-x64 `
  --self-contained `
  -o publish/windows-client

# Build WPF UI (optional)
dotnet publish src/ParentalControl.Client.Windows.UI/ParentalControl.Client.Windows.UI.csproj `
  -c Release `
  -r win-x64 `
  --self-contained `
  -o publish/windows-ui

# Install
cd publish/windows-client
.\install-windows-client.ps1 -ServerUrl "http://your-server-ip:8080"
```

## Configuration

Configuration file location: `C:\ProgramData\ParentalControl\appsettings.json`

```json
{
  "ParentalControl": {
    "ServerUrl": "http://your-server-ip:8080",
    "TickIntervalSeconds": 60,
    "OfflineMode": {
      "Enabled": true,
      "CacheDurationMinutes": 1440
    }
  }
}
```

After editing configuration, restart the service:

```powershell
Restart-Service ParentalControlClient
```

## Service Management

### Check Service Status

```powershell
Get-Service ParentalControlClient
```

### Start Service

```powershell
Start-Service ParentalControlClient
```

### Stop Service

```powershell
Stop-Service ParentalControlClient
```

### View Logs

Logs are located at: `C:\ProgramData\ParentalControl\Logs\`

```powershell
Get-Content "C:\ProgramData\ParentalControl\Logs\client-*.log" -Tail 50
```

### View Event Log

```powershell
Get-EventLog -LogName Application -Source ParentalControl -Newest 20
```

## Uninstallation

Run as Administrator:

```powershell
.\uninstall-windows-client.ps1
```

This will:
- Stop and remove the Windows Service
- Remove binaries from Program Files
- Optionally remove configuration and logs

## Cross-Platform Time Sharing

Time limits are shared across all devices (Windows and Linux):

**Example:**
- User "john" has 120 minutes/day limit
- Uses Linux PC for 30 minutes → 90 minutes remaining
- Switches to Windows PC → Server reports 90 minutes remaining
- Uses Windows for 90 minutes → 0 minutes remaining
- Both clients enforce the limit

## Enforcement

When time limit is reached:
1. **5 minutes remaining**: Warning notification appears
2. **0 minutes remaining**: User is automatically logged off

## Offline Mode

The client continues to work when the server is unavailable:
- Uses cached time limits
- Stores usage records locally
- Syncs when server becomes available

## Troubleshooting

### Service Won't Start

Check logs at `C:\ProgramData\ParentalControl\Logs\`

Common issues:
- Server URL not configured
- Network connectivity issues
- Insufficient permissions

### Time Not Syncing

1. Verify server URL in configuration
2. Test connectivity: `Test-NetConnection your-server-ip -Port 8080`
3. Check service logs
4. Verify firewall rules

### Enforcement Not Working

1. Ensure service is running as Local System
2. Check service permissions
3. Verify user is not in ignored accounts list

## Security

- Service runs as Local System (required for user management)
- Configuration file is protected (Admin-only write access)
- Service cannot be stopped by non-administrators

## Architecture

- **Service**: Background Windows Service (.NET 8)
- **Session Monitor**: WMI-based user session detection
- **Enforcement**: Win32 API (WTSLogoffSession)
- **UI**: WPF notification window (optional)
- **Cache**: In-memory cache with offline support
- **Communication**: HTTP/JSON with server

## Differences from Linux Client

| Feature | Linux | Windows |
|---------|-------|---------|
| Service | systemd | Windows Service |
| Session Detection | D-Bus | WMI |
| Enforcement | loginctl | WTSLogoffSession |
| UI | Avalonia | WPF |
| Config Path | `/opt/parental-control/` | `C:\ProgramData\ParentalControl\` |
| Logs | journald | Event Log + File |

## Support

For issues, please open a ticket at: https://github.com/crowndip/timekeeper-net/issues

## License

See main repository for license information.
