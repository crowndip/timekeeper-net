# Code Review — timekeeper-net

Reviewed: web service (controllers, services, auth, data model) and the Linux client (worker, sync, enforcement, tracking, cache). The Windows client mirrors the Linux one, so most client findings apply there too.

Overall: the architecture is sensible (server owns truth, thin clients report and enforce), the enforcement escalation ladder in `EnforcementEngine` is genuinely well thought out and well commented, and the alias/primary-user model is clean. The main problems cluster in three areas: **the client API has no authentication at all**, **the concurrent-usage fix from v1.67.0 overshoots and stops counting time entirely**, and **timezone handling makes daily resets and allowed-hours wrong for any non-UTC household**.

---

## Critical

### 1. Client API is completely unauthenticated
`src/ParentalControl.WebService/Controllers/ClientController.cs`

An `ApiKey` is generated at registration (`ClientController.cs:39`) and returned to the client, but **no endpoint ever validates it**. Anyone who can reach the server can:

- `POST /api/client/usage` — burn a child's remaining time, or conversely report nothing.
- `POST /api/client/session/end` — close arbitrary sessions.
- `GET /api/client/config/{anything}` — list all child accounts, their limits, enforcement actions, and allowed hours (`ClientController.cs:192-214`; note the `computerId` route parameter is ignored entirely).
- `POST /api/client/register` with a known/guessed hostname — the endpoint matches on `MachineId` **or** `Hostname` (`ClientController.cs:30`) and returns the *existing* computer's `ApiKey` to the caller (`ClientController.cs:53`). So even if you start validating the key later, this endpoint hands it out.

