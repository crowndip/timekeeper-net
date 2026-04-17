# Windows Client Specifications

## Overview

Windows 11 client for the Parental Control System that provides identical functionality to the Linux client while maintaining full interoperability with the centralized server. Time limits are shared across all platforms - a user with 120 minutes can use any combination across Windows and Linux devices.

## Architecture

### Technology Stack

- **.NET 8** - Same as Linux client for code compatibility
- **Windows Service** - Background service (equivalent to systemd on Linux)
- **WPF (Windows Presentation Foundation)** - Native Windows UI for notifications
- **Windows API** - For session management and enforcement

### Project Structure

```
src/
├── ParentalControl.Shared/              # Existing - shared DTOs (no changes)
├── ParentalControl.Client.Windows/      # NEW - Windows service
│   ├── Program.cs                       # Service entry point
│   ├── ParentalControlWorker.cs         # Main worker (similar to Linux)
│   ├── Services/
│   │   ├── ServerSyncService.cs         # Reuse from Linux client
│   │   ├── LocalCache.cs                # Reuse from Linux client
│   │   ├── WindowsEnforcementEngine.cs  # NEW - Windows-specific enforcement
│   │   └── WindowsSessionMonitor.cs     # NEW - Windows session detection
│   ├── appsettings.json
│   └── ParentalControl.Client.Windows.csproj
└── ParentalControl.Client.Windows.UI/   # NEW - WPF notification UI
    ├── App.xaml
    ├── MainWindow.xaml                  # Notification window
    ├── ViewModels/
    │   └── NotificationViewModel.cs
    └── ParentalControl.Client.Windows.UI.csproj
```

## Core Requirements

### 1. Server Interoperability

**CRITICAL**: Windows client must be 100% compatible with existing server API.

#### Shared Components (Reuse from Linux)
- `ParentalControl.Shared` - All DTOs (RegisterComputerRequest, ReportUsageRequest, etc.)
- `ServerSyncService` - HTTP communication with server
- `LocalCache` - SQLite-based offline cache
- Server API endpoints remain unchanged

#### Platform Identification
```csharp
// In RegisterComputerRequest
MachineId = $"{Environment.MachineName}-WIN"  // Distinguish from Linux
OperatingSystem = "Windows 11"
```

#### Time Tracking
- Report usage every 60 seconds (same as Linux)
- Server aggregates time across ALL devices for the user
- Example: User "john" has 120 min/day limit
  - Linux PC reports 30 minutes used
  - Windows PC reports 90 minutes used
  - Server calculates: 120 - (30 + 90) = 0 minutes remaining
  - Both clients receive "0 minutes remaining" in next sync

### 2. Windows Service Implementation

#### Service Characteristics
- **Type**: Windows Background Service (.NET Worker Service)
- **Startup**: Automatic (starts with Windows)
- **Account**: Local System (needs privileges for user management)
- **Recovery**: Restart on failure

#### Service Registration
```powershell
# Installation
sc.exe create "ParentalControlClient" binPath="C:\Program Files\ParentalControl\ParentalControl.Client.Windows.exe"
sc.exe config "ParentalControlClient" start=auto
sc.exe start "ParentalControlClient"

# Or use .NET Worker Service template with Windows Service support
```

#### Configuration
```json
{
  "ParentalControl": {
    "ServerUrl": "http://server-ip:8080",
    "TickIntervalSeconds": 60,
    "OfflineMode": {
      "Enabled": true,
      "CacheDurationMinutes": 1440
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**Location**: `C:\ProgramData\ParentalControl\appsettings.json`

### 3. Session Monitoring (Windows-Specific)

#### User Session Detection

**Windows API Approach**:
```csharp
using System.Management;

public class WindowsSessionMonitor
{
    // Detect current logged-in user
    public string GetCurrentUser()
    {
        // Use WMI to get interactive session user
        var query = new SelectQuery("SELECT UserName FROM Win32_ComputerSystem");
        using var searcher = new ManagementObjectSearcher(query);
        foreach (ManagementObject mo in searcher.Get())
        {
            return mo["UserName"]?.ToString()?.Split('\\').Last();
        }
        return null;
    }
    
