# Post-implementation review — timekeeper-net (after work/fable-instructions.md, v1.68.0)

Reviewed: everything changed since v1.67.0 (server controllers/services/filters, both clients, packaging, migration), against the goals in `work/fable-instructions.md`. Build clean, 174/174 tests pass — I re-ran them myself.

## Verdict

The implementation is faithful to the plan and most of it is genuinely good: the timezone model is consistent end-to-end, cache persistence is atomic-write with corrupt-file tolerance, the crash-proofing (no throwing constructors, retry-forever registration) is exactly right, the enforcement dispatch fix addresses the real cause of the hangs, and the two migration/packaging landmines Sonnet found along the way (model-snapshot drift, conffiles) were real and correctly fixed.

**But it is not deployable as-is.** The rewritten concurrent-usage suppression in `ClientController.ReportUsage` has two serious bugs that the test suite cannot see because tests run on EF's InMemory provider, not PostgreSQL, and because no test covers zero-minute reports. One is a production 500; the other resurrects the exact "second machine = unlimited time" exploit this whole effort was meant to kill. Both are small fixes. Details below, ordered by severity.

---

## Critical

### 1. `DateTime.MinValue` write will throw on PostgreSQL — 500s in production, invisible to tests
`ClientController.cs:180` — new `TimeUsage` rows are created with `LastUpdated = DateTime.MinValue` so a suppressed first-report-of-the-day looks stale rather than fresh. Correct idea, wrong constant: `DateTime.MinValue` has `Kind == Unspecified`, and Npgsql 8 (no `EnableLegacyTimestampBehavior` switch anywhere — verified) **throws** when writing a non-UTC-Kind `DateTime` to a `timestamp with time zone` column. So the exact scenario the row-creation path exists for — a suppressed report that creates a new row — dies in `SaveChangesAsync` with a 500.

This triggers easily: two machines boot together and `SyncAllUsersAsync` fires zero-usage reports for the same users from both; or the first report of a new local day from computer B while computer A was counted in the last 90s. The client treats the 500 as "server unavailable" and falls back to offline mode, so it self-heals eventually, but you'd see recurring 500s and delayed counting daily.

The tests pass because the InMemory provider doesn't enforce timestamp Kind. **Fix:** use `DateTime.UnixEpoch` (already `Kind=Utc`) instead of `DateTime.MinValue`. One token.

### 2. Zero-minute reports claim the suppression window — locked second machine = unlimited time again
`ClientController.cs:190-194` — the window is claimed (`usage.LastUpdated = now`) whenever `!recentUsage`, **including when `request.MinutesActive == 0`**. Zero-active reports are routine: a locked session records `active:0/idle:1` every tick, `CheckTimeRemainingAsync` sends zero probes on every new session, `SyncAllUsersAsync` at startup.

Walk through the child's discovery: log into computer B, lock the screen, walk to computer A, log in.
- B (locked) reports 0 active minutes every 60s. With no one else recently counted, B **wins the window** each time: adds 0 minutes, refreshes `LastUpdated`.
- A's real 1-minute reports always find "another computer updated <90s ago" → suppressed. Forever.

A's usage is never recorded as long as B sits locked. That is precisely the exploit the v1.67.0 fix (and this whole rework) was supposed to close — resurrected through the idle path. It's stable, repeatable, and a teenager will find it in a week.

**Fix:** only count *and only claim the window* when minutes were actually reported:
```csharp
if (!recentUsage && request.MinutesActive > 0)
{
    usage.MinutesUsed += request.MinutesActive;
    usage.LastUpdated = now;
}
```
(Keep the `session.ActiveMinutes` guard aligned.) Zero reports then never suppress anyone, and genuine concurrent usage still resolves to one winner. Add a regression test: locked machine reporting 0-active alternating with an active machine → active machine's minutes all count.

---

## High

