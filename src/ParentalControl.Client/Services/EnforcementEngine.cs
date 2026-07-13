using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using ParentalControl.Shared.DTOs;

namespace ParentalControl.Client.Services;

public interface IEnforcementEngine
{
    Task CheckAndEnforceAsync(UsageReportResponse response, string username, string sessionId);
    Task CheckAndEnforceOfflineAsync(List<UsageRecord> records, string username, string sessionId);

    // Drops per-user tracking state (warnings shown, last-seen remaining time) for users
    // who no longer have a session on this machine, so a daemon that runs for months
    // doesn't accumulate an entry per username that ever logged in.
    void PruneStaleUsers(IEnumerable<string> activeUsernames);
}

public class EnforcementEngine : IEnforcementEngine
{
    // Upper bound on the whole graduated logout ladder for one user. If a step hangs
    // (a D-Bus call that never returns, a desktop that never actually tears down), this
    // is what keeps enforcement from stalling indefinitely -- past this point we stop
    // being graceful and force the issue directly.
    private static readonly TimeSpan LogoutDeadline = TimeSpan.FromSeconds(90);

    private readonly ILogger<EnforcementEngine> _logger;
    private readonly ILocalCache _cache;
    private readonly Dictionary<string, HashSet<int>> _warningsShown = new();
    private readonly Dictionary<string, int> _lastTimeRemaining = new();

    // Tracks usernames with a logout currently running in the background, so a
    // still-over-limit user re-checked on the next tick (or twice in the same tick,
    // online + offline paths) doesn't launch a second concurrent logout ladder.
    private readonly ConcurrentDictionary<string, byte> _logoutInProgress = new(StringComparer.OrdinalIgnoreCase);

