# Implementation Instructions (round 2) — fixes from the post-implementation review

Audience: Claude Sonnet, working in this repository.
Source: `work/fable-review-2.md` (finding numbers below, e.g. "#2", refer to that file). Round-1 context: `work/fable-instructions.md`, `work/sonet-progress.md`.

## Progress tracker

Detailed log goes in `work/sonet-progress2.md`. Update these checkboxes as phases complete.

- [x] **Phase A — Deploy blockers** (#1 Npgsql Kind crash, #2 zero-minute window claiming, #3 offline-backlog loss). 149/149 WebService tests pass.
- [x] **Phase B — Silently-stops-working fixes** (#5 loginctl timeouts, #6 Windows MachineGuid, #8 ladder cancellation, #9 wiped-DB recovery). 182/182 tests pass.
- [x] **Phase C — Hardening batch** (#4 suppression transaction, #7 throttle spoofing/pruning, pgadmin binding, Windows ACLs). 182/182 tests pass.
- [x] **Phase D — Cleanups** (cache lock/fsync/dead state, systemd unit consolidation, cosmetics). 178/178 tests pass after clean rebuild.
- [x] **Finalize** (CHANGELOG `[1.68.1]`, version bump, INSTALLATION.md updates). All phases A-D complete. 178/178 tests pass, solution builds clean.

## Ground rules (unchanged from round 1, plus one new)

- Product goals from `work/fable-instructions.md` still govern: registration never refused, zero client setup, wall-clock shared budgets, no maintenance. Nothing below may regress those.
- After each phase: `dotnet build ParentalControl.sln` and `dotnet test ParentalControl.sln` must pass. Client-logic fixes land in **both** `ParentalControl.Client` and `ParentalControl.Client.Windows` where applicable (each item below says which).
- **New rule — the InMemory blind spot:** findings #1 and #4 exist precisely because the test suite runs on EF's InMemory provider, which does not enforce `DateTime.Kind` for `timestamptz` columns and does not support transactions/isolation. When your fix involves either, (a) add a code comment at the site warning that InMemory can't catch regressions there, and (b) never write a `DateTime` whose `Kind` isn't `Utc` to any entity datetime property, anywhere. Grep for `DateTime.MinValue|DateTime.MaxValue|new DateTime(` in `src/` when you're done with Phase A and verify every hit that flows into an entity is Kind-safe.
- Keep changes surgical; match existing style; locate code by quoted content, not line numbers.

---

## Phase A — Deploy blockers (all in `ClientController.ReportUsage`, do as one coherent edit)

These three findings all live in the same ~40-line block. Fix them together, because the final shape of the counting logic depends on all three. Target end-state for the block (adapt names to the file, keep the existing comments where still true, update where semantics change):

```csharp
var date = _clock.ToLocalDate(request.Timestamp);
var now = _clock.UtcNow;

var allUserIds = await _userResolution.GetAllUserIdsInGroupAsync(primaryUser.Id);

// "Live" = this report describes activity happening right now (normal 60s tick).
// A report whose timestamp is older than the concurrency window is a backlog flush
// (client was offline / server was down) describing PAST windows -- it must neither
// be suppressed by, nor claim, the CURRENT window, or a machine catching up loses
// its minutes permanently (the client marks them synced on any 200). (#3)
var isLive = now - request.Timestamp <= ConcurrentUsageWindow;

var recentUsage = await _context.TimeUsage
    .Where(u => allUserIds.Contains(u.UserId) && u.UsageDate == date && u.ComputerId != request.ComputerId)
    .Where(u => u.LastUpdated >= now.Subtract(ConcurrentUsageWindow))
    .AnyAsync();

var suppressed = isLive && recentUsage;

var usage = await _context.TimeUsage
    .FirstOrDefaultAsync(u => u.UserId == reportedUserId && u.ComputerId == request.ComputerId && u.UsageDate == date);

if (usage == null)
{
    usage = new TimeUsage
    {
        UserId = reportedUserId,
        ComputerId = request.ComputerId,
        UsageDate = date,
        SessionId = request.SessionId,
        // Not DateTime.MinValue: Kind=Unspecified throws in Npgsql when written to a
        // timestamptz column (the InMemory test provider does NOT catch this). (#1)
        LastUpdated = DateTime.UnixEpoch
    };
    _context.TimeUsage.Add(usage);
}

// Count and claim only when real minutes were reported. A zero-active report (locked
// session tick, new-session probe, startup user sync) must NEVER claim the window --
// otherwise a second machine left logged-in-but-locked claims every window with
// 0-minute reports and the actively-used machine's real minutes are suppressed
// forever, i.e. unlimited time. (#2)
if (!suppressed && request.MinutesActive > 0)
{
    usage.MinutesUsed += request.MinutesActive;
    // Claim the current wall-clock window only for live activity; a backlog flush
    // counts its minutes but represents the past, so it must not suppress another
    // machine's *current* minute. (#3)
    if (isLive)
        usage.LastUpdated = now;
}

if (request.SessionId.HasValue)
{
    var session = await _context.Sessions.FindAsync(request.SessionId.Value);
    if (session != null)
    {
        if (!suppressed && request.MinutesActive > 0)
        {
            session.ActiveMinutes += request.MinutesActive;
        }
        session.IdleMinutes += request.MinutesIdle;
        session.IsActive = request.IsSessionActive;
        session.UpdatedAt = DateTime.UtcNow;
    }
}
```

Notes on correctness you must preserve:

- **`DateTime.UnixEpoch`**, not `SpecifyKind(MinValue, Utc)` — UnixEpoch is already `Kind=Utc` and comfortably inside PostgreSQL's `timestamptz` range, and reads as "never claimed" at a glance.
- The `isLive` comparison uses the client-supplied `request.Timestamp`. That's client-controlled, but the client is ours and the failure mode of a skewed clock is no worse than today's behavior. Add that as a comment.
- Clock skew edge: if `request.Timestamp` is slightly in the *future*, `now - request.Timestamp` is negative → still `<= window` → live. Correct; no extra handling needed.
- Do **not** change the `recentUsage` query itself, the alias-group filter, or the 90s window value.

### A tests (add to `ClientAliasIntegrationTests.cs`, using the existing `MutableTestClock` pattern)

1. **Locked-machine starvation (regression for #2, the important one):** machine B reports `MinutesActive: 0, MinutesIdle: 1` every 60s starting at t=0; machine A reports `MinutesActive: 1` every 60s starting at t=30 (so B always reports first). After 10 simulated minutes, the user's total `MinutesUsed` must be **10** (every one of A's minutes counted; B's zero reports claimed nothing).
2. **Zero probe doesn't suppress:** a single `MinutesActive: 0` report from B, followed 30s later by a `MinutesActive: 1` report from A → A's minute counts.
3. **Backlog flush counts (regression for #3):** A reports 1 live minute; 30s later B submits `MinutesActive: 30` with a `Timestamp` 6 hours old → all 30 minutes counted (total 31), and B's flush must **not** suppress A's next live minute 30s after that (total 32).
4. **Live concurrency still suppressed:** the existing alternating-report test must still pass unchanged (10 wall-clock minutes from two live machines → 10 counted).
5. Verify the existing suite still passes — several existing tests submit multi-minute reports with `DateTime.UtcNow` timestamps; those are live and unaffected.

Also add the grep sweep from the ground rules (no non-UTC-Kind DateTime flowing into entities) and note the result in the progress log.

---

## Phase B — Silently-stops-working fixes

### B.1 Timeouts on every external process call in `SystemdSessionMonitor` (#5) — Linux client

`SessionMonitor.cs` runs `loginctl` three times per tick (`GetActiveSessionsAsync`, twice in `IsSessionIdleAsync`) with bare `WaitForExitAsync()` — no timeout, no kill. A wedged logind/D-Bus hangs the whole tick loop forever: no tracking, no enforcement, until a human restarts the service.

- Create `Services/ProcessRunner.cs` in `ParentalControl.Client` with one static method:
  `static async Task<(bool Ok, string StdOut)> RunAsync(ProcessStartInfo psi, TimeSpan timeout, ILogger logger)` — start, read stdout, `WaitForExitAsync` under a `CancellationTokenSource(timeout)`; on timeout `Kill(entireProcessTree: true)`, log a warning, return `(false, "")`; nonzero exit → `(false, capturedOutput)`; any exception → `(false, "")`. Read stdout **before** waiting for exit (or via `ReadToEndAsync` task started first) so a chatty process can't deadlock on a full pipe.
- Use it for all three `loginctl` call sites with a 10-second timeout. Failure semantics (must match today's): `GetActiveSessionsAsync` returns an empty list (an empty tick self-heals next tick); `IsSessionIdleAsync` returns `false` (count time — never give free time because a query failed).
- Optionally migrate `EnforcementEngine`'s private `RunProcessWithTimeoutAsync` to delegate to `ProcessRunner` **only if** the change is purely mechanical (same timeouts, same logging); if anything about it isn't a drop-in, leave the engine untouched — it works and Phase 4 round 1 already hardened it.
- Windows client: `WindowsSessionMonitor` uses WMI/SystemEvents, not child processes — verify, and if (as expected) there's nothing to change, say so in the progress log rather than inventing work.

Test: a unit test that calls `ProcessRunner.RunAsync` with `FileName = "/bin/sleep", Arguments = "30"` and a 1-second timeout and asserts it returns `(false, _)` in under ~5s; plus one with a nonexistent binary asserting `(false, _)` without throwing.

### B.2 Stable Windows machine identity (#6) — Windows client only

`ServerSyncService` (Windows) uses `machineId = $"{hostname}-WIN"`. Rename the PC → new identity; two same-named PCs → one shared identity and API key. Replace with the OS's stable ID:

```csharp
private static string GetMachineId()
{
    try
    {
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        var guid = key?.GetValue("MachineGuid") as string;
        if (!string.IsNullOrWhiteSpace(guid))
            return $"{guid}-WIN";
    }
    catch { /* fall through */ }
    // Legacy fallback -- also what pre-upgrade clients registered as.
    return $"{Environment.MachineName}-WIN";
}
```

Migration consequence (verify, then document in CHANGELOG): on first startup after upgrade the machine registers under the new MachineId → a **new** Computer row and API key; the old row goes stale. This is harmless for budgets — daily/weekly totals sum `TimeUsage` per *user* across computers — but the old computer row lingers in the UI. Say exactly that in the changelog entry. The `Microsoft.Win32.Registry` API is available on `net8.0-windows` without extra packages (verify it compiles; if the analyzer complains about platform checks, guard with `OperatingSystem.IsWindows()`).

### B.3 Recovery from a wiped/restored server DB (#9) — both clients

Today: server DB restored from backup (computer rows gone) while `RequireClientApiKey=false` → client posts usage with its stored `ComputerId` → FK violation → 500 → retries forever; only a service restart re-registers. Fix in both workers:

- Track consecutive submit failures: increment a counter each time `SubmitUsageAsync` returns null while there were pending records; reset it to 0 on any success.
- When the counter reaches **10** (≈10 minutes), set `_registered = false` and reset the counter, so the next tick re-runs the idempotent `RegisterComputerAsync` → fresh Computer row → self-healed. While genuinely offline this just adds one cheap failed register attempt per tick — acceptable.
- Add a comment explaining *why* (the FK-violation scenario), otherwise this looks like cargo-cult retry logic to the next reader.

No new test required (would need a fake sync service returning a null sequence — add one only if trivial with the existing mocks; otherwise log the manual reasoning).

### B.4 Cancel the logout ladder at deadline instead of abandoning it (#8) — Linux client

`EnforcementEngine.LogoutUserAsync` races the ladder against `Task.Delay(LogoutDeadline)`; on deadline the ladder task keeps running detached (can keep escalating after the fallback, exceptions unobserved, `_logoutInProgress` released while it still runs). Restructure:

```csharp
using var cts = new CancellationTokenSource(LogoutDeadline);
try
{
    await RunLogoutLadderAsync(username, sessionId, cts.Token);
    return;
}
catch (OperationCanceledException)
{
    _logger.LogCritical(...deadline message...);
}
// deadline fallback: pkill -KILL, then the UserStillLoggedIn critical check (unchanged)
```

- Thread `CancellationToken` through `RunLogoutLadderAsync`: pass it to every `Task.Delay` inside the ladder and call `token.ThrowIfCancellationRequested()` between steps. That's sufficient — the per-call process timeouts (≤20s) bound each step, so cancellation is observed within one step's timeout of the deadline; you do **not** need to plumb the token into `RunProcessWithTimeoutAsync` itself.
- Net effect: no zombie ladder, exceptions observed, `_logoutInProgress` released only when work has actually stopped. The existing "returns in under 2s" tests must still pass (dispatch is still fire-and-forget via `TriggerLogout` — don't touch that).

Windows: `LogoffUser` is a single non-blocking P/Invoke; nothing to do (confirmed in round 1).

---

## Phase C — Hardening batch

### C.1 Serializable transaction around the suppression check (#4) — server

Two same-instant reports can both read "no recent usage" and both count. Wrap the section of `ReportUsage` from the `recentUsage` query through `SaveChangesAsync` in a serializable transaction:

- Guard for the test provider: `if (_context.Database.IsRelational())` use `await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)`; otherwise run without a transaction (InMemory supports neither transactions nor isolation levels — add the blind-spot comment).
- On `DbUpdateException`/`PostgresException` with SqlState `40001` (serialization failure): retry the whole check-and-count section **once** (clear the change tracker or re-fetch entities before retrying — stale tracked entities are the classic bug here; simplest is to extract the section into a private method and re-invoke it on a fresh scope of queries). If the retry also fails, let it propagate — the client already treats a 500 as "offline this tick" and retries next tick, which is itself a correct outcome.
- Keep `EnsureUserExistsAsync` and the computer `LastSeenAt` update **outside** the transaction.
- Structure it so the InMemory path and the relational path share the same counting code — do not fork the business logic, only the transaction wrapper.

### C.2 Login-throttle spoofing and unbounded state (#7) — server + docs

- `AuthService`: prune `FailuresByIp` — on each `RecordLoginFailure`, if the dictionary exceeds ~1000 entries, remove entries whose `LockedUntil` has passed and whose count is 0-ish; simplest correct version: store a `LastActivity` timestamp per record and drop records idle for >1 hour during the same sweep. Keep it a few lines; this is a home server, not a rate-limiting framework.
- `INSTALLATION.md`, in the settings table row for `TrustForwardedHeaders`, add: enabling it requires that clients cannot reach the web service except through nginx — when nginx runs on the same host, change the compose port mapping to `127.0.0.1:8081:80`; otherwise firewall :8081. Without that, anyone on the LAN can spoof `X-Forwarded-For` and bypass the login throttle.

### C.3 pgadmin exposure — docker-compose

Change the pgadmin port mapping to `"127.0.0.1:8082:80"` with a one-line comment ("admin tool — reach it via SSH tunnel or from the server itself; not exposed to the LAN"). Don't remove the service; the parent may use it.

### C.4 Windows data-directory ACLs — Windows installer

In `install-windows-client.ps1`, after creating `$dataPath`, restrict it so only SYSTEM and Administrators have access (the service runs as SYSTEM; the tray/UI warning exe doesn't read ProgramData):

```powershell
icacls $dataPath /inheritance:r /grant "SYSTEM:(OI)(CI)F" /grant "Administrators:(OI)(CI)F" | Out-Null
```

Verify no user-context component reads that directory first (grep the Windows UI/tray projects for `ProgramData`); if something does, grant that principal read and note it.

---

## Phase D — Cleanups (cheap; do all unless one turns out to be non-trivial)

1. **`LocalCache` read paths** (both platforms): take the same `_lock` in `GetPendingRecordsAsync` / `GetLastKnownLimitsAsync` / `GetTodayUsageAsync` (make them async-await the semaphore). The worker is single-threaded today, but the class exposes a lock and doesn't honor it on reads — either honor it (preferred, trivial) or document the single-threaded contract loudly.
2. **fsync before rename** (both platforms): in `SaveLocked`, call `stream.Flush(flushToDisk: true)` before disposing the temp-file stream, so a power loss can't leave the rename durable but the data not.
3. **Remove dead `_dailyUsage`** (both platforms): nothing in production reads `GetTodayUsageAsync` since the round-1 offline-math fix. Remove the dictionary, the interface member, its persistence field, and the tests that exercised it (System.Text.Json ignores the now-unknown `DailyUsage` property when loading an old cache file — verify with a quick test or manual check, then delete the property). If you find a live caller I missed, stop and leave it, noting where.
4. **Consolidate the systemd unit**: `build-deb.sh` embeds its own (unhardened) unit heredoc. Change it to copy `scripts/parental-control-client.service` (the canonical, hardened one with `ProtectSystem=strict` + correct `ReadWritePaths`) into the package instead. Test the result by inspecting the built package if `dpkg-deb` is available, or at minimum eyeball the copied file path logic. Leave `install-linux-client.sh`'s heredoc alone unless the same copy trick is trivially applicable (it downloads from GitHub releases; it may not have the repo checkout — if so, just sync its heredoc content to match the canonical unit).
5. **`build-deb.sh` cosmetic**: the postinst says `to ${VERSION}` inside a single-quoted heredoc, so it prints empty at runtime. Change the message to `"Upgrading from version $2..."` (drop the target-version half) or split the heredoc quoting.
6. **Comment-only**: in both `SubmitUsageAsync` implementations, note that grouping is by UTC date while the server buckets by household-local date, so a batch spanning local-but-not-UTC midnight lands on one local date — known ±1-2h edge for offline batches, accepted.

---

## Finalize

- CHANGELOG entry `[1.68.1]` (fixes only — lead with #1/#2/#3 in plain language: "fixed a crash on suppressed usage reports against PostgreSQL", "fixed: a locked second computer could prevent the active computer's time from counting", "fixed: usage recorded while the server was unreachable could be lost if another computer was active when it synced"), bump `ParentalControl.WebService.csproj` `Version`/`InformationalVersion` to `1.68.1`.
- `INSTALLATION.md` edits from C.2/C.3.
- Full solution build + test green; state the final counts in `work/sonet-progress2.md`.
- Do not tag/commit/push unless the user asks.

## Explicitly out of scope (unchanged decisions)

- Phase 6 client unification and nullable limits (#14) stay deferred — user decisions from round 1.
- The captive-HttpClient/DNS note and the anonymous `time-status` endpoint are accepted as-is.
- No enrollment tokens, no registration gating — ever (product goal 2).