A tech-savvy child on the LAN can trivially exploit any of this (the register/usage endpoints are also how they'd discover it — the client config lives in `/etc/parental-control/`).

**Recommendation:** require the `ApiKey` (e.g. `X-Api-Key` header) on every `/api/client/*` endpoint except `register`; make `register` a one-time pairing (return the key only on first creation, or require an admin-issued enrollment token for re-registration). Match on `MachineId` only — hostname is user-controlled on the client.

### 2. Concurrent-usage suppression stops counting time altogether
`src/ParentalControl.WebService/Controllers/ClientController.cs:114-141`

The v1.67.0 double-counting fix has a mutual-suppression deadlock. `usage.LastUpdated = now` runs **unconditionally** (line 141), even when the report was suppressed. Walk through two computers reporting every 60s for the same user:

1. t=0 — Computer A reports. No recent other-computer usage → **counted**. A.LastUpdated = 0.
2. t=30 — Computer B reports. A updated 30s ago → **suppressed**, but B.LastUpdated = 30 anyway.
3. t=60 — A reports. B updated 30s ago → **suppressed**. A.LastUpdated = 60.
4. t=90 — B reports. A updated 30s ago → **suppressed**. …and so on forever.

After the first counted minute, *neither* computer's time counts as long as both keep reporting. A child can exploit this: leave a second machine (or an idle TTY box) logged in and reporting, and the primary machine's usage is never recorded. Exactly the opposite of the intent.

**Fix:** only touch `LastUpdated` when minutes were actually added:

```csharp
if (!recentUsage)
{
    usage.MinutesUsed += request.MinutesActive;
    usage.LastUpdated = now;
}
```

Then computer A keeps winning the "who counts" race and wall-clock time is recorded exactly once, which is the intended semantics.

Two related problems in the same block:

- **The check ignores aliases** (`u.UserId == reportedUserId`, line 116). If the same person is logged in on two machines under two usernames that are aliases of one primary, both count — the double-counting the feature exists to prevent. Filter on the alias group (`allUserIds`), not the reported user.
- `session.ActiveMinutes += request.MinutesActive` (line 148) runs even when the usage was suppressed as concurrent, so session totals and `TimeUsage` totals disagree.

### 3. All date/time math is UTC, but limits are conceptually local time
`ClientController.cs:111,159-160`, `TimeCalculationService.cs:85-90`, client `ServerSyncService.cs:189`, `LocalCache.cs:33,38`

The client stamps reports with `DateTime.UtcNow`, and the server derives everything from that:

- **Daily reset:** `DateOnly.FromDateTime(request.Timestamp)` means the day rolls over at UTC midnight — 1:00/2:00 a.m. for a family in CET/CEST. A child still up at 1:30 a.m. gets a fresh daily budget.
- **Allowed hours:** `IsWithinAllowedHoursAsync` compares `TimeOnly.FromDateTime(currentTime)` — a UTC time — against windows the parent entered thinking in local time. Every window is shifted by 1–2 hours depending on DST.
- **Weekly window:** same shift applies to week boundaries.

**Recommendation:** decide on one convention and apply it end-to-end. Simplest robust option: store a `TimeZoneId` per installation (or per computer) on the server, convert the report timestamp with `TimeZoneInfo.ConvertTimeFromUtc` before deriving `DateOnly`/`TimeOnly`. Sending client local time instead would also work but is fragile when client clocks/zones disagree.

### 4. Offline enforcement uses the wrong user's limits — cache is keyed by `Guid.Empty` for everyone
`SessionMonitor.cs:62` → `TimeTracker.cs:33` → `ServerSyncService.cs:105` → `EnforcementEngine.cs:70-84`

`SystemdSessionMonitor` creates every session with `UserId: Guid.Empty` ("server will determine from username"). That empty GUID then flows everywhere the client uses a user ID:

- `SaveLastKnownLimitsAsync(firstRecord.UserId, …)` caches limits under key `Guid.Empty` — for **every** user. On a shared computer, the "last known limits" are whichever user synced last.
- `CheckAndEnforceOfflineAsync` reads them back with the same empty key, so offline enforcement applies user B's remaining time to user A. A parent account's "unlimited" response cached last would give every child unlimited offline time; conversely a child's exhausted budget could lock out a sibling.
- `GetTodayUsageAsync(userId, …)` keys `_dailyUsage` the same way, merging all users' minutes into one counter.

**Fix:** key everything client-side by (normalized) username, which is the identifier the client actually has.

### 5. Offline time-remaining math over-subtracts
`EnforcementEngine.cs:80-89`

```csharp
var timeRemaining = lastLimits.TimeRemainingMinutes - todayUsage;
```

`lastLimits.TimeRemainingMinutes` is already net of usage at the moment it was cached, but `todayUsage` is the *whole day's* local usage. Example: child used 60 min, server said "30 remaining" at last sync; 10 offline minutes later, `todayUsage` = 70 → `30 − 70 = −40` → immediate logout, though 20 minutes genuinely remain.

**Fix:** track usage-since-last-successful-sync (reset the counter whenever `SaveLastKnownLimitsAsync` runs) and subtract that instead.

---

## High

### 6. Admin read endpoints have no auth
`UsersController.cs` — `GET /api/users` (line 23), `GET /api/users/{id}` (35), `time-status` (130), `aliases` (223), `usage-breakdown` (234), plus `AuthController` `status`. Only mutating endpoints carry `[RequireAuth]`. Anyone on the network can enumerate the family's accounts, emails, and per-day usage history. Unless the omission is deliberate (e.g. kids may view their own status), add `[RequireAuth]` to the reads too — and if it *is* deliberate, return a trimmed DTO instead of the raw `User` entity (it exposes `Email`, `PrimaryUserId`, `AccountType`, `IsActive`).

### 7. LocalCache is memory-only — a reboot erases pending usage and offline state
`src/ParentalControl.Client/Services/LocalCache.cs`

`_records`, `_lastKnownLimits`, `_dailyUsage` are all plain in-memory collections. Consequences:

- If the server is unreachable and the child **reboots**, all unsynced minutes vanish — server-side usage stays low, and the fresh process has no cached limits, so `CheckAndEnforceOfflineAsync` logs "cannot enforce offline" and gives up (`EnforcementEngine.cs:73-77`). Reboot-while-offline = unlimited time.
- Even in normal operation, minutes recorded between syncs are lost on service restart.

Given the class is named `LocalCache` and the docs mention offline mode, I suspect persistence was intended. Persist at least the pending records and last-known limits to `/var/lib/parental-control/` (root-owned, since the child may have local admin ambitions). Also `_dailyUsage` never prunes old dates — unbounded growth on a long-running daemon.

### 8. Username case mismatch creates split identities
`ClientController.cs:218` lowercases usernames before lookup/creation, but `UsersController.CreateUser` (line 47-66) does not — and the unique index is case-sensitive in PostgreSQL. If a parent creates "Alice" in the UI and the client reports "alice", you get **two users**, and the profile/limits attached to "Alice" never apply to the "alice" the client actually reports. Silent enforcement bypass. Normalize (or use a case-insensitive collation / `citext`) in one shared place, and validate in `CreateUser` that the stored form matches what clients will report.

### 9. Warning notifications only fire on exact equality
`EnforcementEngine.cs:55-63`

`response.TimeRemainingMinutes == warningMinutes` — but `TimeRemainingMinutes` is the server's `effectiveTimeRemaining`, which can skip values: `Math.Min(timeLimit, minutesUntilAllowedHoursEnd)` jumps when the binding constraint switches, ticks can be >1 minute if a sync was missed, and adjustments move it arbitrarily. When a value is skipped, the warning never fires. The offline path already uses `<=` (`EnforcementEngine.cs:112`); make the online path match (`<=` plus the existing "shown" set and the reset-on-increase logic you already have).

---

## Medium

### 10. `ServerSyncService` constructor can crash the host, and does I/O in a constructor
`src/ParentalControl.Client/Services/ServerSyncService.cs:29-68`

- `new Uri(serverUrl!)` at line 46 throws (`NullReferenceException`/`UriFormatException`) if no URL is configured or the persisted file is corrupt → DI construction fails → the whole worker crash-loops under systemd. Fail soft: log and enter a "not configured" state the way you already do for `_computerId`.
- Reading/writing five files in a constructor makes the class hard to test and hides failures at object-graph build time. Move to an `InitializeAsync` called from the worker.

### 11. `GetConfigurationAsync` reads the wrong computer ID
`ServerSyncService.cs:132` uses `_configuration["ParentalControl:ComputerId"]` — everything else in the class uses `_computerId` from persistent storage. If that config key isn't set (it normally isn't, since registration persists to `/etc/parental-control/computer-id`), the request goes to `/api/client/config/` and fails. Use `_computerId`.

