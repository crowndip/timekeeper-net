# Windows Client Code Review - Critical and High Severity Bugs

Review date: 2026-04-29
Scope: All files in `src/ParentalControl.Client.Windows/`, `src/ParentalControl.Client.Windows.UI/`, `src/ParentalControl.TrayIcon.Windows/`, and `scripts/install-windows-client.ps1`.

---

## Bug #1: SubmitUsageAsync Returns After First Record (HIGH)

**File:** `src/ParentalControl.Client.Windows/Services/ServerSyncService.cs:75-98`

Identical to the Linux client bug. The loop iterates over all pending usage records but returns immediately after the first successful submission:

```csharp
foreach (var record in records)
{
    // ...
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

Then in `ParentalControlWorker.cs:121`, ALL record IDs are marked as synced:

```csharp
await _cache.MarkAsSyncedAsync(pendingRecords.Select(r => r.Id).ToList());
```

**Impact:** After offline periods, only 1 of N accumulated usage records reaches the server. The rest are silently discarded. Time usage is permanently undercounted, giving the child more screen time than allowed.

**Fix:** Submit all records, return the last response:

```csharp
UsageReportResponse? lastResult = null;
foreach (var record in records)
{
    // ... submit ...
    if (response.IsSuccessStatusCode)
    {
        var result = await response.Content.ReadFromJsonAsync<UsageReportResponse>();
        if (result != null)
        {
            await _cache.SaveLastKnownLimitsAsync(record.UserId, result);
            lastResult = result;
        }
    }
}
return lastResult;
```

---

## Bug #2: Cross-User Enforcement - Wrong User Gets Logged Off (HIGH)

**File:** `src/ParentalControl.Client.Windows/ParentalControlWorker.cs:91-143`

The Windows client tracks only one user at a time via `_sessionMonitor.GetCurrentUser()`, but `GetPendingRecordsAsync()` returns records from ALL users. When the current console user is User B, stale records from User A (accumulated before A logged off) are also submitted.

Due to Bug #1 (early return), the server's enforcement response may be for User A. The enforcement then triggers:

```csharp
if (limits.ShouldEnforce)
{
    _enforcement.LogoffUser();  // Logs off the ACTIVE CONSOLE session
}
```

`LogoffUser()` calls `WTSLogoffSession` for the active console session (line 27-29 of WindowsEnforcementEngine.cs):

```csharp
int sessionId = WTSGetActiveConsoleSessionId();
WTSLogoffSession(IntPtr.Zero, sessionId, false);
```

**Impact:** User B (who may have plenty of time left) gets logged off because User A's enforcement response was applied to the console session.

**Fix:** Filter pending records to the current user before submitting. Apply enforcement only when the response corresponds to the currently active user.

---

## Bug #3: EnforcementAction Field Completely Ignored (HIGH)

**File:** `src/ParentalControl.Client.Windows/ParentalControlWorker.cs:145-158`

```csharp
private async Task CheckEnforcementAsync(Guid userId, UsageReportResponse limits)
{
    var remaining = TimeSpan.FromMinutes(limits.TimeRemainingMinutes);

    if (limits.ShouldEnforce)
    {
        _enforcement.LogoffUser();  // Always logoff, never lock
    }
    else if (remaining <= TimeSpan.FromMinutes(5))
    {
        await _enforcement.ShowWarningAsync(remaining);
    }
}
```

The `UsageReportResponse.EnforcementAction` field (which can be `"logout"` or `"lock"`) is never read. The client always calls `LogoffUser()` regardless of the parent's configured enforcement action. If a parent configured "lock" (screen lock only), the child gets fully logged off instead.

**Fix:** Check `EnforcementAction` and dispatch accordingly:

```csharp
if (limits.ShouldEnforce)
{
    switch (limits.EnforcementAction)
    {
        case "lock":
            _enforcement.LockSession();
            break;
        case "logout":
        default:
            _enforcement.LogoffUser();
            break;
    }
}
```

---

## Bug #4: Stale Cached Limits Cause Permanent Lockout in Offline Mode (HIGH)

**File:** `src/ParentalControl.Client.Windows/ParentalControlWorker.cs:124-131`

```csharp
else
{
    var cachedLimits = await _cache.GetLastKnownLimitsAsync(userId);
    if (cachedLimits != null)
    {
        await CheckEnforcementAsync(userId, cachedLimits);
    }
}
```

When the server is unreachable, the client uses the last cached `UsageReportResponse`. This response includes a snapshot of `ShouldEnforce` and `TimeRemainingMinutes` from that exact moment in time.

**Problem 1 - Permanent lockout:** If the last response had `ShouldEnforce=true`, the client will call `LogoffUser()` on every tick (every 60 seconds) indefinitely. Even after midnight when limits reset, even if the parent grants more time via another device. The child physically cannot use the computer until the server is reachable.

**Problem 2 - Stale after midnight:** If the child had 10 minutes remaining at 11:55 PM and the server goes down, at 12:00 AM the daily usage should reset. But the cached response still shows 10 minutes. The child gets the wrong limit for the new day.

**Contrast with Linux client:** The Linux client's offline mode recalculates remaining time from cached limits minus locally-tracked daily usage (`GetTodayUsageAsync`). This is still imperfect but better than using a raw snapshot.

**Fix:** Implement a proper offline calculation like the Linux client, or at minimum, invalidate cached enforcement state at midnight:

```csharp
// Don't re-enforce from stale cache, only use time remaining for new calculations
if (cachedLimits != null && !cachedLimits.ShouldEnforce)
{
    var todayUsage = await _cache.GetTodayUsageAsync(userId, DateOnly.FromDateTime(DateTime.UtcNow));
    var adjustedRemaining = cachedLimits.TimeRemainingMinutes - todayUsage;
    if (adjustedRemaining <= 0)
    {
        _enforcement.LogoffUser();
    }
}
```

---

## Bug #5: UserId Always Guid.Empty Breaks Multi-User Cache (HIGH)

**File:** `src/ParentalControl.Client.Windows/ParentalControlWorker.cs:100`

```csharp
var userId = Guid.Empty; // Placeholder, server determines actual userId
```

This `Guid.Empty` propagates everywhere:
- Into every `UsageRecord` via `IncrementUsageAsync`
- Into `SaveLastKnownLimitsAsync(record.UserId, result)` in ServerSyncService
- Into `GetLastKnownLimitsAsync(userId)` for offline mode
- Into `GetTodayUsageAsync(userId, date)` for daily tracking

Since ALL users share `Guid.Empty` as their userId:
1. The `_lastKnownLimits` dictionary has only one entry (key: `Guid.Empty`), storing whichever user's limits were cached last
2. The `_dailyUsage` dictionary merges all users' usage under one key
3. In offline mode, User B gets enforced with User A's limits (whoever synced last)

**Impact:** On shared family computers (the primary use case for parental control), cached limits are unreliable. One child's tight limit can block another child who has more time, or vice versa.

**Fix:** The server returns the actual userId in responses. Capture it and use it for caching. Alternatively, use username as the cache key since it's always available.

---

## Bug #6: WTSLogoffSession Failure Goes Undetected (HIGH)

**File:** `src/ParentalControl.Client.Windows/Services/WindowsEnforcementEngine.cs:23-35`

```csharp
[DllImport("wtsapi32.dll", SetLastError = true)]
private static extern bool WTSLogoffSession(IntPtr hServer, int SessionId, bool bWait);

