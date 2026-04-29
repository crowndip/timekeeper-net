# Linux Client Code Review - Critical and High Severity Bugs

Review date: 2026-04-29
Scope: All files in `src/ParentalControl.Client/`, `src/ParentalControl.TrayIcon/`, `scripts/`, and related service files.

---

## Bug #1: SubmitUsageAsync Returns After First Record (HIGH)

**File:** `src/ParentalControl.Client/Services/ServerSyncService.cs:82-110`

The loop iterates over all pending usage records but **returns immediately** after the first successful submission:

```csharp
foreach (var record in records)
{
    var request = new UsageReportRequest( ... );
    var response = await _httpClient.PostAsJsonAsync("/api/client/usage", request);
    if (response.IsSuccessStatusCode)
    {
        var result = await response.Content.ReadFromJsonAsync<UsageReportResponse>();
        if (result != null)
        {
            await _cache.SaveLastKnownLimitsAsync(record.UserId, result);
            return result;  // <-- EARLY RETURN: remaining records never submitted
        }
    }
}
```

**Impact:** After an offline period, multiple usage records accumulate. When the server becomes reachable again, only the **first** record is submitted. The caller then marks **all** record IDs as synced (`syncedIds.AddRange(userRecords.Select(u => u.Id))`). The unsubmitted records are permanently lost.

Result: Time usage is undercounted, potentially by many minutes after extended offline periods. The child gets more screen time than allowed.

**Fix:** Submit all records, return the last response:

```csharp
UsageReportResponse? lastResult = null;
foreach (var record in records)
{
    var request = new UsageReportRequest( ... );
    var response = await _httpClient.PostAsJsonAsync("/api/client/usage", request);
    if (response.IsSuccessStatusCode)
    {
        var result = await response.Content.ReadFromJsonAsync<UsageReportResponse>();
        if (result != null)
        {
            await _cache.SaveLastKnownLimitsAsync(record.UserId, result);
            lastResult = result;
        }
    }
    else
    {
        _logger.LogWarning("Failed to submit usage record {Id}, status: {Status}", record.Id, response.StatusCode);
    }
}
return lastResult;
```

---

## Bug #2: Duplicate Username Crash in ProcessTickAsync (HIGH)

**File:** `src/ParentalControl.Client/ParentalControlWorker.cs:92`

```csharp
var sessionMap = sessions.ToDictionary(s => s.Username, s => s.SessionId);
```

If a user has multiple simultaneous sessions (e.g., graphical session + TTY login, or a KDE greeter session alongside the desktop session), `ToDictionary` throws `ArgumentException` for duplicate keys. This unhandled exception crashes the entire tick cycle.

**Impact:** On systems where a user has multiple loginctl sessions (common with KDE/SDDM greeter sessions), the entire monitoring loop fails every 60 seconds. No time tracking or enforcement occurs.

**Fix:** Use `ToLookup` or take the first session per user:

```csharp
var sessionMap = sessions
    .GroupBy(s => s.Username)
    .ToDictionary(g => g.Key, g => g.First().SessionId);
```

---

## Bug #3: Proxy Password World-Readable (HIGH - Security)

**File:** `src/ParentalControl.Client/Services/ServerSyncService.cs:373-374`
**File:** `src/ParentalControl.Client/Program.cs:41-42`

```csharp
File.SetUnixFileMode(ProxyPassPath,
    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
```

The proxy password file at `/etc/parental-control/proxy-pass` is explicitly set to mode `0644` (world-readable). The comment says "needed for tray app running as user".

**Impact:** Any local user (including the child whose time is being controlled) can read the reverse proxy authentication password:
```bash
cat /etc/parental-control/proxy-pass
```

If this password protects the parental control server, a child could use it to directly call the API and bypass time limits.

**Fix:** Use a dedicated group instead of world-readable:

```bash
# Create a group for parental-control readers
groupadd parental-control
usermod -aG parental-control <child-username>
chown root:parental-control /etc/parental-control/proxy-pass
chmod 0640 /etc/parental-control/proxy-pass
```