### 12. Batched usage all lands on the first record's date
`ServerSyncService.cs:83-96` sums all pending records but stamps the request with `firstRecord.Timestamp`. After an offline stretch spanning midnight, yesterday evening's minutes are booked to yesterday's date — correct — but *today's* minutes also get booked to yesterday. Group pending records by `DateOnly` and submit one request per date.

### 13. Weekly limit uses only today's adjustments, and weeks start on Sunday
`TimeCalculationService.cs:56-64`

- `weeklyRemaining = WeeklyLimit - usedThisWeek + adjustments` reuses `adjustments`, which was filtered to `AdjustmentDate == date` (line 51). A +60 bonus granted on Monday is invisible to the weekly calculation on Tuesday. Sum the week's adjustments for the weekly branch.
- `date.AddDays(-(int)date.DayOfWeek)` starts weeks on Sunday. For a European deployment Monday is the expected reset day; at minimum make it configurable.

### 14. A daily limit of 0 means "unlimited", so you can't block a day
`TimeCalculationService.cs:41` (`if (dayLimit == 0) return int.MaxValue;`) and similarly `WeeklyLimit > 0` at line 56. Parents plausibly want "no screen time on school nights" = 0. Use a nullable limit (`null` = no limit, `0` = blocked) instead of a sentinel that conflates the two.

### 15. Mass assignment via EF entities as request bodies
`UsersController.cs:47` (`CreateUser([FromBody] User user)`) — a caller can set `PrimaryUserId`, `CreatedAt`, navigation properties, even the obsolete `IsSupervised` setter. `UpdateUser` copies fields explicitly (good) but still binds the full entity. Use small request DTOs (`CreateUserRequest { Username, FullName, Email, AccountType }`) — you already do this correctly in the client API DTOs.

### 16. `EnsureUserExistsAsync` race on first contact
`ClientController.cs:216-236` — two computers reporting the same new user simultaneously both pass the `FirstOrDefault` check; one `SaveChangesAsync` then throws on the unique index and the request 500s. Catch `DbUpdateException` and re-query, or use an upsert (`INSERT … ON CONFLICT DO NOTHING`).

### 17. Login endpoint has no throttling; password comparison isn't constant-time
`AuthController.cs:17-27`, `AuthService.cs:17-21`

- A single shared password with unlimited attempts is brute-forceable, especially by a motivated teenager with `hydra`. Add a simple failed-attempt delay/lockout (even in-memory per-IP is fine at this scale) and log failures.
- Use `CryptographicOperations.FixedTimeEquals` for the comparison; better, store a hash (even a simple PBKDF2 of the config value at startup) rather than comparing plaintext from config.
- Also consider regenerating the session cookie on login.

