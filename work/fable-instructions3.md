# Implementation Instructions (round 3) — fixes from the round-3 review

Audience: Claude Sonnet, working in this repository.
Source: `work/fable-review-3.md` (finding numbers below refer to that file). Prior context: `work/fable-instructions2.md`, `work/sonnet-progress2.md`.

This round is small: two surgical fixes, no server changes, no new architecture. Everything else in the round-3 review is a deliberate trade-off recorded as a note — do not "fix" any of the Low items.

## Progress tracker

Detailed log goes in `work/sonnet-progress3.md`. Update these checkboxes as you go.

- [ ] **Fix 1 — timeout on `UserStillLoggedInAsync`** (Linux client)
- [ ] **Fix 2 — locale-safe `icacls` SIDs** (Windows installer script)
- [ ] **Finalize** (CHANGELOG, full build + test green)

## Ground rules (unchanged)

- Product goals from `work/fable-instructions.md` still govern: registration never refused, zero client setup, wall-clock shared budgets, no maintenance. Nothing below may regress those.
- After the fixes: `dotnet build ParentalControl.sln` and `dotnet test ParentalControl.sln` must pass (baseline: 178 passed, 2 skipped).
- Keep changes surgical; match existing style; locate code by quoted content, not line numbers.

---

## Fix 1 — `UserStillLoggedInAsync` must use `ProcessRunner` (finding #1) — Linux client only

`EnforcementEngine.UserStillLoggedInAsync` is the last unbounded external-process wait in the daemon: a bare `Process.Start` + `await process.StandardOutput.ReadToEndAsync()` + `await process.WaitForExitAsync()` with no timeout. It is called between the ladder's cancellation checkpoints, so a hung `loginctl` there means (a) the 90s logout deadline never fires (nothing observes the token during that await), and (b) the ladder task never completes, so `TriggerLogout`'s `finally` never releases `_logoutInProgress[username]` — enforcement for that user is then permanently dead until a service restart.

Replace the body's process handling with the existing `ProcessRunner.RunAsync` (same project, `ParentalControl.Client.Services`, already covered by `ProcessRunnerTests`):

```csharp
private async Task<bool> UserStillLoggedInAsync(string username)
{
    // Runs under ProcessRunner's hard timeout: this check sits between the logout
    // ladder's cancellation checkpoints, so an unbounded wait here would defeat the
    // deadline in LogoutUserAsync AND leave _logoutInProgress claimed forever
    // (killing all future enforcement for the user until a service restart).
    var (ok, output) = await ProcessRunner.RunAsync(
        new ProcessStartInfo
        {
            FileName = "loginctl",
            Arguments = "list-sessions --no-legend"
        },
        TimeSpan.FromSeconds(10), _logger);

    // Can't verify -> treat as logged out. Matches the previous catch-block semantics:
    // never escalate destructively (terminate-user, pkill) on the basis of a failed query.
    if (!ok)
        return false;

    return output.Split('\n').Any(line =>
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 && parts[2] == username;
    });
}
```

Notes:
- `ProcessRunner.RunAsync` sets `RedirectStandardOutput`/`UseShellExecute`/`CreateNoWindow` itself — pass only `FileName`/`Arguments`.
- Keep the parsing logic byte-for-byte (`parts.Length >= 3 && parts[2] == username`).
- The old `try/catch` wrapper can go entirely — `ProcessRunner.RunAsync` never throws.
- Do **not** touch `RunProcessWithTimeoutAsync` or any other part of the engine; the round-2 decision to leave it un-migrated stands.
- Deliberately out of scope, matching the review: `SetFileGroup`'s `chgrp` (`ServerSyncService`) and the `set server-url` CLI path in `Program.cs` keep their plain waits — negligible hang risk, not the daemon's tick/enforcement path.
- Windows client: `LogoffUser`/`LockSession` are non-blocking P/Invoke, there is no loginctl equivalent — nothing to do (confirm with a grep, note it in the progress log, don't invent work).

Test: no new test required — the timeout behavior lives in `ProcessRunner`, which `ProcessRunnerTests` already covers (hung binary killed within the timeout, missing binary returns `(false, _)` without throwing). Verify the existing enforcement tests (`EnforcementEngineTests`, the "returns in under 2s" checks) still pass unchanged.

## Fix 2 — locale-independent SIDs in `install-windows-client.ps1` (finding #2)

The ACL hardening added in round 2 grants `"SYSTEM"`, `"Administrators"`, `"Users"` by **English display name**. On localized Windows (Czech: `Administrátoři`, `Uživatelé` — and this household runs Czech) those names don't resolve; `icacls` fails with "no mapping between account names and security IDs". Because `/inheritance:r` runs as a *separate first command* and both commands are piped to `Out-Null` with no exit-code check, the failure mode is an **empty DACL**: inheritance stripped, no grants applied, nobody (including SYSTEM) can access the directory, and the installer says nothing.

Replace the two `icacls` lines with a single invocation using well-known SIDs (locale-independent, documented `icacls` syntax — the `*` prefix means "this is a SID"), and check the result loudly:

```powershell
# S-1-5-18 = SYSTEM, S-1-5-32-544 = Administrators, S-1-5-32-545 = Users.
# SIDs, not names: group display names are localized (Czech "Administrátoři"/"Uživatelé"),
# and a failed name lookup after /inheritance:r would leave an EMPTY DACL -- a directory
# nobody, including the service, can access. One combined invocation also avoids the
# window where inheritance is stripped but the grants haven't been applied yet.
icacls $dataPath /inheritance:r /grant "*S-1-5-18:(OI)(CI)F" /grant "*S-1-5-32-544:(OI)(CI)F" /grant "*S-1-5-32-545:(OI)(CI)RX" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "WARNING: could not restrict permissions on $dataPath (icacls exit code $LASTEXITCODE)." -ForegroundColor Red
    Write-Host "The service will still work, but a local user may be able to tamper with its data files." -ForegroundColor Red
}
```

Notes:
- Keep the existing explanatory comment block above it (why Users keep read access for the tray app) — it's still accurate; just merge, don't duplicate.
- A warning, not an abort: failed hardening must never break the install itself (product goal: zero-maintenance beats defense-in-depth here).
- This script has never shipped with the broken English-name version (all of this is uncommitted working-tree state), so no migration/repair path for previously-broken installs is needed.
- There is no test harness for PowerShell scripts in this repo; verification is by eyeball plus, if `icacls` semantics are in doubt, reasoning in the progress log. Do not add a PowerShell test framework for this.

---

## Finalize

- **CHANGELOG**: fold both fixes into the existing `[1.68.1]` entry as two new bullets (plain language, e.g. "the logout ladder's session-verification `loginctl` call now runs under the same 10-second timeout as every other external call, so a hung logind can no longer permanently disable enforcement for a user" / "the Windows installer now sets data-directory permissions via locale-independent SIDs — the previous English group names would silently fail on non-English Windows and could leave the directory inaccessible"). Do **not** bump to 1.68.2: 1.68.1 has never been committed, tagged, or released, so a new version number would be pure churn.
- Full solution build + test; state the final counts in `work/sonnet-progress3.md`.
- Do not tag/commit/push unless the user asks.

## Explicitly out of scope

- All five Low/notes items in `work/fable-review-3.md` (offline backlog double-count, midnight suppression boundary, rollback-after-failed-commit, throttle prune granularity, Linux machine-id fallback) — recorded as deliberate trade-offs, leave them alone.
- Phase 6 client unification and nullable limits (#14) stay deferred — user decisions from round 1.
