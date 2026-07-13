# Post-implementation review (round 3) — timekeeper-net, after work/fable-instructions2.md (v1.68.1)

Reviewed: the full round-2 delta (`ClientController` rewrite + transaction wrapper, `ProcessRunner`/`SessionMonitor`, `EnforcementEngine` ladder cancellation, both workers' re-registration counter, Windows `MachineGuid` identity, `AuthService` throttle pruning, both `LocalCache` rewrites, docker-compose, both installer scripts, `build-deb.sh`, INSTALLATION.md, CHANGELOG/version) against `work/fable-review-2.md` and `work/fable-instructions2.md`. Build clean, **178/178 tests pass** (149 web service, 18 Linux client, 11+2 skipped Windows client) — I re-ran them myself.

## Verdict

Round 2 is faithfully and correctly implemented. Every finding from review 2 — both deploy blockers, all four "silently stops working" items, the hardening batch, and all six cleanups — is fixed the way the instructions specified, with the right comments in the right places and regression tests that actually exercise the failure scenarios (locked-machine starvation, zero probes, backlog flushes). The deviations Sonnet made (not migrating `RunProcessWithTimeoutAsync` to `ProcessRunner`, no worker test rig for the failure counter) were the correct calls and are honestly logged.

**The system is deployable.** Two findings remain — one is a leftover instance of the exact bug class review 2's #5 was about (my review missed this call site too), one is a latent installer break on non-English Windows. Both are small, neither blocks a Linux-only deployment, but both should be fixed before this is called done, because each one is a "silently stops working / silently broken install" case — the category this project explicitly set out to eliminate.

---

## Medium

### 1. `UserStillLoggedInAsync` still runs `loginctl` with no timeout — it can wedge the logout ladder *and* defeat the new deadline cancellation
`EnforcementEngine.cs:457-486` — `UserStillLoggedInAsync` uses a bare `Process.Start` + `await process.WaitForExitAsync()` with **no timeout and no cancellation token**. This is the one remaining unbounded external-process wait in the daemon (verified by grep across `src/`). It is called five times in the enforcement path: after the graceful-logout wait, after terminate-session, after terminate-user (all inside `RunLogoutLadderAsync`), and after the deadline SIGKILL fallback in `LogoutUserAsync`.

Why this matters more than it looks:
- The B.4 deadline fix works by the ladder *observing* its `CancellationToken` (in `Task.Delay` and between-step checks). A hang inside `UserStillLoggedInAsync` sits between those observation points — the 90s deadline CTS fires, but nothing ever throws `OperationCanceledException`, so the SIGKILL fallback never runs. The deadline guarantee is only as strong as its weakest await, and this await is unbounded.
- Worse: the ladder task never completes, so `TriggerLogout`'s `finally` never runs and `_logoutInProgress[username]` stays claimed **forever**. Every subsequent over-limit tick hits the `TryAdd` guard and is dropped as a "duplicate trigger" — enforcement for that user is permanently dead until a service restart. That's the exact "no tracking *and* no enforcement until someone restarts the service" failure that B.1 was written to kill, resurrected one file over.
- The trigger is the same one the whole #5 effort was about: a wedged logind/D-Bus making `loginctl` hang — and enforcement time (a desktop being torn down mid-logout) is precisely when logind is most likely to misbehave on the Kubuntu setups this project keeps fighting.

**Fix (small):** route `UserStillLoggedInAsync` through the existing `ProcessRunner.RunAsync` (same project, built for exactly this) with the same 10s timeout; on `!ok` return `false`, which matches the current catch-block semantics ("can't verify → treat as logged out → don't escalate destructively on bad data"). One call site, ~10 lines net deleted.

For completeness: the only other timeout-less process waits are `SetFileGroup`'s `chgrp` (`ServerSyncService.cs:628`, plain syscall wrapper, no D-Bus involvement, negligible hang risk) and the one-shot `set server-url` CLI path in `Program.cs` (interactive, not the daemon). Both fine to leave.

### 2. `icacls` with English group names silently breaks the Windows install on localized Windows — and can leave the data directory with an *empty* DACL
`install-windows-client.ps1:38-39` — the new ACL hardening grants `"SYSTEM"`, `"Administrators"`, `"Users"` **by localized display name**. On non-English Windows (Czech: `Administrátoři`, `Uživatelé`; and `SYSTEM` lookups are not reliable either) these names don't resolve — `icacls` fails with "no mapping between account names and security IDs" and applies nothing. Given this household runs Czech (the product ships Czech as its second language), this isn't a theoretical locale.

The failure compounds because of the two-command structure: the first command (`/inheritance:r`) succeeds and strips every inherited ACE; the second (the grants) then fails — **silently**, since both are piped to `Out-Null` with no exit-code check. Result: a directory with an empty DACL, which denies access to everyone including SYSTEM. The service can't write `computer-id.txt`/`api-key.txt`/`cache.json`, the tray app can't read `server-url.txt`, and nothing in the installer output says why. A fresh install on Czech Windows is broken out of the box; my own review-2 suggested command had the same bug, so this one's on me as much as on the implementation.

**Fix (one line):** use the well-known SIDs, which are locale-independent and documented `icacls` syntax:
```powershell
icacls $dataPath /inheritance:r /grant "*S-1-5-18:(OI)(CI)F" /grant "*S-1-5-32-544:(OI)(CI)F" /grant "*S-1-5-32-545:(OI)(CI)RX" | Out-Null
```
(`S-1-5-18` = SYSTEM, `S-1-5-32-544` = Administrators, `S-1-5-32-545` = Users.) Doing it as a single `icacls` invocation also removes the window where inheritance is stripped but grants haven't applied, and it's worth checking `$LASTEXITCODE` and warning loudly on failure instead of `Out-Null`-ing it.

---

## Low / notes (no action required; recorded so they're deliberate)

- **Offline concurrent usage double-counts across machines.** Backlog flushes always count in full by design (#3 fix), so a child on two machines simultaneously *while the server is down* gets both sets of minutes counted when they flush. Correct trade-off: the failure is in the parent's favor, the alternative needs per-window bookkeeping, and the live path still suppresses. Worth one sentence in `INSTALLATION.md`'s known-limitations area someday, nothing more.
- **The suppression window doesn't span the local-midnight boundary** — the `recentUsage` query filters `UsageDate == date`, so machine A counted at 23:59:30 doesn't suppress machine B's 00:00:30 report. Worst case: one double-counted minute per midnight, only with two live machines straddling it. Ignore.
- **Retry-path rollback after a failed commit:** if the serialization failure surfaces at `CommitAsync` (rather than `SaveChangesAsync`), the catch calls `transaction.RollbackAsync()` on a transaction the server already terminated. Npgsql tolerates this (ROLLBACK on an aborted/absent transaction is a warning, not an error), and even if it ever threw, the tick 500s and the client retries next tick — self-healing either way. Noting so nobody "fixes" it into something worse.
- **Throttle pruning is 1-hour-idle-gated**, so a sustained flood of unique spoofed IPs can still grow `FailuresByIp` within any one hour. Bounded by flood rate × 1h, LAN-only by the documented port-binding requirement, entries are tiny. Acceptable as documented.
- **`GetMachineId()` on Linux falls back to a random GUID per call** if `/etc/machine-id` is unreadable — each registration retry would then create a fresh Computer row. `/etc/machine-id` is world-readable on every systemd system and `ProtectSystem=strict` doesn't block `/etc` reads, so this path is effectively dead; fine as-is.

## What's genuinely solid this round

- The `RecordUsageCoreAsync` extraction is exactly the right shape: one copy of the business logic, transaction plumbing forked only at the edge, the InMemory blind spot documented at the site, and the retry correctly detaches tracked entities before re-running.
- The three Phase-A regression tests test the *scenario* (locked machine starving an active one over 10 simulated minutes), not the implementation — they'd catch a re-regression through any future rewrite of that block.
- `ProcessRunner` gets the classic pitfalls right unprompted: stdout read started before the exit wait (pipe-full deadlock), whole-tree kill, exceptions can't escape.
- The `build-deb.sh` verification was done by actually building a `.deb` and inspecting it, and the one mistake made along the way (`rm -rf` of tracked `build/` files) was caught, fully reverted, and honestly reported.

## Recommended action

Fix #1 and #2 — together they're roughly fifteen lines across two files, no test-suite implications beyond re-running it (an optional `ProcessRunnerTests`-style check that `UserStillLoggedInAsync` survives a hung binary would be nice but the shared `ProcessRunner` path is already covered). After that, this is deployable on both platforms and I have nothing further: three review rounds have converged, the remaining items above are all deliberate trade-offs rather than defects, and the codebase now fails toward "counts time conservatively and self-heals" in every path I can construct.