    public EnforcementEngine(ILogger<EnforcementEngine> logger, ILocalCache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    public void PruneStaleUsers(IEnumerable<string> activeUsernames)
    {
        var active = new HashSet<string>(activeUsernames, StringComparer.OrdinalIgnoreCase);

        foreach (var username in _warningsShown.Keys.Where(u => !active.Contains(u)).ToList())
            _warningsShown.Remove(username);

        foreach (var username in _lastTimeRemaining.Keys.Where(u => !active.Contains(u)).ToList())
            _lastTimeRemaining.Remove(username);
    }

    public Task CheckAndEnforceAsync(UsageReportResponse response, string username, string sessionId)
    {
        if (response.ShouldEnforce && !string.IsNullOrEmpty(response.EnforcementAction))
        {
            _logger.LogWarning("Enforcing action: {Action} for user {Username}", response.EnforcementAction, username);

            switch (response.EnforcementAction)
            {
                case "logout":
                    TriggerLogout(username, sessionId);
                    break;
                case "lock":
                    TriggerLock(sessionId);
                    break;
            }
        }

        // Reset warnings when time increases (new day or parent added time)
        var lastTime = _lastTimeRemaining.GetValueOrDefault(username, int.MaxValue);
        var warnings = _warningsShown.TryGetValue(username, out var w) ? w : (_warningsShown[username] = new HashSet<int>());

        if (response.TimeRemainingMinutes > lastTime)
        {
            _logger.LogInformation("Time increased from {Old} to {New} minutes for {Username}, resetting warnings",
                lastTime, response.TimeRemainingMinutes, username);
            warnings.Clear();
        }
        _lastTimeRemaining[username] = response.TimeRemainingMinutes;

        foreach (var warningMinutes in response.WarningMinutes)
        {
            // "<=" rather than "==": TimeRemainingMinutes can skip values entirely (a
            // missed sync counts more than one minute at once, the binding constraint can
            // jump between daily/weekly/allowed-hours, and parent adjustments move it
            // arbitrarily), so an exact-equality check can silently miss a warning threshold.
            if (response.TimeRemainingMinutes <= warningMinutes && !warnings.Contains(warningMinutes))
            {
                _logger.LogInformation("Warning: {Minutes} minutes remaining for {Username}", warningMinutes, username);
                warnings.Add(warningMinutes);
                // TODO: Show notification via UI
            }
        }

        return Task.CompletedTask;
    }

    public async Task CheckAndEnforceOfflineAsync(List<UsageRecord> records, string username, string sessionId)
    {
        if (records.Count == 0) return;

        // Cache state is keyed by username, not the server's user Guid: SystemdSessionMonitor
        // creates every session with UserId = Guid.Empty (server resolves it from username),
        // so keying by Guid would merge every local user's offline state into one bucket.
        var lastLimits = await _cache.GetLastKnownLimitsAsync(username);

        if (lastLimits == null)
        {
            _logger.LogWarning("No cached limits for user {Username}, cannot enforce offline", username);
            return;
        }

        // `records` here is exactly the set of usage records not yet successfully synced to
        // the server -- i.e. usage since the last successful sync, which is also exactly
        // when lastLimits.TimeRemainingMinutes was captured (see ServerSyncService.
        // SubmitUsageAsync, which caches limits only on success and MarkAsSyncedAsync only
        // runs after that same success). Using the whole day's usage here instead would
        // double-subtract minutes the server had already accounted for when it returned
        // lastLimits, logging the child out far earlier than their limit actually allows.
        var minutesSinceLastSync = records.Sum(r => r.MinutesActive);

        // Calculate remaining time based on last known limits
        var timeRemaining = lastLimits.TimeRemainingMinutes - minutesSinceLastSync;

        _logger.LogInformation("Offline mode: User {Username} has {TimeRemaining} minutes remaining (cached)",
            username, timeRemaining);

        if (timeRemaining <= 0)
        {
            _logger.LogWarning("Offline enforcement: Time limit reached for user {Username}", username);

            if (!string.IsNullOrEmpty(lastLimits.EnforcementAction))
            {
                switch (lastLimits.EnforcementAction)
                {
                    case "logout":
                        TriggerLogout(username, sessionId);
                        break;
                    case "lock":
                        TriggerLock(sessionId);
                        break;
                }
            }
        }
        else
        {
            // Check warnings
            var warnings = _warningsShown.TryGetValue(username, out var w) ? w : (_warningsShown[username] = new HashSet<int>());
            foreach (var warningMinutes in lastLimits.WarningMinutes)
            {
                if (timeRemaining <= warningMinutes && !warnings.Contains(warningMinutes))
                {
                    _logger.LogInformation("Offline warning: {Minutes} minutes remaining for {Username}", timeRemaining, username);
                    warnings.Add(warningMinutes);
                }
            }
        }
    }

    // Fire-and-forget dispatch: the caller (CheckAndEnforceAsync / CheckAndEnforceOfflineAsync)
    // runs on the worker's single tick loop, which also drives time tracking and server sync
    // for every other session on the machine. Awaiting the full logout ladder there would
    // stall all of that for up to LogoutDeadline -- this is what used to make the whole
    // client "hang" when a logout attempt got stuck. TryAdd also means a still-over-limit
    // user re-checked before the previous attempt finishes never launches a second ladder.
    private void TriggerLogout(string username, string sessionId)
    {
        if (!_logoutInProgress.TryAdd(username, 0))
        {
            _logger.LogDebug("Logout already in progress for {Username}, ignoring duplicate trigger", username);
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await LogoutUserAsync(username, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during background logout for {Username}", username);
            }
            finally
            {
                _logoutInProgress.TryRemove(username, out _);
            }
        });
    }

