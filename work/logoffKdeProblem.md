# Linux Client Logoff Failure Analysis - KDE/Kubuntu

## Problem Statement

The parental control Linux client correctly tracks and reports time usage, but fails to log off the user when the time limit is exceeded. Observed on a Kubuntu (KDE Plasma) machine.

## Root Cause: Critical Bugs in EnforcementEngine.cs

### Bug #1: `$USER` Not Expanded (CRITICAL)

**File:** `src/ParentalControl.Client/Services/EnforcementEngine.cs:116-121`

```csharp
var process = Process.Start(new ProcessStartInfo
{
    FileName = "loginctl",
    Arguments = "terminate-user $USER",
    UseShellExecute = false   // <-- $USER is NOT expanded
});
```

`UseShellExecute = false` means the process is launched directly via `exec()`, not through a shell (`/bin/sh -c`). The `$USER` environment variable is **never expanded** -- it is passed as the literal string `$USER` to `loginctl`.

The actual command executed is:
```
loginctl terminate-user '$USER'    # literal string, not the username
```

This will always fail because there is no user named `$USER`.

### Bug #2: No Target User Parameter (CRITICAL)

Even if `$USER` were expanded, the service runs as **root** (see `parental-control-client.service` line 10: `User=root`). So `$USER` would resolve to `root`, not the child user whose time has expired.

The `LogoutCurrentUserAsync()` method signature takes no parameters -- it has no way to know **which user** to log off:

```csharp
private async Task LogoutCurrentUserAsync()  // no username parameter
```

The enforcement engine receives a `UsageReportResponse` that triggers enforcement, but at that point the username/session information from the worker loop has been lost. The worker calls:

1. `_serverSync.SubmitUsageAsync(usageData)` -- returns a `UsageReportResponse`
2. `_enforcement.CheckAndEnforceAsync(response)` -- only gets the response, not the session/user info

### Bug #3: `LockSessionAsync()` Has the Same Problem

```csharp
Arguments = "lock-session",   // no session ID specified
```

`loginctl lock-session` without a session ID locks the session of the calling process. Since the service runs as root with no graphical session, this is a no-op.

### Bug #4: Missing SDDM in Session Filter

**File:** `src/ParentalControl.Client/Services/SessionMonitor.cs:58`

```csharp
if (username == "root" || username == "gdm" || username == "lightdm")
    continue;
```

On Kubuntu, the display manager is **SDDM**, not GDM or LightDM. The `sddm` user is not filtered out, so it could be detected as an active session and have time tracked against it. This won't cause logoff failure directly but pollutes session data.

## Impact

- `LogoutCurrentUserAsync()` silently fails every time -- `loginctl` returns an error for unknown user `$USER`, and the error is caught and logged but enforcement never succeeds.
- `LockSessionAsync()` is a no-op when run from a root service with no session.
- The client faithfully reports time to the server and receives the enforcement command, but the actual OS-level action never takes effect.

## Recommended Fixes

### Fix 1: Pass Username Through Enforcement Chain

The enforcement methods need to receive the target username and session ID. Modify the interfaces:

```csharp
// EnforcementEngine should know who to enforce against
public async Task CheckAndEnforceAsync(UsageReportResponse response, string username, string sessionId)
```

The `ParentalControlWorker.ProcessTickAsync()` must pass session info down:

```csharp
// In the usage submission loop, associate response with session
var response = await _serverSync.SubmitUsageAsync(usageData);
if (response != null)
{
    // Need to pass the username from the session that triggered enforcement
    await _enforcement.CheckAndEnforceAsync(response, session.Username, session.SessionId);
}
```

### Fix 2: Use Correct loginctl Commands

```csharp
private async Task LogoutUserAsync(string username)
{
    var process = Process.Start(new ProcessStartInfo
    {
        FileName = "loginctl",
        Arguments = $"terminate-user {username}",
        UseShellExecute = false
    });
    // ...
}

private async Task LockSessionAsync(string sessionId)
{
    var process = Process.Start(new ProcessStartInfo
    {
        FileName = "loginctl",
        Arguments = $"lock-session {sessionId}",
        UseShellExecute = false
    });
    // ...
}
```

**Security note:** `username` comes from `loginctl list-sessions` output (system-controlled), not user input, so command injection risk is minimal. Still, validate that username matches `^[a-z_][a-z0-9_-]*$` as a precaution.

### Fix 3: Add SDDM to Session Filter

```csharp
if (username == "root" || username == "gdm" || username == "lightdm" || username == "sddm")
    continue;
```

### Fix 4: Add Exit Code Logging

Currently the exit code of `loginctl` is not checked after `WaitForExitAsync()`. Add logging so failures are visible:

```csharp
await process.WaitForExitAsync();
if (process.ExitCode != 0)
{
    _logger.LogError("loginctl terminate-user exited with code {ExitCode}", process.ExitCode);
}
```

### Fix 5: Consider KDE-Specific Session Termination Fallback

While `loginctl terminate-user` should work for KDE Plasma sessions (KDE uses systemd-logind integration), some KDE configurations may not terminate cleanly. Consider a fallback chain:

1. `loginctl terminate-user {username}` (primary -- systemd)
2. `qdbus org.kde.Shutdown /Shutdown org.kde.Shutdown.logout` (KDE-specific D-Bus)
3. `pkill -KILL -u {username}` (nuclear option -- kills all user processes)

## Architecture Issue: Enforcement Loop Design

The current `ProcessTickAsync()` processes usage records in bulk but enforcement is triggered from a single `UsageReportResponse`. When multiple users are active, only the last user's response triggers enforcement. The enforcement loop should iterate per-user:

```
For each active session:
  1. Record time for session
  2. Submit usage for THIS user
  3. Check enforcement response for THIS user
  4. If enforcement needed, terminate THIS user's session
```

Currently the loop records all sessions first, then submits all usage, then enforces based on a single response -- losing the user-to-response association.

## Verification Steps

After applying fixes, verify on Kubuntu by:

1. Check service logs: `journalctl -u parental-control-client -f`
2. Confirm loginctl can see user sessions: `loginctl list-sessions`
3. Manually test: `loginctl terminate-user <testuser>`
4. Set a short time limit and verify automatic logoff triggers
5. Confirm SDDM session is filtered from active sessions

## Files Requiring Changes

| File | Changes |
|------|---------|
| `src/ParentalControl.Client/Services/EnforcementEngine.cs` | Add username/sessionId params, fix loginctl commands, add exit code checks |
| `src/ParentalControl.Client/ParentalControlWorker.cs` | Pass session info through enforcement chain |
| `src/ParentalControl.Client/Services/SessionMonitor.cs` | Add `sddm` to system user filter |
| `src/ParentalControl.Shared/DTOs/Responses.cs` | Potentially add username to `UsageReportResponse` if server-side association is preferred |
