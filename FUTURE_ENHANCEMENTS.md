# Future Enhancements

## System Tray Icon (Planned for v1.6.0)

**Feature**: Display remaining time in system notification area (system tray)

**Platforms**: 
- Linux: GNOME, KDE Plasma, XFCE, Cinnamon
- Windows: System tray

**Implementation Plan**:
- Separate lightweight tray application
- Reads time remaining from local status file
- Updates every 10-30 seconds
- Shows tooltip with "Time Remaining: Xh Ym"
- Optional: Click to open web dashboard

**Technical Approach**:
- Cross-platform GUI framework (Avalonia or Electron)
- Service writes status to `/var/run/parental-control/status.txt` (Linux) or `C:\ProgramData\ParentalControl\status.txt` (Windows)
- Tray app reads and displays
- Auto-start with user session

**Challenges**:
- Different tray implementations across desktop environments
- Multiple users on same computer (which user's time to show?)
- Increased package size with GUI dependencies

**Workaround Until Then**:
Users can check remaining time via web dashboard at `http://server:8080`

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