public void LogoffUser()
{
    try
    {
        int sessionId = WTSGetActiveConsoleSessionId();
        _logger.LogWarning("Logging off user, session ID: {SessionId}", sessionId);
        WTSLogoffSession(IntPtr.Zero, sessionId, false);  // Return value ignored
    }
    catch (Exception ex) { ... }
}
```

`WTSLogoffSession` returns `false` on failure, and `SetLastError = true` is declared, but the return value is never checked. If the logoff fails (e.g., insufficient privileges, invalid session), the enforcement silently does nothing. The service logs "Logging off user" but never confirms success.

Additionally, `WTSGetActiveConsoleSessionId()` returns `0xFFFFFFFF` if no user is logged into the physical console (e.g., only RDP sessions). Passing this to `WTSLogoffSession` would fail silently.

**Fix:**

```csharp
public void LogoffUser()
{
    try
    {
        int sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId == -1) // 0xFFFFFFFF
        {
            _logger.LogWarning("No active console session to log off");
            return;
        }
        _logger.LogWarning("Logging off user, session ID: {SessionId}", sessionId);
        bool success = WTSLogoffSession(IntPtr.Zero, sessionId, false);
        if (!success)
        {
            var error = Marshal.GetLastWin32Error();
            _logger.LogError("WTSLogoffSession failed, Win32 error: {Error}", error);
        }
    }
    catch (Exception ex) { ... }
}
```

---

## Bug #7: Server WarningMinutes Ignored, ShowWarning Is a No-Op (HIGH)

**File:** `src/ParentalControl.Client.Windows/ParentalControlWorker.cs:154-157`

```csharp
else if (remaining <= TimeSpan.FromMinutes(5))
{
    await _enforcement.ShowWarningAsync(remaining);
}
```

Two problems:

### 7a: Hardcoded 5-minute threshold

The server sends configurable `WarningMinutes[]` (e.g., `[15, 5, 1]`) in the `UsageReportResponse`, but the Windows client ignores it completely and hardcodes a 5-minute threshold. Parents cannot customize when warnings appear.

### 7b: ShowWarningAsync is a no-op

```csharp
public async Task ShowWarningAsync(TimeSpan timeRemaining)
{
    _logger.LogWarning("Time remaining: {Minutes} minutes", timeRemaining.TotalMinutes);
    // TODO: Implement named pipe communication to UI
    await Task.CompletedTask;
}
```

The warning UI (`MainWindow.xaml`) exists but is never launched. The named pipe communication mentioned in the TODO was never implemented. The child receives **no visual warning** before being logged off.

**Impact:** Children are logged off without any prior warning, potentially losing unsaved work. The "5 minutes remaining" warning that parents expect to see is never shown.

**Fix:** At minimum, launch the warning UI process. For a complete fix, implement the named pipe IPC or use an alternative like a toast notification:

```csharp
public async Task ShowWarningAsync(TimeSpan timeRemaining)
{
    // Launch warning UI as the active console user
    var sessionId = WTSGetActiveConsoleSessionId();
    // Use CreateProcessAsUser to launch UI in user's session
    // Or use a simpler approach with toast notifications
}
```

---

## Bug #8: OS Version Hardcoded as "Windows 11" (MEDIUM)

**File:** `src/ParentalControl.Client.Windows/Services/ServerSyncService.cs:132`

```csharp
var osInfo = "Windows 11";
```

Always reports "Windows 11" regardless of actual OS. The Linux client uses `Environment.OSVersion.ToString()`. This pollutes server-side reporting data.

**Fix:** `var osInfo = Environment.OSVersion.ToString();` or use `RuntimeInformation.OSDescription`.

---

## Summary Table

| # | Severity | File | Description |
|---|----------|------|-------------|
| 1 | HIGH | ServerSyncService.cs:95 | Early return in SubmitUsageAsync loses usage records |
| 2 | HIGH | ParentalControlWorker.cs:118-122 | Cross-user enforcement: wrong user logged off |
| 3 | HIGH | ParentalControlWorker.cs:149-152 | EnforcementAction field ignored, always logoff |
| 4 | HIGH | ParentalControlWorker.cs:126 | Stale cached limits cause permanent lockout offline |
| 5 | HIGH | ParentalControlWorker.cs:100 | UserId always Guid.Empty breaks multi-user cache |
| 6 | HIGH | WindowsEnforcementEngine.cs:29 | WTSLogoffSession failure undetected |
| 7 | HIGH | ParentalControlWorker.cs:154 + WindowsEnforcementEngine.cs:51 | WarningMinutes ignored, ShowWarning is a no-op |
| 8 | MEDIUM | ServerSyncService.cs:132 | OS version hardcoded as "Windows 11" |

---

## Shared Bugs (Present in Both Linux and Windows Clients)

| Bug | Linux File | Windows File |
|-----|-----------|--------------|
| SubmitUsageAsync early return | ServerSyncService.cs:103 | ServerSyncService.cs:95 |
| UserId always Guid.Empty | N/A (uses username grouping) | ParentalControlWorker.cs:100 |

## Architecture Differences from Linux Client

| Aspect | Linux Client | Windows Client |
|--------|-------------|----------------|
| Multi-user tracking | Tracks all active sessions per tick | Tracks single console user per tick |
| Enforcement target | Specific user by username | Active console session (any user) |
| EnforcementAction dispatch | Supports logout + lock | Always logout, ignores action field |
| Offline enforcement | Recalculates from cached limits + daily usage | Uses raw cached response snapshot |
| Warning system | Server WarningMinutes checked (but only logged) | Hardcoded 5 min, ShowWarning no-op |
