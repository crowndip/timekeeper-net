# Sonnet Implementation Progress

Tracking detailed progress implementing `work/fable-instructions.md`. High-level status lives in that file's checkboxes; this file has the blow-by-blow.

## Status legend
- [ ] not started
- [~] in progress
- [x] done

---

## Phase 1: Server time-counting correctness
- [x] 1.1 Fix concurrent-usage suppression (#2)
- [x] 1.2 Fix timezone handling (#3)
- [x] 1.3 Normalize usernames everywhere (#8)
- [x] 1.4 First-contact race (#16)
- [x] 1.5 Weekly adjustments (#13)
- [ ] 1.6 Warning thresholds (#9) — client-side, deferred to Phase 2 (pairs with EnforcementEngine work)

## Phase 2: Client correctness, persistence, crash-proofing
- [x] 2.1 Key client state by username (#4)
- [x] 2.2 Fix offline remaining-time math (#5)
- [x] 2.3 Persist LocalCache to disk (#7)
- [x] 2.4 Never crash the host process (#10)
- [x] 2.5 Use persisted computer ID (#11)
- [x] 2.6 Batch usage by date (#12)
- [x] 2.7 Plug leaks
- [x] 1.6 Warning thresholds (#9) — done here (Linux); already fixed pre-existing on Windows

## Phase 3: Auth hardening
- [x] 3.1 Idempotent MachineId-keyed registration (#18)
- [x] 3.2 Require ApiKey with grace mode (#1)
- [x] 3.3 Client stores/self-heals key
- [x] 3.4 Admin API hardening (#6, #15)
- [x] 3.5 Login endpoint throttling (#17)

## Phase 4: Enforcement/logoff hang fixes
- [x] 4.1 Hard deadline structure
- [x] 4.2 Linux ladder per-desktop (MATE CLI fallback added; LXDE already correctly falls through)
- [x] 4.3 Terminal fallback
- [x] 4.4 Windows (verified — already non-blocking, no changes needed)
- [x] 4.5 Tests

## Phase 5: Remaining server cleanups
- [ ] Nullable limits (#14) — deferred, user chose to skip (see log)
- [x] Merge allowed-hours windows
- [x] GetTimeStatus alias fix
- [x] DataProtection key path
- [x] Proxy password file perms
- [x] GetAllLocalUsersAsync parse fix
- [x] ShouldEnforceAsync simplify

## Phase 6: Unify client codebases
- [ ] Deferred — asked user, chose to skip (2026-07-13). Investigation found the Windows worker's
      control flow doesn't actually mirror Linux's (enforcement decision logic lives in the worker
      itself, not a matching IEnforcementEngine), so a real unification means reconciling two
      designs, not a pure mechanical move — meaningful regression risk on a platform untestable in
      this sandbox. Both clients are independently correct and robust after Phases 1-5.

## Phase 7: Packaging/deployment/verification
- [x] Deb packaging conffiles fix + postinst message
- [x] Verified Windows installer already handles auto-start/recovery/state-preservation correctly
- [x] Documentation (INSTALLATION.md settings reference + upgrade-safety note)
- [x] CHANGELOG + version bump
- [x] Manual acceptance checklist (see log below — for the human, not automatable)

---

## Log

### Phase 1 — complete (2026-07-13)

**1.1 Concurrent-usage suppression deadlock (`ClientController.ReportUsage`)**
- Suppression check now filters on the whole alias group (`GetAllUserIdsInGroupAsync`), not just the reported user, so two alias logins on two computers correctly suppress each other.
- `LastUpdated` (and `MinutesUsed`) are now only touched when a report is actually counted (`!recentUsage`), fixing the v1.67.0 mutual-suppression deadlock where both computers refreshed each other's "recent" window forever without ever counting time.
- `session.ActiveMinutes` only accrues when the report was counted, keeping session totals consistent with `TimeUsage`.
- Suppression window widened from 60s to 90s (`ConcurrentUsageWindow`) so it's safely larger than the client's 60s report interval.
- **Found and fixed a follow-on bug during test-writing**: `TimeUsage.LastUpdated` defaults to `DateTime.UtcNow` at entity construction (model default in `Entities.cs`). A brand-new row created for a *suppressed* report was getting a fresh timestamp by default even though the "only touch LastUpdated when counted" guard skipped explicitly setting it — which could poison future suppression checks against other computers. Fixed by explicitly setting `LastUpdated = DateTime.MinValue` on construction.
- Added `IClockService.UtcNow` (wraps `DateTime.UtcNow`) so `ReportUsage` reads wall-clock time through an injectable seam — needed to deterministically unit-test suppression timing without real sleeps. Added `MutableTestClock` test double.

**1.2 Timezone handling** — new `IClockService`/`ClockService` (`src/ParentalControl.WebService/Services/ClockService.cs`):
- Reads `ParentalControl:TimeZoneId` (IANA name, e.g. `Europe/Prague`); falls back to `TimeZoneInfo.Local` if unset or invalid (logs a warning on invalid).
- `ParentalControl:FirstDayOfWeek` config, defaults to Monday.
- All UTC→local conversions for daily bucket (`DateOnly`), allowed-hours (`TimeOnly`/day-of-week), and week-start now route through this service. Updated `ClientController` (session start, usage report), `TimeCalculationService` (allowed hours, minutes-until-end, week start), `UsersController` (today/date-range defaults), and `Pages/Index.razor` (manual `TimeCalculationService` instantiation needed the new constructor param).
- **Deployment plumbing**: added `tzdata` to `docker/Dockerfile.webservice` (IANA zone names don't resolve without it on the Debian-based aspnet image) and a `TZ: ${TZ:-UTC}` env var to `docker-compose.yml` webservice so `TimeZoneInfo.Local` is correct out of the box when the parent sets `TZ` in their `.env`. Added `ParentalControl.TimeZoneId`/`FirstDayOfWeek` to `appsettings.json` (empty = use `TZ`/local).

**1.3 Username normalization** — `ValidationHelper.NormalizeUsername` (trim + lowercase invariant), applied in `ClientController.EnsureUserExistsAsync` (was already lowercasing, now shared+trims) and `UsersController.CreateUser` (previously didn't normalize at all — this was the actual split-identity bug).

**1.4 First-contact race** — `EnsureUserExistsAsync` wraps `SaveChangesAsync` in try/catch `DbUpdateException`; on conflict, detaches the failed entity and re-queries for the row the concurrent request created. Verified against EF Core's InMemory provider, which does enforce the unique index and throws `DbUpdateException` — the race is reproducible and the fix verified in a test with two separate `DbContext` instances.

**1.5 Weekly adjustments** — `CalculateTimeRemainingAsync` now sums `TimeAdjustments` over the full local week window (via `IClockService.GetWeekStart`) for the weekly branch, instead of reusing the daily-filtered `adjustments` variable.

**Tests**: extended `ClientAliasIntegrationTests.cs` (rewrote `ConcurrentUsage_MultipleAliases_AllEnforced`, which had encoded the pre-fix buggy behavior, into three tests: same-instant alias-group suppression, two-computer alternating-report wall-clock accounting via `MutableTestClock`, single-computer baseline; added mixed-case username + concurrent-first-contact tests). New `TimeZoneHandlingTests.cs` covers CET allowed-hours conversion, local-midnight day boundary, default first-day-of-week, and the weekly-adjustment cross-day fix. All existing tests updated for new constructor signatures (`TestClock.Utc` fixed-UTC clock so date math stays deterministic and unchanged for tests that predate timezone support). **125/125 WebService tests pass.** Full solution builds clean.

Deferred to Phase 2: 1.6 (warning-threshold `==` vs `<=`) lives in client `EnforcementEngine.cs`.

### Phase 2 — complete (2026-07-13)

Applied to **both** `src/ParentalControl.Client` (Linux) and `src/ParentalControl.Client.Windows`.

**2.1 Key client state by username (#4).** `ILocalCache` changed from `Guid userId` keys to `string username` keys (normalized: trim + lowercase) for `GetLastKnownLimitsAsync`/`SaveLastKnownLimitsAsync`/`GetTodayUsageAsync`. `SystemdSessionMonitor` (Linux) always reports `UserId = Guid.Empty`; the Windows client previously worked around this with an MD5-hashed-username-as-Guid trick in `ParentalControlWorker.GetUserIdFromUsername` — replaced with direct username keying (removed the now-dead method) so both clients share the same cache contract ahead of Phase 6.

**2.2 Offline math (#5).** Root cause: `lastLimits.TimeRemainingMinutes` is already net of usage as of the last successful sync; subtracting the *whole day's* usage on top double-counts. Fix used on both platforms: the `records`/`userPending` list passed into offline enforcement *is* exactly "usage since last successful sync" (only unsynced records are ever passed in, and `MarkAsSyncedAsync`/cache-limit-save happen together on success) — so summing that list's `MinutesActive` directly gives the correct subtrahend. No new cache state needed. Linux: `EnforcementEngine.CheckAndEnforceOfflineAsync`. Windows: inline in `ParentalControlWorker.ProcessTickAsync`.

**2.3 Persistence (#7).** New `LocalCache` implementation (`src/ParentalControl.Client/Services/LocalCache.cs`, mirrored in `.Windows`) persists pending records, cached config, last-known limits, and daily usage to a JSON file (Linux default `/var/lib/parental-control/cache.json`, Windows `C:\ProgramData\ParentalControl\cache.json`, overridable via `ParentalControl:CacheFilePath`). Atomic write (temp file + `File.Move(overwrite:true)`), corrupt/missing file → empty start (never throws), prunes daily-usage entries older than 14 days and pending records older than 7 days on load. `ILocalCache.InitializeAsync()` loads it; called once from each worker's `ExecuteAsync` before the tick loop.

**2.4 Crash-proofing (#10).** `ServerSyncService` constructors (both platforms) did file I/O and `new Uri(serverUrl!)` — a null/invalid URL crashed DI construction, crash-looping the service. Moved all of it into `InitializeAsync()`, called at the top of every public method (idempotent, retries every call until it succeeds). Linux `Program.cs` also had an upfront `throw new InvalidOperationException("ServerUrl is not configured")` in `ConfigureServices` — removed; the worker now retries `RegisterComputerAsync()` every tick until it succeeds instead of once at startup, so config dropped in after boot (or a server that's down at boot) self-heals with no restart needed.

**2.5 Computer ID bug (#11).** `GetConfigurationAsync` on both platforms read `_configuration["ParentalControl:ComputerId"]` (normally unset) instead of the `_computerId` field loaded from persistent storage. Fixed both.

**2.6 Batch by date (#12).** `SubmitUsageAsync` (both platforms) now groups pending records by `DateOnly.FromDateTime(r.Timestamp)` and sends one request per date, instead of stamping a whole multi-day offline batch with the first record's timestamp. All-or-nothing per call (if any date's request fails, none of the batch is marked synced, so partial-success accounting can't desync from what the server actually recorded). Returns the response from the most recent (today's) date group for live enforcement decisions. Windows previously sent one HTTP request per individual record (not batched at all, though the #12 date-mislabeling bug didn't apply there since each record already carried its own timestamp) — aligned to the same grouped-request shape as Linux for consistency ahead of Phase 6.

**2.7 Leaks.** Linux: `ParentalControlWorker._seenSessionIds` now intersected against the current tick's live session IDs every tick (`HashSet.IntersectWith`); `EnforcementEngine` gained `PruneStaleUsers(activeUsernames)`, called each tick, dropping `_warningsShown`/`_lastTimeRemaining` entries for usernames with no current session. Windows doesn't have this leak (tracks only one current console user at a time via `_currentSessionId`/single-user `_warningsShown`), so no change needed there.

**1.6/#9 warning threshold.** Linux `EnforcementEngine.CheckAndEnforceAsync` changed `==` to `<=` (matches the offline path, which already used `<=`). Windows's `ParentalControlWorker.CheckEnforcementAsync` already used `<=` — no change needed there.

**Tests**: `LocalCacheTests.cs` rewritten on both platforms (constructor now needs `ILogger`+`IConfiguration`; added persistence round-trip, corrupt-file, missing-file, and username-keying tests). New `EnforcementEngineTests.cs` (Linux) covers the offline-math fix, username-keyed lookup, and warning-threshold-skip fix (using a mocked `ILogger` to observe warnings, since the class has no other observable side effect — verified via Moq's `ILogger.Log` extension-method pattern). New `ServerSyncServiceTests.cs` (Linux) covers date-batching with a fake `HttpMessageHandler`. **151/151 tests pass** across WebService (125), Linux client (14), Windows client (12 + 2 skipped pre-existing Windows-only tests). Full solution builds clean.

Note: while writing the offline-math test, deliberately chose numbers where the *correct* result stays positive, to avoid the test path exercising `EnforcementEngine.LogoutUserAsync` (which shells out to `loginctl`/`pkill`) inside a unit test — a regression that reintroduced the bug would only be caught via the logged remaining-minutes value, not by accidentally invoking real system commands during `dotnet test`.

### Phase 3 — complete (2026-07-13)

**Pre-existing bug found and fixed while working on this phase (unrelated to auth, discovered via `dotnet ef migrations add`)**: `AppDbContextModelSnapshot.cs` had never been updated after the `20260420082000_AddUserAliases` migration — the snapshot was missing `User.PrimaryUserId`/its index/FK entirely. Generating any new migration would have silently bundled in a redundant re-add of that column, which would fail with "column already exists" on any database that already ran `AddUserAliases` (i.e. every real deployment). Fixed by manually patching the snapshot to match reality, then regenerating — the new `DropComputerHostnameUniqueIndex` migration now contains only the intended index change. Verified by inspecting the generated migration's `Up()`/`Down()` bodies before finalizing.

**3.1 Registration (#18)** — `ClientController.Register` now matches on `MachineId` only (dropped the `OR Hostname` match that let a hostname collision hijack another machine's record/ApiKey). Hostname is updated as display metadata on match but never used for lookup. Dropped the unique index on `Computer.Hostname` (migration `DropComputerHostnameUniqueIndex`) since two machines can legitimately share a hostname after a reinstall. Added the same first-contact-race handling as `EnsureUserExistsAsync` (catch `DbUpdateException`, re-query) so two computers racing to register the same brand-new `MachineId` both succeed instead of one 500ing.

**3.2 Client API key + grace mode (#1)** — New `RequireClientApiKeyAttribute` (`Filters/`), applied at the `ClientController` class level (secure-by-default for any endpoint added later) and self-exempting `Register` by action name. Validates `X-Api-Key` against the specific computer the request claims to be about:
- Route `computerId` (for `GetConfig`) checked first.
- Falls back to the new `IHasComputerId` interface (`ParentalControl.Shared.DTOs`), implemented by `UsageReportRequest`/`SessionStartRequest`.
- `SessionEndRequest` (only has `SessionId`) resolved via a `Sessions` lookup for its `ComputerId`.
- Constant-time key comparison (`CryptographicOperations.FixedTimeEquals`).
- Grace mode via `ParentalControl:RequireClientApiKey` (default `false`): missing key → warn + allow; **wrong** key → always 401, in both modes. Parent flips the setting once every client is upgraded.

**3.3 Client self-heals the key** — Both platforms' `ServerSyncService`: `_apiKey` loaded/persisted next to `computer-id` (Linux `/etc/parental-control/api-key`, mode 0600; Windows `C:\ProgramData\ParentalControl\api-key.txt`), sent via `X-Api-Key` header (`ApplyApiKeyHeader`). All outgoing requests now go through `SendWithReauthAsync`, which on a 401 clears the stored key, calls the now-idempotent registration path (`DoRegisterAsync`) to fetch a fresh key, and retries once. Covered by `RequireClientApiKeyAttributeTests.cs` server-side; client-side self-heal isn't separately unit-tested (would need an `HttpMessageHandler` sequencing two different responses) — flagged as a reasonable Phase 6 addition once the two clients are unified.

**3.4 Admin API hardening (#6, #15)** — Added `[RequireAuth]` to `UsersController.GetUsers`, `GetUser`, `GetAliases`, `GetUsageBreakdown` (confirmed via grep that none of these four are called by the Blazor UI — it queries `AppDbContext` directly server-side; only `time-status`/`adjust-time`/alias POST-DELETE are called over HTTP, and `time-status` is deliberately anonymous so a child can check their own remaining time without the parent password — left that one as-is). Did **not** add `[RequireAuth]` to `AuthController.Status` despite the original review listing it: it's the endpoint a page uses to determine whether it's authenticated, so requiring auth on it is a bootstrapping contradiction. Replaced `CreateUser([FromBody] User user)` / `UpdateUser([FromBody] User user)` (mass-assignment risk — could set `PrimaryUserId`, `CreatedAt`, etc.) with the already-defined-but-unused `CreateUserRequest` DTO and a new `UpdateUserRequest` DTO.

**3.5 Login throttling (#17)** — `AuthService.ValidatePassword` uses `CryptographicOperations.FixedTimeEquals`. Added a static per-IP failure tracker (5 failures → 60s lockout, resets on success); `AuthController.Login` checks it first (429 + `Retry-After` header) and records failures/successes, logging the source IP. Since nginx sits in front per this deployment's documented setup, added opt-in `ParentalControl:TrustForwardedHeaders` (default `false`) wiring `ForwardedHeadersOptions` in `Program.cs` — without it, `HttpContext.Connection.RemoteIpAddress` would just be nginx's own address for every client, making the per-IP throttle share one bucket for the whole household. Off by default since trusting X-Forwarded-For without an actual proxy in front lets a client spoof its own IP; enabling it is the parent's explicit statement that nginx is in place. Did not implement session-cookie regeneration on login (mentioned as "also consider" in the original review, not load-bearing, and riskier to get right without a clear win).

**Tests**: `RequireClientApiKeyAttributeTests.cs` exercises the filter directly via a hand-built `ActionExecutingContext` (no `WebApplicationFactory` — Postgres isn't reachable in this sandbox, and the full app needs a DB to start) covering: Register exemption, grace-mode missing-key allow, enforced-mode missing-key reject, wrong-key reject in both modes, correct-key-wrong-computer reject, correct-key-right-computer allow, route-based computerId resolution. `AuthServiceTests.cs` covers password validation and the lockout counter/reset/per-IP-isolation. Extended `ClientAliasIntegrationTests.cs` with idempotent-registration and hostname-collision regression tests. **168/168 tests pass** solution-wide (142 WebService, 14 Linux client, 12+2skip Windows client). Full solution builds clean.

### Phase 4 — complete (2026-07-13)

Investigated `EnforcementEngine.cs` (Linux) first and found it already far more mature than the review's finding implied: the graduated ladder (graceful D-Bus → `terminate-session` → `terminate-user` → `pkill -KILL`), per-desktop D-Bus attempts (KDE/GNOME/Cinnamon/XFCE), per-call timeouts via `RunProcessWithTimeoutAsync`, and verification-between-steps (`UserStillLoggedInAsync`) were all already implemented and already well-commented (including the KDE DRM/KMS freeze root-cause explanation). The actual "sometimes hangs" gap was structural, not in the ladder's step logic:

**4.1 Hard deadline + non-blocking dispatch (the main fix)**:
- `CheckAndEnforceAsync`/`CheckAndEnforceOfflineAsync` used to `await LogoutUserAsync(...)` **directly on the worker's tick loop** — the same loop that tracks time and syncs every other session on the machine. A slow/stuck logout attempt (worst case, summing the existing per-step timeouts, could run past 90s) blocked all of that, which is what "hangs" looked like from the outside. Fixed: enforcement dispatch is now fire-and-forget (`TriggerLogout`/`TriggerLock`, `_ = Task.Run(...)`), so `CheckAndEnforceAsync` returns almost immediately regardless of how long the OS-level logout takes.
- Added a `ConcurrentDictionary<string, byte> _logoutInProgress` keyed by username (`TryAdd`/`TryRemove`) so a still-over-limit user re-checked before the previous attempt finishes (e.g. the online and offline enforcement paths racing in the same tick) can't launch a second concurrent ladder for the same person.
- Added an overall `LogoutDeadline` (90s) wrapping the whole ladder via `Task.WhenAny(ladderTask, Task.Delay(90s))`. If the deadline wins, immediately issues a final `pkill -KILL -u {username}` and logs Critical if the user is still logged in afterward, rather than letting a stuck step run unbounded.
- `CheckAndEnforceAsync` no longer needs to be a real `async` state machine (its only awaits were the now-removed logout/lock calls) — changed to a plain method returning `Task.CompletedTask`, avoiding a CS1998 warning and matching what it actually does.

**4.2 Desktop coverage** — added MATE (`mate-session-save --logout` via `runuser`, reusing the same D-Bus-session-address plumbing the existing D-Bus attempts use, since the CLI command still needs to reach the running `mate-session` process) as a CLI fallback after the existing D-Bus attempts. Confirmed LXDE needs no explicit handling: none of the D-Bus destinations exist there, so `TryGracefulDesktopLogoutAsync` naturally returns `false` and the ladder proceeds straight to `loginctl terminate-session` — already the desired behavior (LXDE has no reliable non-interactive logout command) without extra code.

**4.3 Terminal fallback** — the deadline-triggered fallback (`pkill -KILL`) doubles as this; the existing ladder's own step 4 already had one.

**4.4 Windows — verified, no changes needed.** `WindowsEnforcementEngine.LogoffUser()`/`LockSession()` are single synchronous P/Invoke calls (`WTSLogoffSession(..., bWait: false)`, `LockWorkStation()`) — already non-blocking by construction, no multi-step ladder to hang. `ParentalControlWorker` (Windows) calls them synchronously (not awaited, since they're `void`), so there was never a blocking-the-tick-loop risk on this platform to fix.

**Deferred, deliberately**: the instructions also suggested sending SIGTERM to the user's regular applications before the graceful desktop logout attempt (so documents close first). Skipped: correctly distinguishing "user's regular apps" from "core desktop/session processes" per desktop environment is exactly the kind of thing that, done wrong, could kill the wrong process and cause the *same class* of instability this phase is fixing (c.f. the KDE DRM/KMS freeze war story already documented in the file) — and it isn't verifiable without real hardware across 4 desktop environments, which isn't available in this sandbox. Flagged for a future pass with real-machine testing.

**Tests**: `EnforcementEngineTests.cs` gained two tests asserting `CheckAndEnforceAsync` with `ShouldEnforce=true`/`"logout"` returns in under 2 seconds (both alone, and called twice back-to-back to also exercise the no-double-launch guard) — this is the direct, verifiable proof of the "doesn't hang the caller" fix. Deeper ladder-logic testing (per-desktop dispatch order, deadline-triggered fallback) would need refactoring `Process.Start` behind an injectable runner, which is exactly the kind of larger structural change earmarked for Phase 6's client unification rather than this phase. **170/170 tests pass** solution-wide (142 WebService, 16 Linux client, 12+2skip Windows client). Full solution builds clean.

### Phase 5 — complete except #14, deliberately deferred (2026-07-13)

**#14 nullable limits — asked the user, they chose to defer.** Converting the 8 daily limits + weekly limit from `int` (0 = unlimited) to `int?` (null = unlimited, 0 = truly blocked) touches the DB schema/migration, `TimeCalculationService`'s limit logic, the Profiles API DTOs, *and* `Profiles.razor`'s plain `<input type="number">` bindings (which have no "no limit" checkbox today — typing 0 already means unlimited, so real nullable semantics need a new UI affordance, not just a type change). I can't visually verify a Blazor form in this sandbox (no browser, no reachable Postgres), so this carries real regression risk for something explicitly flagged as lower-priority. Presented the tradeoff; user chose to skip it entirely rather than do a partial (schema-only, no UI) version. Left `TimeProfile.MondayLimit` etc. as plain `int` with 0 = unlimited, unchanged.

**Merge allowed-hours windows** — `TimeCalculationService.GetMinutesUntilAllowedHoursEndAsync` now merges adjacent/overlapping `AllowedHours` rows for the day (new `MergeWindows` helper: sort by start time, extend the last merged window when the next one starts at-or-before its end) before finding which merged window contains "now". Two rows `08:00-12:00` + `12:00-18:00` at 11:00 now correctly report 420 minutes remaining (until 18:00), not 60 (until the first row's own end). A genuine gap (`08:00-12:00` + `13:00-18:00`) is correctly *not* merged.

**GetTimeStatus alias fix** — `UsersController.GetTimeStatus` now resolves the primary user and sums `usedToday` over `GetAllUserIdsInGroupAsync(...)`, matching what `CalculateTimeRemainingAsync` already does internally for `timeRemaining`. Before, a user with an alias (or who *is* an alias) got two numbers in one response that could disagree.

**DataProtection key path** — `Program.cs` reads `ParentalControl:DataProtectionKeysPath` (default unchanged: `/app/keys`), so running outside the Docker image doesn't require that exact path to exist.

**Proxy password file permissions** — both `ServerSyncService.SaveProxyPass` and `Program.cs`'s `set proxy` CLI command used to `File.WriteAllText` then `chmod`, leaving the file world-readable under the default umask for the window in between. Switched to `FileStreamOptions.UnixCreateMode`, which sets the restrictive mode as part of the file-creation syscall itself — no window where it's readable by anyone but the owner. Also: `SetFileGroup`/the inline `chgrp` call now check the exit code and log/print a warning on failure (previously silently swallowed), rather than silently leaving the tray app unable to read the file with no diagnostic.

**GetAllLocalUsersAsync parse fix** — `SessionMonitor.cs` (Linux): `int.Parse(parts[2])` → `int.TryParse`, so one malformed `/etc/passwd` line no longer aborts the whole enumeration into the outer catch (losing every other local user for that tick).

**ShouldEnforceAsync simplify** — the method never actually used its `userId` parameter or needed to be async (`Task.FromResult(timeRemaining < 0)`); the misleading signature suggested per-user logic that doesn't exist. Changed `ITimeCalculationService.ShouldEnforceAsync(Guid, int)` → `ShouldEnforce(int)`, a plain synchronous method. Updated the one production call site (`ClientController.ReportUsage`) and 5 test call sites across `TimeCalculationServiceTests.cs`/`UserScenarioTests.cs` (dropped `await`, removed now-unused `userId` locals, two tests changed from `async Task` to `void` since they had no other awaits left).

**Tests**: `AllowedHoursTests.cs` gained three tests (adjacent-merge, overlapping-merge, genuine-gap-no-merge). New `UserTimeStatusTests.cs` verifies `GetTimeStatus` returns the same `usedToday`/`timeRemaining` whether queried by a primary user's ID or their alias's ID (needed a real `HttpContext`/DI setup since the controller resolves `ITimeCalculationService` via `HttpContext.RequestServices` internally, and used reflection rather than `dynamic` to read the anonymous response type — `dynamic` on an anonymous type defined in a different assembly than the test can throw `RuntimeBinderException` on the accessibility check). **174/174 tests pass** solution-wide (146 WebService, 16 Linux client, 12+2skip Windows client). Full solution builds clean.

### Phase 6 — deferred by user choice (2026-07-13)

Asked the user before starting: unifying the clients means reconciling two genuinely different designs (Linux's `EnforcementEngine` owns warning-dedup state behind `CheckAndEnforceAsync`/`CheckAndEnforceOfflineAsync`; Windows embeds that same decision logic directly in `ParentalControlWorker` and calls a much thinner `IEnforcementEngine` of `LogoffUser`/`LockSession`/`ShowWarningAsync`), not a pure mechanical file move — and it's on a platform (Windows) I can't run or test in this sandbox. User chose to skip it. Both clients are independently correct and robust after Phases 1-5; this remains a real but lower-urgency maintenance improvement for a future dedicated pass.

### Phase 7 — complete (2026-07-13)

**Packaging fixes found and applied**: while auditing `scripts/build-deb.sh` for state-preservation across upgrades, found that `/opt/parental-control/appsettings.json` is shipped as a normal (non-conffile) payload file, so a `.deb` upgrade would silently overwrite a `ServerUrl` the parent had edited directly in that file back to the `http://localhost:8080` placeholder — exactly the kind of silent breakage "no maintenance" rules out. Fixed by:
- Adding `DEBIAN/conffiles` listing that file, so dpkg treats it as user-configuration (prompts on conflict rather than silently overwriting) on upgrade.
- Rewriting the postinst "next steps" message to point at `ParentalControl.Client set server-url <url>` (persists to `/etc/parental-control/server-url`, which `ServerSyncService.LoadServerUrl()` already prefers over the appsettings fallback) instead of steering the parent toward editing appsettings.json directly.

**Verified, no changes needed**:
- `scripts/parental-control-client.service` (used by `install-client.sh`) already has `Restart=always`, `RestartSec=10`, `After=network-online.target`/`Wants=` (not `Requires=`, so the client still runs and enforces offline if the network is slow to come up), and `ReadWritePaths` already includes both `/var/lib/parental-control` (new `LocalCache` persistence) and `/etc/parental-control` (new `api-key` file) under its `ProtectSystem=strict` hardening — Phase 2/3's new files work correctly under this unit with no changes.
- `install-windows-client.ps1` already does auto-start (`start= auto`), failure-recovery (`sc.exe failure ... actions= restart/60000/restart/60000/restart/60000`), and keeps `C:\ProgramData\ParentalControl` (where `computer-id.txt`/`api-key.txt`/`server-url.txt`/`cache.json` all live) entirely separate from and untouched by the binary-copy step, so upgrades already preserve all client state.
- Noted but not consolidated: three different systemd unit definitions exist across `scripts/parental-control-client.service`, `build-deb.sh`'s embedded heredoc, and `install-linux-client.sh`'s embedded heredoc (the latter two lack the hardening directives the first one has). Functionally fine for the new files (no `ProtectSystem` restriction in the less-hardened two, so no permission issue), but is itself a "one gets updated, others drift" maintenance risk worth consolidating in a future pass — out of scope here since it wasn't broken by this work.

**Documentation**: Added a server-settings reference table to `INSTALLATION.md` (all 5 new `ParentalControl:*` settings, defaults, purpose) plus the `RequireClientApiKey` rollout procedure (check logs on every client for "API key" before flipping it to `true`), a `TZ` env var step in the Docker Compose quickstart, and a note in the Upgrading section that client state survives upgrades by design. Added a comprehensive `CHANGELOG.md` entry (`[1.68.0]`) summarizing every fix across Phases 1-5 with finding numbers, and bumped `ParentalControl.WebService.csproj`'s `Version`/`InformationalVersion` to `1.68.0`.

**Manual acceptance checklist** (for the human — real hardware/network needed, not automatable from this sandbox):
1. Fresh install on each desktop (Kubuntu, Mint LXDE/Cinnamon/MATE) + Windows: install package, `set server-url`, (re)start service → computer appears under the server's Computers view, a logged-in user auto-creates itself, time counts.
2. Two computers, one child, small limit (e.g. 10 min): use both simultaneously → both log out at ~10 wall-clock minutes total, not 20 and not never.
3. Time expiry on each desktop → session logs off within ~90s, no hang, login/lock screen reachable afterward, re-login doesn't grant a free extra minute.
4. Disconnect the server mid-session → countdown continues from cached limits, enforcement still triggers at zero; reboot the client mid-offline → cached state and pending minutes survive; reconnect → pending minutes sync to the correct dates (test spanning local midnight if possible).
5. Daily reset happens at local midnight, not UTC midnight (set `TZ`/`ParentalControl:TimeZoneId` to a non-UTC zone to see the difference clearly).
6. Upgrade an existing deb install (or reinstall via `install-linux-client.sh`) → no re-registration, no lost pending minutes, `ServerUrl` (if manually edited in `appsettings.json`) isn't silently reverted.
7. With `RequireClientApiKey: false` (default): confirm an old/keyless client still works. Flip to `true` after upgrading every client, confirm requests with the correct key still succeed and a deliberately wrong key is rejected with 401.
8. Login throttle: 5 wrong admin-password attempts from one browser → 429 for ~60s; a correct attempt from a different network path isn't affected.

## Summary across all phases

Solution builds clean; **174/174 automated tests pass** (146 WebService, 16 Linux client, 12+2 skipped Windows client — the 2 skips are pre-existing Windows-only tests that don't run on Linux CI). Phases 1-5 and 7 are complete; Phase 6 (client unification) was explicitly deferred by user choice given real behavior-reconciliation risk on an untestable platform. Item #14 (nullable time limits) within Phase 5 was also explicitly deferred by user choice given Blazor UI risk. Everything else from `work/fable-instructions.md` is implemented and tested.

