# Implementation Instructions — timekeeper-net hardening

Audience: Claude Sonnet, working in this repository.
Source analysis: `work/fable-comments.md` (finding numbers below, e.g. "#2", refer to that file).

## Progress tracker

Detailed blow-by-blow log lives in `work/sonet-progress.md`. This is the top-level status.

- [x] **Phase 1 — Server time-counting correctness** (concurrent-usage suppression #2, timezone #3, username normalization #8, first-contact race #16, weekly adjustments #13; #9 deferred to Phase 2). 125/125 WebService tests pass.
- [x] **Phase 2 — Client correctness, persistence, crash-proofing** (#4, #5, #7, #9, #10, #11, #12, leaks) — applied to both Linux and Windows clients. 151/151 tests pass solution-wide.
- [x] **Phase 3 — Auth hardening with zero-config grace mode** (#1, #6, #15, #17, #18). Also fixed a pre-existing migration/model-snapshot drift bug found along the way. 168/168 tests pass.
- [x] **Phase 4 — Enforcement/logoff hang fixes** — non-blocking dispatch + 90s hard deadline was the core fix; ladder logic was already mature. 170/170 tests pass.
- [x] **Phase 5 — Remaining server cleanups** (#14 deferred — asked user, chose to skip given Blazor UI risk; rest done). 174/174 tests pass.
- [ ] **Phase 6 — Unify Linux/Windows client codebases** — deferred, asked user, chose to skip (real design-reconciliation risk, not a pure mechanical move; both clients already correct independently)
- [x] **Phase 7 — Packaging, deployment, verification** — found and fixed a `.deb` upgrade bug (appsettings.json silently reverted), verified Windows installer already correct, documented all new settings + acceptance checklist. See `work/sonet-progress.md` for the full checklist.

**Overall: Phases 1, 2, 3, 4, 5 (except deferred #14), and 7 complete. Phase 6 deferred by user choice. 174/174 tests pass, solution builds clean.**

## Product goals (these override any conflicting suggestion in fable-comments.md)

1. **Zero client setup.** Installing the deb (or Windows installer) and setting the server URL in the config is the *only* setup. Everything else — registration, user creation, limits discovery — is automatic.
2. **Registration must never be refused.** The server runs in a protected environment behind an nginx reverse proxy; the LAN threat model is "curious child", not "internet attacker". Do **not** implement enrollment tokens, one-time pairing, or admin approval for registration (fable-comments.md #1 suggests this — skip that part; implement the rest of #1).
3. **Minute-precision tracking.** Time is counted in whole minutes and must be accurate to the minute over a session.
4. **Correct multi-computer semantics.** A child's budget is shared wall-clock time: with 30 minutes left, running 1, 2, or 3 computers simultaneously all give exactly 30 minutes of wall-clock use. Two computers at once must not burn 2× time, and must not burn 0× time (the current v1.67.0 bug).
5. **Reliable enforcement.** When time runs out, the session must close programs and log the user off — always, on every supported desktop, without hanging. Supported: Kubuntu (KDE/SDDM), Linux Mint LXDE, Cinnamon, MATE; plus Windows.
6. **No maintenance.** The system must self-heal: survive reboots, server outages, service restarts, clock/timezone quirks, and never require the parent to SSH in and fix state.

## Ground rules

- Work in phases, in the order below. After each phase: `dotnet build ParentalControl.sln` and `dotnet test` must pass. Add/extend tests for every behavioral fix (test projects exist under `tests/`).
- Line numbers in fable-comments.md are from the review snapshot; locate code by the quoted content, not the line number, and expect drift as you edit.
- **Every fix that touches client logic must land in both `src/ParentalControl.Client` and `src/ParentalControl.Client.Windows`** (they are near-duplicates) until Phase 6 unifies them.
- Backward compatibility: an already-installed older client must keep working against the new server (see the API-key grace rule in Phase 3). Never make a server change that bricks deployed clients.
- Keep changes surgical. Do not reformat files, rename things gratuitously, or "improve" unrelated code. Match existing style.
- Update `CHANGELOG.md` per phase; bump the version once at the end.

---

## Phase 1 — Server: make time counting correct (fixes #2, #3, #8, #13, #16)

### 1.1 Fix concurrent-usage suppression (#2 — the most important fix in this document)

File: `src/ParentalControl.WebService/Controllers/ClientController.cs`, the usage-reporting block (~lines 114–148).

Current bug: `usage.LastUpdated = now` runs even when a report is suppressed as concurrent, so two computers reporting on offset schedules suppress *each other* forever and no time is counted after the first minute.

Required semantics: **for a given alias group, one wall-clock minute is counted at most once, no matter how many computers report it.**

Implement:

1. The "recent usage from another computer" check must filter on the **whole alias group** (`allUserIds` — the resolved primary + aliases), not just the reported `UserId`. Otherwise the same child logged in under two alias usernames double-counts.
2. Only update `LastUpdated` (and `MinutesUsed`) when minutes are actually counted:
   ```csharp
   if (!recentUsage)
   {
       usage.MinutesUsed += request.MinutesActive;
       usage.LastUpdated = now;
   }
   ```
   This makes one computer deterministically "win" each window and count wall-clock time exactly once.
3. `session.ActiveMinutes += request.MinutesActive` must only run when the usage was counted, so session totals and `TimeUsage` totals stay consistent. (If you want per-machine raw activity for display, add a separate field — do not reuse `ActiveMinutes`.)
4. Clamp/validate the suppression window against the client reporting interval (client reports every 60s): the window should be slightly larger than one reporting interval (e.g. 90s), not larger, or legitimate single-computer reports get suppressed after a delayed sync.

Tests to add (WebService tests): 
- Two computers, same user, alternating reports every 30s for 10 simulated minutes → exactly 10 minutes counted.
- Two computers, two usernames that are aliases of one primary → same result.
- One computer, reports with a 3-minute gap (offline batch) → all minutes counted.
- One computer only → every minute counted.

### 1.2 Fix timezone handling (#3)

Files: `ClientController.cs` (report timestamp → `DateOnly`), `Services/TimeCalculationService.cs` (allowed-hours and week math).

All limits (daily reset, allowed hours, weekly windows) are conceptually **household local time**, but the code compares UTC values. For a CET/CEST household the day resets at 01:00/02:00 and every allowed-hours window is shifted.

Implement:

1. Add a server setting `ParentalControl:TimeZoneId` in `appsettings.json` (default: the container/host local zone via `TimeZoneInfo.Local` if the setting is absent — zero-config default that is correct for a self-hosted family server).
2. Create one helper (e.g. in `TimeCalculationService` or a small `IClockService`) that converts a UTC instant to household local time: `TimeZoneInfo.ConvertTimeFromUtc(utc, tz)`. Route **every** derivation of `DateOnly` (daily bucket), `TimeOnly` (allowed hours), and week-start through it. Grep for `DateOnly.FromDateTime`, `TimeOnly.FromDateTime`, `DateTime.UtcNow`, and `DayOfWeek` in the WebService project and convert each usage site.
3. Clients keep sending UTC timestamps (`DateTime.UtcNow`) — do not change the client convention; the server owns local-time interpretation. Ignore client local clocks entirely (a child can change them).
4. Week start: make it Monday, driven by a config value `ParentalControl:FirstDayOfWeek` defaulting to `Monday` (this deployment is European; #13 second bullet).

Tests: daily reset happens at local midnight for a Europe/Prague zone including a DST-transition day; allowed-hours window 16:00–18:00 local admits a 15:30 UTC report in winter (16:30 local) and rejects a 19:30 UTC one.

### 1.3 Normalize usernames everywhere (#8)

`ClientController.EnsureUserExistsAsync` lowercases; `UsersController.CreateUser` does not; the PostgreSQL unique index is case-sensitive. Result: "Alice" (created in UI) and "alice" (reported by client) become two users and limits silently don't apply.

Implement one shared normalization (trim + lowercase, invariant) applied in **every** path that stores or looks up a username: `CreateUser`, `UpdateUser`, alias creation, and the client controller. Add a startup migration/one-time fixup is *not* required, but add a warning log if two existing users differ only by case so the parent can merge them in the UI.

### 1.4 First-contact race (#16)

`EnsureUserExistsAsync`: two computers reporting a brand-new user simultaneously → one `SaveChangesAsync` throws on the unique index → 500 → that client's batch is retried or lost. Wrap in `try/catch (DbUpdateException)` and re-query the user on conflict. Auto-created users must always succeed — this is part of "registration is never refused".

### 1.5 Weekly adjustments (#13, first bullet)

`TimeCalculationService`: the weekly-remaining branch reuses `adjustments` filtered to *today*. Sum adjustments over the whole local week (using the Phase 1.2 local-time week window) for the weekly branch.

### 1.6 Warning thresholds (#9)

`EnforcementEngine` (client, both platforms) online path uses `== warningMinutes`; remaining time can skip values (missed sync, constraint switch). Change to `<=` with the existing "already shown" set and reset-on-increase logic, matching the offline path. (This is client code — you may fold it into Phase 2 work; listed here because it pairs with server tick semantics.)

---

## Phase 2 — Client: correctness, persistence, crash-proofing (fixes #4, #5, #7, #10, #11, #12 + leaks)

All items apply to **both** `ParentalControl.Client` (Linux) and `ParentalControl.Client.Windows`.

### 2.1 Key all client-side state by username, not UserId (#4)

`SystemdSessionMonitor` creates sessions with `UserId: Guid.Empty` (server resolves from username later). That empty GUID is then used as the cache key for last-known limits and daily usage — so on a shared computer every user shares one bucket, and offline enforcement applies the wrong user's limits (a parent's "unlimited" cached last gives every child unlimited offline time).

Change `LocalCache` (`SaveLastKnownLimitsAsync`, `CheckAndEnforceOfflineAsync` path, `GetTodayUsageAsync`, `_dailyUsage`) and every caller to key by **normalized username** (same normalization as Phase 1.3). The client never needs the server's GUID.

### 2.2 Fix offline remaining-time math (#5)

`EnforcementEngine.CheckAndEnforceOfflineAsync` computes `lastLimits.TimeRemainingMinutes - todayUsage`, but the cached value is already net of usage at sync time — this double-subtracts and logs children out early (wrongly, but "fail closed"; still a bug to fix).

Track **minutes used since the last successful sync** per username: reset that counter to 0 inside `SaveLastKnownLimitsAsync` (i.e., whenever fresh server truth arrives), increment it as offline minutes accrue, and compute `remaining = lastLimits.TimeRemainingMinutes - usedSinceSync`.

### 2.3 Persist LocalCache to disk (#7) — critical for "no maintenance"

`LocalCache` is memory-only. Consequences: reboot while offline = all pending minutes lost **and** no cached limits, so offline enforcement gives up → unlimited time. A child who notices this reboots their way to freedom.

Implement JSON persistence:

- Linux: `/var/lib/parental-control/cache.json` (directory created by the deb postinst or the service on startup; owned by root, mode 0700 — the child must not be able to read or edit it).
- Windows: `C:\ProgramData\ParentalControl\cache.json` with an ACL restricting to SYSTEM/Administrators.
- Persist: pending usage records, last-known limits per username, used-since-sync counters per username, daily usage per (username, date).
- Write atomically (write temp file in same directory, then `File.Move` with overwrite) after every mutation or on a short debounce (≤15s); load on startup, tolerating a missing or corrupt file (log a warning, start empty — never crash).
- Prune: drop daily-usage entries older than 14 days and pending records older than 7 days on load and daily thereafter (fixes the unbounded `_dailyUsage` growth).

Tests: round-trip persistence; corrupt file → clean start; reboot-while-offline scenario now enforces from cached limits.

### 2.4 Never crash the host process (#10)

`ServerSyncService` constructor calls `new Uri(serverUrl!)` and reads five files — an unset/corrupt config crash-loops the whole worker under systemd, which means **no enforcement at all**.

- Move all I/O and URL parsing out of the constructor into an `InitializeAsync` called from the worker's `ExecuteAsync`.
- If the server URL is missing/invalid: log clearly, enter a "not configured" state that retries reading config periodically — do not throw. (The whole client's failure policy: any single subsystem failure degrades, never kills the daemon. Wrap the worker loop body in try/catch with a backoff so an unexpected exception in one tick never terminates the service.)
- Also verify `Program.cs`/systemd unit: `Restart=always`, `RestartSec=5` in `scripts/parental-control-client.service` as a second line of defense.

### 2.5 Use the persisted computer ID (#11)

`ServerSyncService.GetConfigurationAsync` reads `_configuration["ParentalControl:ComputerId"]` while everything else uses the `_computerId` field loaded from persistent storage. Use `_computerId`. If it's not yet set (first run before registration), skip the call gracefully.

### 2.6 Batch usage by date (#12)

When flushing pending records after an offline stretch spanning local midnight, group pending records by their `DateOnly` and send one usage request per date, so yesterday's minutes book to yesterday and today's to today.

### 2.7 Plug the slow leaks

`_seenSessionIds` (`ParentalControlWorker`), `_warningsShown`, `_lastTimeRemaining` (`EnforcementEngine`) grow forever. Prune entries for sessions/users that have disappeared (you already enumerate active sessions every tick — remove tracked IDs not in the current set). A daemon that runs for a year must have flat memory.

---

## Phase 3 — Auth: protect the API without breaking zero-config (fixes #1, #6, #15, #17, #18)

Reminder: registration stays **open and idempotent**. Security here is defense-in-depth behind nginx, not a gate that can strand a client.

### 3.1 Registration: idempotent, MachineId-keyed, never refused (#18 + goal 2)

`ClientController.Register`:

- Match existing computers on **`MachineId` only**. Treat `Hostname` as display metadata: update it on the matched record, drop the unique index on hostname (EF migration), and never let a hostname collision hijack another machine's record.
- If no match → create the record and return a new `ApiKey`. If matched → return the **existing** `ApiKey`. (Yes, this means anyone with the MachineId can retrieve the key — accepted trade-off per goal 2; the protected network is the boundary. Do not add pairing.)
- Registration must be safe to call on every client startup (it effectively already is — keep it that way and have the client call it whenever it has no stored key or gets a 401, see 3.3).

### 3.2 Require the ApiKey on client endpoints (#1) — with a compatibility grace

- Add validation of an `X-Api-Key` header against the computer's stored `ApiKey` on every `/api/client/*` endpoint **except `register`**. Implement as an action filter or middleware, not copy-paste per action.
- `GET /api/client/config/{computerId}`: additionally verify the key belongs to *that* computerId (currently the route parameter is ignored — fix that), and return only what the client needs, not all child accounts' details.
- **Grace mode for deployed clients:** add server setting `ParentalControl:RequireClientApiKey` (default `false` initially). When `false`, a missing key is logged as a warning but allowed; a *present-but-wrong* key is still rejected. The parent flips it to `true` after all machines run the updated client. Document this in the CHANGELOG and README. This is what keeps goal 6 (no maintenance/no bricking) intact.

### 3.3 Client side: store and self-heal the key

- Client persists the ApiKey next to `computer-id` (Linux: `/etc/parental-control/api-key`, root-only 0600; Windows: ProgramData with restricted ACL). Send `X-Api-Key` on every request.
- On any 401: drop the stored key, re-register (which returns the current key), retry once. This makes key rotation and server DB restores self-healing with zero manual steps.

### 3.4 Admin API hardening (#6, #15)

- Add `[RequireAuth]` to the read endpoints in `UsersController` (`GET /api/users`, `GET /api/users/{id}`, `time-status`, `aliases`, `usage-breakdown`) and `AuthController.status` — unless the UI relies on unauthenticated reads; check the Blazor pages first and, if any page needs anonymous access, serve it a trimmed DTO (no `Email`, no `ApiKey`-adjacent data) instead of the raw entity.
- Replace `CreateUser([FromBody] User user)` / `UpdateUser` full-entity binding with small request DTOs (`CreateUserRequest { Username, FullName, Email, AccountType }`), mirroring the client API's existing DTO style. This kills the mass-assignment of `PrimaryUserId`, `CreatedAt`, navigation properties.

### 3.5 Login endpoint (#17)

`AuthController`/`AuthService`:
- Constant-time comparison: `CryptographicOperations.FixedTimeEquals` over UTF-8 bytes.
- In-memory throttle: after 5 failures from an IP, delay/deny for 60s; log every failure with the source IP (nginx forwards it — respect `X-Forwarded-For` only if `ForwardedHeaders` middleware is configured; check `Program.cs`).

---

## Phase 4 — Enforcement: logoff must never hang (goal 5)

Files: `src/ParentalControl.Client/Services/EnforcementEngine.cs` (Linux) and the Windows equivalent. Read the existing escalation ladder and its comments carefully before changing anything — the Kubuntu DRM/KMS and LightDM notes there encode hard-won knowledge; preserve that behavior.

### 4.1 Structural rule: enforcement runs under a hard deadline

The reported symptom is "sometimes hangs at logoff". Root causes to eliminate structurally, not case-by-case:

1. **Every** external invocation (`loginctl`, `pkill`, D-Bus calls, `qdbus`, process kills) must run with an explicit timeout and `process.Kill(entireProcessTree: true)` on expiry. Audit the whole file; some paths already do this — make it universal via one helper (e.g. `RunWithTimeoutAsync(cmd, args, TimeSpan)`).
2. Wrap the whole logout ladder for one session in an **overall deadline** (e.g. 90 seconds) using a `CancellationTokenSource`. If the deadline expires, jump straight to the terminal fallback (4.3).
3. Enforcement must not block the worker's monitoring loop: run the ladder as a tracked background task per session (guard against launching it twice for the same session — keep a "logout in progress" set, pruned per Phase 2.7).
4. After the ladder claims success, **verify** the session is actually gone (`loginctl show-session` / session enumeration). If it still exists after the deadline, escalate and log loudly.

### 4.2 The ladder (Linux)

Keep the existing graduated order — it's correct: (a) ask apps to close / graceful desktop-specific logout via D-Bus, (b) `loginctl terminate-session`, (c) `loginctl terminate-user`, (d) `pkill -KILL -u user` — with verification between steps. Improvements:

- Before step (a), send `SIGTERM` to the user's regular applications (children of the session, excluding the session manager itself) so documents/apps close first — that satisfies "close programs and log off" and reduces the chance the session manager stalls waiting on an unresponsive app.
- Desktop-specific graceful logout coverage — implement/verify one D-Bus/CLI path per supported desktop and select by detecting the running session (`XDG_CURRENT_DESKTOP` from the session's environ, or presence of the session manager process):
  - **KDE (Kubuntu):** `qdbus org.kde.Shutdown /Shutdown logout` (or the existing `org.kde.ksmserver` call already in the code — keep whichever the comments say works around the DRM/KMS freeze).
  - **Cinnamon:** `cinnamon-session-quit --logout --force` (as the session user, with `DISPLAY`/`DBUS_SESSION_BUS_ADDRESS` from `/proc` — the code already knows how to fetch these).
  - **MATE:** `mate-session-save --logout` (`--logout-dialog` shows a prompt; use the non-interactive form).
  - **LXDE:** LXDE has no reliable non-interactive D-Bus logout (`lxsession-logout` shows a dialog). Go straight from app-SIGTERM to `loginctl terminate-session` for LXDE.
  - Unknown desktop: skip (a), start at (b).
- Each graceful attempt gets ≤15s before escalation; total ladder ≤90s (4.1).
- After the final step, if `loginctl` reports the session still active, log an error with full diagnostics (session state, remaining user processes) — this is the signal the parent would need if a new desktop version breaks something.

### 4.3 Terminal fallback

If the deadline expires with the session alive: `loginctl kill-session <id> --signal=SIGKILL` then `pkill -KILL -u <user>` once more, verify, and if *that* fails, log at Critical. Do not loop forever; the next worker tick will re-detect the over-limit session and re-run enforcement (which also covers the child logging back in — the existing enforcement-check-before-counting on new sessions already handles the re-login hole; keep it).

### 4.4 Windows

Verify the Windows engine follows the same shape: graceful first (`WTSLogoffSession(WTS_CURRENT_SERVER_HANDLE, sessionId, wait: false)` from the service — do **not** use `ExitWindowsEx`, which only works in the user's own session), timeout, verify via `WTSEnumerateSessions`, escalate to `WTSLogoffSession` with wait, then kill remaining user processes. Same hard-deadline and background-task structure as Linux.

### 4.5 Tests

Unit-test the ladder logic with a mocked process runner: correct escalation order per desktop, deadline expiry jumps to fallback, no double-launch for one session, verification gating. (Real desktop testing is Phase 7.)

---

## Phase 5 — Remaining server cleanups (from "Medium/Low" in fable-comments.md)

Do these after Phases 1–4; each is small:

- **#14** Nullable limits: change daily/weekly limit columns to nullable ints (`null` = unlimited, `0` = blocked) with an EF migration that maps existing `0` → `null` (preserving current behavior), update `TimeCalculationService` branches and the UI editor. This lets a parent actually block a school night.
- Merge adjacent/overlapping allowed-hours windows in `GetMinutesUntilAllowedHoursEndAsync` (or scan to the latest reachable end) so 08:00–12:00 + 12:00–18:00 doesn't warn of a noon cutoff that never comes.
- `GetTimeStatus` in `UsersController`: compute `usedToday` over the alias group (`GetAllUserIdsInGroupAsync`) so both numbers in the response agree.
- `DataProtection` key path (`Program.cs`): configurable, default `/app/keys` only when running in Docker (env check), else a sensible local path.
- Proxy password file (Linux client `ServerSyncService`): create with restrictive `UnixFileMode` *before* writing content; check the `chgrp` exit code; only rewrite the file when the content changed.
- `GetAllLocalUsersAsync`: `int.TryParse` per `/etc/passwd` line so one malformed line doesn't abort enumeration.
- `ShouldEnforceAsync`: simplify to a plain method (no fake async, no unused parameter).

---

## Phase 6 — Unify the two client codebases (deduplicate #4/#5-class risk permanently)

`ParentalControl.Client` and `ParentalControl.Client.Windows` duplicate `ServerSyncService`, `LocalCache`, worker logic, enforcement scaffolding. Every Phase 2 fix landed twice — that's the maintenance hazard goal 6 forbids.

- Create `src/ParentalControl.Client.Core` (netstandard-free, plain net8.0 class library) holding: `ServerSyncService`, `LocalCache`, the worker loop, DTOs, the enforcement *ladder orchestration* (deadlines, escalation state machine, warning logic).
- Platform projects keep only `ISessionMonitor` and the platform half of `IEnforcementEngine` (the actual D-Bus/loginctl vs. WTS calls) plus a `IPlatformPaths` (cache dir, config dir) implementation.
- Move the shared unit tests to a Core test project; keep thin platform test projects.
- This is a mechanical refactor: **no behavior changes in this phase.** Do it as its own commit(s) so regressions bisect cleanly.

---

## Phase 7 — Packaging, deployment, verification

### 7.1 Deb package / installer (goal 1)

Review `scripts/build-deb.sh` and `scripts/install-linux-client.sh` and ensure post-Phase-2/3 reality:

- postinst creates `/etc/parental-control/` and `/var/lib/parental-control/` with root-only permissions, installs a config containing **only** the server URL placeholder, enables + starts the systemd service.
- Upgrading the package preserves `/etc/parental-control/` (server URL, computer-id, api-key) and `/var/lib/parental-control/cache.json` — verify conffile/dir handling; an upgrade must not re-register as a new computer or lose pending usage.
- The systemd unit has `Restart=always`, `RestartSec=5`, and starts after `network-online.target` but **does not require it** (the client must run and enforce offline).
- Windows: same properties for the service installer (`install-windows-client.ps1`): auto-start, restart-on-failure recovery options, config = server URL only, upgrade preserves state.

### 7.2 Server deployment notes

- Document (README) the two new settings: `ParentalControl:TimeZoneId` and `ParentalControl:RequireClientApiKey`, including the flip-to-true step after all clients are updated.
- nginx: no changes required by this work; note that if `X-Forwarded-For` is used for login throttling, `ForwardedHeaders` must be enabled and trusted-proxy configured.

### 7.3 Acceptance checklist (manual, on real machines — list for the human, do not fake)

1. Fresh install on each desktop (Kubuntu, Mint LXDE/Cinnamon/MATE, Windows): install package, set server URL, reboot → computer appears on server, user auto-created, time counts.
2. Two computers, one child, 10-minute limit: use both simultaneously → logged out after ~10 wall-clock minutes on **both**; server shows 10 minutes used, not 20 and not 1.
3. Time expiry on each desktop: apps close, session logs off within 90s, no hang, lock/login screen reachable, child can't race-relogin for free time.
4. Offline: disconnect server, verify countdown continues and enforcement still triggers; reboot mid-offline → cached state survives, enforcement still correct; reconnect → pending minutes sync to the correct dates.
5. Daily reset at local midnight (or simulate by adjusting server `TimeZoneId`).
6. Upgrade an existing deb install → no re-registration, no lost state.

---

## Ordering & commit discipline

- One phase per PR-sized commit series; conventional messages referencing the finding numbers (e.g. "Fix concurrent-usage suppression deadlock (#2)").
- Phases 1 and 2 are the highest-value: they fix "time isn't counted with two computers" and "reboot defeats enforcement". Phase 4 fixes the hang. Phase 3 can ship any time after 1–2. Phases 5–7 close out.
- If any instruction here conflicts with something you find in the code that suggests different intent, stop and flag it in the commit message / summary rather than guessing — except where this document explicitly overrides fable-comments.md (registration openness, grace-mode API key).