### 3. A suppressed offline backlog is silently and permanently lost
The suppression check compares only the report's **arrival** time against other computers' `LastUpdated`; it never looks at `request.Timestamp`. Scenario: the server was down this morning while the child used computer B for 30 minutes; this evening the server is back and the child is on computer A. B's worker flushes its backlog (one request, 30 minutes, timestamped this morning) — it lands within 90s of A's live report → suppressed → 0 minutes recorded. The server still returns 200 with a normal response, so B's worker **marks the records synced** (`ParentalControlWorker.cs:157`) and they're gone. 30 real minutes vanish, in the child's favor.

This isn't exotic: "server briefly down, then every machine flushes at once" is the normal recovery pattern, and multi-machine households are the stated use case.

**Fix (simplest robust):** only apply suppression to *live* reports — `if (recentUsage && now - request.Timestamp < ConcurrentUsageWindow)`. Backlogged batches carry old timestamps and should always count; live ticks keep the one-winner semantics. (Client clock skew is a theoretical concern, but the client is yours and this fails no worse than today.)

### 4. Suppression check is a read-then-write race with no transaction
Two reports for the same alias group landing within the same few milliseconds both read `recentUsage == false` and both count — double-counting that minute. The window is one request's handling time (a few DB round trips), but client ticks are fixed 60s timers that can stay phase-aligned for hours, so "rare" can become "once a minute" for an unlucky boot alignment. Wrap the read-check-update in a serializable transaction (`_context.Database.BeginTransactionAsync(IsolationLevel.Serializable)` with a retry on serialization failure), or accept and document the ±1min/day worst case. Given goals, I'd take the transaction.

---

## Medium

### 5. `SystemdSessionMonitor` has no timeouts — a hung `loginctl` freezes the whole client
`SessionMonitor.cs:39,105,131` — three `loginctl` invocations per tick with plain `WaitForExitAsync()`, no timeout, no kill. Phase 4 moved enforcement off the tick loop, but the loop itself still blocks forever on session enumeration if logind/D-Bus wedges (which is exactly the class of system-level breakage this project keeps colliding with on Kubuntu). Result: no tracking *and* no enforcement until someone restarts the service — the opposite of zero-maintenance. `EnforcementEngine.RunProcessWithTimeoutAsync` already exists; use the same pattern here (10s timeout, kill tree, return empty list on failure — an empty tick is self-healing, a hung tick is not).

### 6. Windows machine identity is still hostname-derived
`ParentalControl.Client.Windows/Services/ServerSyncService.cs:216` — `machineId = $"{hostname}-WIN"`. The server-side fix ("match on MachineId only, hostname is metadata") is only as strong as the MachineId, and on Windows the MachineId *is* the hostname. Renaming a Windows PC silently re-registers it as a new computer (orphaning its usage history and getting a fresh budget); two Windows machines with the same name share one identity and API key. Read `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid` (stable, unique, no privileges needed) with the hostname suffix as a fallback.

### 7. `TrustForwardedHeaders` + published port = spoofable throttle
`Program.cs` clears `KnownProxies`/`KnownNetworks` when `TrustForwardedHeaders=true`, while `docker-compose.yml` still publishes `8081:80` to the LAN. Anyone who can reach :8081 directly (any device in the house) can then set `X-Forwarded-For: <random>` per request — each attempt arrives as a "new IP", fully bypassing the login throttle, and each unique spoofed IP adds a permanent entry to the static `FailuresByIp` dictionary (unbounded growth, never pruned). Two cheap fixes: document that enabling the flag requires binding the published port to the nginx host only (`127.0.0.1:8081:80` when nginx is co-located), and prune `FailuresByIp` entries whose lockout has expired.

### 8. Deadline path abandons the ladder task instead of cancelling it
`EnforcementEngine.cs:220-238` — when the 90s deadline wins `Task.WhenAny`, the ladder task keeps running detached: it can keep escalating (terminate-user, pkill) *after* the deadline fallback already ran, `_logoutInProgress` is released while the zombie is still active (a second `TriggerLogout` can overlap it), and if the abandoned task later throws, the exception is unobserved. Pass a `CancellationToken` into `RunLogoutLadderAsync` (check it between steps, pass to the process helper) and cancel at deadline; or at minimum attach a continuation that logs faults.