    // Monitor session changes
    public void MonitorSessionChanges()
    {
        // Use SystemEvents.SessionSwitch
        Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
    }
    
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLogon:
                // User logged in - start tracking
                break;
            case SessionSwitchReason.SessionLogoff:
                // User logged off - stop tracking
                break;
            case SessionSwitchReason.SessionLock:
                // Session locked - pause tracking
                break;
            case SessionSwitchReason.SessionUnlock:
                // Session unlocked - resume tracking
                break;
        }
    }
}
```

#### Alternative: Process Monitoring
```csharp
// Monitor explorer.exe for user sessions
var processes = Process.GetProcessesByName("explorer");
foreach (var proc in processes)
{
    var username = GetProcessOwner(proc);
    // Track this user
}
```

### 4. Enforcement Engine (Windows-Specific)

#### Enforcement Actions

**1. Warning Notification**
- Show WPF notification window when 5 minutes remaining
- Non-dismissible, always-on-top
- Countdown timer

**2. Forced Logoff**
```csharp
using System.Runtime.InteropServices;

public class WindowsEnforcementEngine
{
    [DllImport("wtsapi32.dll", SetLastError = true)]
    static extern bool WTSLogoffSession(IntPtr hServer, int SessionId, bool bWait);
    
    [DllImport("kernel32.dll")]
    static extern IntPtr WTSGetActiveConsoleSessionId();
    
    public void LogoffUser()
    {
        int sessionId = (int)WTSGetActiveConsoleSessionId();
        WTSLogoffSession(IntPtr.Zero, sessionId, false);
    }
}
```

**3. Session Lock** (Alternative)
```csharp
[DllImport("user32.dll")]
static extern bool LockWorkStation();

public void LockSession()
{
    LockWorkStation();
}
```

#### Enforcement Strategy
```csharp
public async Task CheckAndEnforceAsync()
{
    var username = _sessionMonitor.GetCurrentUser();
    if (string.IsNullOrEmpty(username)) return;
    
    // Get time remaining from server or cache
    var timeRemaining = await GetTimeRemainingAsync(username);
    
    if (timeRemaining <= TimeSpan.FromMinutes(5) && timeRemaining > TimeSpan.Zero)
    {
        // Show warning notification
        ShowWarningNotification(timeRemaining);
    }
    else if (timeRemaining <= TimeSpan.Zero)
    {
        // Enforce limit
        LogoffUser();
    }
}
```

### 5. Offline Mode

**Identical to Linux Client**:
- Cache last known limits in SQLite
- Cache usage records when server unavailable
- Sync when server becomes available
- Enforce cached limits even offline

**Cache Location**: `C:\ProgramData\ParentalControl\cache.db`

### 6. WPF Notification UI

#### Notification Window

**Features**:
- Always-on-top
- Non-dismissible (no close button)
- Shows time remaining
- Countdown timer
- Warning message

**XAML Example**:
```xml
<Window x:Class="ParentalControl.Client.Windows.UI.NotificationWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Parental Control Warning"
        Height="200" Width="400"
        WindowStyle="None"
        ResizeMode="NoResize"
        Topmost="True"
        WindowStartupLocation="CenterScreen"
        ShowInTaskbar="True">
    <Grid Background="#FFF3F3F3">
        <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
            <TextBlock Text="⚠️ Time Limit Warning" 
                       FontSize="24" 
                       FontWeight="Bold"
                       HorizontalAlignment="Center"
                       Margin="0,0,0,20"/>
            <TextBlock Text="{Binding TimeRemaining}" 
                       FontSize="48" 
                       FontWeight="Bold"
                       HorizontalAlignment="Center"
                       Foreground="Red"
                       Margin="0,0,0,20"/>
            <TextBlock Text="minutes remaining"
                       FontSize="18"
                       HorizontalAlignment="Center"
                       Margin="0,0,0,20"/>
            <TextBlock Text="Please save your work and log off."
                       FontSize="14"
                       HorizontalAlignment="Center"
                       TextWrapping="Wrap"
                       MaxWidth="350"/>
        </StackPanel>
    </Grid>
