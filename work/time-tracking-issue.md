# Time Tracking Issue Analysis

## Problem Statement
- User: Tonik (UserId: 510ea9ac-f009-4d61-a55a-b46d4e4f101f)
- Date: April 23rd, 2026
- Expected usage: ~90 minutes (14:28 onwards)
- Recorded usage: Only 3 minutes
- Platform: Kubuntu with client + tray app

## Database Evidence
TimeUsage table shows only 2 records:
1. 3 minutes - last updated 21:09
2. 0 minutes - last updated 14:28

## Analysis Areas

### 1. Client Service Status
Need to verify:
- Is the client service running?
- Check: `systemctl status parental-control-client`
- Check logs: `/var/log/parental-control/client-*.log`

### 2. Time Tracking Logic
The client tracks time in two ways:

**Active Time Tracking (Client Service):**
- Location: `src/ParentalControl.Client/Services/TimeTrackingService.cs`
- Monitors user activity every tick (default 60 seconds)
- Sends usage reports to server via `/api/client/usage`
- Only counts "active" time (keyboard/mouse activity)

**Session Tracking:**
- Tracks login/logout events
- Records in Sessions table
- Should show session start/end times

### 3. Possible Issues

#### Issue A: Client Service Not Running
If the service stopped or crashed:
- No usage reports sent to server
- Time not tracked at all
- Check: Service status and logs

#### Issue B: Activity Detection Failure
If activity detection not working:
- User logged in but system thinks idle
- Only counts time when keyboard/mouse detected
- Kubuntu/KDE specific issue with X11/Wayland?

#### Issue C: Network/Server Communication Failure
If client can't reach server:
- Client tracks locally but can't sync
- Should have offline mode data
- Check: Network logs, server connectivity

#### Issue D: User Session Not Detected
If client doesn't detect user login:
- Service running but not tracking this user
- Username mismatch?
- Check: What username does client detect vs database

#### Issue E: Time Calculation Bug
If server receives reports but calculates wrong:
- Check Sessions table for this user/date
- Check TimeUsage calculation logic
- Server-side bug in aggregation

### 4. Key Files to Examine

**Client Side:**
- `/opt/parental-control/appsettings.json` - Configuration
- `/var/log/parental-control/client-*.log` - Client logs
- Service status: `systemctl status parental-control-client`

**Server Side:**
- Sessions table - Should show login at 14:28
- TimeUsage table - Should accumulate minutes
- Server logs - Check for received usage reports

**Code Locations:**
- `src/ParentalControl.Client/Services/TimeTrackingService.cs` - Activity detection
- `src/ParentalControl.Client/Services/ServerSyncService.cs` - Server communication
- `src/ParentalControl.WebService/Controllers/ClientController.cs` - Usage endpoint
- `src/ParentalControl.WebService/Services/TimeCalculationService.cs` - Time aggregation

### 5. Diagnostic Steps

1. **Check client service logs on Tonik's computer:**
   ```bash
   sudo journalctl -u parental-control-client --since "2026-04-23 14:00" --until "2026-04-23 22:00"
   ```

2. **Check if service was running:**
   ```bash
   sudo systemctl status parental-control-client
   ```

3. **Check Sessions table in database:**
   ```sql
   SELECT * FROM "Sessions" 
   WHERE "UserId" = '510ea9ac-f009-4d61-a55a-b46d4e4f101f' 
   AND "SessionStart" >= '2026-04-23 00:00:00'
   ORDER BY "SessionStart" DESC;
   ```

4. **Check server logs for usage reports:**
   ```bash
   docker logs parental-control-web --since "2026-04-23T14:00:00" | grep -i "tonik\|510ea9ac"
   ```

5. **Check tray app logs (if available):**
   - Tray app also sends reports
   - May show connectivity issues

### 6. Most Likely Scenarios

**Scenario 1: Service Not Running (80% probability)**
- Client service crashed or was stopped
- No tracking happening at all
- Solution: Ensure service is enabled and running

**Scenario 2: Activity Detection Not Working (15% probability)**
- Service running but not detecting activity
- KDE/Wayland compatibility issue
- Solution: Check activity detection code for Kubuntu

**Scenario 3: Network Issue (5% probability)**
- Client can't reach server
- Offline mode should cache data
- Solution: Check network connectivity and offline storage

## Critical Finding: The Issue

After analyzing the code, I found the problem:

### The Time Tracking Flow:
1. **Client service runs every 60 seconds** (tick interval)
2. **Gets active sessions** using `loginctl list-sessions`
3. **Records 1 minute** for each active session (if not idle/locked)
4. **Accumulates locally** in cache until sync
5. **Syncs with server** - sends accumulated minutes
6. **Server updates TimeUsage table** - adds minutes to existing record

### The Problem: Session Detection

The client only tracks time for sessions detected by `loginctl list-sessions`. 

**Key Questions:**
1. Was the client service running during 14:28-16:00?
2. Did `loginctl` detect Tonik's session?
3. Was the session marked as "idle" or "locked"?

### Why Only 3 Minutes Recorded?

Two possible scenarios:

**Scenario A: Service Started Late**
- Service wasn't running at 14:28
- Started later (around 21:06)
- Only tracked 3 minutes before user logged off
- **Evidence**: First record at 14:28 with 0 minutes, second at 21:09 with 3 minutes

**Scenario B: Session Not Detected**
- Service was running
- But `loginctl` didn't show Tonik's session as active
- Possible reasons:
  - Session type not detected (Wayland vs X11)
  - Session marked as "closing" or "lingering"
  - Username mismatch (case sensitivity?)

### The 0 Minutes Record at 14:28

This is suspicious! It suggests:
- Client DID detect the session at 14:28
- But recorded 0 active minutes
- This means: **Session was detected as IDLE/LOCKED**

The code shows:
```csharp
await _cache.IncrementUsageAsync(
    session.UserId,
    session.Username,
    sessionGuid,
    activeMinutes: isLocked ? 0 : 1,  // 0 if locked!
    idleMinutes: isLocked ? 1 : 0
);
```

**Most Likely Cause**: The session was detected as "locked" or "idle" for most of the time!

### Session Idle Detection

The client checks if session is idle using:
```bash
loginctl show-session {sessionId} -p LockedHint --value
loginctl show-session {sessionId} -p IdleHint --value
```

If either returns "yes", the session is considered idle and **time is NOT counted**.

## Root Cause Analysis

**Primary Issue**: Session was detected as IDLE/LOCKED

Possible reasons:
1. **Screen saver activated** - KDE screen saver marked session as idle
2. **Screen locked** - User locked screen, system marked as locked
3. **Idle detection too aggressive** - KDE's idle detection triggered
4. **No keyboard/mouse activity detected** - Gaming with controller?

### Gaming Scenario

If Tonik was playing a game:
- **With keyboard/mouse**: Should be detected as active
- **With game controller only**: System might mark as idle!
- **Fullscreen game**: Might prevent idle detection from working

KDE's idle detection might not see controller input as "activity"!


## Diagnostic Commands to Run on Tonik's Computer

```bash
# 1. Check if service is running
systemctl status parental-control-client

# 2. Check service logs for April 23rd
sudo journalctl -u parental-control-client --since "2026-04-23 14:00" --until "2026-04-23 22:00" > /tmp/client-logs.txt

# 3. Check what loginctl shows now
loginctl list-sessions

# 4. Check idle hints for current session
loginctl show-session $(loginctl list-sessions --no-legend | grep tonik | awk '{print $1}') -p IdleHint -p LockedHint

# 5. Check KDE idle detection settings
kreadconfig5 --file kscreenlockerrc --group Daemon --key Timeout

# 6. Check if screen saver is active
qdbus org.freedesktop.ScreenSaver /ScreenSaver org.freedesktop.ScreenSaver.GetActive
```

## SQL Queries to Run

```sql
-- Check Sessions table for Tonik on April 23rd
SELECT 
    "Id",
    "UserId", 
    "ComputerId",
    "SessionStart",
    "SessionEnd",
    "ActiveMinutes",
    "IdleMinutes",
    "IsActive"
FROM "Sessions" 
WHERE "UserId" = '510ea9ac-f009-4d61-a55a-b46d4e4f101f' 
AND "SessionStart" >= '2026-04-23 00:00:00'
ORDER BY "SessionStart" DESC;

-- Check TimeUsage details
SELECT 
    "Id",
    "UserId",
    "ComputerId", 
    "UsageDate",
    "MinutesUsed",
    "SessionId",
    "LastUpdated"
FROM "TimeUsage"
WHERE "UserId" = '510ea9ac-f009-4d61-a55a-b46d4e4f101f'
AND "UsageDate" = '2026-04-23'
ORDER BY "LastUpdated" DESC;

-- Check computer info
SELECT "Id", "Hostname", "LastSeenAt" 
FROM "Computers" 
WHERE "Id" IN (
    SELECT DISTINCT "ComputerId" 
    FROM "TimeUsage" 
    WHERE "UserId" = '510ea9ac-f009-4d61-a55a-b46d4e4f101f'
);
```

## Immediate Fix Options

### Option 1: Disable Idle Detection (Quick Fix)
Modify the client to NOT check idle status - count all time when session exists.

**Pros**: Simple, counts all logged-in time
**Cons**: Counts time even when user is away

### Option 2: Improve Idle Detection (Better Fix)
Make idle detection more lenient or add alternative activity detection.

**Pros**: More accurate
**Cons**: More complex

### Option 3: Add Manual Time Adjustment
Allow parent to manually add the missing 87 minutes.

**Pros**: Immediate correction
**Cons**: Doesn't fix root cause

## Recommended Actions

1. **Immediate**: Check the diagnostic commands above
2. **Verify**: Run SQL queries to see Sessions table data
3. **Confirm**: Check if IdleHint was "yes" during gaming
4. **Fix**: Based on findings, either:
   - Disable idle detection for gaming scenarios
   - Adjust KDE idle timeout settings
   - Add controller input detection
5. **Compensate**: Manually adjust Tonik's time if needed