    private void TriggerLock(string sessionId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await LockSessionAsync(sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during background lock of session {SessionId}", sessionId);
            }
        });
    }

    // Runs the graduated logout ladder under a hard overall deadline. If a step hangs
    // (or the accumulated time from every step's own per-call timeout runs long), this
    // is what guarantees the child is actually logged off within a bounded time instead
    // of the enforcement attempt stalling indefinitely.
    //
    // The ladder runs under a CancellationToken tied to the deadline rather than racing
    // it with Task.WhenAny: WhenAny lets the loser keep running detached after the
    // deadline "wins" -- it can keep escalating (terminate-user, pkill) after the
    // fallback below already ran, the in-progress guard is released while it's still
    // active, and an exception from the abandoned task is never observed. Cancelling
    // the ladder itself avoids all three.
    private async Task LogoutUserAsync(string username, string sessionId)
    {
        if (!IsValidUsername(username))
        {
            _logger.LogError("Invalid username format, refusing to enforce: {Username}", username);
            return;
        }

        using var cts = new CancellationTokenSource(LogoutDeadline);
        try
        {
            await RunLogoutLadderAsync(username, sessionId, cts.Token);
            return;
        }
        catch (OperationCanceledException)
        {
            _logger.LogCritical(
                "Logout ladder for {Username} exceeded the {Deadline}s deadline; forcing immediate SIGKILL fallback",
                username, LogoutDeadline.TotalSeconds);
        }

        await RunProcessWithTimeoutAsync("pkill", $"-KILL -u {username}", timeoutSeconds: 10);

        if (await UserStillLoggedInAsync(username))
            _logger.LogCritical("User {Username} is STILL logged in after the deadline SIGKILL fallback; giving up for this tick", username);
    }

    // Graduated logout: graceful first, escalating to forceful.
    // The root cause of the Kubuntu full-system freeze is that calling
    // `loginctl terminate-user` kills kwin_wayland (the Wayland compositor)
    // without giving it time to release DRM/KMS resources cleanly, leaving
    // the display pipeline in a broken state with no process able to render.
    //
    // `token` is tied to the overall deadline in LogoutUserAsync. Each per-call process
    // timeout (10-20s) already bounds an individual step, so checking the token between
    // steps (rather than plumbing it into RunProcessWithTimeoutAsync itself) is enough to
    // observe the deadline within one step's timeout of it actually expiring.
    private async Task RunLogoutLadderAsync(string username, string sessionId, CancellationToken token)
    {
        _logger.LogInformation("Logging out user {Username} (session {SessionId})", username, sessionId);

        // Step 1: graceful desktop logout via D-Bus.
        // Gives the Wayland compositor (kwin_wayland on KDE, mutter on GNOME) time to
        // release DRM buffers and hand the VT back to SDDM cleanly.
        // IMPORTANT: a D-Bus exit-code 0 only means the message was accepted, not that
        // logout actually happened. On Cinnamon/LightDM a stub service on the systemd
        // user bus acknowledges the call but does nothing. We wait up to 15 s and then
        // verify the session is really gone before treating this step as done.
        if (await TryGracefulDesktopLogoutAsync(username))
        {
            _logger.LogInformation("Graceful desktop logout sent for {Username}, waiting up to 15s for session to close", username);
            await Task.Delay(TimeSpan.FromSeconds(15), token);

            if (!await UserStillLoggedInAsync(username))
            {
                _logger.LogInformation("Graceful desktop logout completed for {Username}", username);
                return;
            }
            _logger.LogWarning("D-Bus logout did not close session for {Username} after 15s (stub service?), escalating", username);
        }

        token.ThrowIfCancellationRequested();

        // Step 2: end the specific loginctl session.
        // Less destructive than terminate-user: logind manages the VT transition
        // and notifies SDDM, giving it a chance to reclaim the display.
        // NOTE: on LightDM-based desktops (Linux Mint/Cinnamon) this only kills
        // PAM-scoped processes (e.g. the tray icon) but leaves Xorg and the desktop
        // alive because those are LightDM children, not logind children.
        // We therefore verify the user is actually gone before treating it as done.
        if (!string.IsNullOrEmpty(sessionId))
        {
            _logger.LogInformation("Attempting loginctl terminate-session {SessionId}", sessionId);
            await RunProcessWithTimeoutAsync("loginctl", $"terminate-session {sessionId}", timeoutSeconds: 20);

            await Task.Delay(TimeSpan.FromSeconds(3), token);
            if (!await UserStillLoggedInAsync(username))
            {
                _logger.LogInformation("loginctl terminate-session succeeded for {Username}", username);
                return;
            }
            _logger.LogWarning("loginctl terminate-session did not fully log out {Username}, escalating", username);
        }

        token.ThrowIfCancellationRequested();

        // Step 3: terminate all user sessions (can cause Wayland freeze on KDE).
        _logger.LogWarning("Attempting loginctl terminate-user {Username}", username);
        await RunProcessWithTimeoutAsync("loginctl", $"terminate-user {username}", timeoutSeconds: 20);

        await Task.Delay(TimeSpan.FromSeconds(3), token);
        if (!await UserStillLoggedInAsync(username))
        {
            _logger.LogInformation("loginctl terminate-user succeeded for {Username}", username);
            return;
        }

        token.ThrowIfCancellationRequested();

        // Step 4: nuclear option — direct kernel-level kill of all user processes.
        // Catches LightDM-parented Xorg/cinnamon-session that loginctl cannot reach.
        _logger.LogWarning("loginctl failed, using pkill -KILL for {Username}", username);
        await RunProcessWithTimeoutAsync("pkill", $"-KILL -u {username}", timeoutSeconds: 10);
    }

    // Attempts a graceful D-Bus logout for the user's running desktop environment.
    private async Task<bool> TryGracefulDesktopLogoutAsync(string username)
    {
        var uid = GetUserUid(username);
        if (uid < 0)
        {
            _logger.LogWarning("Cannot resolve UID for {Username}, skipping graceful logout", username);
            return false;
        }

        // Prefer the real session bus address read from the user's process environment.
        // /run/user/{uid}/bus is the systemd user-instance bus, which is NOT where the
        // session manager registers on LightDM-based desktops (Linux Mint/Cinnamon).
        // LightDM starts D-Bus via dbus-launch, giving each session a unique socket in
        // /tmp whose address is only discoverable from the process environment.
        var dbusAddress = GetSessionBusAddress(uid) ?? $"unix:path=/run/user/{uid}/bus";
        _logger.LogInformation("D-Bus session bus for uid {Uid}: {Address}", uid, dbusAddress);

        // KDE Plasma (Kubuntu default)
        if (await TryDbusLogoutAsync(username, dbusAddress,
            "org.kde.Shutdown", "/Shutdown", "org.kde.Shutdown.logout"))
            return true;

        // GNOME / Unity / Cinnamon — cinnamon-session is a GNOME fork and registers
        // under org.gnome.SessionManager for backwards compatibility.
        if (await TryDbusLogoutAsync(username, dbusAddress,
            "org.gnome.SessionManager", "/org/gnome/SessionManager",
            "org.gnome.SessionManager.Logout", extraArgs: "uint32:1"))
            return true;

        // Cinnamon (newer versions that may use their own name)
        if (await TryDbusLogoutAsync(username, dbusAddress,
            "org.cinnamon.SessionManager", "/org/cinnamon/SessionManager",
            "org.cinnamon.SessionManager.Logout", extraArgs: "uint32:1"))
            return true;

        // XFCE
        if (await TryDbusLogoutAsync(username, dbusAddress,
            "org.xfce.SessionManager", "/org/xfce/SessionManager",
            "org.xfce.SessionManager.Logout", extraArgs: "boolean:true boolean:false"))
            return true;

        // MATE (Linux Mint MATE edition) -- mate-session-manager's D-Bus interface isn't
        // consistently present across distro builds, so fall back to its own CLI logout
        // command, which still needs the session bus address to reach the running
        // mate-session process.
        if (await TryCliLogoutAsync(username, dbusAddress, "mate-session-save", "--logout"))
            return true;

        // LXDE has no reliable non-interactive logout command (lxsession-logout only
        // shows a confirmation dialog), so there is deliberately no attempt here -- it
        // falls through to the loginctl-based steps in RunLogoutLadderAsync.
        return false;
    }

    // Runs a graceful-logout CLI command (as opposed to a D-Bus method call) as the
    // target user, with the session bus address available for commands that need to
    // signal a running session manager process.
    private async Task<bool> TryCliLogoutAsync(string username, string dbusAddress, string command, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "runuser",
                Arguments = $"-u {username} -- {command} {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.Environment["DBUS_SESSION_BUS_ADDRESS"] = dbusAddress;

            var ok = await RunProcessWithTimeoutAsync(psi, timeoutSeconds: 10);
            _logger.LogInformation("CLI logout {Command} for {Username}: {Result}", command, username, ok ? "sent" : "failed");
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CLI logout {Command} not available for {Username}", command, username);
            return false;
        }
    }

    // Reads DBUS_SESSION_BUS_ADDRESS from the environment of a process running as uid.
    // This works across display managers (LightDM, GDM, SDDM) regardless of whether
    // the session bus was started by systemd --user or by dbus-launch.
    private string? GetSessionBusAddress(int uid)
    {
        try
        {
            foreach (var pidDir in Directory.GetDirectories("/proc")
                .Where(d => int.TryParse(Path.GetFileName(d), out _)))
            {
                try
                {
                    var statusPath = Path.Combine(pidDir, "status");
                    if (!File.Exists(statusPath)) continue;

                    var uidLine = File.ReadAllLines(statusPath)
                        .FirstOrDefault(l => l.StartsWith("Uid:"));
                    if (uidLine == null) continue;

                    var parts = uidLine.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2 || !int.TryParse(parts[1], out var processUid) || processUid != uid)
                        continue;

                    var environPath = Path.Combine(pidDir, "environ");
                    if (!File.Exists(environPath)) continue;

                    const string key = "DBUS_SESSION_BUS_ADDRESS=";
                    var entry = File.ReadAllText(environPath)
                        .Split('\0')
                        .FirstOrDefault(v => v.StartsWith(key));

                    if (entry != null)
                    {
                        _logger.LogInformation("Found D-Bus session bus address for uid {Uid}: {Address}", uid, entry.Substring(key.Length));
                        return entry.Substring(key.Length);
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read D-Bus address from /proc for uid {Uid}", uid);
        }
        _logger.LogWarning("No DBUS_SESSION_BUS_ADDRESS found in /proc for uid {Uid}, falling back to systemd user bus", uid);
        return null;
    }

    // Returns true if the user still has at least one active loginctl session.
    //
    // Runs under ProcessRunner's hard timeout: this check sits between the logout
    // ladder's cancellation checkpoints, so an unbounded wait here would defeat the
    // deadline in LogoutUserAsync AND leave _logoutInProgress claimed forever (killing
    // all future enforcement for the user until a service restart).
    private async Task<bool> UserStillLoggedInAsync(string username)
    {
        var (ok, output) = await ProcessRunner.RunAsync(
            new ProcessStartInfo
            {
                FileName = "loginctl",
                Arguments = "list-sessions --no-legend"
            },
            TimeSpan.FromSeconds(10), _logger);

        // Can't verify -> treat as logged out. Matches the previous catch-block
        // semantics: never escalate destructively (terminate-user, pkill) on the basis
        // of a failed query.
        if (!ok)
            return false;

        return output.Split('\n').Any(line =>
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 3 && parts[2] == username;
        });
    }

    private async Task<bool> TryDbusLogoutAsync(string username, string dbusAddress,
        string destination, string objectPath, string method, string? extraArgs = null)
    {
        try
        {
            var dbusArgs = extraArgs != null
                ? $"--session --type=method_call --dest={destination} {objectPath} {method} {extraArgs}"
                : $"--session --type=method_call --dest={destination} {objectPath} {method}";

            // runuser switches to the target user so dbus-send connects to their session bus.
            // Use PATH resolution (not a hardcoded path) so this works on pre-usrmerge
            // Debian/Ubuntu where the binary may be at /sbin/runuser instead of /usr/sbin/runuser.
            var psi = new ProcessStartInfo
            {
                FileName = "runuser",
                Arguments = $"-u {username} -- dbus-send {dbusArgs}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            // Pass the session D-Bus address into the child environment.
            psi.Environment["DBUS_SESSION_BUS_ADDRESS"] = dbusAddress;

            var ok = await RunProcessWithTimeoutAsync(psi, timeoutSeconds: 10);
            _logger.LogInformation("D-Bus logout {Destination}.{Method} for {Username}: {Result}",
                destination, method, username, ok ? "sent" : "failed");
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "D-Bus logout {Destination} not available for {Username}", destination, username);
            return false;
        }
    }

    private async Task LockSessionAsync(string sessionId)
    {
        _logger.LogInformation("Locking session {SessionId}", sessionId);
        if (!await RunProcessWithTimeoutAsync("loginctl", $"lock-session {sessionId}", timeoutSeconds: 10))
            _logger.LogError("loginctl lock-session {SessionId} failed", sessionId);
        else
            _logger.LogInformation("Session {SessionId} locked", sessionId);
    }

    private async Task<bool> RunProcessWithTimeoutAsync(string filename, string arguments, int timeoutSeconds)
    {
        return await RunProcessWithTimeoutAsync(new ProcessStartInfo
        {
            FileName = filename,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }, timeoutSeconds);
    }

    private async Task<bool> RunProcessWithTimeoutAsync(ProcessStartInfo psi, int timeoutSeconds)
    {
        Process? process = null;
        try
        {
            process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogWarning("Failed to start process: {FileName}", psi.FileName);
                return false;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("{FileName} {Arguments} timed out after {Timeout}s, killing it",
                    psi.FileName, psi.Arguments, timeoutSeconds);
                process.Kill(entireProcessTree: true);
                return false;
            }

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("{FileName} {Arguments} exited with code {ExitCode}",
                    psi.FileName, psi.Arguments, process.ExitCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running {FileName} {Arguments}", psi.FileName, psi.Arguments);
            try { process?.Kill(entireProcessTree: true); } catch { }
            return false;
        }
    }

    // Reads the UID for a username from /etc/passwd.
    private static int GetUserUid(string username)
    {
        try
        {
            foreach (var line in File.ReadAllLines("/etc/passwd"))
            {
                var parts = line.Split(':');
                if (parts.Length >= 3 && parts[0] == username && int.TryParse(parts[2], out var uid))
                    return uid;
            }
        }
        catch { }
        return -1;
    }

    // Basic sanity check to avoid passing arbitrary strings to shell commands.
    // Username comes from loginctl output (system-controlled), but we validate anyway.
    private static bool IsValidUsername(string username) =>
        !string.IsNullOrEmpty(username) &&
        Regex.IsMatch(username, @"^[a-z_][a-z0-9_\-]{0,31}$");
}