</Window>
```

#### Inter-Process Communication
```csharp
// Service -> UI communication via Named Pipes
public class NotificationService
{
    private const string PipeName = "ParentalControlNotifications";
    
    public async Task ShowWarningAsync(TimeSpan timeRemaining)
    {
        using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
        await pipe.ConnectAsync(1000);
        
        var message = JsonSerializer.Serialize(new
        {
            Type = "Warning",
            TimeRemaining = timeRemaining.TotalMinutes
        });
        
        var bytes = Encoding.UTF8.GetBytes(message);
        await pipe.WriteAsync(bytes, 0, bytes.Length);
    }
}
```

### 7. Installation & Deployment

#### Installer Requirements
- **MSI Installer** (WiX Toolset or Advanced Installer)
- Install to: `C:\Program Files\ParentalControl\`
- Config to: `C:\ProgramData\ParentalControl\`
- Create Windows Service
- Set service to auto-start
- Require Administrator privileges

#### Installation Steps
1. Copy binaries to Program Files
2. Copy configuration template to ProgramData
3. Register Windows Service
4. Configure service recovery options
5. Start service
6. Add firewall rule (if needed)

#### Uninstallation
1. Stop service
2. Delete service
3. Remove binaries
4. Optionally remove configuration and cache

### 8. Security Considerations

#### Service Privileges
- Run as Local System (needs user management rights)
- Protect configuration file (Admin-only write access)
- Protect cache database (Admin-only write access)

#### Tamper Protection
```csharp
// Prevent service stop by non-admins
// Set service security descriptor
sc.exe sdset "ParentalControlClient" "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;IU)"
```

#### Process Protection
- Service runs in protected process (if possible)
- Monitor for service stop attempts
- Auto-restart on failure

### 9. Account Type Handling

**Identical to Linux**:
- **Child**: Tracked and enforced
- **Parent**: Not tracked, no limits
- **Technical**: System accounts, not tracked

**Windows-Specific**:
```csharp
// Ignore built-in accounts
var ignoredAccounts = new[] 
{ 
    "SYSTEM", 
    "LOCAL SERVICE", 
    "NETWORK SERVICE",
    "Administrator" // Optional
};

if (ignoredAccounts.Contains(username, StringComparer.OrdinalIgnoreCase))
{
    return; // Don't track
}
```

### 10. Logging

**Location**: `C:\ProgramData\ParentalControl\Logs\`

**Serilog Configuration**:
```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        path: @"C:\ProgramData\ParentalControl\Logs\client-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .WriteTo.EventLog("ParentalControl", manageEventSource: true)
    .CreateLogger();
