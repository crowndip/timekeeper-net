# Future Enhancements

## ✅ System Tray Icon (Completed in v1.7.0)

**Feature**: Display remaining time in system notification area (system tray)

**Status**: ✅ **IMPLEMENTED**

**Installation**:

**Linux**:
```bash
# Optional - install tray icon
sudo dpkg -i parental-control-tray_1.7.0_amd64.deb
```

**Windows**:
```powershell
# Extract and run
Expand-Archive tray-windows-x64.zip
.\tray-windows-x64\ParentalControl.TrayIcon.Windows.exe

# Optional: Add to startup folder
# Copy shortcut to: %APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup
```

**Features**:
- Shows remaining time for currently logged-in user
- Updates every 30 seconds
- Lightweight - minimal resource usage
- Auto-starts with user session (Linux)
- Reads configuration from persistent storage
- Communicates directly with server

**Platforms**: 
- ✅ Linux: GNOME, KDE Plasma, XFCE, Cinnamon
- ✅ Windows: System tray

---

## Other Planned Features

### v1.6.0
- System tray icon
- Desktop notifications when time is running low
- Grace period before enforcement

### v1.7.0
- Mobile app for parents (iOS/Android)
- Push notifications for parents
- Activity reports

### v2.0.0
- Application-specific limits (e.g., games vs homework apps)
- Website filtering
- Screen time scheduling (bedtime mode)