Or better: have the tray icon communicate with the local root-owned service via a Unix socket or D-Bus rather than reading credentials directly.

---

## Bug #4: Hardened Service File Missing ReadWritePaths and WorkingDirectory (HIGH)

**File:** `scripts/parental-control-client.service`

```ini
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/log/parental-control /var/lib/parental-control
```

Two problems:

### 4a: `/etc/parental-control` not in ReadWritePaths

`ProtectSystem=strict` makes the entire filesystem read-only except paths listed in `ReadWritePaths`. The client writes to `/etc/parental-control/` for:
- `computer-id` (on first registration)
- `server-url` (on startup)
- `proxy-user` / `proxy-pass` (on config)

These writes silently fail (caught by try/catch). On service restart, `computer-id` is lost, causing re-registration with the server. The server creates a new computer entry each time, breaking usage history continuity.

### 4b: Missing `WorkingDirectory`

The hardened service file lacks `WorkingDirectory=/opt/parental-control`. Without it, the working directory is `/`, and `Host.CreateDefaultBuilder` cannot find `appsettings.json`. The startup check at `Program.cs:64` throws:

```csharp
if (string.IsNullOrEmpty(config["ServerUrl"]))
    throw new InvalidOperationException("ServerUrl is not configured");
```

The service crashes immediately on startup.

**Note:** The auto-installer (`install-linux-client.sh`) generates its own service file inline with `WorkingDirectory` set. The deb package service also has it. Only the manually installed service file (`scripts/parental-control-client.service`) is affected.

**Fix:**

```ini
WorkingDirectory=/opt/parental-control
ReadWritePaths=/var/log/parental-control /var/lib/parental-control /etc/parental-control
```

---

## Bug #5: Warnings Never Reset Across Days (HIGH)

**File:** `src/ParentalControl.Client/Services/EnforcementEngine.cs:16`

```csharp
private readonly HashSet<int> _warningsShown = new();
```

Once a warning threshold (e.g., 15 minutes, 5 minutes) fires, it is added to `_warningsShown` and **never removed**. On subsequent days, the user never receives time warnings again until the service restarts.

**Impact:** After the first day, the child gets no "15 minutes remaining" or "5 minutes remaining" warnings. They get logged off without notice.

**Fix:** Reset `_warningsShown` when the day changes, or when `TimeRemainingMinutes` increases (indicating a new day or added time):

```csharp
private int _lastTimeRemaining = int.MaxValue;

// In CheckAndEnforceAsync, before warning check:
if (response.TimeRemainingMinutes > _lastTimeRemaining)
{
    _warningsShown.Clear(); // New day or time was added
}
_lastTimeRemaining = response.TimeRemainingMinutes;
```

---

## Bug #6: Service Type Mismatch (MEDIUM)

**File:** `scripts/parental-control-client.service:7`

```ini
Type=simple
```

But `Program.cs:59` calls `.UseSystemd()`, which sends `sd_notify(READY=1)` to indicate readiness. This requires `Type=notify`. With `Type=simple`, systemd ignores the notification and considers the service started immediately, which can cause race conditions with dependent services.

The auto-installer and deb package service files correctly use `Type=notify`.

**Fix:** Change to `Type=notify` in `scripts/parental-control-client.service`.

---

## Summary Table

| # | Severity | File | Description |
|---|----------|------|-------------|
| 1 | HIGH | ServerSyncService.cs:103 | Early return in SubmitUsageAsync loses usage records |
| 2 | HIGH | ParentalControlWorker.cs:92 | ToDictionary crash on duplicate user sessions |
| 3 | HIGH | ServerSyncService.cs:373, Program.cs:41 | Proxy password world-readable (0644) |
| 4 | HIGH | parental-control-client.service | Missing ReadWritePaths and WorkingDirectory |
| 5 | HIGH | EnforcementEngine.cs:16 | Warnings never reset across days |
| 6 | MEDIUM | parental-control-client.service:7 | Type=simple should be Type=notify |