```

## Implementation Phases

### Phase 1: Core Service (Week 1)
- [ ] Create Windows Service project
- [ ] Implement WindowsSessionMonitor
- [ ] Reuse ServerSyncService from Linux
- [ ] Reuse LocalCache from Linux
- [ ] Basic time tracking
- [ ] Test server communication

### Phase 2: Enforcement (Week 1)
- [ ] Implement WindowsEnforcementEngine
- [ ] Logoff functionality
- [ ] Lock functionality
- [ ] Test enforcement actions

### Phase 3: UI (Week 2)
- [ ] Create WPF notification project
- [ ] Implement notification window
- [ ] Named pipe communication
- [ ] Test UI display

### Phase 4: Offline Mode (Week 2)
- [ ] Implement offline caching
- [ ] Test offline enforcement
- [ ] Test sync after reconnection

### Phase 5: Installation (Week 3)
- [ ] Create MSI installer
- [ ] Service registration
- [ ] Configuration management
- [ ] Test installation/uninstallation

### Phase 6: Testing & Polish (Week 3)
- [ ] Integration testing with server
- [ ] Cross-platform testing (Windows + Linux)
- [ ] Time aggregation verification
- [ ] Edge case testing
- [ ] Documentation

## Testing Strategy

### Unit Tests
- WindowsSessionMonitor tests (mocked WMI)
- WindowsEnforcementEngine tests (mocked Win32 API)
- Configuration loading tests

### Integration Tests
- Server communication tests
- Time aggregation tests (Windows + Linux)
- Offline mode tests
- Enforcement tests (in VM)

### Cross-Platform Tests
**Critical**: Verify time sharing works correctly
```
Scenario: User has 120 min/day limit
1. User logs into Linux PC at 9:00 AM
2. Uses Linux for 30 minutes (9:00-9:30)
3. Logs off Linux
4. Logs into Windows PC at 10:00 AM
5. Server should report: 90 minutes remaining
6. Uses Windows for 90 minutes (10:00-11:30)
7. Server should report: 0 minutes remaining
8. Windows client enforces logoff
9. User tries to log into Linux PC at 12:00 PM
10. Linux client should enforce immediately (0 minutes remaining)
```

## API Compatibility Matrix

| Feature | Linux Client | Windows Client | Server API |
|---------|-------------|----------------|------------|
| Register Computer | ✅ | ✅ | `/api/client/register` |
| Report Usage | ✅ | ✅ | `/api/client/usage` |
| Get Config | ✅ | ✅ | `/api/client/config` |
| Start Session | ✅ | ✅ | `/api/client/session/start` |
| End Session | ✅ | ✅ | `/api/client/session/end` |
| Offline Cache | ✅ SQLite | ✅ SQLite | N/A |
| Time Aggregation | ✅ | ✅ | Server-side |

## Configuration Compatibility

**Both clients use identical configuration structure**:
```json
{
  "ParentalControl": {
    "ServerUrl": "http://server-ip:8080",
    "TickIntervalSeconds": 60,
    "OfflineMode": {
      "Enabled": true,
      "CacheDurationMinutes": 1440
    }
  }
}
```

## Differences from Linux Client

| Aspect | Linux | Windows |
|--------|-------|---------|
| Service | systemd | Windows Service |
| Session Detection | D-Bus / loginctl | WMI / Win32 API |
| Enforcement | loginctl / systemctl | WTSLogoffSession / LockWorkStation |
| UI Framework | Avalonia | WPF |
| Config Location | `/opt/parental-control/` | `C:\ProgramData\ParentalControl\` |
| Cache Location | `/opt/parental-control/cache.db` | `C:\ProgramData\ParentalControl\cache.db` |
| Logging | `/var/log/` or journald | `C:\ProgramData\ParentalControl\Logs\` + Event Log |
| Installation | systemd unit + scripts | MSI installer + Windows Service |

## Server Changes Required

**NONE** - Server is already designed for multi-platform support:
- Time aggregation is per-user, not per-computer
- Server tracks all computers for each user
- Time limits are enforced based on total usage across all devices
- Existing API endpoints work for both platforms

## Success Criteria

1. ✅ Windows client connects to existing server without server modifications
2. ✅ Time limits are shared across Windows and Linux clients
3. ✅ User with 120 min limit can use 30 min on Linux + 90 min on Windows
4. ✅ Enforcement works on Windows (logoff/lock)
5. ✅ Offline mode works identically to Linux
6. ✅ Notifications display correctly on Windows
7. ✅ Service starts automatically with Windows
8. ✅ Service survives reboots
9. ✅ Installation is straightforward (MSI installer)
10. ✅ Uninstallation is clean

## Future Enhancements

- **Windows 10 Support** - Test and verify compatibility
- **Application Blocking** - Block specific applications (beyond time limits)
- **Website Filtering** - Integration with DNS/proxy
- **Screen Time Reports** - Detailed usage reports
- **Remote Management** - Configure client from web UI
- **.deb Package** - For Linux client (currently tar.gz only)
- **macOS Client** - Extend to macOS using similar architecture

## References

- Windows Service: https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service
- WPF: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/
- WTS API: https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/
- Session Monitoring: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.sessionswitch
- WiX Toolset: https://wixtoolset.org/

## Notes

- **Code Reuse**: Maximize code sharing with Linux client (ServerSyncService, LocalCache, DTOs)
- **Platform Abstraction**: Use interfaces for platform-specific code (ISessionMonitor, IEnforcementEngine)
- **Testing**: Extensive testing in VMs before production deployment
- **Documentation**: Provide clear installation and configuration guides for Windows
- **Support**: Windows client should be as reliable and maintainable as Linux client
