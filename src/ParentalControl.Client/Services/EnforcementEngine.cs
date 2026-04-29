using System.Diagnostics;
using System.Text.RegularExpressions;
using ParentalControl.Shared.DTOs;

namespace ParentalControl.Client.Services;

public interface IEnforcementEngine
{
    Task CheckAndEnforceAsync(UsageReportResponse response, string username, string sessionId);
    Task CheckAndEnforceOfflineAsync(List<UsageRecord> records, string username, string sessionId);
}

public class EnforcementEngine : IEnforcementEngine
{
    private readonly ILogger<EnforcementEngine> _logger;
    private readonly ILocalCache _cache;
    private readonly HashSet<int> _warningsShown = new();
    private int _lastTimeRemaining = int.MaxValue;

    public EnforcementEngine(ILogger<EnforcementEngine> logger, ILocalCache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task CheckAndEnforceAsync(UsageReportResponse response, string username, string sessionId)
    {
        if (response.ShouldEnforce && !string.IsNullOrEmpty(response.EnforcementAction))
        {
            _logger.LogWarning("Enforcing action: {Action} for user {Username}", response.EnforcementAction, username);

            switch (response.EnforcementAction)
            {
                case "logout":
                    await LogoutUserAsync(username, sessionId);
                    break;
                case "lock":
                    await LockSessionAsync(sessionId);
                    break;
            }
        }

        // Reset warnings when time increases (new day or parent added time)
        if (response.TimeRemainingMinutes > _lastTimeRemaining)
        {
            _logger.LogInformation("Time increased from {Old} to {New} minutes, resetting warnings",
                _lastTimeRemaining, response.TimeRemainingMinutes);
            _warningsShown.Clear();
        }
        _lastTimeRemaining = response.TimeRemainingMinutes;

        foreach (var warningMinutes in response.WarningMinutes)
        {
            if (response.TimeRemainingMinutes == warningMinutes && !_warningsShown.Contains(warningMinutes))
            {
                _logger.LogInformation("Warning: {Minutes} minutes remaining", warningMinutes);
                _warningsShown.Add(warningMinutes);
                // TODO: Show notification via UI
            }
        }
    }

    public async Task CheckAndEnforceOfflineAsync(List<UsageRecord> records, string username, string sessionId)
    {
        if (records.Count == 0) return;

        var userId = records[0].UserId;
        var lastLimits = await _cache.GetLastKnownLimitsAsync(userId);

        if (lastLimits == null)
        {
            _logger.LogWarning("No cached limits for user {Username}, cannot enforce offline", username);
            return;
        }

        // Calculate today's usage from cache
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayUsage = await _cache.GetTodayUsageAsync(userId, today);

        // Calculate remaining time based on last known limits
        var timeRemaining = lastLimits.TimeRemainingMinutes - todayUsage;

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
                        await LogoutUserAsync(username, sessionId);
                        break;
                    case "lock":
                        await LockSessionAsync(sessionId);
                        break;
                }
            }
        }
        else
        {
            // Check warnings
            foreach (var warningMinutes in lastLimits.WarningMinutes)
            {
                if (timeRemaining <= warningMinutes && !_warningsShown.Contains(warningMinutes))
                {
                    _logger.LogInformation("Offline warning: {Minutes} minutes remaining", timeRemaining);
                    _warningsShown.Add(warningMinutes);
                }
            }
        }
    }

    // Graduated logout: graceful first, escalating to forceful.
    // The root cause of the Kubuntu full-system freeze is that calling
    // `loginctl terminate-user` kills kwin_wayland (the Wayland compositor)
    // without giving it time to release DRM/KMS resources cleanly, leaving
    // the display pipeline in a broken state with no process able to render.
    private async Task LogoutUserAsync(string username, string sessionId)
    {
        if (!IsValidUsername(username))
        {
            _logger.LogError("Invalid username format, refusing to enforce: {Username}", username);
            return;
        }

        _logger.LogInformation("Logging out user {Username} (session {SessionId})", username, sessionId);

        // Step 1: graceful desktop logout via D-Bus.
        // Gives the Wayland compositor (kwin_wayland on KDE, mutter on GNOME) time to
        // release DRM buffers and hand the VT back to SDDM cleanly.
        if (await TryGracefulDesktopLogoutAsync(username))
        {
            _logger.LogInformation("Graceful desktop logout sent for {Username}", username);
            return;
        }

        // Step 2: end the specific loginctl session.
        // Less destructive than terminate-user: logind manages the VT transition
        // and notifies SDDM, giving it a chance to reclaim the display.
        if (!string.IsNullOrEmpty(sessionId))
        {
            _logger.LogInformation("Attempting loginctl terminate-session {SessionId}", sessionId);
            await RunProcessWithTimeoutAsync("loginctl", $"terminate-session {sessionId}", timeoutSeconds: 20);
            return;
        }

        // Step 3: terminate all user sessions (can cause Wayland freeze on KDE).
        _logger.LogWarning("No session ID, attempting loginctl terminate-user {Username}", username);
        if (!await RunProcessWithTimeoutAsync("loginctl", $"terminate-user {username}", timeoutSeconds: 20))
        {
            // Step 4: nuclear option — direct kernel-level kill of all user processes.
            _logger.LogWarning("loginctl failed, using pkill -KILL for {Username}", username);
            await RunProcessWithTimeoutAsync("pkill", $"-KILL -u {username}", timeoutSeconds: 10);
        }
    }

    // Attempts a graceful D-Bus logout for the user's running desktop environment.
    // Requires the user's session D-Bus bus to be accessible at /run/user/{uid}/bus.
    private async Task<bool> TryGracefulDesktopLogoutAsync(string username)
    {
        var uid = GetUserUid(username);
        if (uid < 0)
        {
            _logger.LogWarning("Cannot resolve UID for {Username}, skipping graceful logout", username);
            return false;
        }

        var dbusAddress = $"unix:path=/run/user/{uid}/bus";

        // KDE Plasma (Kubuntu default)
        if (await TryDbusLogoutAsync(username, dbusAddress,
            "org.kde.Shutdown", "/Shutdown", "org.kde.Shutdown.logout"))
            return true;

        // GNOME / Unity
        if (await TryDbusLogoutAsync(username, dbusAddress,
            "org.gnome.SessionManager", "/org/gnome/SessionManager",
            "org.gnome.SessionManager.Logout", extraArgs: "uint32:1"))
            return true;

        // Cinnamon (Linux Mint)
        if (await TryDbusLogoutAsync(username, dbusAddress,
            "org.cinnamon.SessionManager", "/org/cinnamon/SessionManager",
            "org.cinnamon.SessionManager.Logout", extraArgs: "uint32:1"))
            return true;

        // XFCE
        if (await TryDbusLogoutAsync(username, dbusAddress,
            "org.xfce.SessionManager", "/org/xfce/SessionManager",
            "org.xfce.SessionManager.Logout", extraArgs: "boolean:true boolean:false"))
            return true;

        return false;
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
            _logger.LogDebug("D-Bus logout {Destination}.{Method} for {Username}: {Result}",
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
