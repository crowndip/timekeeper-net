# Sonnet Implementation Progress (round 2)

Tracking detailed progress implementing `work/fable-instructions2.md`. High-level status lives in that file's checkboxes; this file has the blow-by-blow.

## Status legend
- [ ] not started
- [~] in progress
- [x] done

---

## Phase A: Deploy blockers
- [x] Fix ReportUsage block (#1 Npgsql Kind, #2 zero-minute claiming, #3 offline-backlog loss)
- [x] Regression tests (5 scenarios)
- [x] Grep sweep for non-UTC-Kind DateTime into entities

## Phase B: Silently-stops-working
- [x] B.1 ProcessRunner + loginctl timeouts
- [x] B.2 Windows MachineGuid identity
- [x] B.3 Wiped-DB recovery (both clients)
- [x] B.4 Logout ladder cancellation

## Phase C: Hardening batch
- [x] C.1 Serializable transaction around suppression check
- [x] C.2 Throttle pruning + TrustForwardedHeaders docs
- [x] C.3 pgadmin binding
- [x] C.4 Windows ACLs

## Phase D: Cleanups
- [x] D.1 LocalCache read locking
- [x] D.2 fsync before rename
- [x] D.3 Remove dead _dailyUsage
- [x] D.4 Consolidate systemd unit
- [x] D.5 build-deb.sh cosmetic
- [x] D.6 Comment on UTC/local date grouping mismatch

## Finalize
- [x] CHANGELOG 1.68.1 + version bump
- [x] INSTALLATION.md edits (done during Phase C.2; verified no other doc references needed updating for C.3)
- [x] Final build+test

---

## Log

### Phase A — complete (2026-07-13)

Rewrote the concurrency block in `ClientController.ReportUsage` as one coherent edit, exactly per the instructions' target end-state:

- **#1**: `LastUpdated = DateTime.MinValue` → `DateTime.UnixEpoch` on new-row creation. `MinValue` has `Kind=Unspecified`; Npgsql throws writing that to a `timestamptz` column (confirmed: no `EnableLegacyTimestampBehavior` anywhere in the codebase, so Npgsql 8's strict Kind enforcement applies). `UnixEpoch` is already `Kind=Utc`.
- **#2**: added `suppressed = isLive && recentUsage`, and gated both the minute-count and the `LastUpdated` claim on `!suppressed && request.MinutesActive > 0`. A locked machine's `MinutesActive: 0` idle-tick reports can no longer claim the concurrency window and starve an actively-used second machine.
- **#3**: added `isLive = now - request.Timestamp <= ConcurrentUsageWindow`. Suppression (`suppressed`) now only applies to live reports; a backlog flush (old timestamp) always counts its minutes in full but never claims the current window via `if (isLive) usage.LastUpdated = now;` — so it can't suppress another machine's live activity, and its own minutes are never silently dropped.
- Left the `recentUsage` query itself, the alias-group filter, and the 90s `ConcurrentUsageWindow` value untouched, as instructed.

**Grep sweep**: `grep -rn "DateTime.MinValue\|DateTime.MaxValue\|new DateTime(" src/` — only hit is the explanatory comment left at the fix site. No other non-UTC-Kind `DateTime` construction flows into an entity anywhere in `src/`. Also checked for stray `DateTime.Now` (Kind=Local) in the WebService project — none.

**Tests** added to `ClientAliasIntegrationTests.cs` (using the existing `MutableTestClock` pattern from round 1):
1. `ConcurrentUsage_LockedMachineZeroReports_DoesNotStarveActiveMachine` — locked machine B reports 0-active every 60s, active machine A reports 1-active every 60s offset 30s after B, for 10 simulated minutes → total counted = 10 (all of A's, none of B's, and critically A was never suppressed).
2. `ConcurrentUsage_SingleZeroProbe_DoesNotSuppressFollowingActiveReport` — one 0-active probe from B, then a 1-active report from A 30s later → total = 1.
3. `ConcurrentUsage_OldBacklogFlush_CountsInFullAndDoesNotSuppressLiveActivity` — A reports 1 live minute; B flushes 30 minutes timestamped 6 hours old 30s later (counts in full, total 31); A's next live minute 30s after that still counts (total 32, proving B's backlog flush didn't claim the window).
4. Existing `ConcurrentUsage_TwoComputers_AlternatingReports_CountsWallClockTimeOnce` (two *live* machines alternating) still passes unchanged, confirming live-concurrency suppression is intact.
5. Full existing suite re-run before and after — 146 passed before, 149 after (146 + 3 new; the 4th point above is an existing test, not a new one).

Used a fixed `new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc)` start time (noon) for the new tests instead of `DateTime.UtcNow`, so a 6-hour-old backlog timestamp can never accidentally cross a local-date boundary and complicate the assertion (the sum query doesn't filter by date, so this wasn't strictly necessary, but it removes a source of test flakiness/confusion).

**149/149 WebService tests pass.** Solution builds clean.

### Phase B — complete (2026-07-13)

**B.1 (#5)** — new `Services/ProcessRunner.cs` (Linux client): static `RunAsync(ProcessStartInfo, TimeSpan, ILogger)` returning `(bool Ok, string StdOut)`; starts stdout reading immediately (before waiting for exit) so a chatty process can't deadlock on a full pipe; on timeout kills the whole process tree and returns `(false, "")`; nonzero exit returns `(false, capturedOutput)`; any exception returns `(false, "")`. Wired into all three `loginctl` call sites in `SystemdSessionMonitor` with a 10s timeout each. Failure semantics preserved exactly as specified: `GetActiveSessionsAsync` returns an empty list on failure (self-heals next tick); `IsSessionIdleAsync` returns `false` on failure (counts time — never grants free time because a status query failed).
- Did **not** migrate `EnforcementEngine.RunProcessWithTimeoutAsync` to delegate to `ProcessRunner`: its return type (`Task<bool>`) doesn't match `ProcessRunner`'s `(bool, string)` tuple, and 8 call sites would need touching — not a pure drop-in per the instructions' explicit guidance, so left untouched (it already works and was hardened in round 1).
- Verified `WindowsSessionMonitor` uses WMI (`ManagementObjectSearcher`) and `SystemEvents`, not child processes (`grep -n "Process\.\|ProcessStartInfo"` → no hits) — nothing to change there.
- Tests: `ProcessRunnerTests.cs` — a `/bin/sleep 30` against a 1s timeout returns `(false, _)` within 5s; a nonexistent binary returns `(false, _)` without throwing; a normal `/bin/echo hello` returns `(true, "hello...")` as a sanity check.

**B.2 (#6)** — Windows `ServerSyncService.GetMachineId()` now reads `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid` (stable across hostname renames and reinstalls), falling back to the old `{hostname}-WIN` scheme only if the registry read fails. Compiles clean on `net8.0-windows` with no `OperatingSystem.IsWindows()` guard needed (the project already only targets that TFM, so the platform-compat analyzer doesn't require one). Migration consequence (documented in progress log + will go in CHANGELOG): existing installs will register under a *new* MachineId on first startup after upgrade, creating a new Computer row and losing the old one's `LastSeenAt`/hostname association in the admin UI — harmless for budgets, since daily/weekly totals sum `TimeUsage` per user across computers, not per computer.

**B.3 (#9)** — both workers now track `_consecutiveSyncFailures`, incremented once per tick whenever any `SubmitUsageAsync` call for a pending-usage group returns null, reset to 0 on any successful submission. At 10 consecutive failed ticks (~10 minutes), the worker sets `_registered = false`, which makes the next tick re-run the idempotent `RegisterComputerAsync()` — self-healing a wiped/restored server database without a service restart. No test added: there's no existing test harness for either `ParentalControlWorker` (a `BackgroundService` with five constructor dependencies to mock), and building one from scratch for a single counter isn't "trivial" per the instructions' own bar — logged the reasoning here instead of inventing a disproportionate test rig.

**B.4 (#8)** — `EnforcementEngine.LogoutUserAsync` no longer races the ladder against `Task.Delay` via `Task.WhenAny` (which let the loser keep running detached, potentially escalating past the deadline, releasing `_logoutInProgress` while still active, and swallowing any exception from the abandoned task). Restructured to run the ladder under a `CancellationTokenSource(LogoutDeadline)` and catch `OperationCanceledException` to trigger the same SIGKILL fallback. `RunLogoutLadderAsync` now takes a `CancellationToken`, threaded into each `Task.Delay` call and checked via `token.ThrowIfCancellationRequested()` between steps 1→2, 2→3, and 3→4 — sufficient to observe the deadline within one step's own timeout (≤20s) of it actually expiring, without needing to plumb the token into `RunProcessWithTimeoutAsync` itself (per instructions). Confirmed the existing "`CheckAndEnforceAsync` returns in under 2s" tests still pass unchanged (dispatch is still fire-and-forget via `TriggerLogout`, untouched).
Windows: confirmed (again) `LogoffUser`/`LockSession` are single non-blocking P/Invoke calls — no ladder, nothing to do.

**182/182 tests pass** solution-wide (149 WebService, 19 Linux client, 12+2skip Windows client). Full solution builds clean.

### Phase C — complete (2026-07-13)

**C.1 (#4)** — extracted the counting logic from `ReportUsage` into `RecordUsageCoreAsync` (identical business logic, unchanged) and added `RecordUsageWithConcurrencyControlAsync` as the transaction wrapper:
- `_context.Database.IsRelational()` gate: InMemory (the test provider) runs `RecordUsageCoreAsync` directly with a plain `SaveChangesAsync()` and no transaction (it supports neither transactions nor isolation levels) — added a comment noting the test suite therefore cannot catch a regression in the transaction/retry plumbing itself, only in the shared counting logic.
- Relational path: `BeginTransactionAsync(IsolationLevel.Serializable)`, run the core logic, `SaveChangesAsync`, `CommitAsync`. On a caught exception that unwraps (via a new `IsSerializationFailure` helper walking `InnerException`) to a `Npgsql.PostgresException` with `SqlState == "40001"`, rolls back, detaches every tracked entity (so the retry re-queries fresh state instead of reusing stale tracked entities — the classic bug here), and retries once. A second failure is not caught (the `when (attempt < maxAttempts ...)` guard is false) and propagates, which the client already treats as "offline this tick, retry next tick" — a correct outcome for a race this rare.
- Moved `computer.LastSeenAt` update + its own `SaveChangesAsync()` to before the transactional section, per the instructions ("keep EnsureUserExistsAsync and the computer LastSeenAt update outside the transaction").
- All 149 WebService tests (including the 3 new Phase-A regression tests, which exercise the exact code path this wraps) still pass unchanged — confirms the InMemory path's behavior is untouched.

**C.2 (#7)** — `AuthService.FailureRecord` gained a `LastActivity` timestamp, updated on every `RecordLoginFailure`. After each failure, if `FailuresByIp.Count > 1000`, sweeps and removes every record whose `LastActivity` is older than 1 hour. `INSTALLATION.md`'s `TrustForwardedHeaders` section now explicitly says enabling it requires either binding the webservice port to `127.0.0.1` (nginx co-located) or firewalling it from the LAN otherwise, and that skipping this lets any LAN device spoof `X-Forwarded-For` and dodge the throttle.

**C.3** — `docker-compose.yml` pgadmin port mapping changed from `"8082:80"` to `"127.0.0.1:8082:80"`, with a comment on the default admin/admin credential.

**C.4** — `install-windows-client.ps1`: before anything is written into `$dataPath`, runs `icacls /inheritance:r` + grants `SYSTEM`/`Administrators` full control and `Users` read+execute only. Checked first (per instructions) whether any user-context component reads that directory: `ParentalControl.TrayIcon.Windows/Program.cs` does — it reads `server-url.txt`, `computer-id.txt`, `proxy-user.txt`, `proxy-pass.txt`, and `appsettings.json` directly from `C:\ProgramData\ParentalControl` to show the child their remaining time. Since ACLs apply per-directory (not per-file) here and `api-key.txt`/`cache.json` live in the same directory, they remain user-*readable* (same exposure as before this change — nothing regresses) but are no longer user-*writable*, closing the actual gap (a child editing `server-url.txt` to point at a fake server, or `cache.json` to fake cached limits, is now blocked; reading them was already possible before this fix and isn't newly introduced).

**182/182 tests pass** solution-wide (149 WebService, 19 Linux client, 12+2skip Windows client). Full solution builds clean.

### Phase D — complete (2026-07-13)

**D.1/D.2/D.3** — rewrote `LocalCache.cs` on both platforms in one pass (the three items touch the same file):
- **D.1**: `GetPendingRecordsAsync`, `GetLastKnownLimitsAsync`, `GetCachedConfigAsync` now take `_lock` like every mutating method already did, instead of reading `_records`/`_lastKnownLimits`/`_cachedConfig` unsynchronized.
- **D.2**: `SaveLocked` now calls `stream.Flush(flushToDisk: true)` on the temp-file `FileStream` before it's disposed and renamed over the real path, so a power loss can't leave the rename durable while the content wasn't actually on disk yet.
- **D.3**: confirmed via `grep -rn "GetTodayUsageAsync" src/` that nothing outside the interface/implementation itself calls it (the offline-math fix in round 1 replaced its only real caller with `records.Sum(...)`). Removed `GetTodayUsageAsync` from `ILocalCache`, `_dailyUsage`, `DailyUsageKey`, the daily-usage pruning block, and the `DailyUsage` field on the persisted-state DTO, on both platforms.
- Removed the now-invalid `GetTodayUsage_ReturnsAccumulatedMinutes` and `GetTodayUsage_IsKeyedByUsername_NotUserId` tests from both `LocalCacheTests.cs` files, replacing them with `Persistence_OldCacheFileWithDailyUsageKey_LoadsGracefully` — writes a cache file containing an old `"DailyUsage"` JSON key and confirms `InitializeAsync` doesn't throw and the fields that still exist (`LastKnownLimits`) still load correctly (`System.Text.Json` ignores unknown properties by default, so this is a real behavior guarantee, not just documentation).

**D.4** — `build-deb.sh` no longer embeds its own systemd unit heredoc; it now does `cp "${SCRIPT_DIR}/parental-control-client.service" "${PACKAGE_DIR}/etc/systemd/system/..."` (resolving `SCRIPT_DIR` via `$(dirname "${BASH_SOURCE[0]}")` so it doesn't depend on the caller's CWD). **Verified by actually running the script**: `./build-deb.sh 9.9.9-test` built a real `.deb`, and `dpkg-deb -c`/`--fsys-tarfile` confirmed the shipped unit is the canonical hardened one (`ProtectSystem=strict`, `ReadWritePaths=/var/log/parental-control /var/lib/parental-control /etc/parental-control`, `After=network-online.target`/`Wants=`) and that `DEBIAN/conffiles` correctly lists `appsettings.json`. Left `install-linux-client.sh`'s embedded heredoc untouched (it downloads from GitHub releases at install time, no local repo checkout to copy from).
  - **Incident during verification**: cleaning up my test build with `rm -rf build/` deleted 251 pre-existing **tracked** files under `build/` that I didn't realize were checked into git (an older `1.10.1` package build). Caught it via `git status` immediately after and ran `git checkout -- build/`, which fully restored the tracked state (confirmed via `git status --short build/` going from 251 changed lines to 0). No data was lost, but the `rm -rf` should have been preceded by a `git status` check — flagged to the user directly.

**D.5** — the postinst upgrade message's `${VERSION}` was inside a single-quoted (`<< 'EOF'`) heredoc, so it was never interpolated and printed empty at runtime. Changed the message to `"Upgrading from version $2..."` (dropping the unresolvable target-version half) and added a comment explaining why `$2` (postinst's own positional parameter, resolved at install time) is correct to leave un-expanded here while `${VERSION}` (the outer script's variable) was not.

**D.6** — added a comment at the `GroupBy(r => DateOnly.FromDateTime(r.Timestamp))` call in both platforms' `SubmitUsageAsync`, noting that grouping is by UTC date while the server buckets by household local date (`IClockService`), so a batch spanning local-but-not-UTC midnight can end up grouped differently than the server's own local-date bucketing -- a known, accepted ±1-2h edge case for offline batches (the server remains the source of truth for which local day a minute belongs to), not a correctness bug.

**178/178 tests pass** solution-wide after a full clean rebuild (149 WebService, 18 Linux client, 11+2skip Windows client — the Linux/Windows client counts each dropped by 1 net from Phase D's test removal-and-replacement). Solution builds clean with zero warnings on a clean rebuild (a transient `MSB3277` package-version-conflict warning appeared once, traced to stale `bin/Release` state left by the `build-deb.sh` verification run, and disappeared after `rm -rf` of all `bin`/`obj` directories followed by a fresh build).

### Finalize — complete (2026-07-13)

- Bumped `ParentalControl.WebService.csproj` `Version`/`InformationalVersion` from `1.68.0` to `1.68.1`.
- Added a `[1.68.1]` `CHANGELOG.md` entry, leading with the three deploy-blocker fixes (#1/#2/#3) described in plain, non-finding-number language as instructed, followed by every other Phase A-D fix.
- `INSTALLATION.md`: the `TrustForwardedHeaders` port-binding/firewall guidance (C.2) was already added during Phase C. Checked for any other pgadmin/8082 references needing updates (C.3) — `grep -n "8082\|pgadmin\|pgAdmin" INSTALLATION.md PORTAINER_DEPLOYMENT.md` found none, so no further doc changes needed there.
- Final full-solution build + test run: **178/178 tests pass**, 0 errors, 0 warnings beyond the pre-existing unrelated `Tmds.DBus.Protocol` NuGet advisory on `ParentalControl.Client.UI`.
- Verified `git status` shows exactly the expected changed/new files (37 modified, 11 new) and nothing else — confirms the earlier `build/` incident was fully and correctly reverted with no side effects on the rest of the working tree.

## Summary across both rounds

Round 1 (Phases 1-5, 7) delivered the initial robustness pass; round 2 (Phases A-D) fixed two deploy-blocking regressions in that work (both invisible to the InMemory-backed test suite) plus four "silently stops working" gaps and a batch of hardening/cleanup items an independent review surfaced. **178/178 automated tests pass, solution builds clean.** Phase 6 (client unification) and finding #14 (nullable time limits) remain explicitly deferred by user choice from round 1. No commits, tags, or pushes were made — all changes are in the working tree awaiting the user's review.