### 18. Registration upsert lets a hostname collision hijack a computer record
`ClientController.cs:29-50` — if a *different* machine registers with an existing hostname, the match on `Hostname` overwrites the stored `MachineId` (line 47) and the old machine's record (with its usage attribution and ApiKey) is silently taken over. Match on `MachineId` only; treat hostname as display metadata (and drop the unique index on it — two machines named `family-pc` after a reinstall is a realistic scenario).

---

## Low / cleanups

- **`_seenSessionIds`, `_warningsShown`, `_lastTimeRemaining` grow forever** (`ParentalControlWorker.cs:15`, `EnforcementEngine.cs:17-18`). systemd session IDs increment indefinitely; a daemon that runs for months slowly accumulates. Prune when sessions disappear.
- **`GetMinutesUntilAllowedHoursEndAsync` doesn't merge adjacent windows** (`TimeCalculationService.cs:111-119`). With windows 08:00–12:00 and 12:00–18:00, at 11:00 it reports 60 minutes — the client shows warnings and counts down to a cutoff that never comes. Merge contiguous/overlapping windows, or scan for the latest reachable end.
- **`ShouldEnforceAsync` ignores its `userId` parameter and needs no async** (`TimeCalculationService.cs:70`) — make it a static/plain method or fold `timeRemaining < 0` inline; the interface method suggests logic that isn't there.
- **Proxy password handling** (`ServerSyncService.cs:364-389`): the file is written *then* chmod'd — briefly world-readable under default umask. Create it with restrictive permissions first (`File.Create` + `SetUnixFileMode` before writing, or `UnixFileMode` overload of `File.WriteAllText` on .NET 8+). Also `SaveProxyPass` runs on every startup when config contains credentials, and `chgrp` shells out — `File.SetUnixFileMode`'s sibling `chown` via `System.IO.UnixFileSystemInfo` isn't available, fine, but check the exit code instead of swallowing.
- **`GetAllLocalUsersAsync` will throw on malformed `/etc/passwd` lines** (`SessionMonitor.cs:166` — `int.Parse` unguarded inside the loop; one bad line aborts the whole enumeration into the catch). Use `int.TryParse` like `GetUserUid` does.
- **`DataProtection` key path is hardcoded to `/app/keys`** (`Program.cs:59`) — crashes or silently warns outside the container. Make it configurable with `/app/keys` as the Docker default.
- **`GetTimeStatus` bypasses aliases** (`UsersController.cs:141-143`): `usedToday` sums only the requested user's rows while `timeRemaining` resolves the alias group — the two numbers in one response can disagree. Use `GetAllUserIdsInGroupAsync` for both.
- **Duplicate client codebases**: `ParentalControl.Client` and `ParentalControl.Client.Windows` duplicate `ServerSyncService`, `LocalCache`, worker logic. The platform-specific parts are really just `ISessionMonitor`/`IEnforcementEngine` — move the rest into a shared `ParentalControl.Client.Core` so fixes (like #4/#5 above) don't have to land twice.
- **`RequireAuthAttribute` is fail-closed on missing service — good** — but it resolves `AuthService` via service locator; constructor-inject via `TypeFilter`/`IFilterFactory` if you want it testable.
- **Health endpoint** (`Program.cs:127`) exposes DB reachability unauthenticated; harmless on a LAN but worth knowing.

---

## What's good

- The graduated logout ladder in `EnforcementEngine.LogoutUserAsync` (graceful D-Bus → `terminate-session` → `terminate-user` → `pkill`) with verification between steps is excellent, and the comments explaining the Kubuntu DRM/KMS freeze and the LightDM stub-service trap are exactly the kind of comments worth writing.
- Username validation before shelling out (`IsValidUsername`), process timeouts with `Kill(entireProcessTree: true)`, and reading `DBUS_SESSION_BUS_ADDRESS` from `/proc` are all careful, defensive touches.
- Enforcement-check-before-counting for new sessions (`ParentalControlWorker.cs:80-88`) closes the "free minute after re-login" hole neatly.
- Input bounds on usage reports, the alias resolution model, and keeping the reported user for audit while charging the primary user are all sound design.

## Suggested priority

1. Authenticate the client API (#1) and admin reads (#6).
2. Fix the concurrent-usage suppression (#2) — it currently defeats time tracking entirely with two machines.
3. Fix timezone handling (#3) — limits are wrong by 1–2h every day for CET users.
4. Fix the `Guid.Empty` cache keying + offline math (#4, #5) and persist the cache (#7).
5. The rest as convenient; #8 (case mismatch) is cheap and sneaky enough to do early.