### 9. No recovery path from a wiped/restored server DB while in grace mode
If the server's DB is restored from backup mid-run (computer rows gone), the client keeps posting usage with its stored `ComputerId`; the `TimeUsage` FK insert fails → 500 → client retries forever. The 401→re-register self-heal only engages once `RequireClientApiKey=true`. Cheap hardening: in the worker, reset `_registered = false` after N consecutive `SubmitUsageAsync` failures so the idempotent re-registration path runs; it already fixes everything once it does.

---

## Low / notes

- **`LocalCache` reads bypass the semaphore** (`GetPendingRecordsAsync`, `GetLastKnownLimitsAsync`, `GetTodayUsageAsync`). Safe today because the worker loop is the only caller, but the lock's presence implies a guarantee it doesn't give. Either lock the reads or comment the single-threaded assumption.
- **Cache save doesn't fsync before rename** — on power loss the rename can survive while data didn't, yielding an empty/corrupt file. Handled gracefully (corrupt → start empty), so this only means losing ≤1 day of offline state in a rare event; add `stream.Flush(true)` if you want it airtight.
- **`_dailyUsage` in both LocalCaches is now dead** — nothing in production reads `GetTodayUsageAsync` since the offline-math fix. Remove it (and its persistence) or keep deliberately; dead state in a persisted file tends to confuse later.
- **Client batches by UTC date, server buckets by local date** (`SubmitUsageAsync` groups on `DateOnly.FromDateTime(r.Timestamp)`). An offline batch spanning *local* (not UTC) midnight still lands on one date. 1–2h edge for CET; only affects offline stretches; fine to leave, worth a comment.
- **pgadmin on :8082 with default `admin/admin`** in docker-compose. It has no DB connection saved by default, but it's an unauthenticated-ish admin tool on the family LAN. Remove it from the default compose or bind to localhost.
- **`build-deb.sh` postinst** prints `Upgrading from version $2 to ${VERSION}` inside a single-quoted heredoc — `${VERSION}` is empty at runtime. Cosmetic.
- **Captive HttpClient** — the singleton worker holds one transient `ServerSyncService` (typed client) forever, so `SetHandlerLifetime(5m)` never rotates the handler and a DNS change to the server address needs a client restart. Pre-existing, acceptable for a home LAN; noting for completeness.
- **Windows ProgramData ACLs** — `cache.json`/`api-key.txt` are not restricted to SYSTEM/Administrators as the instructions specified. On default ACLs a child can read them (mildly interesting) and ProgramData directory ACL nuances may allow tampering. Worth a `New-Item` + `icacls` step in the installer.
- **Three divergent systemd unit definitions** (scripts/, build-deb.sh heredoc, install-linux-client.sh heredoc) — Sonnet noted this too; consolidate eventually so hardening changes don't drift.

## What's genuinely solid

- The suppression *architecture* (claim-on-count, alias-group filter, 90s window) is right — the two critical bugs are in edge inputs, not the design.
- Timezone handling is consistent everywhere it matters, including Docker (`tzdata` + `TZ`) — this was easy to get half-right and it wasn't.
- `LocalCache` persistence, the non-throwing `InitializeAsync` pattern, retry-forever registration, and the API-key grace mode with 401 self-heal are exactly the "zero maintenance" shape: every failure mode converges back to working without human action — *except* the ones listed above.
- Catching the pre-existing model-snapshot drift (which would have broken every future migration on a real DB) and the `.deb` conffiles bug were both real saves.
- Honest deferrals with user sign-off (Phase 6, nullable limits) beat silent half-implementations.

## Recommended order

1. Fix #1 and #2 (both are a few lines in `ClientController.ReportUsage`) + regression tests for zero-minute reports and, if feasible, a Postgres-backed smoke test or at least a comment warning that InMemory hides Kind errors.
2. Fix #3 (timestamp-gated suppression) and #5 (loginctl timeouts) — the remaining "silently loses time" and "silently stops working" cases.
3. #4, #6, #8, #9 as a hardening batch.
4. The Low items opportunistically.

With 1–2 done the system is deployable; with 1–3 plus #5 it credibly meets "very robust, almost zero maintenance."
