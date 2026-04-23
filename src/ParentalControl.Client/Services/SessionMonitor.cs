namespace ParentalControl.Client.Services;

public record UserSession(Guid UserId, string Username, string SessionId, bool IsIdle);

public interface ISessionMonitor
{
    Task<List<UserSession>> GetActiveSessionsAsync();
    Task<List<string>> GetAllLocalUsersAsync();
    Task<bool> IsSessionIdleAsync(string sessionId);
}

public class SystemdSessionMonitor : ISessionMonitor
{
    private readonly ILogger<SystemdSessionMonitor> _logger;
    
    public SystemdSessionMonitor(ILogger<SystemdSessionMonitor> logger) => _logger = logger;
    
    public async Task<List<UserSession>> GetActiveSessionsAsync()
    {
        var sessions = new List<UserSession>();
        
        try
        {
            // Use loginctl to get active sessions
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "loginctl",
                    Arguments = "list-sessions --no-legend",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            if (process.ExitCode != 0)
            {
                _logger.LogWarning("loginctl failed with exit code {ExitCode}", process.ExitCode);
                return sessions;
            }
            
            // Parse output: SESSION UID USER SEAT TTY
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var sessionId = parts[0];
                    var username = parts[2];
                    
                    // Skip system users
                    if (username == "root" || username == "gdm" || username == "lightdm")
                        continue;
                    
                    sessions.Add(new UserSession(
                        UserId: Guid.Empty, // Server will determine userId from username
                        Username: username,
                        SessionId: sessionId,
                        IsIdle: false
                    ));
                    
                    _logger.LogDebug("Detected session: {SessionId} for user {Username}", sessionId, username);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active sessions");
        }
        
        return sessions;
    }
    
    public async Task<bool> IsSessionIdleAsync(string sessionId)
    {
        try
        {
            // Check session state - we want to stop counting only if:
            // 1. Session is locked (user locked screen)
            // 2. Session is closing (logout/shutdown)
            // We DO count time for:
            // - Active sessions (watching videos, etc.)
            // - Idle sessions (no keyboard/mouse but logged in)
            
            var stateProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "loginctl",
                    Arguments = $"show-session {sessionId} -p State --value",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            stateProcess.Start();
            var state = await stateProcess.StandardOutput.ReadToEndAsync();
            await stateProcess.WaitForExitAsync();
            
            state = state.Trim();
            
            // Only stop counting if session is closing or lingering
            if (state == "closing" || state == "lingering")
            {
                _logger.LogDebug("Session {SessionId} is {State}, not counting time", sessionId, state);
                return true;
            }
            
            // Check if screen is locked (not just idle)
            var lockedProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "loginctl",
                    Arguments = $"show-session {sessionId} -p LockedHint --value",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            lockedProcess.Start();
            var lockedOutput = await lockedProcess.StandardOutput.ReadToEndAsync();
            await lockedProcess.WaitForExitAsync();
            
            if (lockedOutput.Trim() == "yes")
            {
                _logger.LogDebug("Session {SessionId} is locked, not counting time", sessionId);
                return true;
            }
            
            // Session is active or idle but unlocked - COUNT THE TIME
            // This includes watching videos, reading, etc.
            _logger.LogDebug("Session {SessionId} is active (state: {State}), counting time", sessionId, state);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking session state for {SessionId}", sessionId);
            // On error, assume active to avoid missing time
            return false;
        }
    }
    
    public async Task<List<string>> GetAllLocalUsersAsync()
    {
        var users = new List<string>();
        
        try
        {
            // Read /etc/passwd to get all users with UID >= 1000 (regular users)
            var lines = await File.ReadAllLinesAsync("/etc/passwd");
            foreach (var line in lines)
            {
                var parts = line.Split(':');
                if (parts.Length >= 4)
                {
                    var username = parts[0];
                    var uid = int.Parse(parts[2]);
                    
                    // Regular users have UID >= 1000 (skip system users)
                    if (uid >= 1000 && uid < 65534)
                    {
                        users.Add(username);
                        _logger.LogDebug("Found local user: {Username} (UID: {Uid})", username, uid);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading local users");
        }
        
        return users;
    }
}
