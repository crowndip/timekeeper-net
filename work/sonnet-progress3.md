# Sonnet Implementation Progress (round 3)

Tracking progress implementing `work/fable-instructions3.md`. Source: `work/fable-review-3.md`.

## Status legend
- [ ] not started
- [~] in progress
- [x] done

---

- [x] **Fix 1 — timeout on `UserStillLoggedInAsync`** (Linux client)
- [x] **Fix 2 — locale-safe `icacls` SIDs** (Windows installer script)
- [x] **Finalize** (CHANGELOG, full build + test green)

---

## Log

### Fix 1 (finding #1) — complete

`EnforcementEngine.UserStillLoggedInAsync` (`src/ParentalControl.Client/Services/EnforcementEngine.cs`) rewritten to call `ProcessRunner.RunAsync` (same `ParentalControl.Client.Services` namespace, no new `using` needed) with a 10-second timeout, instead of a bare `Process.Start` + unbounded `WaitForExitAsync`.

- On `!ok` (timeout, missing binary, any exception inside `ProcessRunner`) returns `false` — identical semantics to the old catch block ("can't verify → don't escalate destructively").
- Parsing logic (`parts.Length >= 3 && parts[2] == username`) left byte-for-byte identical.
- The old `try/catch` wrapper removed entirely — `ProcessRunner.RunAsync` never throws, so it was dead code once the call goes through it.
- Left `SetFileGroup`'s `chgrp` (`ServerSyncService.cs`) and the `set server-url` CLI path in `Program.cs` untouched, per instructions — neither is in the daemon's tick/enforcement path.
- Windows client: `grep -rn "Process\.\|ProcessStartInfo" src/ParentalControl.Client.Windows/` → no hits. Confirmed nothing to change there (matches round 1/2 findings: `LogoffUser`/`LockSession` are non-blocking P/Invoke, no loginctl equivalent).

No new test added — the timeout behavior itself lives in `ProcessRunner` and is already covered by `ProcessRunnerTests` (hung binary killed within timeout, missing binary returns `(false, _)` without throwing). Existing `EnforcementEngineTests` (including the "returns in under 2s" dispatch tests) still pass unchanged.

### Fix 2 (finding #2) — complete

`scripts/install-windows-client.ps1`: replaced the two-command English-group-name `icacls` sequence with a single invocation using well-known SIDs:

```powershell
icacls $dataPath /inheritance:r /grant "*S-1-5-18:(OI)(CI)F" /grant "*S-1-5-32-544:(OI)(CI)F" /grant "*S-1-5-32-545:(OI)(CI)RX" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "WARNING: could not restrict permissions on $dataPath (icacls exit code $LASTEXITCODE)." -ForegroundColor Red
    Write-Host "The service will still work, but a local user may be able to tamper with its data files." -ForegroundColor Red
}
```

- `S-1-5-18` = SYSTEM, `S-1-5-32-544` = Administrators, `S-1-5-32-545` = Users — locale-independent, unlike the display names the round-2 version used, which don't resolve on non-English Windows (e.g. Czech `Administrátoři`/`Uživatelé`) and could leave an empty DACL after `/inheritance:r` silently stripped inheritance with no grants applied.
- Combined into one `icacls` call (was two) to remove the window between stripping inheritance and applying grants.
- Failure now warns loudly instead of disappearing into `Out-Null` — but does not abort the install (`$ErrorActionPreference = "Stop"` only affects terminating errors/exceptions, not external-command exit codes, so this doesn't change control flow beyond the explicit check added).
- Kept the existing comment block explaining why `Users` needs read access (tray app) — merged the new SID rationale into the same comment rather than duplicating it.
- No previously-broken installs need a migration/repair path: this script has never been committed or released (all changes are uncommitted working-tree state from earlier this session).
- No PowerShell test harness exists in this repo and none was added — verification is by eyeball against documented `icacls` well-known-SID syntax, per instructions.

### Finalize — complete

- Folded both fixes into the existing (still-unreleased) `[1.68.1]` CHANGELOG entry as two new bullets, placed immediately after the related round-2 bullets (loginctl timeout, Windows ACL hardening) they follow up on. Did **not** bump to 1.68.2 — 1.68.1 has never been committed, tagged, or released, so a new version number would be pure churn, per instructions.
- Full solution build: clean, 0 errors, 2 warnings (both the pre-existing unrelated `Tmds.DBus.Protocol` NuGet advisory on `ParentalControl.Client.UI`, listed twice due to multi-targeting in the build log — not new).
- Full test run: **178/178 tests pass** (149 WebService, 18 Linux client, 11+2 skipped Windows client) — identical counts to the round-2 baseline, confirming no regression.

## Summary

Both round-3 findings fixed as specified: the last unbounded `loginctl` wait in the Linux client's enforcement path now goes through the same `ProcessRunner` timeout as everything else (closing the gap where a wedged `logind` could defeat the logout deadline and permanently wedge `_logoutInProgress`), and the Windows installer's ACL hardening now uses locale-independent SIDs instead of English group names (closing a silent-failure path that could leave the data directory with an empty DACL on non-English Windows). No test regressions, no scope creep — the five Low-priority notes from `fable-review-3.md` were left untouched as deliberate trade-offs. No commits, tags, or pushes were made — all changes are in the working tree awaiting review.
